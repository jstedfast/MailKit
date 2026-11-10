#Requires -Version 7.0
<#
.SYNOPSIS
Builds and smoke-tests real NuGet packages using isolated, package-only consumers.
.DESCRIPTION
Requires the .NET 10 SDK, .NET 8 and 10 runtimes, and Native AOT prerequisites.
Building packages also requires Windows and nuget.exe. Other hosts can consume
prebuilt packages with -PackageDirectory. Nothing is published or installed in
certificate stores. Failed runs retain their temporary artifacts for diagnosis.
#>
[CmdletBinding()]
param(
    [string] $PackageDirectory,
    [string] $DependencySource = 'https://api.nuget.org/v3/index.json',
    [switch] $SkipCryptography,
    [switch] $SkipAot,
    [switch] $KeepArtifacts
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path $PSScriptRoot -Parent
$mimekit = Join-Path $root 'submodules' 'MimeKit'
$frameworks = @('net462', 'net47', 'net48', 'netstandard2.0', 'netstandard2.1', 'net8.0', 'net10.0')
$specifications = [ordered]@{
    'MailKit' = Join-Path $root 'nuget' 'MailKit.nuspec'
    'MimeKit.Core' = Join-Path $mimekit 'nuget' 'MimeKit.Core.nuspec'
}
if (!$SkipCryptography) {
    $specifications['MimeKit.Cryptography'] = Join-Path $mimekit 'nuget' 'MimeKit.Cryptography.nuspec'
}

$versions = @{}
foreach ($id in $specifications.Keys) {
    if (!(Test-Path -LiteralPath $specifications[$id])) {
        throw "Missing $($specifications[$id]). Initialize submodules with git submodule update --init --recursive."
    }
    [xml] $specification = Get-Content -LiteralPath $specifications[$id] -Raw
    $versions[$id] = [string] $specification.package.metadata.version
}

$work = Join-Path ([IO.Path]::GetTempPath()) ("mailkit-package-smoke-" + [Guid]::NewGuid().ToString('N'))
$feed = Join-Path $work 'feed'
$cache = Join-Path $work 'packages'
$config = Join-Path $work 'NuGet.Config'
$log = Join-Path $work 'smoke.log'
$environment = @{}
foreach ($name in @('NUGET_PACKAGES', 'NUGET_HTTP_CACHE_PATH', 'NUGET_PLUGINS_CACHE_PATH')) {
    $environment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$succeeded = $false

function Invoke-Checked {
    param([string] $Command, [string[]] $Arguments, [switch] $RejectILWarnings)

    Write-Host "`n> $Command $($Arguments -join ' ')"
    & $Command @Arguments 2>&1 | Tee-Object -FilePath $log -Append | Tee-Object -Variable output | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "$Command failed with exit code $LASTEXITCODE. See $log."
    }
    if ($RejectILWarnings -and ($output -match '\bwarning IL\d+\b')) {
        throw "Native AOT emitted trimming/AOT warnings. See $log."
    }
}

function Test-Package {
    param([string] $Id, [string] $Path)

    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('/', '\') })
        foreach ($framework in $frameworks) {
            foreach ($extension in @('dll', 'xml', 'pdb')) {
                $entry = "lib\$framework\$Id.$extension"
                if ($entries -cnotcontains $entry) {
                    throw "$Path is missing $entry."
                }
            }
        }
        if ($Id -eq 'MailKit' -and $entries -cnotcontains 'docs\PortingGuide.md') {
            throw "$Path is missing docs\PortingGuide.md."
        }
        $manifest = $archive.GetEntry("$Id.nuspec")
        if ($null -eq $manifest) {
            throw "$Path is missing $Id.nuspec."
        }
        $reader = [IO.StreamReader]::new($manifest.Open())
        try {
            [xml] $metadata = $reader.ReadToEnd()
        } finally {
            $reader.Dispose()
        }
        if ($metadata.package.metadata.id -cne $Id -or $metadata.package.metadata.version -cne $versions[$Id]) {
            throw "$Path does not contain the expected package ID/version."
        }
    } finally {
        $archive.Dispose()
    }
}

function New-Consumer {
    param([string] $Name, [string[]] $Frameworks, [bool] $Cryptography = $false)

    $directory = Join-Path $work $Name
    New-Item -ItemType Directory -Path $directory | Out-Null
    Copy-Item -Path (Join-Path $PSScriptRoot 'PackageSmokeTests' '*') -Destination $directory
    $cryptoVersion = if ($Cryptography) { $versions['MimeKit.Cryptography'] } else { '' }
    @"
<Project>
  <PropertyGroup>
    <SmokeFrameworks>$($Frameworks -join ';')</SmokeFrameworks>
    <MailKitVersion>$($versions['MailKit'])</MailKitVersion>
    <CryptographyVersion>$cryptoVersion</CryptographyVersion>
    <WithCryptography>$($Cryptography.ToString().ToLowerInvariant())</WithCryptography>
  </PropertyGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $directory 'Directory.Build.props')
    return Join-Path $directory 'PackageSmokeTests.csproj'
}

