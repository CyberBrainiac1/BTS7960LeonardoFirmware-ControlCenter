using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using ArduinoFFBControlCenter.Services;

namespace ArduinoFFBControlCenter;

public partial class App : Application
{
    private static string CrashLogPath => Path.Combine(Path.GetTempPath(), "ArduinoFFBControlCenter-crash.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;

        try
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Load();
            // Ensure settings.json exists on first launch.
            settingsService.Save(settings);
            var theme = new ThemeService();
            theme.ApplyTheme(settings.ThemeMode);
        }
        catch (Exception ex)
        {
            WriteCrashLog("Startup", ex);
            MessageBox.Show(
                $"Startup failed. Crash log written to:\n{CrashLogPath}",
                "Arduino FFB Control Center",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

        base.OnStartup(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog("DispatcherUnhandledException", e.Exception);
        MessageBox.Show(
            $"Unexpected error. Crash log written to:\n{CrashLogPath}",
            "Arduino FFB Control Center",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(-1);
    }

    private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            WriteCrashLog("AppDomainUnhandledException", ex);
        }
    }

    private static void OnTaskSchedulerUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrashLog("TaskSchedulerUnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    private static void WriteCrashLog(string source, Exception ex)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine(DateTimeOffset.Now.ToString("O"));
            sb.AppendLine(source);
            sb.AppendLine(ex.ToString());
            sb.AppendLine(new string('-', 80));
            File.AppendAllText(CrashLogPath, sb.ToString());
        }
        catch
        {
            // Never throw from crash logging.
        }
    }
}
