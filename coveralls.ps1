$NUnitConsoleRunner = Join-Path $Home ".nuget\packages\nunit.consolerunner\3.12.0\tools\nunit3-console.exe"
$Coveralls = Join-Path $Home ".nuget\packages\coveralls.net\0.7.0\tools\csmacnz.Coveralls.exe"
$OpenCoverDir = Join-Path $Home ".nuget\packages\opencover\4.6.519\tools"
$OpenCoverProfiler32 = Join-Path $OpenCoverDir "x86\OpenCover.Profiler.dll"
$OpenCoverProfiler64 = Join-Path $OpenCoverDir "x64\OpenCover.Profiler.dll"
$OpenCover = Join-Path $OpenCoverDir "OpenCover.Console.exe"
$OutputDir = "UnitTests\bin\Debug\net48"

& regsvr32 $OpenCoverProfiler32

& regsvr32 $OpenCoverProfiler64

& $OpenCover -filter:"+[MailKit]* -[MimeKit]* -[UnitTests]*" `
	-target:"$NUnitConsoleRunner" `
	-targetdir:"$OutputDir" `
	-targetargs:"--domain:single UnitTests.dll" `
	-output:opencover.xml

& $Coveralls --opencover -i opencover.xml `
	--repoToken $env:COVERALLS_REPO_TOKEN `
	--useRelativePaths `
	--basePath $OutputDir `
	--commitId $env:GIT_COMMIT_SHA `
	--commitBranch $env:GIT_REF.Replace('refs/heads/', '') `
	--commitAuthor $env:GIT_ACTOR `
	--commitEmail $env:GIT_ACTOR_EMAIL `
	--commitMessage $env:GIT_COMMIT_MESSAGE `
	--jobId $env:COVERALLS_JOB_ID