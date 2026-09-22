using System.Security.Cryptography;
using System.Text.Json;
using PTBox.UpdateCore;

try
{
    if (args is ["init-key", var privateFile, var trustFile])
    {
        if (File.Exists(privateFile)) throw new IOException("Private key already exists; it will not be replaced.");
        var trust = JsonSerializer.Deserialize<ReleaseTrust>(File.ReadAllBytes(trustFile), UpdateProtocol.Json)!;
        if (trust.Keys.Count != 0) throw new IOException("Trust already configured; restore its private key instead of replacing trust.");
        using var rsa = RSA.Create(3072);
        var id = "ptbox-" + DateTime.UtcNow.ToString("yyyyMMdd");
        var privateBytes = rsa.ExportPkcs8PrivateKey();
        try { using var file = new FileStream(privateFile, FileMode.CreateNew, FileAccess.Write, FileShare.None); file.Write(LocalSecret.Protect(privateBytes)); }
        finally { CryptographicOperations.ZeroMemory(privateBytes); }
        trust.Keys.Add(id, rsa.ExportSubjectPublicKeyInfoPem()); UpdateProtocol.WriteJson(trustFile, trust);
        Console.WriteLine("Created signing key. Public key ID: " + id); return 0;
    }
    if (args is ["create", var version, var installer, var notes, var secret, var output])
    {
        UpdateProtocol.ParseVersion(version);
        if (Directory.Exists(output)) throw new IOException("Release output already exists. Do not overwrite a version; choose another version or archive the local output first.");
        var trust = ReleaseTrust.Embedded(); using var rsa = RSA.Create();
        var privateBytes = LocalSecret.Unprotect(File.ReadAllBytes(secret));
        try { rsa.ImportPkcs8PrivateKey(privateBytes, out _); } finally { CryptographicOperations.ZeroMemory(privateBytes); }
        var pem = rsa.ExportSubjectPublicKeyInfoPem(); var keyId = trust.Keys.Single(x => x.Value == pem).Key;
        var name = $"PTBox-Setup-{version}-win-x64.exe";
        var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(installer);
        if ((info.ProductVersion ?? "").Split('+')[0].Trim() != version) throw new InvalidDataException("Installer version does not match release version.");
        using var stream = File.OpenRead(installer);
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        var manifest = new UpdateManifest(1, version, "stable", "win-x64", 22000, UpdateProtocol.UpdaterVersion, name,
            $"https://github.com/{trust.Repository}/releases/download/v{version}/{name}", stream.Length, hash, DateTimeOffset.UtcNow, keyId);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, UpdateProtocol.Json);
        var signature = rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        UpdateProtocol.VerifyManifest(bytes, signature, trust);
        // Stage completely before advertising a local release directory.
        var staging = output + ".staging-" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(staging);
        File.Copy(installer, Path.Combine(staging, name));
        File.WriteAllBytes(Path.Combine(staging, "update.json"), bytes); File.WriteAllBytes(Path.Combine(staging, "update.json.sig"), signature);
        File.WriteAllText(Path.Combine(staging, name + ".sha256"), hash + "  " + name + "\n");
        File.Copy(notes, Path.Combine(staging, "release-notes.md")); Directory.Move(staging, output);
        Console.WriteLine("Local release prepared: " + Path.GetFullPath(output)); return 0;
    }
    if (args is ["verify", var directory])
    {
        var m = UpdateProtocol.VerifyManifest(File.ReadAllBytes(Path.Combine(directory, "update.json")), File.ReadAllBytes(Path.Combine(directory, "update.json.sig")), ReleaseTrust.Embedded());
        await using var verified = await UpdateProtocol.OpenVerifiedPackageAsync(Path.Combine(directory, m.AssetName), m);
        Console.WriteLine("Verified release " + m.Version + " / " + m.Size + " bytes"); return 0;
    }
    if (args is ["export-key", var encryptedKey, var backupFile])
    {
        if (File.Exists(backupFile)) throw new IOException("Backup already exists.");
        using var rsa = RSA.Create(); var bytes = LocalSecret.Unprotect(File.ReadAllBytes(encryptedKey));
        try { rsa.ImportPkcs8PrivateKey(bytes, out _); } finally { CryptographicOperations.ZeroMemory(bytes); }
        File.WriteAllText(backupFile, rsa.ExportPkcs8PrivateKeyPem());
        Console.WriteLine("Exported PRIVATE key for offline backup; do not upload or commit it."); return 0;
    }
    if (args is ["import-key", var pemBackup, var protectedFile])
    {
        if (File.Exists(protectedFile)) throw new IOException("Protected key already exists; it will not be replaced.");
        using var rsa = RSA.Create(); rsa.ImportFromPem(File.ReadAllText(pemBackup));
        if (!ReleaseTrust.Embedded().Keys.Values.Contains(rsa.ExportSubjectPublicKeyInfoPem())) throw new InvalidDataException("Backup does not match the existing public trust. Do not rotate trust during restore.");
        var bytes = rsa.ExportPkcs8PrivateKey();
        try { using var secretStream = new FileStream(protectedFile, FileMode.CreateNew, FileAccess.Write, FileShare.None); secretStream.Write(LocalSecret.Protect(bytes)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
        Console.WriteLine("Restored the existing release key for this Windows account."); return 0;
    }
    Console.Error.WriteLine("Commands: init-key ENCRYPTED_KEY TRUST_JSON | create VERSION INSTALLER NOTES ENCRYPTED_KEY OUTPUT | verify DIRECTORY | export-key ENCRYPTED_KEY BACKUP_PEM | import-key BACKUP_PEM ENCRYPTED_KEY"); return 2;
}
catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
