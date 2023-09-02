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
    Write-Output "Instrumenting code..."

    & dotnet AltCover -i="$OutputDir" --inplace -s="System.*" -s="Microsoft.*" -s="Newtonsoft.*" -s="BouncyCastle.*" -s="MimeKit" -s="NUnit*" -s="AltCover.*" -s="testhost" -s="UnitTests"
    # & dotnet AltCover Runner --recorderDirectory=$OutputDir --executable=$NUnitConsoleRunner --summary=O -- --domain:single $UnitTestsAssembly
}

Write-Output "Running the UnitTests"

& $NUnitConsoleRunner --domain:single $UnitTestsAssembly
