param(
    [string]$EncryptedKey = '',
    [string]$BackupFile = ''
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (!$EncryptedKey) { $EncryptedKey = Join-Path $projectRoot '.tools\release-secrets\release.private.bin' }
if (!$BackupFile) { $BackupFile = Join-Path $projectRoot '.tools\release-secrets\release-backup.private.pem' }

# Run on the original Windows account that created the release key.
# Windows PowerShell 5.1 can export the DPAPI-protected PKCS#8 key without the .NET SDK.
Add-Type -AssemblyName System.Security
$inputPath = (Resolve-Path -LiteralPath $EncryptedKey).Path
$outputPath = [IO.Path]::GetFullPath($BackupFile)
if (Test-Path -LiteralPath $outputPath) { throw 'Backup already exists; it will not be overwritten.' }
if (!(Test-Path -LiteralPath ([IO.Path]::GetDirectoryName($outputPath)) -PathType Container)) {
    throw 'Create the backup destination directory first.'
}
$privateBytes = $null
$pemBytes = $null
try {
    $privateBytes = [Security.Cryptography.ProtectedData]::Unprotect(
        [IO.File]::ReadAllBytes($inputPath), $null,
        [Security.Cryptography.DataProtectionScope]::CurrentUser)
    $encoded = [Convert]::ToBase64String($privateBytes)
    $lines = for ($offset = 0; $offset -lt $encoded.Length; $offset += 64) {
        $encoded.Substring($offset, [Math]::Min(64, $encoded.Length - $offset))
    }
    $pem = "-----BEGIN PRIVATE KEY-----`n" + ($lines -join "`n") + "`n-----END PRIVATE KEY-----`n"
    $pemBytes = [Text.Encoding]::ASCII.GetBytes($pem)
    $stream = [IO.File]::Open($outputPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($pemBytes, 0, $pemBytes.Length) } finally { $stream.Dispose() }
    Write-Output "Private key backup created: $outputPath"
    Write-Output 'Copy it securely to the new publishing PC. Do not upload it to GitHub or paste its contents into chat.'
} finally {
    if ($null -ne $privateBytes) { [Array]::Clear($privateBytes, 0, $privateBytes.Length) }
    if ($null -ne $pemBytes) { [Array]::Clear($pemBytes, 0, $pemBytes.Length) }
    $encoded = $null; $pem = $null; $lines = $null
}
