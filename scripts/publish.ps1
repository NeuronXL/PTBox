. "$PSScriptRoot\common.ps1"
$publishDirectory = Join-Path $projectRoot 'artifacts\PTBox-win-x64'
$existingConfig = Join-Path $publishDirectory 'Config\config.json'
$configBytes = if (Test-Path -LiteralPath $existingConfig) { [System.IO.File]::ReadAllBytes($existingConfig) } else { $null }
try {
    & $dotnetExe publish src\PTBox.Launcher\PTBox.Launcher.csproj -c Release -r win-x64 --self-contained true -o $publishDirectory -p:PublishSingleFile=false -m:1
    Assert-DotnetSuccess
} finally {
    if ($null -ne $configBytes) { [System.IO.File]::WriteAllBytes($existingConfig, $configBytes) }
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $publishDirectory -Force
Write-Output "Ready: $publishDirectory\PTBox.Launcher.exe"
