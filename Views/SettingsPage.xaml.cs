using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using LauncherP5XFr.Models;
using LauncherP5XFr.Services;
using WinRT.Interop;

namespace LauncherP5XFr.Views;

public sealed partial class SettingsPage : Page
{
    private readonly IConfigService _configService;
    private readonly IGameLocator _gameLocator;
    private readonly ILocalizationService _locService;
    private readonly ILoggerService _logger;

    private bool _isInitializing = true;

    public SettingsPage()
    {
        _configService = ConfigService.Instance;
        _gameLocator = GameLocator.Instance;
        _locService = LocalizationService.Instance;
        _logger = LoggerService.Instance;

        InitializeComponent();

        Loaded += SettingsPage_Loaded;
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSettings();
        _isInitializing = false;
    }

    private void LoadSettings()
    {
        var cfg = _configService.Config;

        GamePathTextBox.Text = cfg.GamePath ?? "";
        GameExeTextBox.Text = cfg.GameExeName ?? "P5X.exe";
        LaunchViaSteamToggleSwitch.IsOn = cfg.LaunchViaSteam;
        BepInExConsoleToggleSwitch.IsOn = cfg.EnableBepInExConsole;

        if (cfg.UseDoorstopConfigToggle)
        {
            RadioDoorstopConfig.IsChecked = true;
        }
        else
        {
            RadioRenameDll.IsChecked = true;
        }

        // Theme
        foreach (ComboBoxItem item in ThemeComboBox.Items)
        {
            if (string.Equals(item.Tag as string, cfg.Theme, StringComparison.OrdinalIgnoreCase))
            {
                ThemeComboBox.SelectedItem = item;
                break;
            }
        }
        if (ThemeComboBox.SelectedItem == null)
            ThemeComboBox.SelectedIndex = 0;

        // Language
        foreach (ComboBoxItem item in LanguageComboBox.Items)
        {
            if (string.Equals(item.Tag as string, cfg.Language, StringComparison.OrdinalIgnoreCase))
            {
                LanguageComboBox.SelectedItem = item;
                break;
            }
        }
        if (LanguageComboBox.SelectedItem == null)
            LanguageComboBox.SelectedIndex = 0;
    }

