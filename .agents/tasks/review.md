# Hybrid Windows Service + GUI Mode Implementation

Dual-boot architecture enabling the application to run as a Windows Service (24/7 background) or as a GUI app (monitoring/config), with independent headless CLI mode preserved. The implementation cleanly separates concerns: Program.cs routes entry points, ServiceManager wraps the background service lifecycle, and App.xaml.cs conditionally initializes WPF based on startup mode.

**Watch for:** WPF initialization check in Program.cs lacks fallback for unexpected startup; ServiceManager passes `null!` to WhenStarted/WhenStopped callbacks without documentation; App.xaml defensive check silently shuts down and would mask configuration errors.

**Verdict**: APPROVED

## High-level view

The architecture routes on command-line arguments before WPF initialization: `--gui` or `--scan`/`--send` enter the WPF runtime (App.xaml.cs decides whether to show a window); service verbs (install/uninstall/start/stop) bypass WPF and route to Topshelf. Program.cs correctly sets `StartupUri` conditionally and avoids WPF type references in the service code path, so service mode operates without WPF dependencies. ServiceManager starts and stops a SchedulerService instance, sharing the same SettingsService and log file path as GUI mode. App.xaml sets `ShutdownMode="OnExplicitShutdown"` so closing the GUI doesn't terminate the background service if the service is installed. The headless mode logic is preserved and produces admin summary emails with scanned user lists.

OutputType remains WinExe, which is correct: the app is a Windows GUI executable that can also run as a service via Topshelf.

Build completed successfully; no blocking warnings or errors.

## Issues

<details>
<summary>Issues (3)</summary>

1. **Potential WPF initialization race** — Program.cs assumes App() constructor and InitializeComponent() will succeed and not reference GUI resources. If App.xaml.cs throws an exception during component initialization and a CLI arg is present, the error is caught but logs only via LogService, which may not exist if SettingsService hasn't loaded yet. The try-catch should initialize LogService first.

2. **Null coalescing in Topshelf callbacks** — ServiceManager passes `null!` (null-forgiving operator) to WhenStarted and WhenStopped, but Topshelf calls these with a non-null HostControl argument. While this works in practice (the parameter is not used), it silently ignores the contract and would confuse maintainers. The callback signatures should match Topshelf's expected HostControl contract or be documented.

3. **Silent mode detection flaw** — App.xaml.cs defensive check exits silently when no recognized args are present (not `--gui`, `--scan`, `--send`, `--headless`). If someone runs the app with an invalid flag or misspells an argument, the app shuts down with only a warning log, not an error, leaving no feedback to the user.

</details>

<details>
<summary>Details</summary>

### Service mode does not reference WPF types

Program.cs routes to `RunServiceHost` when no GUI or headless args are present. The ServiceManager class instantiates LogService, SettingsService, AdService, EmailService, and SchedulerService — all non-WPF types — and never imports System.Windows or calls any XAML functionality. The service executable runs without WPF runtime overhead, which is correct for a background service.

### GUI mode correctly conditional

When `--gui` is present, Program.cs calls `StartWpfApp`, which instantiates `new App()`, sets `StartupUri` (which App.xaml.cs then overrides to null in the constructor), and calls `app.Run()`. App.xaml.cs constructor sets `this.StartupUri = null;` to prevent auto-creation of MainWindow, then in `OnStartup`, the GUI-mode code path creates MainWindow explicitly after services are initialized. The flow is: CLI arg routes to WPF, App constructor disables auto-window, OnStartup detects mode and creates window if appropriate.

However, Program.cs sets StartupUri *after* App() constructor runs: `var app = new App { StartupUri = new Uri(...) }`. This property assignment happens after the constructor that set it to null, so the line in Program.cs is redundant but harmless. App.xaml.cs will still override it later in OnStartup.

### Headless mode preserves all features

`--scan`, `--send`, and `--headless` arguments trigger headless mode in App.xaml.cs `OnStartup`. The RunHeadlessMode method calls `ad.ScanExpiringUsersAsync`, `email.SendWarningsAsync`, and generates an admin summary email with a formatted HTML table of scanned users. The admin email logic was new in this PR (the original request asked to include scanned user info in the admin summary). The implementation iterates through the user list and builds HTML with alternating row colors and proper formatting. All four headless arguments are checked but only `--scan` and `--send` are used; `--headless` is present but redundant (could be removed or used as a sentinel).

### Shared appsettings and log paths

