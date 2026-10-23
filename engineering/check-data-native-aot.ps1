param(
    [string[]]$Frameworks = @("net8.0", "net10.0"),
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Configuration = "Release",
    [string]$DotnetExecutable = "dotnet"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$serverProject = Join-Path $repositoryRoot "fixtures/AtomUI.City.Data.HeadlessApp/AtomUI.City.Data.HeadlessApp.csproj"
$clientProject = Join-Path $repositoryRoot "fixtures/AtomUI.City.Data.AotApp/AtomUI.City.Data.AotApp.csproj"
$gateRoot = Join-Path $repositoryRoot "output/data-native-aot"
$serverDirectory = Join-Path $gateRoot "server"
$serverOutLog = Join-Path $gateRoot "server.out.log"
$serverErrorLog = Join-Path $gateRoot "server.err.log"
$serverProcess = $null

function Get-FreeTcpPort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try {
        return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Wait-ForServerReady {
    param([System.Diagnostics.Process]$Process)

    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($Process.HasExited) {
            $details = if (Test-Path -LiteralPath $serverErrorLog) {
                Get-Content -LiteralPath $serverErrorLog -Raw
            } else { "" }
            throw "Data managed protocol server exited before readiness. $details"
        }

        if ((Test-Path -LiteralPath $serverOutLog) -and
            (Select-String -LiteralPath $serverOutLog -SimpleMatch "DATA_AOT_SERVER_READY" -Quiet)) {
            return
        }

        Start-Sleep -Milliseconds 200
    }

    throw "Data managed protocol server did not become ready within 45 seconds."
}

function Invoke-NativeClient {
    param(
        [string]$Executable,
        [string]$HttpEndpoint,
        [string]$GrpcEndpoint,
        [string]$Framework
    )

    $runOutLog = Join-Path $gateRoot "client-$Framework.out.log"
    $runErrorLog = Join-Path $gateRoot "client-$Framework.err.log"
    Remove-Item -LiteralPath $runOutLog, $runErrorLog -Force -ErrorAction SilentlyContinue
    $process = Start-Process `
        -FilePath $Executable `
        -ArgumentList @("--http-endpoint", $HttpEndpoint, "--grpc-endpoint", $GrpcEndpoint) `
        -RedirectStandardOutput $runOutLog `
        -RedirectStandardError $runErrorLog `
        -WindowStyle Hidden `
        -PassThru
    try {
        if (-not $process.WaitForExit(90000)) {
            $process.Kill($true)
            throw "Data NativeAOT client timed out for $Framework/$RuntimeIdentifier."
        }

        $runOutput = @()
        if (Test-Path -LiteralPath $runOutLog) {
            $runOutput += Get-Content -LiteralPath $runOutLog
        }
        if (Test-Path -LiteralPath $runErrorLog) {
            $runOutput += Get-Content -LiteralPath $runErrorLog
        }
        $runOutput | Write-Host
        if ($process.ExitCode -ne 0) {
            throw "Data NativeAOT client failed for $Framework/$RuntimeIdentifier."
        }
        if (-not ($runOutput | Select-String -SimpleMatch "DATA_AOT_OK")) {
            throw "Data NativeAOT success marker is missing for $Framework/$RuntimeIdentifier."
        }

        $jsonLine = $runOutput | Where-Object { $_ -like '{*}' } | Select-Object -Last 1
        $result = $jsonLine | ConvertFrom-Json
        if (-not $result.success -or
            -not $result.http -or
            -not $result.grpc -or
            -not $result.signalRInvoke -or
            -not $result.signalRPush -or
            -not $result.generatedDescriptor -or
            -not $result.scheduler -or
            -not $result.cacheFingerprint) {
            throw "Data NativeAOT business assertions failed for $Framework/$RuntimeIdentifier."
        }
    }
    finally {
        if (-not $process.HasExited) {
            $process.Kill($true)
        }
        $process.Dispose()
    }
}

New-Item -ItemType Directory -Path $gateRoot -Force | Out-Null
Remove-Item -LiteralPath $serverOutLog, $serverErrorLog -Force -ErrorAction SilentlyContinue
$httpPort = Get-FreeTcpPort
$grpcPort = Get-FreeTcpPort
while ($grpcPort -eq $httpPort) {
    $grpcPort = Get-FreeTcpPort
}
$httpEndpoint = "http://127.0.0.1:$httpPort"
$grpcEndpoint = "http://127.0.0.1:$grpcPort"

try {
    Write-Host "Publishing managed Data protocol server."
    & $DotnetExecutable publish $serverProject `
        --configuration $Configuration `
        -p:AtomUICityDevelopTargetFramework=net10.0 `
        --output $serverDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Data managed protocol server publish failed."
    }

    $serverDll = Join-Path $serverDirectory "AtomUI.City.Data.HeadlessApp.dll"
    $serverProcess = Start-Process `
        -FilePath $DotnetExecutable `
        -ArgumentList @($serverDll, "--server-only", "--http-port", $httpPort, "--grpc-port", $grpcPort) `
        -RedirectStandardOutput $serverOutLog `
        -RedirectStandardError $serverErrorLog `
        -WindowStyle Hidden `
        -PassThru
    Wait-ForServerReady -Process $serverProcess

    foreach ($framework in $Frameworks) {
        $isolatedIntermediateRoot = Join-Path $gateRoot "obj/$framework"
        $publishDirectory = Join-Path $gateRoot "publish/$framework/$RuntimeIdentifier"

        Write-Host "Publishing Data NativeAOT real protocol client: $framework/$RuntimeIdentifier"
        $publishOutput = & $DotnetExecutable publish $clientProject `
            --configuration $Configuration `
            --runtime $RuntimeIdentifier `
            --self-contained true `
            -p:AtomUICityDevelopTargetFramework=$framework `
            -p:AtomUICityIsolatedIntermediateRoot=$isolatedIntermediateRoot `
            -p:AtomUICityDataPublishAot=true `
            -p:AtomUICityStrictAot=true `
            --output $publishDirectory 2>&1
        $publishOutput | Write-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Data NativeAOT publish failed for $framework/$RuntimeIdentifier."
        }
        if ($publishOutput | Select-String -Pattern '\bIL[23]\d{3}\b|will always throw') {
            throw "Data NativeAOT publish emitted trimming/AOT warnings for $framework/$RuntimeIdentifier."
        }

        $executable = Join-Path $publishDirectory "AtomUI.City.Data.AotApp.exe"
        if (-not (Test-Path -LiteralPath $executable)) {
            throw "Data NativeAOT executable was not produced: $executable"
        }

        Invoke-NativeClient `
            -Executable $executable `
            -HttpEndpoint $httpEndpoint `
            -GrpcEndpoint $grpcEndpoint `
            -Framework $framework
    }
}
finally {
    if ($null -ne $serverProcess) {
        if (-not $serverProcess.HasExited) {
            $serverProcess.Kill($true)
            $serverProcess.WaitForExit()
        }
        $serverProcess.Dispose()
    }
}

Write-Host "Data NativeAOT real protocol gate passed for: $($Frameworks -join ', ')."
