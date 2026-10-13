using System.Windows;
using PasswordExpiryNotification.Services;
using Topshelf;

namespace PasswordExpiryNotification;

class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        // Check for command-line arguments before starting WPF
        if (args.Contains("--gui"))
        {
            // Start WPF app in GUI mode
            return StartWpfApp(args);
        }

        if (args.Contains("--scan") || args.Contains("--send") || args.Contains("--headless"))
        {
            // Start WPF app in headless mode
            return StartWpfApp(args);
        }

        // Otherwise: install/uninstall/start/stop (Topshelf service management)
        return RunServiceHost(args);
    }

    private static int StartWpfApp(string[] args)
    {
        try
        {
            var app = new App { StartupUri = new Uri("MainWindow.xaml", UriKind.Relative) };
            app.InitializeComponent();
            return app.Run();
        }
        catch (Exception ex)
        {
            var log = new LogService();
            log.Error($"WPF uygulama hatası: {ex.Message}");
            return 1;
        }
    }

    private static int RunServiceHost(string[] args)
    {
        var exitCode = HostFactory.Run(hostConfigurator =>
        {
            hostConfigurator.Service<ServiceManager>(serviceConfigurator =>
            {
                serviceConfigurator.ConstructUsing(s => new ServiceManager());
                serviceConfigurator.WhenStarted(s => s.Start(null!));
                serviceConfigurator.WhenStopped(s => s.Stop(null!));
            });

            hostConfigurator.SetServiceName("PasswordExpiryNotification");
            hostConfigurator.SetDisplayName("Password Expiry Notification");
            hostConfigurator.SetDescription("Monitors AD password expiration and sends email warnings");
            hostConfigurator.StartAutomatically();
            hostConfigurator.RunAsLocalSystem();
        });

        return (int)exitCode;
    }
}
