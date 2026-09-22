. "$PSScriptRoot\common.ps1"
& $dotnetExe build PTBox.sln -c Release -m:1
Assert-DotnetSuccess
& $dotnetExe tests\PTBox.Tests\bin\Release\net8.0-windows\PTBox.Tests.dll tests\PTBox.TestApp\bin\Release\net8.0-windows\PTBox.TestApp.exe artifacts\verification
Assert-DotnetSuccess
