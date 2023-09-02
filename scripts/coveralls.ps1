[xml]$project = Get-Content UnitTests\UnitTests.csproj

# Get the OutputPath
$targetFramework = $project.SelectSingleNode("/Project/PropertyGroup/TargetFramework")
$binDir = Join-Path "UnitTests" "bin"
$debugDir = Join-Path $binDir "Debug"
$OutputDir = Join-Path $debugDir $targetFramework.InnerText

# Upload code-coverage data to coveralls.io
& dotnet tool run csmacnz.Coveralls --opencover -i coverage.xml `
	--repoToken $env:COVERALLS_REPO_TOKEN `
	--useRelativePaths `
	--basePath $OutputDir `
	--commitId $env:GIT_COMMIT_SHA `
	--commitBranch $env:GIT_REF.Replace('refs/heads/', '') `
	--commitAuthor $env:GIT_ACTOR `
	--commitEmail $env:GIT_ACTOR_EMAIL `
	--commitMessage $env:GIT_COMMIT_MESSAGE `
	--jobId $env:COVERALLS_JOB_ID
