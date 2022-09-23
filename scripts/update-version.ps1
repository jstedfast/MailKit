[CmdletBinding()]
param (
    [Parameter()]
    [string] $ProjectName = "MailKit",
    [string] $Version
)

function Update-AssemblyVersionValue {
    param (
        [string] $AssemblyInfoPath,
        [string] $AttributeName,
        [string] $Version
    )

    $pattern = "\[assembly:(\s+)$AttributeName(\s+)\(""(.*?)""\)\]"
    (Get-Content $AssemblyInfoPath) | ForEach-Object {
        $_ -creplace $pattern, "[assembly:`$1$AttributeName`$2(""$Version"")]"
    } | Set-Content $AssemblyInfoPath
}

function Update-AssemblyInfo {
    param (
        [string] $AssemblyInfoPath,
        [string] $Version
    )

    $versionInfo = $Version.Split('.')
    if ($versionInfo.Count -ge 2) {
        $assemblyVersion = $versionInfo[0] + "." + $versionInfo[1] + ".0.0"
    } else {
        $assemblyVersion = $versionInfo[0] + ".0.0.0"
    }

    switch ($versionInfo.Count) {
        3 { $assemblyFileVersion = $Version + ".0" }
        2 { $assemblyFileVersion = $Version + ".0.0" }
        1 { $assemblyFileVersion = $Version + ".0.0.0" }
        default { $assemblyFileVersion = $Version }
    }

    Write-Host "AssemblyInformationalVersion: $assemblyFileVersion"
    Write-Host "AssemblyFileVersion: $assemblyFileVersion"
    Write-Host "AssemblyVersion: $assemblyVersion"

    Update-AssemblyVersionValue -AssemblyInfoPath $AssemblyInfoPath -AttributeName "AssemblyInformationalVersion" -Version $assemblyFileVersion
    Update-AssemblyVersionValue -AssemblyInfoPath $AssemblyInfoPath $csharp -AttributeName "AssemblyFileVersion" -Version $assemblyFileVersion
    Update-AssemblyVersionValue -AssemblyInfoPath $AssemblyInfoPath $csharp -AttributeName "AssemblyVersion" -Version $assemblyVersion
}

function Update-Project {
    param (
        [string] $ProjectFile,
        [string] $Version
    )

    $project = New-Object -TypeName System.Xml.XmlDocument
    $project.PreserveWhitespace = $True
    $project.Load($ProjectFile)
    $versionPrefix = $project.SelectSingleNode("/Project/PropertyGroup/VersionPrefix")
    $versionPrefix.InnerText = $Version
    $project.Save($ProjectFile)
}

function Update-NuGetPackageVersion {
    param (
        [string] $ProjectFile,
        [string] $PackageName,
        [string] $PackageVersion
    )

    $project = New-Object -TypeName System.Xml.XmlDocument
    $project.PreserveWhitespace = $True
    $project.Load($ProjectFile)
    $xmlns = $project.Project.GetAttribute("xmlns")
    if ($null -ne $xmlns) {
        $ns = New-Object System.Xml.XmlNamespaceManager($project.NameTable)
        $ns.AddNamespace("ns", $xmlns)
        $packageReference = $project.SelectSingleNode("ns:Project/ns:ItemGroup/ns:PackageReference[@Include='$PackageName']", $ns)
    } else {
        $packageReference = $project.SelectSingleNode("/Project/ItemGroup/PackageReference[@Include='$PackageName']")
    }
    $packageReference.Version = $PackageVersion
    $project.Save($ProjectFile)
}

$assemblyInfo = Join-Path $ProjectName "Properties" "AssemblyInfo.cs"
$assemblyInfo = Resolve-Path $assemblyInfo
Write-Host "Updating $assemblyInfo..."
Update-AssemblyInfo -AssemblyInfoPath $assemblyInfo -Version $Version

$fileName = $ProjectName + ".csproj"
$projectFile = Join-Path $ProjectName $fileName
$projectFile = Resolve-Path $projectFile
Write-Host "Updating $projectFile..."
Update-Project -ProjectFile $projectFile -Version $Version

$fileName = $ProjectName + "Lite.csproj"
$projectFile = Join-Path $ProjectName $fileName
$projectFile = Resolve-Path $projectFile
Write-Host "Updating $projectFile..."
Update-Project -ProjectFile $projectFile -Version $Version

$sampleProjects = Get-ChildItem "samples" -Filter "*.csproj" -Recurse
foreach ($projectFile in $sampleProjects) {
    $projectFile = Resolve-Path $projectFile
    Write-Host "Updating $projectFile..."
    Update-NuGetPackageVersion -ProjectFile $projectFile -PackageName "MimeKit" -PackageVersion $Version
}
