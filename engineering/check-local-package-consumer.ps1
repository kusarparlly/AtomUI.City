param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$env:AVALONIA_TELEMETRY_OPTOUT = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$versionProps = [xml](Get-Content (Join-Path $repositoryRoot "build/Version.props") -Raw)
$baseVersion = $versionProps.Project.PropertyGroup.AtomUICityVersion
$version = "$baseVersion-local-gate"
$validationRoot = Join-Path $repositoryRoot ".artifacts/local-package-consumer"
$localFeed = Join-Path $validationRoot "local-feed"
$workspace = Join-Path $validationRoot "workspace"
$packageCache = Join-Path $validationRoot "packages"
$dotnetHome = Join-Path $validationRoot "dotnet-home"
$templateRoot = Join-Path $repositoryRoot "engineering/package-consumers/release"

$expectedRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot ".artifacts/local-package-consumer"))
if (-not [IO.Path]::GetFullPath($validationRoot).StartsWith($expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean unexpected package consumer path: $validationRoot"
}
if (Test-Path $validationRoot) {
    Remove-Item -LiteralPath $validationRoot -Recurse -Force
}
foreach ($path in @($localFeed, $workspace, $packageCache, $dotnetHome)) {
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

$projectNames = @("Build", "Core", "EventBus", "State", "Mvvm", "Routing", "Data", "Localization", "Security", "Presentation")
foreach ($projectName in $projectNames) {
    $project = Join-Path $repositoryRoot "src/AtomUI.City.$projectName/AtomUI.City.$projectName.csproj"
    & dotnet pack $project --configuration $Configuration --output $localFeed --no-restore `
        -p:TreatWarningsAsErrors=true -p:AtomUICityVersion=$version
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to pack AtomUI.City.$projectName."
    }

    $candidatePackage = Join-Path $localFeed "AtomUI.City.$projectName.$version.nupkg"
    if (-not (Test-Path $candidatePackage)) {
        throw "Candidate package is missing: $candidatePackage"
    }
}

$templateMap = @{
    "Release.PackageConsumer.csproj.template" = "Release.PackageConsumer.csproj"
    "Program.cs.template" = "Program.cs"
    "NuGet.Config.template" = "NuGet.Config"
    "Directory.Build.props.template" = "Directory.Build.props"
    "Directory.Build.targets.template" = "Directory.Build.targets"
    "Directory.Packages.props.template" = "Directory.Packages.props"
}
foreach ($entry in $templateMap.GetEnumerator()) {
    Copy-Item (Join-Path $templateRoot $entry.Key) (Join-Path $workspace $entry.Value)
}
if (Select-String -Path (Join-Path $workspace "*") -Pattern "<ProjectReference" -Quiet) {
    throw "The isolated release package consumer must not contain ProjectReference."
}

$env:NUGET_PACKAGES = $packageCache
$env:DOTNET_CLI_HOME = $dotnetHome
$userPackageCache = Join-Path ([Environment]::GetFolderPath("UserProfile")) ".nuget/packages"
$consumerProject = Join-Path $workspace "Release.PackageConsumer.csproj"
$commonProperties = @(
    "-p:AtomUICityConsumerVersion=$version",
    "-p:NuGetAudit=false",
    "-p:RestoreFallbackFolders=$userPackageCache"
)
& dotnet restore $consumerProject --configfile (Join-Path $workspace "NuGet.Config") --no-http-cache @commonProperties
if ($LASTEXITCODE -ne 0) { throw "Release package consumer restore failed." }
& dotnet build $consumerProject --configuration Release --no-restore @commonProperties
if ($LASTEXITCODE -ne 0) { throw "Release package consumer multi-target build failed." }
& dotnet publish $consumerProject --configuration Release --framework net10.0 --no-restore --output (Join-Path $workspace "publish") @commonProperties
if ($LASTEXITCODE -ne 0) { throw "Release package consumer publish failed." }

$assets = Get-Content (Join-Path $workspace "obj/project.assets.json") -Raw
foreach ($projectName in $projectNames) {
    if ($assets -notmatch [regex]::Escape("AtomUI.City.$projectName/$version")) {
        throw "Consumer assets are missing local package AtomUI.City.$projectName/$version."
    }

    $packageMetadata = Join-Path $packageCache "$($projectName.Insert(0, 'atomui.city.').ToLowerInvariant())/$version/.nupkg.metadata"
    if (-not (Test-Path $packageMetadata)) {
        throw "Consumer package metadata is missing: $packageMetadata"
    }
    $metadata = Get-Content $packageMetadata -Raw | ConvertFrom-Json
    if (-not [IO.Path]::GetFullPath($metadata.source).StartsWith(
            [IO.Path]::GetFullPath($localFeed),
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Consumer package did not originate from the candidate local feed: AtomUI.City.$projectName."
    }
}

$consumerAssembly = Join-Path $workspace "publish/Release.PackageConsumer.dll"
$consumerOutput = & dotnet $consumerAssembly 2>&1
if ($LASTEXITCODE -ne 0 -or ($consumerOutput -join "`n") -notmatch "RELEASE_PACKAGE_CONSUMER_OK") {
    throw "Release package consumer failed.`n$($consumerOutput -join "`n")"
}

Write-Host ($consumerOutput -join "`n")
Write-Host "Release local package consumer gate passed with isolated package cache: $packageCache"