    private async void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = await PickFolderAsync();
        if (folder != null)
        {
            GamePathTextBox.Text = folder;
            _configService.Config.GamePath = folder;
            _configService.Save();

            if (_gameLocator.IsValidGameDirectory(folder, out var exe))
            {
                ShowInfo(InfoBarSeverity.Success, "Jeu détecté", $"Exécutable reconnu : {exe}");
            }
            else
            {
                ShowInfo(InfoBarSeverity.Warning, "Attention", "Aucun exécutable de jeu standard (P5X.exe) n'a été trouvé dans ce dossier.");
            }
        }
    }

    private async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
        picker.FileTypeFilter.Add("*");

        var hWnd = WindowNative.GetWindowHandle(App.MainWindowInstance);
        InitializeWithWindow.Initialize(picker, hWnd);

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    private void AutoDetectFolders_Click(object sender, RoutedEventArgs e)
    {
        _gameLocator.AutoDetectGameInstallation();
        LoadSettings();
        var detected = _gameLocator.GetGameDirectory();
        if (!string.IsNullOrEmpty(detected))
        {
            ShowInfo(InfoBarSeverity.Success, "Recherche réussie", $"Installation trouvée : {detected}");
        }
        else
        {
            ShowInfo(InfoBarSeverity.Informational, "Recherche terminée", "Aucune installation Steam trouvée automatiquement. Veuillez sélectionner le dossier manuellement.");
        }
    }

    private void GamePathTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitializing) return;
        _configService.Config.GamePath = GamePathTextBox.Text.Trim();
        _configService.Save();
    }

    private void GameExeTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitializing) return;
        _configService.Config.GameExeName = GameExeTextBox.Text.Trim();
        _configService.Save();
    }

    private void ToggleMethodRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        _configService.Config.UseDoorstopConfigToggle = RadioDoorstopConfig.IsChecked == true;
        _configService.Save();
    }

    private void LaunchViaSteamToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _configService.Config.LaunchViaSteam = LaunchViaSteamToggleSwitch.IsOn;
        _configService.Save();
    }

    private void BepInExConsoleToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        _configService.Config.EnableBepInExConsole = BepInExConsoleToggleSwitch.IsOn;
        _configService.Save();

        var gameDir = _gameLocator.GetGameDirectory();
        if (!string.IsNullOrEmpty(gameDir))
        {
            try
            {
                var targetDir = GameLocator.GetGameClientDirectory(gameDir);
                var configDir = Path.Combine(targetDir, "BepInEx", "config");
                if (Directory.Exists(configDir))
                {
                    var cfgPath = Path.Combine(configDir, "BepInEx.cfg");
                    var enabled = BepInExConsoleToggleSwitch.IsOn;
                    if (File.Exists(cfgPath))
                    {
                        var content = File.ReadAllText(cfgPath);
                        var regex = new System.Text.RegularExpressions.Regex(@"(\[Logging\.Console\][\s\S]*?Enabled\s*=\s*)(true|false)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (regex.IsMatch(content))
                        {
                            content = regex.Replace(content, $"${{1}}{(enabled ? "true" : "false")}", 1);
                            File.WriteAllText(cfgPath, content);
                        }
                    }
                }
            }
            catch { }
        }
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (ThemeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _configService.Config.Theme = tag;
            _configService.Save();
            App.MainWindowInstance?.ApplyTheme(tag);
        }
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _locService.CurrentLanguage = tag;
        }
    }

    private async void ReinstallBepInEx_Click(object sender, RoutedEventArgs e)
    {
        var gameDir = _gameLocator.GetGameDirectory();
        if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir))
        {
            ShowInfo(InfoBarSeverity.Warning, "Dossier manquant", "Veuillez définir un dossier de jeu valide.");
            return;
        }

        ShowInfo(InfoBarSeverity.Informational, "Installation en cours", "Téléchargement de la dernière version de BepInEx...");

        var progress = new Progress<DownloadProgressInfo>(p =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ShowInfo(InfoBarSeverity.Informational, "Installation en cours", p.StatusMessage);
            });
        });

        var (success, msg) = await BepInExInstallService.Instance.InstallBepInExAsync(gameDir, progress);
        if (success)
        {
            ShowInfo(InfoBarSeverity.Success, "BepInEx", msg);
        }
        else
        {
            ShowInfo(InfoBarSeverity.Error, "Échec BepInEx", msg);
        }
    }

    private void OpenLogFile_Click(object sender, RoutedEventArgs e)
    {
        var logFile = _logger.GetLogFilePath();
        if (File.Exists(logFile))
        {
            Process.Start(new ProcessStartInfo { FileName = "notepad.exe", Arguments = $"\"{logFile}\"", UseShellExecute = true });
        }
        else
        {
            ShowInfo(InfoBarSeverity.Informational, "Journal", "Le fichier journal est vide ou n'a pas encore été créé.");
        }
    }

    private void OpenGameFolder_Click(object sender, RoutedEventArgs e)
    {
        var gameDir = _gameLocator.GetGameDirectory();
        if (!string.IsNullOrEmpty(gameDir) && Directory.Exists(gameDir))
        {
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{gameDir}\"", UseShellExecute = true });
        }
        else
        {
            ShowInfo(InfoBarSeverity.Warning, "Dossier manquant", "Veuillez définir un dossier de jeu valide.");
        }
    }

    private void OpenAssetsFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var assetsDir = Path.Combine(Helpers.AppEnvironment.AppDirectory, "Assets");
            if (!Directory.Exists(assetsDir))
            {
                Directory.CreateDirectory(assetsDir);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = assetsDir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowInfo(InfoBarSeverity.Error, "Erreur", $"Impossible d'ouvrir le dossier Assets : {ex.Message}");
        }
    }

    private void ShowInfo(InfoBarSeverity severity, string title, string message)
    {
        SettingsInfoBar.Severity = severity;
        SettingsInfoBar.Title = title;
        SettingsInfoBar.Message = message;
        SettingsInfoBar.IsOpen = true;
    }
}
