param(
    [ValidateSet("Verify", "Capture", "Compare")]
    [string]$Mode = "Compare",
    [ValidateRange(1, 9)]
    [int]$Rounds = 3,
    [ValidateSet("Dry", "Short", "Medium")]
    [string]$Job = "Medium"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$project = Join-Path $repositoryRoot "benchmarks/AtomUI.City.Presentation.Benchmarks/AtomUI.City.Presentation.Benchmarks.csproj"
$baseline = Join-Path $repositoryRoot "benchmarks/AtomUI.City.Presentation.Benchmarks/baselines/windows-x64.json"
$artifacts = Join-Path $repositoryRoot "output/presentation-benchmark-gate"

if (-not $IsWindows) {
    throw "Presentation approved performance baselines are Windows-only."
}

$candidateFiles = Get-ChildItem @(
        (Join-Path $repositoryRoot "src/AtomUI.City.Presentation"),
        (Join-Path $repositoryRoot "benchmarks/AtomUI.City.Presentation.Benchmarks"),
        (Join-Path $repositoryRoot "Directory.Build.props"),
        (Join-Path $repositoryRoot "Directory.Build.targets"),
        (Join-Path $repositoryRoot "Directory.Packages.props"),
        (Join-Path $repositoryRoot "global.json")
    ) -File -Recurse | Where-Object {
        $_.FullName -notmatch "[\\/](bin|obj|baselines)[\\/]"
    } | Sort-Object FullName
$manifest = ($candidateFiles | ForEach-Object {
    $relativePath = [IO.Path]::GetRelativePath($repositoryRoot, $_.FullName).Replace("\", "/")
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash`t$relativePath"
}) -join "`n"
$manifestBytes = [Text.Encoding]::UTF8.GetBytes($manifest)
$candidateFingerprint = [Convert]::ToHexString(
    [Security.Cryptography.SHA256]::HashData($manifestBytes)).ToLowerInvariant()

$env:PRESENTATION_GIT_COMMIT = (git -C $repositoryRoot rev-parse HEAD).Trim()
$env:PRESENTATION_CANDIDATE_FINGERPRINT = $candidateFingerprint
$benchmarkArguments = @(
    "run", "--project", $project,
    "--configuration", "Release",
    "--",
    "--baseline-mode", $Mode,
    "--baseline-file", $baseline,
    "--gate-artifacts", $artifacts,
    "--rounds", $Rounds,
    "--filter", "*",
    "--job", $Job,
    "--memory"
)

& dotnet @benchmarkArguments
if ($LASTEXITCODE -ne 0) {
    throw "Presentation benchmark gate failed with exit code $LASTEXITCODE."
}

Write-Host "Presentation benchmark gate passed: mode=$Mode rounds=$Rounds job=$Job fingerprint=$candidateFingerprint"
