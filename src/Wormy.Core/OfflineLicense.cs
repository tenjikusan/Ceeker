using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Wormy.Core;
public sealed record LicenseClaims(string Product, string LicenseId, string Device, DateTimeOffset IssuedUtc, DateTimeOffset? ExpiresUtc);
public static class OfflineLicense
{
    public static LicenseClaims Verify(string envelope, string publicKey, string device, DateTimeOffset now)
    {
        if(envelope.Length>16384 || publicKey.Length>8192) throw new InvalidDataException("License is too large.");
        if(publicKey.Contains("PRIVATE KEY",StringComparison.Ordinal)) throw new InvalidDataException("Bundle only the issuer public key.");
        var parts=envelope.Trim().Split('.');
        if(parts.Length!=2) throw new InvalidDataException("Invalid signed license format.");
        var payload=Convert.FromBase64String(parts[0]); var signature=Convert.FromBase64String(parts[1]);
        using var rsa=RSA.Create(); rsa.ImportFromPem(publicKey);
        if(rsa.KeySize<2048 || !rsa.VerifyData(payload,signature,HashAlgorithmName.SHA256,RSASignaturePadding.Pss)) throw new InvalidDataException("License signature is invalid.");
        var claims=JsonSerializer.Deserialize<LicenseClaims>(payload) ?? throw new InvalidDataException("Missing license claims.");
        if(claims.Product!="Wormy" || string.IsNullOrWhiteSpace(claims.LicenseId) || claims.LicenseId.Length>128 || claims.Device!=device) throw new InvalidDataException("License is for another product or device.");
        if(claims.IssuedUtc>now.AddMinutes(5) || claims.ExpiresUtc is { } expires && (expires<=now || expires<=claims.IssuedUtc)) throw new InvalidDataException("License is expired or has an invalid date.");
        return claims;
    }
    public static string Issue(LicenseClaims claims,string privateKey)
    {
        using var rsa=RSA.Create(); rsa.ImportFromPem(privateKey);
        if(rsa.KeySize<2048) throw new InvalidDataException("Use an RSA key of at least 2048 bits.");
        var payload=JsonSerializer.SerializeToUtf8Bytes(claims);
        return Convert.ToBase64String(payload)+"."+Convert.ToBase64String(rsa.SignData(payload,HashAlgorithmName.SHA256,RSASignaturePadding.Pss));
    }
}
