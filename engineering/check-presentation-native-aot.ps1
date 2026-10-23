param(
    [string[]]$Frameworks = @("net8.0", "net10.0"),
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Configuration = "Release",
    [string]$DotnetExecutable = "dotnet"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot "fixtures/AtomUI.City.Presentation.DesktopApp/AtomUI.City.Presentation.DesktopApp.csproj"
$gateRoot = Join-Path $repositoryRoot "output/presentation-native-aot"

foreach ($framework in $Frameworks) {
    $isolatedIntermediateRoot = Join-Path $gateRoot "obj/$framework"
    $publishDirectory = Join-Path $gateRoot "publish/$framework/$RuntimeIdentifier"

    Write-Host "Publishing Presentation NativeAOT desktop self-test: $framework/$RuntimeIdentifier"
    $publishOutput = & $DotnetExecutable publish $project `
        --configuration $Configuration `
        --runtime $RuntimeIdentifier `
        --self-contained true `
        -p:AtomUICityDevelopTargetFramework=$framework `
        -p:AtomUICityIsolatedIntermediateRoot=$isolatedIntermediateRoot `
        -p:PublishAot=true `
        -p:AtomUICityStrictAot=true `
        --output $publishDirectory 2>&1
    $publishOutput | Write-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Presentation NativeAOT publish failed for $framework/$RuntimeIdentifier."
    }
    if ($publishOutput | Select-String -Pattern '\bIL[23]\d{3}\b|will always throw') {
        throw "Presentation NativeAOT publish emitted trimming/AOT warnings for $framework/$RuntimeIdentifier."
    }

    $executable = Join-Path $publishDirectory "AtomUI.City.Presentation.DesktopApp.exe"
    if (-not (Test-Path -LiteralPath $executable)) {
        throw "Presentation NativeAOT executable was not produced: $executable"
    }

    $outLog = Join-Path $gateRoot "desktop-$framework.out.log"
    $errorLog = Join-Path $gateRoot "desktop-$framework.err.log"
    Remove-Item -LiteralPath $outLog, $errorLog -Force -ErrorAction SilentlyContinue
    $process = Start-Process `
        -FilePath $executable `
        -RedirectStandardOutput $outLog `
        -RedirectStandardError $errorLog `
        -WindowStyle Hidden `
        -PassThru
    try {
        if (-not $process.WaitForExit(90000)) {
            $process.Kill($true)
            throw "Presentation NativeAOT desktop self-test timed out for $framework/$RuntimeIdentifier."
        }

        $runOutput = @()
        if (Test-Path -LiteralPath $outLog) {
            $runOutput += Get-Content -LiteralPath $outLog
        }
        if (Test-Path -LiteralPath $errorLog) {
            $runOutput += Get-Content -LiteralPath $errorLog
        }
        $runOutput | Write-Host
        if ($process.ExitCode -ne 0) {
            throw "Presentation NativeAOT desktop self-test failed for $framework/$RuntimeIdentifier."
        }
        if (-not ($runOutput | Select-String -SimpleMatch "Presentation Windows desktop self-test passed.")) {
            throw "Presentation NativeAOT desktop success marker is missing for $framework/$RuntimeIdentifier."
        }
    }
    finally {
        if (-not $process.HasExited) {
            $process.Kill($true)
        }
        $process.Dispose()
    }
}

Write-Host "Presentation NativeAOT desktop gate passed for: $($Frameworks -join ', ')."
