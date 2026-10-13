# Masaüstüne uygulama kısayolu oluşturur; simge olarak kök dizindeki diagram.png / app.ico kullanır.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$exeCandidates = @(
    (Join-Path $root 'bin\Release\net8.0-windows\PasswordExpiryNotification.exe'),
    (Join-Path $root 'bin\Debug\net8.0-windows\PasswordExpiryNotification.exe')
)

$target = $exeCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $target) {
    Write-Host "Önce projeyi derleyin: dotnet build -c Release"
    throw "PasswordExpiryNotification.exe bulunamadı."
}

$ico = Join-Path $root 'app.ico'
if (-not (Test-Path $ico)) {
    & (Join-Path $PSScriptRoot 'Convert-Icon.ps1')
}

$desktop = [Environment]::GetFolderPath('Desktop')
$lnkPath = Join-Path $desktop 'Password Expiry Notification.lnk'

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($lnkPath)
$shortcut.TargetPath = $target
$shortcut.WorkingDirectory = Split-Path $target
$shortcut.IconLocation = "$ico,0"
$shortcut.Description = 'AD şifre dolum uyarı uygulaması'
$shortcut.Save()

Write-Host "Kısayol oluşturuldu: $lnkPath"
Write-Host "Hedef: $target"
Write-Host "Simge: $ico"
