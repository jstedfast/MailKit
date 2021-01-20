$NUnitConsoleRunner = Join-Path $Home ".nuget\packages\nunit.consolerunner\3.12.0\tools\nunit3-console.exe"

& $NUnitConsoleRunner --domain:single "UnitTests\bin\Release\net48\UnitTests.dll"
