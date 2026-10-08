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

    # Note: MailKit must be re-signed with its strong-name key after instrumentation or else MimeKit's
    # InternalsVisibleTo ("MailKit, PublicKey=...") will no longer match, causing MethodAccessExceptions.
    $StrongNameKey = Join-Path "MailKit" "mailkit.snk"

    & dotnet AltCover -i="$OutputDir" --inplace --strongNameKey="$StrongNameKey" -s="System.*" -s="Microsoft.*" -s="Newtonsoft.*" -s="BouncyCastle.*" -s="MimeKit" -s="NUnit*" -s="AltCover.*" -s="testhost" -s="UnitTests"
    # & dotnet AltCover Runner --recorderDirectory=$OutputDir --executable=$NUnitConsoleRunner --summary=O -- --domain:single $UnitTestsAssembly
}

Write-Output "Running the UnitTests"

& dotnet nunit $UnitTestsAssembly
