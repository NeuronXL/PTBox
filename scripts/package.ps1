param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '1.1.3')
. "$PSScriptRoot\common.ps1"
$compiler = Join-Path $projectRoot '.tools\inno-6.7.3\ISCC.exe'
if (!(Test-Path -LiteralPath $compiler)) { & "$PSScriptRoot\setup-installer.ps1" }
if (!(Test-Path -LiteralPath $compiler)) { throw 'Inno Setup compiler is unavailable.' }

# Unique clean staging prevents personal config, logs, caches and backups from entering the installer.
$staging = Join-Path $projectRoot ('.tools\installer-stage\' + [Guid]::NewGuid().ToString('N'))
$output = Join-Path $projectRoot 'artifacts\installer'
New-Item -ItemType Directory -Path $staging,$output -Force | Out-Null
& $dotnetExe publish src\PTBox.Launcher\PTBox.Launcher.csproj -c Release -r win-x64 --self-contained true -o $staging -p:PublishSingleFile=false "-p:Version=$Version" -m:1
Assert-DotnetSuccess
& $dotnetExe publish src\PTBox.Updater\PTBox.Updater.csproj -c Release -r win-x64 --self-contained true -o (Join-Path $staging 'Updater') -p:PublishSingleFile=false "-p:Version=$Version" -m:1
Assert-DotnetSuccess
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $staging
$defaultConfig = Get-Content -LiteralPath (Join-Path $staging 'Config\config.json') -Raw | ConvertFrom-Json
if ($defaultConfig.apps.Count -ne 0) { throw 'Installer must start with an empty app list.' }
& $compiler '/Qp' "/DAppVersion=$Version" "/DPublishDir=$staging" "/DOutputDir=$output" (Join-Path $projectRoot 'installer\PTBox.iss')
if ($LASTEXITCODE -ne 0) { throw "Installer compiler failed: $LASTEXITCODE" }
$setup = Join-Path $output "PTBox-Setup-$Version-win-x64.exe"
$hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash
Set-Content -LiteralPath "$setup.sha256" -Value "$hash  $([IO.Path]::GetFileName($setup))" -Encoding ascii
Write-Output "Ready: $setup"
Write-Output "SHA256: $hash"
