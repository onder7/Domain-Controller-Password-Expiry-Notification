# Implementation Plan: Hybrid Windows Service + GUI Mode

## Overview
Refactor the WPF application to support three modes:
1. **Service Mode** (default): Runs as Windows Service via Topshelf, no GUI, SchedulerService runs automatically
2. **GUI Mode** (--gui flag): WPF MainWindow opens, reads shared state, GUI close doesn't stop service
3. **Headless Mode** (--scan --send): Existing CLI behavior preserved

## Architecture Decisions

### 1. Entry Point Architecture
- **Current:** App.xaml has OnStartup entry point
- **New:** Create Program.cs as true entry point to detect mode BEFORE WPF Application starts
- **Detection:** Parse `args` before instantiating `Application` object
- **For Service Mode:** Do NOT instantiate WPF Application at all (no dispatcher, no XAML loading)
- **For GUI Mode:** Start WPF Application normally
- **For Headless Mode:** Preserve existing App.xaml.cs headless logic

**Rationale:** WPF Application is heavy and resource-consuming. Service mode needs zero UI infrastructure. Program.cs as entry point lets us avoid WPF initialization entirely for service mode.

### 2. Topshelf Package Choice
- **Package:** Topshelf v10.0.0+ (compatible with .NET 8)
- **License:** Apache 2.0 (open source, production-ready)
- **Why:** Standard for Windows Service hosting in .NET; manages Install/Start/Stop/Uninstall via command-line args automatically
- **Service Identity:** Will run as SYSTEM by default (full AD access, DPAPI works with LocalMachine scope)

**Rationale:** Topshelf abstracts away Windows Service registration details and automates lifecycle management. It's the standard choice for .NET service projects.

### 3. Shared State Between Service and GUI
- **appsettings.json:** Read from AppContext.BaseDirectory (exe directory). Both service and GUI load from same path.
- **Logs/ folder:** Same as appsettings.json. Service writes logs, GUI reads and displays them.
- **No IPC:** Service and GUI do NOT communicate at runtime. GUI simply reads the files service writes.
- **No conflicts:** Service runs once daily (via scheduled time). GUI runs independently. No file locking conflicts.

**Rationale:** Shared files eliminate complexity of IPC/named pipes. Service only writes logs once per day. GUI is read-only when service is running.

### 4. OutputType Stays WinExe
- **Current:** OutputType = WinExe (required for WPF)
- **Change:** Keep it. Topshelf works fine with WinExe.
- **Service Mode Trick:** We never instantiate `Application` in service mode. Topshelf.Host runs as a console app framework within the WinExe binary.

**Rationale:** Changing to Exe breaks WPF resources. Topshelf can host a service inside a WinExe binary without issues.

### 5. DPAPI and Service Account
- **Current:** DpapiProtectionService uses `DataProtectionScope.LocalMachine`
- **Why it works:** Service will run as SYSTEM account (or a dedicated service account). LocalMachine scope means any account on the machine can decrypt.
- **No change needed:** DpapiProtectionService already correct.

**Rationale:** LocalMachine scope is required for multi-user scenarios (service decrypt, GUI decrypt in any user context).

## Implementation Steps

### Step 1: Add Topshelf NuGet Package
- **File:** PasswordExpiryNotification.csproj
- **Change:** Add `<PackageReference Include="Topshelf" Version="10.0.0" />`
- **Verify:** `dotnet restore` succeeds, no version conflicts

**Rationale:** Topshelf must be available before Program.cs runs.

---

