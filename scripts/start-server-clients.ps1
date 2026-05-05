Param(
    [int]$ClientCount = 3,
    [switch]$KillExisting,
    [string]$Configuration = "Debug"
)

# Determine repository root (script placed in scripts/)
$repoRoot = if (Test-Path (Join-Path $PSScriptRoot "..")) { (Resolve-Path (Join-Path $PSScriptRoot "..")).Path } else { $PSScriptRoot }

# Locate server executable (try common TFMs)
$serverExe = Join-Path $repoRoot "EldenBingoServerStandalone\bin\$Configuration\net6.0\EldenBingoServerStandalone.exe"
if (-not (Test-Path $serverExe)) {
    $serverExe = Join-Path $repoRoot "EldenBingoServerStandalone\bin\$Configuration\net6.0-windows\EldenBingoServerStandalone.exe"
}

# Locate client executable (try net6.0-windows then net6.0)
$clientExe = $null
foreach ($fw in @("net6.0-windows","net6.0")) {
    $candidate = Join-Path $repoRoot "EldenBingo\bin\$Configuration\$fw\EldenBingo.exe"
    if (Test-Path $candidate) { $clientExe = $candidate; break }
}

if ($KillExisting) {
    Write-Host "Killing existing EldenBingo and EldenBingoServerStandalone processes..."
    Get-Process -Name "EldenBingo" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Get-Process -Name "EldenBingoServerStandalone" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}

if (-not (Test-Path $serverExe)) {
    Write-Warning "Server executable not found: $serverExe"
} else {
    Write-Host "Starting server: $serverExe"
    Start-Process -FilePath $serverExe -WorkingDirectory (Split-Path $serverExe -Parent)
}

if (-not $clientExe) {
    Write-Warning "Client executable not found under EldenBingo/bin/$Configuration/*"
} else {
    for ($i = 1; $i -le $ClientCount; $i++) {
        Write-Host "Starting client #$i: $clientExe"
        Start-Process -FilePath $clientExe -WorkingDirectory (Split-Path $clientExe -Parent)
        Start-Sleep -Milliseconds 300
    }
}

Write-Host "Launched $($ClientCount) client(s) and server (if available)."
