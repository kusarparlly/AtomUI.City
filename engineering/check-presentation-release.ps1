param(
    [switch]$NoRestore,
    [ValidateSet("Verify", "Compare")]
    [string]$BenchmarkMode = "Compare",
    [ValidateRange(1, 9)]
    [int]$BenchmarkRounds = 3,
    [ValidateSet("Dry", "Short", "Medium")]
    [string]$BenchmarkJob = "Medium"
)

$ErrorActionPreference = "Stop"
$env:AVALONIA_TELEMETRY_OPTOUT = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $repositoryRoot

if (-not $IsWindows) {
    throw "The Presentation 1.0 release candidate gate is Windows-only."
}

function Invoke-Gate {
    param([string]$Name, [scriptblock]$Action)

    Write-Host "Running Presentation RC gate: $Name"
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "Presentation RC gate failed: $Name (exit $LASTEXITCODE)"
    }
}

$projects = @(
    "tests/AtomUI.City.Build.Tests/AtomUI.City.Build.Tests.csproj",
    "tests/AtomUI.City.Presentation.Tests/AtomUI.City.Presentation.Tests.csproj",
    "tests/AtomUI.City.Generators.Tests/AtomUI.City.Generators.Tests.csproj",
    "benchmarks/AtomUI.City.Presentation.Benchmarks/AtomUI.City.Presentation.Benchmarks.csproj"
)
if (-not $NoRestore) {
    foreach ($project in $projects) {
        Invoke-Gate "restore $project" { dotnet restore $project -p:Configuration=Release }
    }
}

Invoke-Gate "format" {
    dotnet format AtomUICity.slnx --verify-no-changes --no-restore `
        --include src/AtomUI.City.Presentation `
        --include tests/AtomUI.City.Presentation.Tests `
        --include fixtures/AtomUI.City.Presentation.HeadlessApp `
        --include fixtures/AtomUI.City.Presentation.DesktopApp `
        --include benchmarks/AtomUI.City.Presentation.Benchmarks
}
Invoke-Gate "Presentation build" {
    dotnet build src/AtomUI.City.Presentation/AtomUI.City.Presentation.csproj `
        --configuration Release --no-restore -p:TreatWarningsAsErrors=true
}
Invoke-Gate "engineering contract tests" {
    dotnet test tests/AtomUI.City.Build.Tests/AtomUI.City.Build.Tests.csproj `
        --configuration Release --no-restore -p:TreatWarningsAsErrors=true
}
Invoke-Gate "Presentation unit, Headless, stress and desktop tests" {
    dotnet test tests/AtomUI.City.Presentation.Tests/AtomUI.City.Presentation.Tests.csproj `
        --configuration Release --no-restore -p:TreatWarningsAsErrors=true
}
Invoke-Gate "Presentation generator tests" {
    dotnet test tests/AtomUI.City.Generators.Tests/AtomUI.City.Generators.Tests.csproj `
        --configuration Release --no-restore --filter FullyQualifiedName~Presentation `
        -p:TreatWarningsAsErrors=true
}
Invoke-Gate "public API and SourceLink" { & engineering/check-presentation-public-api.ps1 }
Invoke-Gate "package consumer" { & engineering/check-presentation-package-consumer.ps1 }
Invoke-Gate "performance baseline" {
    & engineering/check-presentation-benchmarks.ps1 `
        -Mode $BenchmarkMode -Rounds $BenchmarkRounds -Job $BenchmarkJob
}

$candidateFiles = Get-ChildItem @(
        "src/AtomUI.City.Presentation",
        "tests/AtomUI.City.Presentation.Tests",
        "fixtures/AtomUI.City.Presentation.HeadlessApp",
        "fixtures/AtomUI.City.Presentation.DesktopApp",
        "benchmarks/AtomUI.City.Presentation.Benchmarks",
        "docs/modules/presentation",
        "engineering/package-consumers/presentation"
    ) -File -Recurse | Where-Object {
        $_.FullName -notmatch "[\\/](bin|obj)[\\/]"
    } | Sort-Object FullName
$manifest = ($candidateFiles | ForEach-Object {
    $relativePath = [IO.Path]::GetRelativePath($repositoryRoot, $_.FullName).Replace("\", "/")
    "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())`t$relativePath"
}) -join "`n"
$fingerprint = [Convert]::ToHexString(
    [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($manifest))).ToLowerInvariant()
$dirtyState = if (git status --porcelain --untracked-files=all) { "dirty" } else { "clean" }
Write-Host "PRESENTATION_RC_IDENTITY head=$(git rev-parse HEAD) worktree=$dirtyState fingerprint=$fingerprint files=$($candidateFiles.Count)"
Write-Host "Presentation Release Candidate gates completed for Release."
