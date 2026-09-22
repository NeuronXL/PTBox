. "$PSScriptRoot\common.ps1"
& $dotnetExe build src\PTBox.Launcher\PTBox.Launcher.csproj -c Release -m:1
Assert-DotnetSuccess
$launcherExe = Join-Path $projectRoot 'src\PTBox.Launcher\bin\Release\net8.0-windows\PTBox.Launcher.exe'
Start-Process -FilePath $launcherExe -WorkingDirectory (Split-Path $launcherExe -Parent)
