using System.Runtime.InteropServices;
namespace Wormy.App;
internal sealed class DesktopNative : IDisposable
{
    private const int HotkeyId=0xCE01;
    private const uint TrayMessage=0x8029;
    private readonly nint window;
    private readonly SubclassProc callback;
    private readonly Action quickSearch,downloads;
    private readonly uint taskbarCreated;
    private nint icon;
    private bool trayVisible,disposed;
    private bool subclassed;
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint SubclassProc(nint hwnd,uint msg,nuint wp,nint lp,nuint id,nuint data);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct NotifyData
    {
        public uint Size; public nint Window;public uint Id,Flags,Message;public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Tip;
        public uint State,StateMask;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)]public string Title;
        public uint InfoFlags;public Guid Guid;public nint BalloonIcon;
    }
    public DesktopNative(nint hwnd,string iconPath,Action search,Action recent)
    {
        window=hwnd;quickSearch=search;downloads=recent;callback=WndProc;
        subclassed=SetWindowSubclass(window,callback,0xCE01,0);
        if(!subclassed)throw new InvalidOperationException("Unable to initialize desktop shortcuts.");
        taskbarCreated=RegisterWindowMessage("TaskbarCreated");
        icon=LoadImage(0,iconPath,1,32,32,0x10);
    }
    public bool RegisterShortcut(bool enabled,uint modifiers,uint key)
    {
        UnregisterHotKey(window,HotkeyId);
        return !enabled||RegisterHotKey(window,HotkeyId,modifiers|0x4000,key);
    }
    public bool SetTray(bool enabled)
    {
        var data=Data();Shell_NotifyIcon(2,ref data);trayVisible=false;
        if(!enabled)return true;
        trayVisible=Shell_NotifyIcon(0,ref data);
        if(trayVisible){data.Version=4;Shell_NotifyIcon(4,ref data);}return trayVisible;
    }
    private NotifyData Data()=>new(){Size=(uint)Marshal.SizeOf<NotifyData>(),Window=window,Id=1,Flags=7,Message=TrayMessage,Icon=icon,Tip="Ceeker · Recent downloads and Quick Search",Info="",Title=""};
    private nint WndProc(nint hwnd,uint msg,nuint wp,nint lp,nuint id,nuint data)
    {
        try
        {
            if(msg==0x312 && wp==HotkeyId){quickSearch();return 0;}
            if(msg==TrayMessage)
            {
                var notification=(uint)((long)lp & 0xffff);
                if(notification is 0x202 or 0x205 or 0x400 or 0x401){downloads();return 0;}
            }
            if(msg==taskbarCreated && trayVisible)SetTray(true);
        }
        catch { /* Never let a managed exception cross the window callback. */ }
        return DefSubclassProc(hwnd,msg,wp,lp);
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;UnregisterHotKey(window,HotkeyId);var data=Data();Shell_NotifyIcon(2,ref data);
        if(subclassed)RemoveWindowSubclass(window,callback,0xCE01);
        if(icon!=0)DestroyIcon(icon);icon=0;GC.KeepAlive(callback);
    }
    [DllImport("comctl32.dll")]private static extern bool SetWindowSubclass(nint h,SubclassProc p,nuint id,nuint data);
    [DllImport("comctl32.dll")]private static extern bool RemoveWindowSubclass(nint h,SubclassProc p,nuint id);
    [DllImport("comctl32.dll")]private static extern nint DefSubclassProc(nint h,uint m,nuint w,nint l);
    [DllImport("user32.dll",SetLastError=true)]private static extern bool RegisterHotKey(nint h,int id,uint mods,uint key);
    [DllImport("user32.dll")]private static extern bool UnregisterHotKey(nint h,int id);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern uint RegisterWindowMessage(string msg);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern nint LoadImage(nint inst,string path,uint type,int cx,int cy,uint flags);
    [DllImport("user32.dll")]private static extern bool DestroyIcon(nint icon);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,EntryPoint="Shell_NotifyIconW")]private static extern bool Shell_NotifyIcon(uint message,ref NotifyData data);
}
