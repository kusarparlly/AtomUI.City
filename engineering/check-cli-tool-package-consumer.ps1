param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$versionProps = [xml](Get-Content (Join-Path $repositoryRoot "build/Version.props") -Raw)
$version = $versionProps.Project.PropertyGroup.AtomUICityVersion
$validationRoot = Join-Path $repositoryRoot "output/cli-tool-package-consumer"
$localFeed = Join-Path $validationRoot "local-feed"
$toolPath = Join-Path $validationRoot "tool-path"
$packageCache = Join-Path $validationRoot "packages"
$dotnetHome = Join-Path $validationRoot "dotnet-home"

$expectedRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot "output/cli-tool-package-consumer"))
if ([IO.Path]::GetFullPath($validationRoot) -ne $expectedRoot) {
    throw "Refusing to clean unexpected CLI tool validation path: $validationRoot"
}

if (Test-Path $validationRoot) {
    Remove-Item -LiteralPath $validationRoot -Recurse -Force
}
foreach ($path in @($localFeed, $toolPath, $packageCache, $dotnetHome)) {
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

$env:NUGET_PACKAGES = $packageCache
$env:DOTNET_CLI_HOME = $dotnetHome
$project = Join-Path $repositoryRoot "src/AtomUI.City.Cli/AtomUI.City.Cli.csproj"
& dotnet pack $project --configuration $Configuration --output $localFeed --no-restore `
    -p:TreatWarningsAsErrors=true -p:EnablePackageValidation=false
if ($LASTEXITCODE -ne 0) {
    throw "Failed to pack AtomUI.City.Cli as a .NET Tool."
}

$package = Join-Path $localFeed "AtomUI.City.Cli.$version.nupkg"
if (-not (Test-Path $package)) {
    throw "CLI tool candidate package is missing: $package"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $entries = @($archive.Entries | ForEach-Object FullName)
    foreach ($required in @(
        "tools/net10.0/any/DotnetToolSettings.xml",
        "tools/net10.0/any/AtomUI.City.Cli.dll",
        "tools/net10.0/any/AtomUI.City.Core.dll",
        "tools/net10.0/any/AtomUI.City.PluginSystem.dll",
        "tools/net10.0/any/AtomUI.City.Templates.dll"
    )) {
        if ($entries -notcontains $required) {
            throw "CLI tool package is missing required runtime asset: $required"
        }
    }

    if ($entries | Where-Object { $_ -match "AtomUI\.City\.Build(?:\.Tasks)?\.dll$" }) {
        throw "CLI tool package contains a forbidden Build runtime assembly."
    }
}
finally {
    $archive.Dispose()
}

& dotnet tool install AtomUI.City.Cli --version $version --tool-path $toolPath `
    --add-source $localFeed --ignore-failed-sources --no-cache
if ($LASTEXITCODE -ne 0) {
    throw "CLI tool installation from the isolated local feed failed."
}

$command = Join-Path $toolPath ($(if ($IsWindows) { "atomui.exe" } else { "atomui" }))
$successOutput = & $command city explain AUCCLI0709 --json 2>&1
if ($LASTEXITCODE -ne 0 -or ($successOutput -join "`n") -notmatch '"code":"AUCCLI0709"') {
    throw "Installed CLI tool did not execute the success path.`n$($successOutput -join "`n")"
}

$failureOutput = & $command city explain UNKNOWN --json 2>&1
if ($LASTEXITCODE -ne 2 -or ($failureOutput -join "`n") -notmatch '"code":"AUCCLI0802"') {
    throw "Installed CLI tool did not preserve the expected failure exit/envelope.`n$($failureOutput -join "`n")"
}

Write-Host ($successOutput -join "`n")
Write-Host "CLI .NET Tool package consumer gate passed with isolated package cache: $packageCache"
