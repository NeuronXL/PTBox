param(
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$FromVersion = '1.1.0',
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$ToVersion = '1.1.1',
    [switch]$InsideSandbox
)
$ErrorActionPreference = 'Stop'
if (!$InsideSandbox) {
    $project = Split-Path $PSScriptRoot -Parent
    $releases = Join-Path $project 'artifacts\releases'
    foreach ($version in @($FromVersion, $ToVersion)) {
        if (!(Test-Path -LiteralPath (Join-Path $releases "$version\PTBox-Setup-$version-win-x64.exe"))) { throw "Prepare local signed release $version first using release.ps1." }
    }
    $resultDirectory = Join-Path $project ('artifacts\sandbox-results\' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $resultDirectory -Force | Out-Null
    $escape = { param($text) [Security.SecurityElement]::Escape($text) }
    $releaseXml = & $escape $releases
    $scriptsXml = & $escape $PSScriptRoot
    $resultsXml = & $escape $resultDirectory
    $command = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\PTBoxTest\scripts\test-update-sandbox.ps1 -InsideSandbox -FromVersion $FromVersion -ToVersion $ToVersion"
    $commandXml = & $escape $command
    $xml = @"
<Configuration>
  <Networking>Disable</Networking>
  <MappedFolders>
    <MappedFolder><HostFolder>$releaseXml</HostFolder><SandboxFolder>C:\PTBoxTest\releases</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$scriptsXml</HostFolder><SandboxFolder>C:\PTBoxTest\scripts</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$resultsXml</HostFolder><SandboxFolder>C:\PTBoxTest\results</SandboxFolder><ReadOnly>false</ReadOnly></MappedFolder>
  </MappedFolders>
  <LogonCommand><Command>$commandXml</Command></LogonCommand>
</Configuration>
"@
    $configuration = Join-Path $resultDirectory 'PTBox-update-test.wsb'
    Set-Content -LiteralPath $configuration -Value $xml -Encoding utf8
    Write-Output "Prepared sandbox configuration: $configuration"
    Write-Output 'Open this .wsb on a Windows 11 PC with Windows Sandbox enabled; this script does not enable features or launch it automatically.'
    return
}
if ($env:USERNAME -ne 'WDAGUtilityAccount') { throw 'The install harness runs only inside Windows Sandbox, never on your normal account.' }
$results = 'C:\PTBoxTest\results'
$releases = 'C:\PTBoxTest\releases'
$install = Join-Path $env:LOCALAPPDATA 'Programs\PTBox'
$data = Join-Path $env:LOCALAPPDATA 'PTBox'
if (Test-Path -LiteralPath $data) { throw 'Sandbox is not clean.' }
$checks = [Collections.Generic.List[string]]::new()
function Check([bool]$condition, [string]$message) { if (!$condition) { throw $message }; $checks.Add($message) }
function Write-Json([string]$path, $value) { $json = $value | ConvertTo-Json -Depth 20; [IO.File]::WriteAllText($path, $json, [Text.UTF8Encoding]::new($false)) }
try {
    $setup = Join-Path $releases "$FromVersion\PTBox-Setup-$FromVersion-win-x64.exe"
    $process = Start-Process -FilePath $setup -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/LOG=`"$results\bootstrap-install.log`"" -PassThru -Wait -WindowStyle Hidden
    Check ($process.ExitCode -eq 0) 'Bootstrap installation succeeds'
    New-Item -ItemType Directory -Path (Join-Path $data 'Updates') -Force | Out-Null
    Write-Json (Join-Path $data 'Updates\preferences.json') @{ automatic=$false }
    $legacy = Join-Path $install 'Config\config.json'
    Write-Json $legacy @{version=1; apps=@(@{id='personal'; name='个人配置保留'; type='exe'; path='explorer.exe'; category='apps'; launchBehavior='fireAndForget'; reuseExisting=$true}); lastSelectedTile='personal'; autoStart=$false; startFullscreen=$false; theme='Charcoal'; background='' }
    # Visible only inside the manually opened interactive Sandbox, for UI acceptance and graceful window close.
    $parent = Start-Process -FilePath (Join-Path $install 'PTBox.Launcher.exe') -PassThru -WindowStyle Normal
    Check ($parent.WaitForInputIdle(30000)) 'Old version starts'
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while (!(Test-Path -LiteralPath (Join-Path $data 'Config\config.json')) -and $timer.Elapsed.TotalSeconds -lt 30) { Start-Sleep -Milliseconds 200 }
    Check (Test-Path -LiteralPath (Join-Path $data 'Config\config.json')) 'Legacy config migrates'
    $id = [Guid]::NewGuid().ToString('N')
    $job = Join-Path $data "Updates\jobs\$id"
    New-Item -ItemType Directory -Path $job -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $install 'Updater') -Destination (Join-Path $job 'runner') -Recurse
    Copy-Item -LiteralPath (Join-Path $releases "$ToVersion\PTBox-Setup-$ToVersion-win-x64.exe") -Destination (Join-Path $job 'package.exe')
    foreach ($name in @('update.json','update.json.sig')) { Copy-Item -LiteralPath (Join-Path $releases "$ToVersion\$name") -Destination $job }
    Write-Json (Join-Path $job 'job.json') @{ id=$id; installDirectory=$install; parentPid=$parent.Id; parentStartTicks=$parent.StartTime.ToUniversalTime().Ticks; currentVersion=$FromVersion }
    $updater = Start-Process -FilePath (Join-Path $job 'runner\PTBox.Updater.exe') -ArgumentList $id -PassThru -WindowStyle Normal
    $timer.Restart()
    while (!(Test-Path -LiteralPath (Join-Path $job 'ready')) -and !$updater.HasExited -and $timer.Elapsed.TotalSeconds -lt 40) { Start-Sleep -Milliseconds 200 }
    Check (Test-Path -LiteralPath (Join-Path $job 'ready')) 'Updater verifies signed package and parent identity'
    [IO.File]::WriteAllText((Join-Path $job 'go'), 'go')
    $parent.Refresh(); Check ($parent.CloseMainWindow()) 'Launcher closes gracefully'
    Check ($parent.WaitForExit(30000)) 'Old process exits before installation'
    Check ($updater.WaitForExit(180000)) 'Updater finishes in bounded test time'
    $result = Get-Content -LiteralPath (Join-Path $job 'result.json') -Raw | ConvertFrom-Json
    Check ($result.state -eq 'Completed') 'Installed new version acknowledges startup'
    $model = Get-Content -LiteralPath (Join-Path $data 'Config\config.json') -Raw | ConvertFrom-Json
    Check ($model.apps[0].name -eq '个人配置保留' -and $model.theme -eq 'Charcoal') 'Personal app and theme preserved'
    Check (([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $install 'PTBox.Launcher.exe'))).ProductVersion -eq $ToVersion) 'Installed version matches target'
    $newProcesses = @(Get-Process PTBox.Launcher -ErrorAction SilentlyContinue)
    Check ($newProcesses.Count -eq 1) 'Only one new Launcher instance runs'
    Copy-Item -LiteralPath (Join-Path $job 'setup.log') -Destination $results
    Write-Json (Join-Path $results 'result.json') @{ state='Passed'; from=$FromVersion; to=$ToVersion; checks=$checks.ToArray(); at=(Get-Date -Format o) }
} catch {
    $_ | Out-String | Set-Content -LiteralPath (Join-Path $results 'failure.txt') -Encoding utf8
    if (Test-Path -LiteralPath $data) { Get-ChildItem -LiteralPath (Join-Path $data 'Updates\jobs') -Filter '*.log' -File -Recurse -ErrorAction SilentlyContinue | Copy-Item -Destination $results -Force }
    Write-Json (Join-Path $results 'result.json') @{ state='Failed'; message=$_.Exception.Message; checks=$checks.ToArray() }
    throw
}
