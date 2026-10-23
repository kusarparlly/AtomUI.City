param(
    [string[]]$Frameworks = @("net8.0", "net10.0"),
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Configuration = "Release",
    [string]$DotnetExecutable = "dotnet"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot "fixtures/AtomUI.City.NativeAot.RuntimeMatrix/AtomUI.City.NativeAot.RuntimeMatrix.csproj"

foreach ($framework in $Frameworks) {
    $isolatedIntermediateRoot = Join-Path $repositoryRoot "output/native-aot-runtime-matrix/obj/$framework"
    $publishDirectory = Join-Path $repositoryRoot "output/native-aot-runtime-matrix/publish/$framework/$RuntimeIdentifier"

    Write-Host "Publishing City NativeAOT runtime matrix: $framework/$RuntimeIdentifier"
    $publishOutput = & $DotnetExecutable publish $project `
        --configuration $Configuration `
        --runtime $RuntimeIdentifier `
        --self-contained true `
        -p:AtomUICityDevelopTargetFramework=$framework `
        -p:AtomUICityIsolatedIntermediateRoot=$isolatedIntermediateRoot `
        -p:AtomUICityRuntimeMatrixPublishAot=true `
        -p:AtomUICityStrictAot=true `
        --output $publishDirectory 2>&1
    $publishOutput | Write-Host
    if ($LASTEXITCODE -ne 0) {
        throw "City NativeAOT runtime matrix publish failed for $framework/$RuntimeIdentifier."
    }

    $aotWarnings = $publishOutput | Select-String -Pattern '\bIL[23]\d{3}\b|will always throw'
    if ($aotWarnings) {
        throw "City NativeAOT runtime matrix emitted trimming/AOT warnings for $framework/$RuntimeIdentifier."
    }

    $executable = Join-Path $publishDirectory "AtomUI.City.NativeAot.RuntimeMatrix.exe"
    if (-not (Test-Path -LiteralPath $executable)) {
        throw "City NativeAOT runtime matrix executable was not produced: $executable"
    }

    Write-Host "Running City NativeAOT runtime matrix: $framework/$RuntimeIdentifier"
    $runOutput = & $executable 2>&1
    $runOutput | Write-Host
    if ($LASTEXITCODE -ne 0) {
        throw "City NativeAOT runtime matrix process failed for $framework/$RuntimeIdentifier."
    }
    if (-not ($runOutput | Select-String -SimpleMatch "CITY_NATIVEAOT_RUNTIME_MATRIX_OK")) {
        throw "City NativeAOT runtime matrix success marker is missing for $framework/$RuntimeIdentifier."
    }

    $jsonLine = $runOutput | Where-Object { $_ -like '{*}' } | Select-Object -Last 1
    $result = $jsonLine | ConvertFrom-Json
    if (-not $result.success -or
        $result.moduleInitializationCount -ne 1 -or
        $result.moduleShutdownCount -ne 1 -or
        $result.stateNotificationCount -ne 2 -or
        $result.routeCount -lt 1 -or
        -not $result.securityAllowed -or
        -not $result.securityChallenge -or
        -not $result.mvvmGeneratedValidation -or
        -not $result.localizationEmbedded -or
        -not $result.localizationFile -or
        -not $result.localizationInMemory) {
        throw "City NativeAOT runtime matrix business assertions failed for $framework/$RuntimeIdentifier."
    }
}

Write-Host "City NativeAOT runtime matrix passed for: $($Frameworks -join ', ')."
