[CmdletBinding()]
param (
    [Parameter()]
    [string]
    $Configuration = "Debug",
    [string]
    $GenerateCodeCoverage = "no"
)

Write-Output "Configuration:        $Configuration"
Write-Output "GenerateCodeCoverage: $GenerateCodeCoverage"
Write-Output ""

[xml]$project = Get-Content UnitTests\UnitTests.csproj

$nugetPackagesDir = Join-Path $Home ".nuget" "packages"

# Get the NUnit.ConsoleRunner executable path
$packageReference = $project.SelectSingleNode("/Project/ItemGroup/PackageReference[@Include='NUnit.ConsoleRunner']")
$consoleRunnerVersion = $packageReference.GetAttribute("Version")

$NUnitConsoleRunner = Join-Path $nugetPackagesDir "nunit.consolerunner" $consoleRunnerVersion "tools" "nunit3-console.exe"

# Get the OutputPath
$targetFramework = $project.SelectSingleNode("/Project/PropertyGroup/TargetFramework")
$OutputDir = Join-Path "UnitTests" "bin" $Configuration $targetFramework.InnerText
$UnitTestsAssembly = Join-Path $OutputDir "UnitTests.dll"

if ($GenerateCodeCoverage -eq 'yes') {
    # Get the OpenCover executable path
    $packageReference = $project.SelectSingleNode("/Project/ItemGroup/PackageReference[@Include='OpenCover']")
    $openCoverVersion = $packageReference.GetAttribute("Version")
    $openCoverToolsDir = Join-Path $nugetPackagesDir "opencover" $openCoverVersion "tools"

    $OpenCoverProfiler32 = Join-Path $openCoverToolsDir "x86" "OpenCover.Profiler.dll"
    $OpenCoverProfiler64 = Join-Path $openCoverToolsDir "x64" "OpenCover.Profiler.dll"
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
        -targetargs:"--domain:single $UnitTestsAssembly" `
        -output:opencover.xml
} else {
    Write-Output "Running the UnitTests"

    # Run OpenCover
    & $NUnitConsoleRunner --domain:single $UnitTestsAssembly
}