### Step 2: Create Program.cs Entry Point
- **File:** c:\Users\OHOME\Documents\csharp\PasswordExpiryNotification\Program.cs (new)
- **Structure:**
  ```csharp
  using System.Windows;
  using PasswordExpiryNotification;
  using PasswordExpiryNotification.Services;
  using Topshelf;

  var args = Environment.GetCommandLineArgs().Skip(1).ToArray();

  // Detect mode from args
  if (args.Contains("--gui"))
  {
      // GUI Mode: Start WPF Application normally
      var app = new App();
      app.InitializeComponent(); // Loads App.xaml resources
      app.Run();
      return;
  }

  if (args.Contains("--scan") || args.Contains("--send") || args.Contains("--headless"))
  {
      // Headless Mode: Delegate to App.xaml.cs logic (OnStartup processes args, then Shutdown)
      var app = new App();
      app.InitializeComponent();
      app.Run();
      return;
  }

  // Service Mode: Host via Topshelf (default, no --gui, no --scan, etc.)
  var exitCode = HostFactory.Run(x =>
  {
      x.Service<ServiceManager>(s =>
      {
          s.ConstructUsing(name => new ServiceManager());
          s.WhenStarted(tc => tc.Start());
          s.WhenStopped(tc => tc.Stop());
          s.WhenShutdown(tc => tc.Shutdown());
      });

      x.RunAsLocalSystem();
      x.SetServiceName("PasswordExpiryNotification");
      x.SetDisplayName("Password Expiry Notification Service");
      x.SetDescription("Monitors AD for expiring passwords and sends automated notifications");
      x.EnableServiceRecovery(r => r.RestartService(1)); // Restart on crash after 1 minute
  });

  return (int)exitCode;
  ```
- **Decision:** On --gui or --scan/--send, WPF Application runs normally (existing App.xaml.cs OnStartup handles all modes)
- **Decision:** Without any mode args, Topshelf Host runs (service mode)
- **Verify:**
  - Service mode: `dotnet build && .\bin\Debug\net8.0-windows\PasswordExpiryNotification.exe install` (no GUI appears, service installs)
  - GUI mode: `.\bin\Debug\net8.0-windows\PasswordExpiryNotification.exe --gui` (WPF window opens)
  - Headless mode: `.\bin\Debug\net8.0-windows\PasswordExpiryNotification.exe --scan --send` (runs headless, exits)

---

### Step 3: Create ServiceManager Class
- **File:** c:\Users\OHOME\Documents\csharp\PasswordExpiryNotification\Services\ServiceManager.cs (new)
- **Structure:**
  - Constructor: Initialize LogService, SettingsService, AdService, EmailService, SchedulerService
  - `Start()` method: Create ServiceManager instances, call SchedulerService.Start(settings)
  - `Stop()` method: Call SchedulerService.Stop()
  - `Shutdown()` method: Cleanup (Stop + dispose resources)
- **Services held:**
  ```csharp
  public class ServiceManager
  {
      private ILogService? _log;
      private ISettingsService? _settings;
      private IAdService? _ad;
      private IEmailService? _email;
      private ISchedulerService? _scheduler;

      public void Start()
      {
          _log = new LogService();
          _settings = new SettingsService(_log);
          _ad = new AdService(_log);
          _email = new EmailService(_log);
          _scheduler = new SchedulerService(_ad, _email, _log);

          var appSettings = _settings.Load();
          _scheduler.Start(appSettings);
          _log.Info("Password Expiry Notification Service started.");
      }

      public void Stop()
      {
          _scheduler?.Stop();
          _log?.Info("Password Expiry Notification Service stopped.");
      }

      public void Shutdown()
      {
          Stop();
          (_scheduler as IDisposable)?.Dispose();
          _log?.Info("Password Expiry Notification Service shutdown.");
      }
  }
  ```
- **Rationale:** ServiceManager encapsulates all service-mode dependencies. Topshelf calls Start/Stop/Shutdown at lifecycle events. SchedulerService.Start() reads settings and begins timer-based automation.
- **Verify:** `dotnet build` succeeds, no compile errors

---

### Step 4: Update App.xaml to Remove StartupUri
- **File:** c:\Users\OHOME\Documents\csharp\PasswordExpiryNotification\App.xaml
- **Current:** Likely has `StartupUri="MainWindow.xaml"` or similar
- **Change:** Remove StartupUri attribute (Program.cs now controls startup)
- **Old:** `<Application x:Class="PasswordExpiryNotification.App" ... StartupUri="MainWindow.xaml">`
- **New:** `<Application x:Class="PasswordExpiryNotification.App" ...>` (no StartupUri)
- **Rationale:** OnStartup in code-behind now controls window creation. Declarative StartupUri would auto-open MainWindow even in service mode.
- **Verify:** `dotnet build`, check no compile errors

---

