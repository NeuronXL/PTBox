param(
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '1.1.4',
    [string]$NotesFile = '',
    [string]$PrivateKey = '',
    [switch]$SkipTests
)
. "$PSScriptRoot\common.ps1"
# Local preparation only: this script never initializes Git or contacts GitHub to upload/publish.
if (!$NotesFile) { $NotesFile = Join-Path $projectRoot "docs\releases\$Version.md" }
if (!$PrivateKey) { $PrivateKey = Join-Path $projectRoot '.tools\release-secrets\release.private.bin' }
foreach ($file in @($NotesFile, $PrivateKey)) { if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw "Required local file missing: $file" } }
$output = Join-Path $projectRoot "artifacts\releases\$Version"
if (Test-Path -LiteralPath $output) { throw "Release $Version already exists locally; do not overwrite it." }
if (!$SkipTests) { & "$PSScriptRoot\test.ps1" }
& "$PSScriptRoot\package.ps1" -Version $Version
& $dotnetExe run --project tools\PTBox.ReleaseTool -c Release -- create $Version (Join-Path $projectRoot "artifacts\installer\PTBox-Setup-$Version-win-x64.exe") $NotesFile $PrivateKey $output
Assert-DotnetSuccess
& $dotnetExe run --project tools\PTBox.ReleaseTool -c Release -- verify $output
Assert-DotnetSuccess
Write-Output "Prepared locally for https://github.com/NeuronXL/PTBox/releases : $output"
