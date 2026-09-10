param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$env:AVALONIA_TELEMETRY_OPTOUT = "1"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$project = Join-Path $repositoryRoot "src/AtomUI.City.Presentation/AtomUI.City.Presentation.csproj"
$shippedApi = Join-Path $repositoryRoot "src/AtomUI.City.Presentation/PublicAPI.Shipped.txt"
$unshippedApi = Join-Path $repositoryRoot "src/AtomUI.City.Presentation/PublicAPI.Unshipped.txt"
$validationOutput = Join-Path $repositoryRoot "output/presentation-public-api"

foreach ($path in @($project, $shippedApi, $unshippedApi)) {
    if (-not (Test-Path $path)) { throw "Missing Presentation public API gate input: $path" }
}
$signatures = Get-Content $shippedApi | Where-Object { $_ -and -not $_.StartsWith("#") }
if ($signatures.Count -eq 0) { throw "Presentation shipped public API baseline is empty." }
$projectText = Get-Content $project -Raw
if ($projectText -notmatch "Microsoft.CodeAnalysis.PublicApiAnalyzers" -or
    $projectText -notmatch "<EnablePackageValidation>true</EnablePackageValidation>") {
    throw "Presentation must enable PublicApiAnalyzers and SDK package validation."
}

& dotnet restore $project -p:Configuration=$Configuration
if ($LASTEXITCODE -ne 0) { throw "Presentation public API restore failed." }
& dotnet build $project --configuration $Configuration --no-restore -p:TreatWarningsAsErrors=true
if ($LASTEXITCODE -ne 0) { throw "Presentation public API build failed." }

$headRevision = (git -C $repositoryRoot rev-parse HEAD).Trim()
foreach ($framework in @("net8.0", "net10.0")) {
    $xmlPath = Join-Path $repositoryRoot "output/bin/$Configuration/AtomUI.City.Presentation/$framework/AtomUI.City.Presentation.xml"
    $sourceLinkPath = Join-Path $repositoryRoot "output/AtomUI.City.Presentation/obj/$Configuration/$framework/AtomUI.City.Presentation.sourcelink.json"
    if (-not (Test-Path $xmlPath)) { throw "Presentation XML documentation is missing for $framework." }
    if (-not (Test-Path $sourceLinkPath)) { throw "Presentation SourceLink is missing for $framework." }
    $sourceLink = Get-Content $sourceLinkPath -Raw
    if ($sourceLink -notmatch "https://raw\.githubusercontent\.com/AtomUI/AtomUI\.City/$headRevision/") {
        throw "Presentation SourceLink is not canonical or does not reference HEAD for $framework."
    }
}

New-Item -ItemType Directory -Path $validationOutput -Force | Out-Null
& dotnet pack $project --configuration $Configuration --no-build --no-restore --output $validationOutput -p:TreatWarningsAsErrors=true
if ($LASTEXITCODE -ne 0) { throw "Presentation public API package validation failed." }
$package = Get-ChildItem $validationOutput -Filter "AtomUI.City.Presentation.*.nupkg" |
    Where-Object Extension -eq ".nupkg" |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if ($null -eq $package) { throw "Presentation validation package was not produced." }
$nuspec = (& tar -xOf $package.FullName AtomUI.City.Presentation.nuspec) -join "`n"
if ($nuspec -notmatch "repository type=`"git`" url=`"https://github\.com/AtomUI/AtomUI\.City`"" -or
    $nuspec -notmatch "commit=`"$headRevision`"") {
    throw "Presentation NuGet repository metadata is not canonical or does not reference HEAD."
}

Write-Host "Presentation public API gate passed: $($signatures.Count) frozen signatures across net8.0/net10.0."