### Step 5: Update App.xaml.cs to Support GUI Mode
- **File:** c:\Users\OHOME\Documents\csharp\PasswordExpiryNotification\App.xaml.cs
- **Current behavior:** OnStartup initializes services, checks for headless args, runs headless or GUI
- **Change needed:** Distinguish between GUI mode (--gui) and normal GUI (no args from WPF Application.Run() perspective)
  - Service mode: Program.cs never reaches App, so OnStartup never runs
  - Headless mode: Program.cs detects --scan/--send, runs WPF briefly to call OnStartup (which exits via Shutdown)
  - GUI mode: Program.cs detects --gui, runs WPF normally (OnStartup initializes and shows MainWindow)
- **Update OnStartup logic:**
  ```csharp
  protected override void OnStartup(StartupEventArgs e)
  {
      base.OnStartup(e);

      // Initialize services
      var log = new LogService();
      var settingsService = new SettingsService(log);
      var adService = new AdService(log);
      var emailService = new EmailService(log);
      var schedulerService = new SchedulerService(adService, emailService, log);
      _schedulerService = schedulerService;

      // Headless mode (--scan, --send, --headless)
      if (HasHeadlessArgs(e.Args))
      {
          RunHeadlessMode(log, settingsService, adService, emailService, e.Args);
          Shutdown(0);
          return;
      }

      // GUI mode (no headless args) — open MainWindow and start scheduler (as before)
      var mainVm = new MainViewModel(settingsService, adService, emailService, log, schedulerService);
      var window = new MainWindow(mainVm);
      window.Show();
  }
  ```
- **No change to headless logic:** RunHeadlessMode, HasHeadlessArgs stay the same
- **Verify:** 
  - GUI mode: `--gui` flag launches window, SchedulerService starts (if Automatic mode in settings)
  - Headless: `--scan --send` runs and exits
  - Service mode: Program.cs never invokes WPF, so OnStartup never runs (verified by no logs appearing from OnStartup)

---

### Step 6: Create Install-Service.ps1 Script
- **File:** c:\Users\OHOME\Documents\csharp\PasswordExpiryNotification\Install-Service.ps1 (new)
- **Purpose:** Simplify service installation
- **Content:**
  ```powershell
  <#
  .SYNOPSIS
  Installs and starts the Password Expiry Notification Windows Service.

  .EXAMPLE
  .\Install-Service.ps1
  #>

  $exePath = "$PSScriptRoot\bin\Release\net8.0-windows\PasswordExpiryNotification.exe"

  if (-not (Test-Path $exePath)) {
      Write-Error "Executable not found at $exePath. Build first with: dotnet build -c Release"
      exit 1
  }

  Write-Host "Installing service..." -ForegroundColor Cyan
  & $exePath install

  Write-Host "Starting service..." -ForegroundColor Cyan
  net start PasswordExpiryNotification

  Write-Host "Service installed and started. Check: Get-Service PasswordExpiryNotification" -ForegroundColor Green
  ```
- **Rationale:** One-click script for deployment. Users build, then run script to install.
- **Verify:** `Get-Service PasswordExpiryNotification` shows service in running state

---

### Step 7: Create Start-ServiceGui.ps1 Script
- **File:** c:\Users\OHOME\Documents\csharp\PasswordExpiryNotification\Start-ServiceGui.ps1 (new)
- **Purpose:** Launch GUI while service runs in background
- **Content:**
  ```powershell
  <#
  .SYNOPSIS
  Launches the Password Expiry Notification GUI in a separate window.
  Service continues running in background.

  .EXAMPLE
  .\Start-ServiceGui.ps1
  #>

  $exePath = "$PSScriptRoot\bin\Release\net8.0-windows\PasswordExpiryNotification.exe"

  if (-not (Test-Path $exePath)) {
      Write-Error "Executable not found at $exePath. Build first with: dotnet build -c Release"
      exit 1
  }

  Write-Host "Launching GUI (service runs in background)..." -ForegroundColor Cyan
  & $exePath --gui
  ```
- **Rationale:** Clear separation: service runs 24/7, GUI can be opened anytime without affecting service.
- **Verify:** GUI opens, service keeps running. Close GUI → service still runs.

---

