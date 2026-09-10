param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$env:AVALONIA_TELEMETRY_OPTOUT = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$versionProps = [xml](Get-Content (Join-Path $repositoryRoot "build/Version.props") -Raw)
$version = $versionProps.Project.PropertyGroup.AtomUICityVersion
$versions = [xml](Get-Content (Join-Path $repositoryRoot "build/Version.props") -Raw)
$avaloniaVersion = $versions.Project.PropertyGroup.AvaloniaVersion
$packages = [xml](Get-Content (Join-Path $repositoryRoot "Directory.Packages.props") -Raw)
$microsoftExtensionsVersion = ($packages.Project.ItemGroup.PackageVersion |
    Where-Object Include -eq "Microsoft.Extensions.DependencyInjection.Abstractions").Version
$validationRoot = Join-Path $repositoryRoot "output/presentation-package-consumer"
$localFeed = Join-Path $validationRoot "local-feed"
$workspace = Join-Path $validationRoot "workspace"
$packageCache = Join-Path $validationRoot "packages"
$dotnetHome = Join-Path $validationRoot "dotnet-home"
$templateRoot = Join-Path $repositoryRoot "engineering/package-consumers/presentation"

if (-not $IsWindows) {
    throw "The Presentation package consumer gate is Windows-only."
}

foreach ($path in @($localFeed, $workspace, $packageCache, $dotnetHome)) {
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

$expectedRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot "output/presentation-package-consumer"))
foreach ($path in @($workspace, $packageCache)) {
    if (-not [IO.Path]::GetFullPath($path).StartsWith($expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean unexpected consumer path: $path"
    }
    Get-ChildItem $path -Force | Remove-Item -Recurse -Force
}
Get-ChildItem $localFeed -Filter "AtomUI.City.*.nupkg" | Remove-Item -Force
Get-ChildItem $localFeed -Filter "AtomUI.City.*.snupkg" | Remove-Item -Force

$projectNames = @("Core", "Mvvm", "Routing", "Security", "State", "Presentation")
foreach ($projectName in $projectNames) {
    $project = Join-Path $repositoryRoot "src/AtomUI.City.$projectName/AtomUI.City.$projectName.csproj"
    & dotnet pack $project --configuration $Configuration --output $localFeed -p:TreatWarningsAsErrors=true
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to pack AtomUI.City.$projectName."
    }
}

$presentationPackage = Join-Path $localFeed "AtomUI.City.Presentation.$version.nupkg"
if (-not (Test-Path $presentationPackage)) {
    throw "Presentation candidate package is missing: $presentationPackage"
}

$entries = (& tar -tf $presentationPackage) -replace "\\", "/"
$requiredEntries = @(
    "lib/net8.0/AtomUI.City.Presentation.dll",
    "lib/net8.0/AtomUI.City.Presentation.pdb",
    "lib/net8.0/AtomUI.City.Presentation.xml",
    "lib/net10.0/AtomUI.City.Presentation.dll",
    "lib/net10.0/AtomUI.City.Presentation.pdb",
    "lib/net10.0/AtomUI.City.Presentation.xml",
    "LICENSE", "README.nuget.md", "RELEASE_NOTES.md"
)
foreach ($entry in $requiredEntries) {
    if ($entries -notcontains $entry) {
        throw "Presentation package is missing required entry: $entry"
    }
}
if (($entries -join "`n") -match "(^|/)(tests|fixtures|benchmarks|analyzers)/|AtomUI\.City\.Generators") {
    throw "Presentation package contains a forbidden test, fixture, benchmark, or analyzer asset."
}

$nuspec = & tar -xOf $presentationPackage AtomUI.City.Presentation.nuspec
foreach ($dependency in @("AtomUI.City.Core", "AtomUI.City.Mvvm", "AtomUI.City.Routing", "AtomUI.City.Security", "AtomUI.City.State")) {
    if (($nuspec -join "`n") -notmatch "<dependency id=`"$([regex]::Escape($dependency))`" version=`"$([regex]::Escape($version))`"") {
        throw "Presentation package does not depend on matching candidate $dependency $version."
    }
}
if (($nuspec -join "`n") -notmatch "<dependency id=`"Avalonia`"") {
    throw "Presentation package does not declare Avalonia."
}
if (($nuspec -join "`n") -match "<dependency id=`"(AtomUI|AtomUI.City.Localization)`"") {
    throw "Presentation package must not depend on AtomUI or Localization."
}

$templateMap = @{
    "Presentation.PackageConsumer.csproj.template" = "Presentation.PackageConsumer.csproj"
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
    throw "The isolated Presentation package consumer must not contain ProjectReference."
}

$env:NUGET_PACKAGES = $packageCache
$env:DOTNET_CLI_HOME = $dotnetHome
$consumerProject = Join-Path $workspace "Presentation.PackageConsumer.csproj"
$commonProperties = @(
    "-p:AtomUICityConsumerVersion=$version",
    "-p:AvaloniaConsumerVersion=$avaloniaVersion",
    "-p:MicrosoftExtensionsConsumerVersion=$microsoftExtensionsVersion"
)
& dotnet restore $consumerProject --configfile (Join-Path $workspace "NuGet.Config") --no-http-cache --runtime win-x64 -p:SelfContained=true @commonProperties
if ($LASTEXITCODE -ne 0) { throw "Presentation package consumer restore failed." }
& dotnet publish $consumerProject --configuration Release --runtime win-x64 --self-contained true --no-restore --output (Join-Path $workspace "publish") @commonProperties
if ($LASTEXITCODE -ne 0) { throw "Presentation package consumer publish failed." }

$assets = Get-Content (Join-Path $workspace "obj/project.assets.json") -Raw
foreach ($projectName in $projectNames) {
    if ($assets -notmatch [regex]::Escape("AtomUI.City.$projectName/$version")) {
        throw "Consumer assets are missing local package AtomUI.City.$projectName/$version."
    }
}

$consumerExecutable = Join-Path $workspace "publish/Presentation.PackageConsumer.exe"
$standardOutputPath = Join-Path $workspace "consumer.stdout.log"
$standardErrorPath = Join-Path $workspace "consumer.stderr.log"
$consumerProcess = Start-Process -FilePath $consumerExecutable -PassThru -NoNewWindow `
    -RedirectStandardOutput $standardOutputPath -RedirectStandardError $standardErrorPath
if (-not $consumerProcess.WaitForExit(60000)) {
    $consumerProcess.Kill($true)
    $consumerProcess.WaitForExit()
    $timedOutOutput = if (Test-Path $standardOutputPath) { Get-Content $standardOutputPath -Raw } else { "" }
    throw "Presentation package consumer timed out after 60 seconds.`n$timedOutOutput"
}
$consumerProcess.WaitForExit()
$consumerOutput = Get-Content $standardOutputPath -Raw
$consumerError = Get-Content $standardErrorPath -Raw
if ($consumerProcess.ExitCode -ne 0 -or $consumerOutput -notmatch "PRESENTATION_PACKAGE_CONSUMER_OK") {
    throw "Presentation package consumer failed with exit code $($consumerProcess.ExitCode).`n$consumerOutput`n$consumerError"
}

Write-Host $consumerOutput
Write-Host "Presentation local package consumer gate passed with isolated package cache: $packageCache"
