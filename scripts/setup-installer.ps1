$ErrorActionPreference = 'Stop'
$packageRoot = Split-Path $PSScriptRoot -Parent
$toolDirectory = Join-Path $packageRoot '.tools\inno-6.7.3'
$compiler = Join-Path $toolDirectory 'ISCC.exe'
if (Test-Path -LiteralPath $compiler) { Write-Output $compiler; return }
$download = Join-Path $packageRoot '.tools\innosetup-6.7.3.exe'
$expectedHash = '9C73C3BAE7ED48D44112A0F48E66742C00090BDB5BEF71D9D3C056C66E97B732'
New-Item -ItemType Directory -Path (Split-Path $download -Parent) -Force | Out-Null
if (!(Test-Path -LiteralPath $download)) {
    Invoke-WebRequest -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $download
}
if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Inno Setup download SHA256 mismatch.' }
$signature = Get-AuthenticodeSignature -LiteralPath $download
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notlike '*O=Pyrsys B.V.*') { throw 'Inno Setup publisher signature verification failed.' }
# Portable compiler only: no machine install, shortcuts, associations, or uninstall registration.
$arguments = @('/PORTABLE=1', '/CURRENTUSER', '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', "/DIR=`"$toolDirectory`"")
$process = Start-Process -FilePath $download -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $compiler)) { throw "Compiler extraction failed: $($process.ExitCode)" }
Write-Output $compiler
