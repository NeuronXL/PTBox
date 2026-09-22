$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$localDotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if (Test-Path -LiteralPath $localDotnet) {
    $dotnetExe = $localDotnet
    $env:DOTNET_ROOT = Split-Path $localDotnet -Parent
    $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
} else {
    $dotnetExe = (Get-Command dotnet -ErrorAction Stop).Source
}
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
Set-Location -LiteralPath $projectRoot
function Assert-DotnetSuccess { if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE" } }
