# Hybrid Windows Service + GUI Mode - Build Result

## Status: ✓ SUCCESS

Build completed successfully on 2026-10-13.

## Build Command
```
dotnet clean
dotnet build --configuration Release
```

### Output
```
PasswordExpiryNotification net8.0-windows succeeded
Build succeeded in 5.0s
Exit Code: 0
```

## Files Created/Modified

### New Files
1. **Program.cs** - Custom entry point
   - Handles command-line arguments (--gui, --scan, --send, --headless)
   - Routes to WPF app or Windows Service host (Topshelf)

2. **Services/ServiceManager.cs** - Windows Service implementation
   - Implements Start/Stop for Topshelf service host
   - Creates and manages SchedulerService for background operation

3. **Install-Service.ps1** - Service installation script
   - Installs PasswordExpiryNotification as Windows Service
   - Automatically starts the service after installation

4. **Start-ServiceGui.ps1** - GUI launch script
   - Starts the application in GUI mode (--gui flag)
   - Can be run while service is running in background

### Modified Files
1. **PasswordExpiryNotification.csproj**
   - Added `<PackageReference Include="Topshelf" Version="4.3.0" />`
   - Set `<StartupObject>PasswordExpiryNotification.Program</StartupObject>` to use custom entry point

2. **App.xaml**
   - Added `ShutdownMode="OnExplicitShutdown"` - prevents auto-exit in headless mode

3. **App.xaml.cs**
   - Added constructor to set `StartupUri = null` - prevents auto-window creation
   - Updated OnStartup to:
     - Load settings before headless check
     - Add defensive check for unexpected WPF startup
     - Pass AppSettingsModel to SchedulerService (not called with Start() yet)

## Architecture

### Service Modes
1. **Install/Service Mode**: `PasswordExpiryNotification.exe install` → Topshelf host
2. **GUI Mode**: `PasswordExpiryNotification.exe --gui` → WPF window
3. **Headless Mode**: `PasswordExpiryNotification.exe --scan --send` → No GUI, direct execution

### Topshelf Configuration
- ServiceName: `PasswordExpiryNotification`
- DisplayName: `Password Expiry Notification`
- Description: `Monitors AD password expiration and sends email warnings`
- StartAutomatically: Yes
- RunAs: LocalSystem

## Executable
- Location: `bin\Release\net8.0-windows\PasswordExpiryNotification.exe`
- Size: 164,864 bytes
- Created: 2026-10-13 12:53 UTC

## Next Steps
1. Test service installation: `.\Install-Service.ps1`
2. Test GUI mode: `.\Start-ServiceGui.ps1`
3. Test service control: `net start PasswordExpiryNotification`, `net stop PasswordExpiryNotification`
