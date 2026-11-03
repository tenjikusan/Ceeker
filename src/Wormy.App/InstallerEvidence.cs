using System.Runtime.InteropServices;
using System.Text;


namespace Wormy.App;
public static class InstallerEvidence
{
    [DllImport("msi.dll", CharSet=CharSet.Unicode)] private static extern uint MsiOpenDatabase(string path, IntPtr mode, out uint handle);
    [DllImport("msi.dll", CharSet=CharSet.Unicode)] private static extern uint MsiDatabaseOpenView(uint db, string query, out uint view);
    [DllImport("msi.dll")] private static extern uint MsiViewExecute(uint view, uint record);
    [DllImport("msi.dll")] private static extern uint MsiViewFetch(uint view, out uint record);
    [DllImport("msi.dll", CharSet=CharSet.Unicode)] private static extern uint MsiRecordGetString(uint record, uint field, StringBuilder value, ref uint length);
    [DllImport("msi.dll", CharSet=CharSet.Unicode)] private static extern int MsiQueryProductState(string code);
    [DllImport("msi.dll", CharSet=CharSet.Unicode)] private static extern uint MsiGetProductInfo(string code, string property, StringBuilder value, ref uint length);
    [DllImport("msi.dll")] private static extern uint MsiCloseHandle(uint handle);
    private static string Property(uint db, string property)
    {
        if(MsiDatabaseOpenView(db,$"SELECT `Value` FROM `Property` WHERE `Property` = '{property}'",out var view)!=0) return "";
        try { if(MsiViewExecute(view,0)!=0 || MsiViewFetch(view,out var record)!=0) return "";
            try { var value=new StringBuilder(1024); uint length=1023; return MsiRecordGetString(record,1,value,ref length)==0 ? value.ToString() : ""; }
            finally { MsiCloseHandle(record); }
        } finally { MsiCloseHandle(view); }
    }
    public static string Inspect(string path)
    {
        if(!Path.GetExtension(path).Equals(".msi",StringComparison.OrdinalIgnoreCase)) return "Unverified · exact product matching is available for MSI packages only";
        var info=new FileInfo(path);
        if(!info.Exists || (info.Attributes & (FileAttributes.ReparsePoint|FileAttributes.Offline))!=0) return "Unavailable · linked/cloud-only files skipped";
        if(MsiOpenDatabase(path,IntPtr.Zero,out var db)!=0) return "Unverified · MSI metadata could not be read";
        try {
            var code=Property(db,"ProductCode"); var version=Property(db,"ProductVersion");
            if(!Guid.TryParse(code,out _) || version.Length==0) return "Unverified · missing MSI product identity";
            if(MsiQueryProductState(code)!=5) return "No installed MSI registration found for this product";
            var installed=new StringBuilder(1024); uint length=1023;
            if(MsiGetProductInfo(code,"VersionString",installed,ref length)!=0) return "Product registered · installed version unavailable";
            return installed.ToString()==version ? $"Installed product/version match · {version} · review before recycling" : $"Product registered · installed {installed}, installer {version} · versions differ";
        } finally { MsiCloseHandle(db); }
    }
}

