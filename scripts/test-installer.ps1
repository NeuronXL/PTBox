param([string]$Installer = '', [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '1.1.4')
$ErrorActionPreference = 'Stop'
$packageRoot = Split-Path $PSScriptRoot -Parent
if (!$Installer) { $Installer = Join-Path $packageRoot "artifacts\installer\PTBox-Setup-$Version-win-x64.exe" }
$Installer = (Resolve-Path -LiteralPath $Installer).Path
$userData = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'PTBox'
if (Test-Path -LiteralPath $userData) { throw 'Existing PTBox personal data found; use a clean account or Windows Sandbox for installer tests.' }
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{84089F79-568E-4B87-A01C-9FE490CAB973}_is1'
foreach ($key in @($uninstallKey, $uninstallKey.Replace('HKCU:', 'HKLM:'), $uninstallKey.Replace('Software\Microsoft', 'Software\WOW6432Node\Microsoft'))) {
    if (Test-Path -LiteralPath $key) { throw 'PTBox is already installed. Use a clean user account for installer tests.' }
}
if (Get-Process PTBox.Launcher -ErrorAction SilentlyContinue) { throw 'Exit PTBox before running installer tests.' }
$testId = [Guid]::NewGuid().ToString('N')
$testRoot = Join-Path $packageRoot "artifacts\installer-tests\$testId"
$installDir = Join-Path $testRoot 'installed'
$group = "PTBox Installer Test $testId"
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) "$group\PTBox.lnk"
if (Test-Path -LiteralPath (Join-Path ([Environment]::GetFolderPath('Programs')) 'PTBox')) { throw 'Existing PTBox start menu folder found; use a clean account for installer tests.' }
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$autorunBefore = (Get-ItemProperty -LiteralPath $runKey -ErrorAction SilentlyContinue).'PTBox.Launcher'
$portableConfig = Join-Path $packageRoot 'artifacts\PTBox-win-x64\Config\config.json'
$portableHash = if (Test-Path -LiteralPath $portableConfig) { (Get-FileHash -LiteralPath $portableConfig).Hash } else { $null }
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$checks = [Collections.Generic.List[string]]::new()
function Check([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw $Message }
    $checks.Add($Message); Write-Output "PASS $Message"
}
function Run-Setup([string]$LogName) {
    $arguments = @('/SP-', '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/LANG=chinesesimplified', "/DIR=`"$installDir`"", "/GROUP=`"$group`"", '/TASKS=""', "/LOG=`"$(Join-Path $testRoot $LogName)`"")
    $process = Start-Process -FilePath $Installer -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Installer failed ($($process.ExitCode)); see $LogName" }
}
function Remove-TestInstall {
    $uninstaller = Join-Path $installDir 'unins000.exe'
    if (!(Test-Path -LiteralPath $uninstaller)) { return }
    $expected = [IO.Path]::GetFullPath((Join-Path $packageRoot 'artifacts\installer-tests')) + '\'
    if (![IO.Path]::GetFullPath($installDir).StartsWith($expected,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test uninstall path.' }
    if (Test-Path -LiteralPath $uninstallKey) {
        $registered = (Get-ItemProperty -LiteralPath $uninstallKey).InstallLocation.TrimEnd('\')
        if ($registered -ne $installDir) { throw 'Uninstall registration belongs to another installation.' }
    }
    $process = Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/LOG=`"$(Join-Path $testRoot 'uninstall.log')`"" -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Uninstaller failed: $($process.ExitCode)" }
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ((Test-Path -LiteralPath $uninstaller) -and $timer.Elapsed.TotalSeconds -lt 15) { Start-Sleep -Milliseconds 200 }
}
try {
    Run-Setup 'install.log'
    $exe = Join-Path $installDir 'PTBox.Launcher.exe'
    $config = Join-Path $installDir 'Config\config.json'
    Check (Test-Path -LiteralPath $exe) 'Application installed'
    Check (Test-Path -LiteralPath (Join-Path $installDir 'coreclr.dll')) 'Self-contained runtime installed'
    Check (Test-Path -LiteralPath $uninstallKey) 'Windows uninstall registration created'
    Check (Test-Path -LiteralPath $shortcut) 'Start menu shortcut created'
    $shell = New-Object -ComObject WScript.Shell
    Check ($shell.CreateShortcut($shortcut).TargetPath -eq $exe) 'Start menu shortcut targets installed application'
    $model = Get-Content -LiteralPath $config -Raw | ConvertFrom-Json
    Check ($model.apps.Count -eq 0) 'Fresh install has no preset or personal applications'
    Check (!(Test-Path -LiteralPath (Join-Path $installDir 'Logs')) -and !(Test-Path -LiteralPath (Join-Path $installDir 'Cache'))) 'No development logs or cache bundled'
    Check (@(Get-ChildItem -LiteralPath $installDir -Recurse -File -Filter '*.bak').Count -eq 0) 'No personal configuration backups bundled'
    $model.startFullscreen = $false
    Set-Content -LiteralPath $config -Value ($model | ConvertTo-Json -Depth 20) -Encoding utf8
    $appProcess = Start-Process -FilePath $exe -WorkingDirectory $installDir -WindowStyle Hidden -PassThru
    try {
        Check ($appProcess.WaitForInputIdle(15000) -and !$appProcess.HasExited) 'Installed WPF application starts successfully'
    } finally {
        if (!$appProcess.HasExited) { Stop-Process -Id $appProcess.Id -Force }
        $appProcess.WaitForExit()
    }
    if (Test-Path -LiteralPath (Join-Path $installDir 'ptbox.install')) {
        Check (Test-Path -LiteralPath (Join-Path $installDir 'Updater\PTBox.Updater.exe')) 'Independent updater is installed'
        $config = Join-Path $userData 'Config\config.json'
        Check (Test-Path -LiteralPath $config) 'Installed app migrates configuration to personal data directory'
        $migrated = Get-Content -LiteralPath $config -Raw | ConvertFrom-Json
        Check ($migrated.startFullscreen -eq $false -and $migrated.apps.Count -eq 0) 'Migration preserves configured startup mode and empty app list'
    }
    # Use a customized valid config to verify upgrade preservation byte-for-byte.
    $model.apps = @(@{ id='installer-test'; name='个人应用保留测试'; type='exe'; path='explorer.exe'; category='apps'; launchBehavior='fireAndForget'; reuseExisting=$true })
    Set-Content -LiteralPath $config -Value ($model | ConvertTo-Json -Depth 20) -Encoding utf8
    $customHash = (Get-FileHash -LiteralPath $config).Hash
    Run-Setup 'upgrade.log'
    Check ((Get-FileHash -LiteralPath $config).Hash -eq $customHash) 'Upgrade preserves personal configuration byte-for-byte'
    Remove-TestInstall
    Check (!(Test-Path -LiteralPath $exe)) 'Uninstall removes application binaries'
    Check (!(Test-Path -LiteralPath $uninstallKey)) 'Uninstall removes Windows registration'
    Check (!(Test-Path -LiteralPath $shortcut)) 'Uninstall removes test shortcuts'
    Check ((Get-FileHash -LiteralPath $config).Hash -eq $customHash) 'Uninstall preserves personal configuration'
    Check ((Get-ItemProperty -LiteralPath $runKey -ErrorAction SilentlyContinue).'PTBox.Launcher' -eq $autorunBefore) 'Existing autorun setting is unchanged'
    if ($portableHash) { Check ((Get-FileHash -LiteralPath $portableConfig).Hash -eq $portableHash) 'Portable configuration is unchanged' }
    @{ installer=$Installer; sha256=(Get-FileHash -LiteralPath $Installer).Hash; date=(Get-Date -Format o); checks=$checks.ToArray(); testRoot=$testRoot } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $testRoot 'result.json') -Encoding utf8
    Write-Output "PASS: $($checks.Count) installer checks. Report: $testRoot\result.json"
} finally {
    # Only the unique test directory and its own installer registration are removed.
    if (Test-Path -LiteralPath (Join-Path $installDir 'unins000.exe')) { Remove-TestInstall }
}
