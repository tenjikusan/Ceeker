using System.Runtime.InteropServices;
using Wormy.App;
static class InstallerChecks
{
    [DllImport("msi.dll",CharSet=CharSet.Unicode)] static extern uint MsiOpenDatabase(string path,IntPtr mode,out uint db);
    [DllImport("msi.dll",CharSet=CharSet.Unicode)] static extern uint MsiDatabaseOpenView(uint db,string sql,out uint view);
    [DllImport("msi.dll")] static extern uint MsiViewExecute(uint view,uint record);
    [DllImport("msi.dll")] static extern uint MsiDatabaseCommit(uint db);
    [DllImport("msi.dll")] static extern uint MsiCloseHandle(uint handle);
    public static void Run()
    {
        var path=Path.Combine(Path.GetTempPath(),"wormy-installer-fixture-"+Guid.NewGuid()+".msi");
        if(MsiOpenDatabase(path,new IntPtr(3),out var db)!=0) throw new Exception("Cannot create MSI metadata fixture.");
        try {
            void Sql(string sql) { if(MsiDatabaseOpenView(db,sql,out var view)!=0) throw new Exception("MSI fixture SQL failed."); try { if(MsiViewExecute(view,0)!=0) throw new Exception("MSI fixture execute failed."); } finally { MsiCloseHandle(view); } }
            Sql("CREATE TABLE `Property` (`Property` CHAR(72) NOT NULL, `Value` CHAR(0) LOCALIZABLE PRIMARY KEY `Property`)");
            Sql($"INSERT INTO `Property` (`Property`,`Value`) VALUES ('ProductCode','{{{Guid.NewGuid()}}}')");
            Sql("INSERT INTO `Property` (`Property`,`Value`) VALUES ('ProductVersion','1.2.3')");
            if(MsiDatabaseCommit(db)!=0) throw new Exception("MSI fixture commit failed.");
        } finally { MsiCloseHandle(db); }
        try {
            var before=System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path));
            if(!InstallerEvidence.Inspect(path).StartsWith("No installed MSI registration")) throw new Exception("Unknown MSI product falsely matched.");
            if(!before.SequenceEqual(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))) throw new Exception("Installer modified by read-only evidence check.");
            if(!InstallerEvidence.Inspect("fixture.exe").StartsWith("Unverified")) throw new Exception("EXE identity falsely verified.");
            Console.WriteLine("PASS: native read-only MSI product/version metadata, unknown product exclusion, source preservation and EXE unverified fallback.");
        } finally { File.Delete(path); }
    }
}
