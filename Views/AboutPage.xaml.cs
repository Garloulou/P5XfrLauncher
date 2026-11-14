using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using LauncherP5XFr.Models;
using LauncherP5XFr.Services;

namespace LauncherP5XFr.Views;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();

        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        LauncherVersionTextBlock.Text = ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "v1.0.2";

        var osDesc = RuntimeInformation.OSDescription;
        var arch = RuntimeInformation.ProcessArchitecture;
        var dotNetVer = Environment.Version;

        SysInfoTextBlock.Text = $"OS : {osDesc} ({arch})\nRuntime : .NET {dotNetVer}\nWindows App SDK : 2.6.5 (WinUI 3)\nEmplacement : {AppContext.BaseDirectory}";

        LoadImages();
    }

    private void LoadImages()
    {
        try
        {
            var logoPath = Helpers.AppEnvironment.GetAssetPath("p5x_logo.png");
            if (logoPath != null)
            {
                AboutLogoImage.Source = new BitmapImage(new Uri(logoPath));
                AboutLogoImage.Visibility = Visibility.Visible;
                AboutFallbackTitlePanel.Visibility = Visibility.Collapsed;
            }

            var capsulePath = Helpers.AppEnvironment.GetAssetPath("p5x_capsule.jpg")
                           ?? Helpers.AppEnvironment.GetAssetPath("p5x_header.jpg");
            if (capsulePath != null)
            {
                AboutCapsuleImage.Source = new BitmapImage(new Uri(capsulePath));
                AboutCapsuleBorder.Visibility = Visibility.Visible;
            }
            else
            {
                AboutCapsuleBorder.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warn($"Erreur chargement images AboutPage: {ex.Message}");
        }
    }

    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        UpdateProgressBar.Visibility = Visibility.Visible;
        UpdateProgressBar.IsIndeterminate = true;

        AboutInfoBar.Severity = InfoBarSeverity.Informational;
        AboutInfoBar.Title = "Recherche en cours";
        AboutInfoBar.Message = "Vérification des mises à jour sur GitHub...";
        AboutInfoBar.IsOpen = true;

        try
        {
            var (isUpdateAvailable, release, message) = await SelfUpdateService.Instance.CheckLauncherUpdateAsync();

            if (isUpdateAvailable && release != null)
            {
                AboutInfoBar.Severity = InfoBarSeverity.Informational;
                AboutInfoBar.Title = "Mise à jour disponible";
                AboutInfoBar.Message = $"Téléchargement et installation de la version {release.TagName}...";

                UpdateProgressBar.IsIndeterminate = false;
                var progress = new Progress<DownloadProgressInfo>(p =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        UpdateProgressBar.Value = p.Percentage;
                        AboutInfoBar.Message = p.StatusMessage;
                    });
                });

                await SelfUpdateService.Instance.ApplyLauncherUpdateAsync(release, progress);
            }
            else
            {
                AboutInfoBar.Severity = InfoBarSeverity.Success;
                AboutInfoBar.Title = "Launcher à jour";
                AboutInfoBar.Message = message;
            }
        }
        catch (Exception ex)
        {
            AboutInfoBar.Severity = InfoBarSeverity.Error;
            AboutInfoBar.Title = "Erreur de mise à jour";
            AboutInfoBar.Message = ex.Message;
        }
        finally
        {
            UpdateProgressBar.Visibility = Visibility.Collapsed;
            CheckUpdateButton.IsEnabled = true;
        }
    }
}
