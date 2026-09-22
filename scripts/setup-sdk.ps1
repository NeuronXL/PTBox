$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$toolsDirectory = Join-Path $projectRoot '.tools'
New-Item -ItemType Directory -Force -Path $toolsDirectory | Out-Null
$metadata = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json'
$release = $metadata.releases | Where-Object { $_.sdk.version -eq $metadata.'latest-sdk' } | Select-Object -First 1
$archive = $release.sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.zip' } | Select-Object -First 1
if (-not $archive) { throw 'No Windows x64 SDK found in official metadata.' }
$zipPath = Join-Path $toolsDirectory 'sdk.zip'
Invoke-WebRequest -Uri $archive.url -OutFile $zipPath -UseBasicParsing
if ((Get-FileHash -LiteralPath $zipPath -Algorithm SHA512).Hash -ne $archive.hash) { throw 'SDK SHA512 verification failed.' }
Expand-Archive -LiteralPath $zipPath -DestinationPath (Join-Path $toolsDirectory 'dotnet') -Force
Write-Output "Local SDK $($release.sdk.version) ready. System installations and PATH unchanged."
