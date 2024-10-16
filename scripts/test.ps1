[CmdletBinding()]
param (
    [Parameter()]
    [string]
    $Configuration = "Debug",
    [string]
    $GenerateCodeCoverage = "false"
)

Write-Output "Configuration:        $Configuration"
Write-Output "GenerateCodeCoverage: $GenerateCodeCoverage"
Write-Output ""

[xml]$project = Get-Content UnitTests\UnitTests.csproj

# Get the OutputPath
$targetFramework = $project.SelectSingleNode("/Project/PropertyGroup/TargetFramework")
$OutputDir = Join-Path "UnitTests" "bin" $Configuration $targetFramework.InnerText
$UnitTestsAssembly = Join-Path $OutputDir "UnitTests.dll"

if ($GenerateCodeCoverage -eq 'true') {
    Write-Output "Instrumenting code..."

    & dotnet AltCover -i="$OutputDir" --inplace -s="System.*" -s="Microsoft.*" -s="Newtonsoft.*" -s="BouncyCastle.*" -s="MimeKit" -s="NUnit*" -s="AltCover.*" -s="testhost" -s="UnitTests"
    # & dotnet AltCover Runner --recorderDirectory=$OutputDir --executable=$NUnitConsoleRunner --summary=O -- --domain:single $UnitTestsAssembly
}

Write-Output "Running the UnitTests"

& dotnet nunit $UnitTestsAssembly
