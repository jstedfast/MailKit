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

# Get the OpenCover executable path
$packageReference = $project.SelectSingleNode("/Project/ItemGroup/PackageReference[@Include='OpenCover']")
$openCoverVersion = $packageReference.GetAttribute("Version")
$openCoverBasePackageDir = Join-Path $nugetPackagesDir "opencover"
$openCoverPackageDir = Join-Path $openCoverBasePackageDir $openCoverVersion
$openCoverToolsDir = Join-Path $openCoverPackageDir "tools"

$OpenCoverProfiler32 = Join-Path $openCoverToolsDir "x86\OpenCover.Profiler.dll"
$OpenCoverProfiler64 = Join-Path $openCoverToolsDir "x64\OpenCover.Profiler.dll"
$OpenCover = Join-Path $openCoverToolsDir "OpenCover.Console.exe"

# Get the OutputPath
$targetFramework = $project.SelectSingleNode("/Project/PropertyGroup/TargetFramework")
$OutputDir = Join-Path "UnitTests\bin\Debug" $targetFramework.InnerText

& regsvr32 $OpenCoverProfiler32

& regsvr32 $OpenCoverProfiler64

Write-Host "Running the UnitTests"

# Run OpenCover
& $OpenCover -filter:"+[MailKit]* -[MimeKit]* -[UnitTests]*" `
	-target:"$NUnitConsoleRunner" `
	-targetdir:"$OutputDir" `
	-targetargs:"--domain:single UnitTests.dll" `
	-output:opencover.xml

Write-Host "Uploading coverage data to coveralls.io"

# Upload code-coverage data to coveralls.io
& dotnet tool run csmacnz.Coveralls --opencover -i opencover.xml `
	--repoToken $env:COVERALLS_REPO_TOKEN `
	--useRelativePaths `
	--basePath $OutputDir `
	--commitId $env:GIT_COMMIT_SHA `
	--commitBranch $env:GIT_REF.Replace('refs/heads/', '') `
	--commitAuthor $env:GIT_ACTOR `
	--commitEmail $env:GIT_ACTOR_EMAIL `
	--commitMessage $env:GIT_COMMIT_MESSAGE `
	--jobId $env:COVERALLS_JOB_ID
