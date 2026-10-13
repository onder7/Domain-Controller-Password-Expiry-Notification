# Starts the PasswordExpiryNotification application in GUI mode
# This allows monitoring and configuration while the service runs in the background

param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

# Locate executable
$exePath = Join-Path $PSScriptRoot "bin\$Configuration\net8.0-windows\PasswordExpiryNotification.exe"
if (-not (Test-Path $exePath)) {
    $exePath = Join-Path $PSScriptRoot "PasswordExpiryNotification.exe"
}

if (-not (Test-Path $exePath)) {
    Write-Error "Executable bulunamadı: $exePath"
    exit 1
}

Write-Host "Starting GUI mode from: $exePath"
Start-Process -FilePath $exePath -ArgumentList "--gui"
