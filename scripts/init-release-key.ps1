. "$PSScriptRoot\common.ps1"
$secretDir = Join-Path $projectRoot '.tools\release-secrets'
New-Item -ItemType Directory -Path $secretDir -Force | Out-Null
# Windows DPAPI encrypts private material for the current Windows account.
& $dotnetExe run --project tools\PTBox.ReleaseTool -c Release -- init-key (Join-Path $secretDir 'release.private.bin') (Join-Path $projectRoot 'src\PTBox.UpdateCore\ReleaseTrust.json')
Assert-DotnetSuccess
Write-Output 'Key is DPAPI encrypted for this Windows account. Use export-key for a secure offline backup before reinstalling Windows.'
