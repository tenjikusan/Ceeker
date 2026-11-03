namespace Wormy.Core;

public sealed record FileInspection(long Length, DateTime CreatedUtc, DateTime ModifiedUtc, DateTime AccessedUtc, FileAttributes Attributes)
{
    public ulong? AllocatedBytes { get; init; }
    public string? FileIdentity { get; init; }
    [System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]
    private static extern uint GetCompressedFileSizeW(string path,out uint high);
    [System.Runtime.InteropServices.DllImport("kernel32.dll",SetLastError=true)]
    private static extern bool GetFileInformationByHandleEx(Microsoft.Win32.SafeHandles.SafeFileHandle handle,int kind,IntPtr data,uint size);
    public static FileInspection Read(string path)
    {
        var info = new FileInfo(path);
        info.Refresh();
        if (!info.Exists) throw new FileNotFoundException("File is no longer available. Refresh the library.", path);
        ulong? allocated = null;
        string? identity=null;
        if (OperatingSystem.IsWindows())
        {
            System.Runtime.InteropServices.Marshal.SetLastPInvokeError(0);
            var low=GetCompressedFileSizeW(path,out var high);
            if (low != uint.MaxValue || System.Runtime.InteropServices.Marshal.GetLastWin32Error() == 0) allocated=((ulong)high<<32)|low;
            try
            {
                using var handle=File.OpenHandle(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
                var buffer=System.Runtime.InteropServices.Marshal.AllocHGlobal(24);
                try { if(GetFileInformationByHandleEx(handle,18,buffer,24)) { var bytes=new byte[24]; System.Runtime.InteropServices.Marshal.Copy(buffer,bytes,0,24); identity=Convert.ToHexString(bytes); } }
                finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { }
        }
        return new(info.Length, info.CreationTimeUtc, info.LastWriteTimeUtc, info.LastAccessTimeUtc, info.Attributes) { AllocatedBytes=allocated,FileIdentity=identity };
    }
}

/// <summary>A fixed snapshot of the displayed results; navigation stops at either end.</summary>
public sealed class PreviewSequence
{
    private readonly FileEntry[] files;
    private int position;
    public PreviewSequence(IEnumerable<FileEntry> visible, FileEntry start)
    {
        files = visible.ToArray();
        position = Array.FindIndex(files, f => string.Equals(f.Path, start.Path, StringComparison.OrdinalIgnoreCase));
        if (position < 0) { files = [start]; position = 0; }
    }
    public FileEntry Current => files[position];
    public int Position => position;
    public int Count => files.Length;
    public bool CanPrevious => position > 0;
    public bool CanNext => position + 1 < files.Length;
    public bool Move(int delta)
    {
        var target = (long)position + delta;
        if (target < 0 || target >= files.Length) return false;
        position = (int)target;
        return true;
    }
}
