param(
    [Parameter(Mandatory)][string]$InputFile,
    [Parameter(Mandatory)][string]$OutputFile,
    [Parameter(Mandatory)][string]$CertificateThumbprint,
    [Parameter(Mandatory)][uri]$TimestampUrl,
    [Parameter(Mandatory)][string]$SignTool
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$source = (Resolve-Path -LiteralPath $InputFile).Path
$destination = [IO.Path]::GetFullPath($OutputFile)
if (-not $destination.StartsWith($workspace + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Signing output must stay inside this workspace.' }
if (Test-Path -LiteralPath $destination) { throw 'Output exists; refusing to overwrite a release.' }
if ([IO.Path]::GetExtension($source) -ne '.exe') { throw 'Only executable release files are supported.' }
if ($TimestampUrl.Scheme -notin @('http','https')) { throw 'Timestamp server must use HTTP or HTTPS.' }
$thumbprint = $CertificateThumbprint.Replace(' ','')
if ($thumbprint -notmatch '^[A-Fa-f0-9]{40}$') { throw 'Invalid certificate thumbprint.' }
$certificate = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object Thumbprint -EQ $thumbprint | Select-Object -First 1
if (-not $certificate -or -not $certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date) -or $certificate.NotBefore -gt (Get-Date)) { throw 'A current code-signing certificate with its private key is required.' }
if (-not (Test-Path -LiteralPath $SignTool)) { throw 'SignTool executable not found.' }
New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
Copy-Item -LiteralPath $source -Destination $destination
& $SignTool sign /s My /sha1 $thumbprint /fd SHA256 /tr $TimestampUrl.AbsoluteUri /td SHA256 $destination
if ($LASTEXITCODE -ne 0) { throw 'Signing failed; output is not a verified release.' }
& $SignTool verify /pa /all /v $destination
if ($LASTEXITCODE -ne 0) { throw 'Signature verification failed.' }
$signature = Get-AuthenticodeSignature -LiteralPath $destination
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $thumbprint -or -not $signature.TimeStamperCertificate) { throw 'Trusted publisher signature and timestamp were not confirmed.' }
$hash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
"$hash  $([IO.Path]::GetFileName($destination))" | Set-Content -LiteralPath "$destination.sha256" -Encoding ascii
[ordered]@{ file=$destination; sha256=$hash; publisher=$signature.SignerCertificate.Subject; timestamped=$true } | ConvertTo-Json
