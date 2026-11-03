using System.Security.Cryptography;
using Wormy.Core;
if(args.Length==2 && args[0]=="create-key")
{
    var folder=Path.GetFullPath(args[1]); Directory.CreateDirectory(folder);
    var secret=Path.Combine(folder,"issuer-private.pem"); var trust=Path.Combine(folder,"issuer-public.pem");
    if(File.Exists(secret)||File.Exists(trust)) throw new IOException("Issuer key files already exist. Refusing to replace them.");
    using var rsa=RSA.Create(3072);
    using(var writer=new StreamWriter(new FileStream(secret,FileMode.CreateNew))) writer.Write(rsa.ExportPkcs8PrivateKeyPem());
    using(var writer=new StreamWriter(new FileStream(trust,FileMode.CreateNew))) writer.Write(rsa.ExportSubjectPublicKeyInfoPem());
    Console.WriteLine("Created issuer key pair. Keep issuer-private.pem private and outside the application/repository. Bundle only issuer-public.pem."); return;
}
if(args.Length==5 && args[0]=="issue")
{
    if(!Guid.TryParseExact(args[2],"N",out _)) throw new ArgumentException("Device ID must be a 32-character GUID.");
    if(string.IsNullOrWhiteSpace(args[3])||args[3].Length>128) throw new ArgumentException("Invalid license ID.");
    var destination=Path.GetFullPath(args[4]);
    var signed=OfflineLicense.Issue(new("Wormy",args[3],args[2],DateTimeOffset.UtcNow,null),File.ReadAllText(args[1]));
    using var writer=new StreamWriter(new FileStream(destination,FileMode.CreateNew)); writer.Write(signed);
    Console.WriteLine("Issued device-bound perpetual license. Delivery/payment verification is handled by the owner."); return;
}
Console.WriteLine("create-key <private-directory> | issue <private-pem> <device-id> <license-id> <new-output-file>");
Environment.ExitCode=1;
