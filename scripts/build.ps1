. "$PSScriptRoot\common.ps1"
& $dotnetExe build PTBox.sln -c Release -m:1
Assert-DotnetSuccess
