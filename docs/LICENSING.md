# Offline license issuance

Checkout and license enforcement are inactive. The app retains all existing functionality. A public issuer key is not configured in this preview.

The owner tool is `tools/Wormy.LicenseIssuer`. Run with .NET 10:

```
dotnet run --project tools/Wormy.LicenseIssuer -- create-key <private-directory-outside-repository>
dotnet run --project tools/Wormy.LicenseIssuer -- issue <issuer-private.pem> <32-character-device-id> <unique-license-id> <new-license-output-file>
```

Create the production key only when ready to manage licenses. Keep its private PEM out of the source repository, release payload and customer machines. Protect its containing directory with owner-only access. Copy only issuer-public.pem into src/Wormy.App/licenses before building a licensed release. Changing that public key invalidates previous licenses.

The customer opens Settings > License and device activation, copies the device ID and gives it to the issuer. The owner verifies purchase externally and issues a signed device-bound perpetual license. The customer pastes it to activate. No activation request, payment data or device ID is automatically transmitted. The issuer console is not part of the app installer.

Local activation and device ID live in LocalAppData/Wormy/licensing. Reinstalling with that directory retained preserves identity. Use Deactivate this device twice to confirm local removal. A transfer is manually issued for the new device. Offline verification cannot prevent restoring old licenses, copying device IDs or clock manipulation; it is not a server-enforced one-device commercial licensing service. Strong transfer/revocation enforcement requires a separately authorized activation service.

No payment integration, production private key or license enforcement has been activated. Checkout remains inactive at the user's request.
