using System.Security.Cryptography;
using Wormy.Core;
static class LicenseChecks
{
    public static void Run()
    {
        using var issuer=RSA.Create(2048); using var wrong=RSA.Create(2048);
        var now=DateTimeOffset.UtcNow; var claims=new LicenseClaims("Wormy","fixture-license","fixture-device",now.AddMinutes(-1),now.AddDays(1));
        var signed=OfflineLicense.Issue(claims,issuer.ExportPkcs8PrivateKeyPem()); var trust=issuer.ExportSubjectPublicKeyInfoPem();
        if(OfflineLicense.Verify(signed,trust,claims.Device,now)!=claims) throw new Exception("Signed license roundtrip failed.");
        void Reject(Action action) { try { action(); } catch(Exception ex) when(ex is InvalidDataException or CryptographicException or FormatException or System.Text.Json.JsonException) { return; } throw new Exception("Invalid license accepted."); }
        Reject(()=>OfflineLicense.Verify(signed,trust,"other-device",now));
        Reject(()=>OfflineLicense.Verify(signed,wrong.ExportSubjectPublicKeyInfoPem(),claims.Device,now));
        Reject(()=>OfflineLicense.Verify(signed,issuer.ExportPkcs8PrivateKeyPem(),claims.Device,now));
        Reject(()=>OfflineLicense.Verify(signed,trust,claims.Device,now.AddDays(2)));
        var pieces=signed.Split('.'); var bytes=Convert.FromBase64String(pieces[0]); bytes[0]^=1;
        Reject(()=>OfflineLicense.Verify(Convert.ToBase64String(bytes)+"."+pieces[1],trust,claims.Device,now));
        Reject(()=>OfflineLicense.Verify(new string('x',16385),trust,claims.Device,now));
        Console.WriteLine("PASS: signed offline activation, wrong-device/key, expiration, tampering and size limits.");
    }
}
