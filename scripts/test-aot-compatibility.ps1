param([string]$useMailKitLite)

$rootDirectory = Split-Path $PSScriptRoot -Parent
$publishOutput = dotnet publish $rootDirectory/AotCompatibility/AotCompatibility.csproj -nodeReuse:false /p:UseSharedCompilation=false /p:ExposeExperimentalFeatures=true /p:UseMailKitLite=$useMailKitLite

$actualWarningCount = 0

foreach ($line in $($publishOutput -split "`r`n"))
{
    if ($line -like "*warning IL*")
    {
        Write-Host $line

        $actualWarningCount += 1
    }
}

pushd $rootDirectory/AotCompatibility/bin/Release/net8.0/win-x64/publish

Write-Host "Executing test App..."
./AotCompatibility.exe
Write-Host "Finished executing test App"

if ($LastExitCode -ne 0)
{
  Write-Host "There was an error while executing AotCompatibility Test App. LastExitCode is:", $LastExitCode
}

popd

Write-Host "Actual warning count is:", $actualWarningCount
$expectedWarningCount = 0

$testPassed = 0
if ($actualWarningCount -ne $expectedWarningCount)
{
    $testPassed = 1
    Write-Host "Actual warning count:", actualWarningCount, "is not as expected. Expected warning count is:", $expectedWarningCount
}

Exit $testPassed
