# Run as Administrator
# Installs and starts the PasswordExpiryNotification Windows Service

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

Write-Host "Installing service from: $exePath"
& $exePath install

if ($LASTEXITCODE -eq 0) {
    Write-Host "Service installed successfully."
    Write-Host "Starting service..."
    Start-Service -Name "PasswordExpiryNotification" -ErrorAction SilentlyContinue
    Write-Host "Service started."
} else {
    Write-Error "Service installation failed."
    exit 1
}
