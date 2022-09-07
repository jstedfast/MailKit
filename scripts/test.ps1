[CmdletBinding()]
param (
    [Parameter()]
    [string]
    $TestResults = "TestResults.xml",
    [string]
    $GenerateCodeCoverage = "no"
)

Write-Output "TestResults:          $TestResults"
Write-Output "GenerateCodeCoverage: $GenerateCodeCoverage"
Write-Output ""

[xml]$project = Get-Content UnitTests\UnitTests.csproj

$nugetDir = Join-Path $Home ".nuget"
$nugetPackagesDir = Join-Path $nugetDir "packages"

# Get the NUnit.ConsoleRunner executable path
$packageReference = $project.SelectSingleNode("/Project/ItemGroup/PackageReference[@Include='NUnit.ConsoleRunner']")
$consoleRunnerVersion = $packageReference.GetAttribute("Version")
$consoleRunnerBasePackageDir = Join-Path $nugetPackagesDir "nunit.consolerunner"
$consoleRunnerPackageDir = Join-Path $consoleRunnerBasePackageDir $consoleRunnerVersion
$consoleRunnerToolsDir = Join-Path $consoleRunnerPackageDir "tools"

$NUnitConsoleRunner = Join-Path $consoleRunnerToolsDir "nunit3-console.exe"

# Get the OutputPath
$targetFramework = $project.SelectSingleNode("/Project/PropertyGroup/TargetFramework")
$OutputDir = Join-Path "UnitTests\bin\Debug" $targetFramework.InnerText
$UnitTestsAssembly = Join-Path $OutputDir "UnitTests.dll"

if ($GenerateCodeCoverage -eq 'yes') {
    # Get the OpenCover executable path
    $packageReference = $project.SelectSingleNode("/Project/ItemGroup/PackageReference[@Include='OpenCover']")
    $openCoverVersion = $packageReference.GetAttribute("Version")
    $openCoverBasePackageDir = Join-Path $nugetPackagesDir "opencover"
    $openCoverPackageDir = Join-Path $openCoverBasePackageDir $openCoverVersion
    $openCoverToolsDir = Join-Path $openCoverPackageDir "tools"

    $OpenCoverProfiler32 = Join-Path $openCoverToolsDir "x86\OpenCover.Profiler.dll"
    $OpenCoverProfiler64 = Join-Path $openCoverToolsDir "x64\OpenCover.Profiler.dll"
    $OpenCover = Join-Path $openCoverToolsDir "OpenCover.Console.exe"

    try {
        & regsvr32 $OpenCoverProfiler32
    } catch {
        Write-Output "Failed to register $OpenCoverProfiler32"
    }

    try {
        & regsvr32 $OpenCoverProfiler64
    } catch {
        Write-Output "Failed to register $OpenCoverProfiler64"
    }

    Write-Output "Running the UnitTests (code coverage enabled)"

    # Run OpenCover
    & $OpenCover -filter:"+[MailKit]* -[MimeKit]* -[UnitTests]*" `
        -target:"$NUnitConsoleRunner" `
        -targetargs:"--domain:single --output:$TestResults $UnitTestsAssembly" `
        -output:opencover.xml
} else {
    Write-Output "Running the UnitTests"

    # Run OpenCover
    & $NUnitConsoleRunner --domain:single --output:$TestResults $UnitTestsAssembly
}