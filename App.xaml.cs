using System;
using System.IO;
using Microsoft.UI.Xaml;
using LauncherP5XFr.Services;

namespace LauncherP5XFr;

public partial class App : Application
{
    public static MainWindow? MainWindowInstance { get; private set; }

    public App()
    {
        InitializeComponent();

        UnhandledException += (sender, e) =>
        {
            var logDir = Path.Combine(Helpers.AppEnvironment.AppDirectory, "logs");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, "launcher.log");
            var err = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [XAML_CRASH] {e.Message} | Exception: {e.Exception?.GetType().Name}: {e.Exception?.Message}\n{e.Exception?.StackTrace}\n";
            File.AppendAllText(logFile, err);
            LoggerService.Instance.Error($"Crash XAML non géré : {e.Message}", e.Exception);
            e.Handled = true;
        };
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            LoggerService.Instance.Info("=== Lancement de Persona 5: The Phantom X Launcher FR ===");

            try
            {
                GameLocator.Instance.AutoDetectGameInstallation();
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Warn($"Détection automatique du jeu non terminée : {ex.Message}");
            }

            MainWindowInstance = new MainWindow();
            MainWindowInstance.Activate();
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error("Crash dans OnLaunched", ex);
            throw;
        }
    }
}