ServiceManager and GUI mode both instantiate SettingsService (which loads from `appsettings.json` in the output directory) and LogService (which writes to the `Logs/` directory). There is no separate service log path; both use the same files. This is confirmed by both code paths calling `new SettingsService(_log)` and `new LogService()` without any path remapping. Logs are genuinely shared.

### Topshelf installation and control

Program.cs calls `HostFactory.Run` with service configuration: service name `PasswordExpiryNotification`, display name `Password Expiry Notification`, set to start automatically and run as LocalSystem. The Install-Service.ps1 script locates the executable, calls `PasswordExpiryNotification.exe install`, and then calls `Start-Service` if the install succeeds. This flow allows Topshelf to register the service in Windows SCM, and the script then starts it.

The service can be controlled via `net start` / `net stop` or PowerShell's `Start-Service` / `Stop-Service` commands. Topshelf also provides uninstall via the `uninstall` verb (not shown in the scripts but functional). The Topshelf configuration correctly specifies LocalSystem as the run-as account, which has permissions for AD and SMTP operations.

### Program.cs routing logic

Program.cs Main method checks for args in order:
1. If `--gui` → Start WPF app
2. Else if `--scan` or `--send` or `--headless` → Start WPF app (in headless mode)
3. Else → Run Topshelf service host

The logic is sound. The `StartWpfApp` method is called in both GUI and headless modes, so both paths initialize the WPF application. The App.xaml.cs OnStartup method then decides whether to show a window based on the arguments.

### OutputType is WinExe

The csproj file contains `<OutputType>WinExe</OutputType>`, which is correct. WinExe produces a GUI executable (.exe) without a console window. Topshelf can run WinExe applications as Windows services; this is a supported scenario.

### ServiceManager Start/Stop lifecycle

ServiceManager.Start() instantiates services and calls `_scheduler.Start(appSettings)`. ServiceManager.Stop() calls `_scheduler.Stop()`. Both methods return bool (required by Topshelf's interface). The Start method loads settings before starting the scheduler, which is the correct order. The Stop method checks for null before calling Stop (using `?.`), which is defensive.

One concern: the `null!` operator is used in Program.cs when passing callbacks to Topshelf:

```csharp
serviceConfigurator.WhenStarted(s => s.Start(null!));
serviceConfigurator.WhenStopped(s => s.Stop(null!));
```

The HostControl parameter is passed but immediately discarded via `null!`. This works because ServiceManager.Start and Stop don't use the hostControl argument, but it violates the interface contract and would confuse maintainers. A better approach would be to accept the parameter and document why it's unused, or to use a discard `_` parameter.

### App.xaml.cs defensive checks

The OnStartup method includes a defensive check: if no args match any expected pattern, it logs a warning and shuts down. This is protective against accidental WPF startup, but the check is overly broad. The condition is:

```csharp
if (!e.Args.Contains("--gui") && !e.Args.Contains("--scan") && 
    !e.Args.Contains("--send") && !e.Args.Contains("--headless"))
{
    log.Warning("Uygulamaya beklenmeyen başlatma: GUI başlatılmıyor.");
    Shutdown(0);
    return;
}
```

This means that if the app is launched with any unrecognized flag (e.g., `--help`, `--config`, or a typo like `--guu`), it silently exits. A user running the app without arguments would also hit this check and exit without feedback. The correct behavior would be to default to GUI mode or to display usage information if no args are provided.

### Build successful

Build-result.md confirms the build succeeded (exit code 0) on 2026-10-13. Executable is 164,864 bytes. No errors or warnings are reported.

</details>

<details>
<summary>File map</summary>

- **Program.cs** (new): Custom entry point with CLI argument routing to WPF app or Topshelf service host
- **Services/ServiceManager.cs** (new): Topshelf service implementation that instantiates and manages SchedulerService lifecycle
- **App.xaml** (modified): Added `ShutdownMode="OnExplicitShutdown"` to prevent auto-shutdown when GUI closes while service runs
- **App.xaml.cs** (modified): Constructor disables auto-window creation; OnStartup conditionally shows GUI or runs headless mode; admin summary email with user list added
- **PasswordExpiryNotification.csproj** (modified): Added Topshelf 4.3.0 dependency; set StartupObject to Program
- **Install-Service.ps1** (new): PowerShell script to install and start the Windows Service
- **Start-ServiceGui.ps1** (new): PowerShell script to launch GUI mode while service runs in background

[Full diff available via git diff]

</details>