function Test-DependencyGraph {
    param([string] $Project, [string[]] $Frameworks, [bool] $Cryptography = $false)

    $assetsPath = Join-Path (Split-Path $Project -Parent) 'obj' 'project.assets.json'
    $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json -AsHashtable
    foreach ($library in $assets.libraries.GetEnumerator()) {
        if ($library.Value.type -ne 'package') {
            throw "Consumer resolved a non-package dependency: $($library.Key)."
        }
        if (!$Cryptography -and $library.Key -match '^(BouncyCastle[^/]*|MimeKit\.Cryptography)/') {
            throw "Core consumer unexpectedly depends on $($library.Key)."
        }
    }
    $ids = @('MailKit', 'MimeKit.Core')
    if ($Cryptography) {
        $ids += 'MimeKit.Cryptography'
        if (!($assets.libraries.Keys -match '^BouncyCastle\.Cryptography/')) {
            throw 'Cryptography consumer did not resolve BouncyCastle.Cryptography.'
        }
    }
    foreach ($id in $ids) {
        $key = "$id/$($versions[$id])"
        if (!$assets.libraries.ContainsKey($key)) {
            throw "Consumer did not resolve $key."
        }
        foreach ($framework in $Frameworks) {
            $expected = "lib/$framework/$id.dll"
            $selected = @($assets.targets.Values | Where-Object {
                $_.ContainsKey($key) -and $_[$key].compile.ContainsKey($expected)
            })
            if ($selected.Count -eq 0) {
                throw "Consumer did not select $expected."
            }
        }
    }
}

try {
    New-Item -ItemType Directory -Path $feed, $cache | Out-Null
    Write-Host "Package smoke test artifacts: $work"

    if (!$PackageDirectory) {
        if (!$IsWindows) {
            throw 'Build the seven-framework packages on Windows, then use -PackageDirectory on this host.'
        }
        Get-Command nuget -ErrorAction Stop | Out-Null
        Invoke-Checked dotnet @('build', (Join-Path $root 'MailKit' 'MailKit.csproj'), '-c', 'Release', '-v', 'minimal')
        if (!$SkipCryptography) {
            Invoke-Checked dotnet @('build', (Join-Path $mimekit 'MimeKit.Cryptography' 'MimeKit.Cryptography.csproj'), '-c', 'Release', '-v', 'minimal')
        }
        foreach ($id in $specifications.Keys) {
            Invoke-Checked nuget @('pack', $specifications[$id], '-OutputDirectory', $feed, '-NonInteractive')
        }
    } else {
        foreach ($id in $specifications.Keys) {
            $package = Join-Path $PackageDirectory "$id.$($versions[$id]).nupkg"
            Copy-Item -LiteralPath $package -Destination $feed
        }
    }
    foreach ($id in $specifications.Keys) {
        Test-Package $id (Join-Path $feed "$id.$($versions[$id]).nupkg")
    }

    $escapedFeed = [Security.SecurityElement]::Escape($feed)
    $escapedSource = [Security.SecurityElement]::Escape($DependencySource)
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="smoke" value="$escapedFeed" />
    <add key="dependencies" value="$escapedSource" />
  </packageSources>
  <fallbackPackageFolders><clear /></fallbackPackageFolders>
  <packageSourceMapping>
    <clear />
    <packageSource key="smoke"><package pattern="MailKit" /><package pattern="MimeKit.*" /></packageSource>
    <packageSource key="dependencies"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content -LiteralPath $config
    # Neither the global packages folder nor the HTTP/plugin caches can mask missing dependencies.
    $env:NUGET_PACKAGES = $cache
    $env:NUGET_HTTP_CACHE_PATH = Join-Path $work 'http-cache'
    $env:NUGET_PLUGINS_CACHE_PATH = Join-Path $work 'plugin-cache'

    $core = New-Consumer 'core' $frameworks
    Invoke-Checked dotnet @('restore', $core, '--configfile', $config, '--packages', $cache)
    Test-DependencyGraph $core $frameworks
    Invoke-Checked dotnet @('build', $core, '-c', 'Release', '--no-restore', '-v', 'minimal')
    foreach ($framework in @('net8.0', 'net10.0')) {
        Invoke-Checked dotnet @((Join-Path (Split-Path $core -Parent) 'bin' 'Release' $framework 'PackageSmokeTests.dll'))
    }

    if (!$SkipCryptography) {
        $crypto = New-Consumer 'cryptography' @('net8.0', 'net10.0') $true
        Invoke-Checked dotnet @('restore', $crypto, '--configfile', $config, '--packages', $cache)
        Test-DependencyGraph $crypto @('net8.0', 'net10.0') $true
        Invoke-Checked dotnet @('build', $crypto, '-c', 'Release', '--no-restore', '-v', 'minimal')
        foreach ($framework in @('net8.0', 'net10.0')) {
            Invoke-Checked dotnet @((Join-Path (Split-Path $crypto -Parent) 'bin' 'Release' $framework 'PackageSmokeTests.dll'))
        }
    } else {
        Write-Warning 'Cryptography smoke test skipped by request.'
    }

    if (!$SkipAot) {
        $aot = New-Consumer 'aot' @('net8.0')
        $publish = Join-Path $work 'publish'
        Invoke-Checked dotnet @('publish', $aot, '-c', 'Release', '-f', 'net8.0', '-o', $publish,
            '-p:PublishAot=true', ('-p:RestoreConfigFile=' + $config), ('-p:RestorePackagesPath=' + $cache),
            '-p:TrimmerSingleWarn=false', '-p:SuppressTrimAnalysisWarnings=false') -RejectILWarnings
        Test-DependencyGraph $aot @('net8.0')
        $executable = if ($IsWindows) { 'PackageSmokeTests.exe' } else { 'PackageSmokeTests' }
        Invoke-Checked (Join-Path $publish $executable) @()
    } else {
        Write-Warning 'Native AOT smoke test skipped by request.'
    }

    $succeeded = $true
    Write-Host "`nPackage contents, all seven compilation targets, and .NET 8/10 runtime probes passed."
    Write-Host "Cryptography included: $(!$SkipCryptography); Native AOT included: $(!$SkipAot)."
} finally {
    foreach ($name in $environment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $environment[$name], 'Process')
    }
    if ($succeeded -and !$KeepArtifacts) {
        Remove-Item -LiteralPath $work -Recurse -Force
    } else {
        Write-Host "Artifacts retained at: $work"
    }
}
