using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Wormy.Core;
namespace Wormy.App;
public static class VerifiedUpdates
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct TrustFile { public uint Size; public IntPtr Path; public IntPtr Handle; public IntPtr Subject; }
    [StructLayout(LayoutKind.Sequential)] private struct TrustData
    {
        public uint Size; public IntPtr Policy; public IntPtr Sip; public uint Ui; public uint Revocation; public uint Choice; public IntPtr File; public uint Action; public IntPtr State; public IntPtr Url; public uint Flags; public uint Context; public IntPtr Signature;
    }
    [DllImport("wintrust.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] private static extern int WinVerifyTrust(IntPtr window,[In] ref Guid action,ref TrustData data);
    public static void VerifySignature(string path,string publisher,bool onlineRevocation=false)
    {
        publisher=publisher.Replace(" ","").ToUpperInvariant();
        if(publisher.Length is not (40 or 64) || publisher.Any(c=>!Uri.IsHexDigit(c))) throw new InvalidDataException("Configure a trusted publisher certificate fingerprint before installation.");
        var filename=Marshal.StringToCoTaskMemUni(path); var pointer=Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>());
        try
        {
            Marshal.StructureToPtr(new TrustFile { Size=(uint)Marshal.SizeOf<TrustFile>(),Path=filename },pointer,false);
            var data=new TrustData { Size=(uint)Marshal.SizeOf<TrustData>(),Ui=2,Revocation=1,Choice=1,File=pointer,Action=1,Flags=onlineRevocation ? 0x80u : 0x1080u };
            var action=new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE"); var status=WinVerifyTrust(IntPtr.Zero,ref action,ref data);
            try
            {
                if(status!=0) throw new InvalidDataException($"Installer signature is not trusted (0x{status:X8}). No installer was run.");
#pragma warning disable SYSLIB0057
                using var certificate=new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
                var actual=publisher.Length==64 ? certificate.GetCertHashString(HashAlgorithmName.SHA256) : certificate.Thumbprint;
                if(!string.Equals(actual,publisher,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Installer publisher does not match the configured certificate.");
            }
            finally { data.Action=2; WinVerifyTrust(IntPtr.Zero,ref action,ref data); }
        }
        finally { Marshal.FreeHGlobal(pointer); Marshal.FreeCoTaskMem(filename); }
    }
    public static async Task<string> DownloadAsync(ReleaseUpdate release,string publisher,CancellationToken token)
    {
        if(string.IsNullOrWhiteSpace(publisher)) throw new InvalidDataException("Configure the trusted publisher certificate first.");
        var folder=Path.Combine(Path.GetTempPath(),"Wormy","verified-updates",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var path=Path.Combine(folder,"Ceeker-Setup.exe");
        try
        {
            using var client=new HttpClient(new HttpClientHandler { AllowAutoRedirect=false }) { Timeout=TimeSpan.FromMinutes(10) };
            using var response=await client.GetAsync(ReleaseUpdate.Https(release.InstallerUrl),HttpCompletionOption.ResponseHeadersRead,token); response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentLength>1_000_000_000) throw new InvalidDataException("Installer exceeds the 1 GB download limit.");
            await using(var source=await response.Content.ReadAsStreamAsync(token)) await using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {
                var buffer=new byte[65536]; long total=0; int read;
                while((read=await source.ReadAsync(buffer,token))>0) { total+=read; if(total>1_000_000_000) throw new InvalidDataException("Installer exceeds download limit."); await output.WriteAsync(buffer.AsMemory(0,read),token); }
            }
            await using var stream=File.OpenRead(path); var hash=Convert.ToHexString(await SHA256.HashDataAsync(stream,token));
            if(!hash.Equals(release.Sha256,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Installer checksum mismatch. No installer was run.");
            await Task.Run(()=>VerifySignature(path,publisher,true),token); token.ThrowIfCancellationRequested(); return path;
        }
        catch { if(File.Exists(path)) File.Delete(path); throw; }
    }
    public static async Task<ReleaseUpdate> CheckAsync(string address,CancellationToken token)
    {
        using var client=new HttpClient(new HttpClientHandler { AllowAutoRedirect=false }) { Timeout=TimeSpan.FromSeconds(15) };
        using var response=await client.GetAsync(ReleaseUpdate.Https(address),HttpCompletionOption.ResponseHeadersRead,token); response.EnsureSuccessStatusCode();
        await using var stream=await response.Content.ReadAsStreamAsync(token); using var buffer=new MemoryStream(); var bytes=new byte[4096]; int read;
        while((read=await stream.ReadAsync(bytes,token))>0) { if(buffer.Length+read>65536) throw new InvalidDataException("Release manifest exceeds limit."); buffer.Write(bytes,0,read); }
        return ReleaseUpdate.Parse(System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
    }
    public static async Task LaunchAsync(string path,ReleaseUpdate release,string publisher,CancellationToken token,Func<bool>? ready=null)
    {
        await using var retained=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(!Convert.ToHexString(await SHA256.HashDataAsync(retained,token)).Equals(release.Sha256,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Downloaded installer changed.");
        await Task.Run(()=>VerifySignature(path,publisher,true),token); token.ThrowIfCancellationRequested();
        if(ready is not null&&!ready()) throw new OperationCanceledException("Installation deferred because the app is no longer idle.");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute=true });
    }
}