### Step 8: Update SchedulerService to Handle Service Mode
- **File:** c:\Users\OHOME\Documents\csharp\PasswordExpiryNotification\Services\SchedulerService.cs
- **Current:** Already has Start(settings), Stop(), IsRunning, events (OnScanCompleted, OnSendCompleted, OnError)
- **Verify:** No changes needed. SchedulerService already implements correct interface and behavior.
- **Rationale:** SchedulerService is already designed for automation. ServiceManager.Start() calls it, and it runs timers independently.

---

### Step 9: Create Uninstall-Service.ps1 Script (Optional but Recommended)
- **File:** c:\Users\OHOME\Documents\csharp\PasswordExpiryNotification\Uninstall-Service.ps1 (new)
- **Content:**
  ```powershell
  <#
  .SYNOPSIS
  Stops and uninstalls the Password Expiry Notification Windows Service.

  .EXAMPLE
  .\Uninstall-Service.ps1
  #>

  $serviceName = "PasswordExpiryNotification"

  Write-Host "Stopping service..." -ForegroundColor Cyan
  net stop $serviceName

  Write-Host "Uninstalling service..." -ForegroundColor Cyan
  $exePath = "$PSScriptRoot\bin\Release\net8.0-windows\PasswordExpiryNotification.exe"
  & $exePath uninstall

  Write-Host "Service uninstalled." -ForegroundColor Green
  ```
- **Rationale:** Admins may need to upgrade or remove service. Automated script reduces errors.

---

## Build and Test

### Verification Commands

1. **Compile check:**
   ```powershell
   cd c:\Users\OHOME\Documents\csharp\PasswordExpiryNotification
   dotnet build
   ```
   **Expected:** No errors, successful build

2. **Service install (dry-run/test):**
   ```powershell
   .\bin\Debug\net8.0-windows\PasswordExpiryNotification.exe install
   Get-Service PasswordExpiryNotification  # Should exist
   ```
   **Expected:** Service appears in Windows Services

3. **GUI mode test:**
   ```powershell
   .\bin\Debug\net8.0-windows\PasswordExpiryNotification.exe --gui
   ```
   **Expected:** MainWindow opens, no crash

4. **Headless mode test (if appsettings configured):**
   ```powershell
   .\bin\Debug\net8.0-windows\PasswordExpiryNotification.exe --scan --send
   ```
   **Expected:** Runs headless, logs to Logs/ folder, exits with code 0

5. **Service cleanup:**
   ```powershell
   net stop PasswordExpiryNotification
   .\bin\Debug\net8.0-windows\PasswordExpiryNotification.exe uninstall
   Get-Service PasswordExpiryNotification  # Should not exist
   ```
   **Expected:** Service removed cleanly

---

## Files to Create / Modify

### Create (New Files)
- Program.cs
- Services/ServiceManager.cs
- Install-Service.ps1
- Start-ServiceGui.ps1
- Uninstall-Service.ps1

### Modify (Existing Files)
- PasswordExpiryNotification.csproj (add Topshelf package)
- App.xaml (remove StartupUri)
- App.xaml.cs (no changes needed, but verify headless logic still works)

### No Changes
- Services/SchedulerService.cs (already correct)
- Services/DpapiProtectionService.cs (already uses LocalMachine)
- appsettings.json (shared between service and GUI)
- Logs/ folder (shared between service and GUI)

---

## Summary of Architecture

| Mode | Entry Point | WPF App | Services | Timer | Output |
|------|---|---|---|---|---|
| Service | Program.cs → Topshelf | ❌ No | ServiceManager | ✓ SchedulerService | Logs/ folder |
| GUI | Program.cs → App.xaml.cs | ✓ Yes | MainViewModel | ✓ SchedulerService | UI + Logs/ |
| Headless | Program.cs → App.xaml.cs | ✓ Brief | Same as GUI | ❌ No timer | Logs/, then exit |

**Key Points:**
- Same .exe, three behaviors controlled by args
- Service mode = zero UI, pure automation
- GUI mode = can run while service is active
- Headless mode = backward-compatible CLI mode
- All modes share appsettings.json and Logs/ folder
- DPAPI works across all modes (LocalMachine scope)

