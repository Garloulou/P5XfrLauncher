using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;
using LauncherP5XFr.Models;
using LauncherP5XFr.Services;
using WinRT.Interop;

namespace LauncherP5XFr.Views;

public sealed partial class HomePage : Page
{
    private readonly IConfigService _configService;
    private readonly IGameLocator _gameLocator;
    private readonly IModToggleService _modToggleService;
    private readonly IGameLauncherService _launcherService;
    private readonly IBepInExInstallService _bepInExService;
    private readonly IGitHubUpdateService _updateService;
    private readonly ILoggerService _logger;

    private bool _isInitializing = true;

    public HomePage()
    {
        // 1. Initialize services BEFORE InitializeComponent to prevent NullReference on early XAML events
        _configService = ConfigService.Instance;
        _gameLocator = GameLocator.Instance;
        _modToggleService = ModToggleService.Instance;
        _launcherService = GameLauncherService.Instance;
        _bepInExService = BepInExInstallService.Instance;
        _updateService = GitHubUpdateService.Instance;
        _logger = LoggerService.Instance;

        InitializeComponent();

        Loaded += HomePage_Loaded;
    }

    private async void HomePage_Loaded(object sender, RoutedEventArgs e)
    {
        _isInitializing = true;
        try
        {
            LoadImages();
            TranslationToggleSwitch.IsOn = _configService.Config.TranslationEnabled;
            RefreshUIState();

            // Auto-check on startup
            var gameDir = _gameLocator.GetGameDirectory();
            if (!string.IsNullOrEmpty(gameDir) && Directory.Exists(gameDir))
            {
                if (!_bepInExService.IsBepInExInstalled(gameDir))
                {
                    MainInfoBar.Severity = InfoBarSeverity.Warning;
                    MainInfoBar.Title = "Installation de BepInEx requise";
                    MainInfoBar.Message = "BepInEx n'est pas encore présent dans le dossier du jeu. Cliquez sur 'Installer BepInEx' pour l'installer automatiquement.";
                    InfoBarInstallBepInExButton.Visibility = Visibility.Visible;
                    InstallBepInExButton.Visibility = Visibility.Visible;
                    MainInfoBar.IsOpen = true;
                }
                else if (!string.IsNullOrWhiteSpace(_configService.Config.TranslationRepo) &&
                         !_configService.Config.TranslationRepo.Contains("[OWNER/REPO]"))
                {
                    // Check for translation update in background
                    _ = CheckTranslationUpdateSilentlyAsync(gameDir);
                }
            }
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private void LoadImages()
    {
        try
        {
            // 1. Hero widescreen background
            var heroPath = Helpers.AppEnvironment.GetAssetPath("p5x_hero.jpg")
                        ?? Helpers.AppEnvironment.GetAssetPath("p5x_header.jpg")
                        ?? Helpers.AppEnvironment.GetAssetPath("keyart.png")
                        ?? Helpers.AppEnvironment.GetAssetPath("banner.jpg");
            if (heroPath != null)
            {
                HeroBackgroundImage.Source = new BitmapImage(new Uri(heroPath));
            }

            // 2. Official P5X transparent logo
            var logoPath = Helpers.AppEnvironment.GetAssetPath("p5x_logo.png");
            if (logoPath != null)
            {
                HeroLogoImage.Source = new BitmapImage(new Uri(logoPath));
                HeroLogoImage.Visibility = Visibility.Visible;
                HeroFallbackTitlePanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                HeroLogoImage.Visibility = Visibility.Collapsed;
                HeroFallbackTitlePanel.Visibility = Visibility.Visible;
            }

            // 3. Official Vertical Capsule Poster (Box Art)
            var capsulePath = Helpers.AppEnvironment.GetAssetPath("p5x_capsule.jpg")
                           ?? Helpers.AppEnvironment.GetAssetPath("p5x_header.jpg");
            if (capsulePath != null)
            {
                GameCapsuleImage.Source = new BitmapImage(new Uri(capsulePath));
                GameCapsuleImage.Visibility = Visibility.Visible;
                GameCapsuleFallbackIcon.Visibility = Visibility.Collapsed;
            }
            else
            {
                GameCapsuleImage.Visibility = Visibility.Collapsed;
                GameCapsuleFallbackIcon.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warn($"Erreur de chargement des images HomePage : {ex.Message}");
        }
    }

    private void RefreshUIState()
    {
        try
        {
            var gameDir = _gameLocator.GetGameDirectory();
            var isValid = _gameLocator.IsValidGameDirectory(gameDir, out var detectedExe);

            // 1. Update Game Path
            if (isValid && !string.IsNullOrEmpty(gameDir))
            {
                GamePathTextBlock.Text = $"{gameDir} ({detectedExe})";
                GamePathTextBlock.ClearValue(TextBlock.ForegroundProperty);
            }
            else
            {
                GamePathTextBlock.Text = "Non configuré ou introuvable. Cliquez sur 'Changer...'";
                if (Application.Current.Resources.TryGetValue("P5CrimsonBrush", out var crimsonBrush) && crimsonBrush is Brush cb)
                {
                    GamePathTextBlock.Foreground = cb;
                }
            }

            // 2. BepInEx & Translation Status
            var isBepInExInstalled = _bepInExService.IsBepInExInstalled(gameDir);
            var localTrans = _updateService.GetLocalTranslationVersion(gameDir);

            if (!isBepInExInstalled)
            {
                StatusBadgeText.Text = "BepInEx non installé";
                StatusBadgeIcon.Glyph = "\uE711"; // Cross
                StatusBadgeIcon.ClearValue(FontIcon.ForegroundProperty);
                TranslationDetailsTextBlock.Text = "BepInEx n'est pas installé dans le dossier du jeu. Cliquez sur 'Installer BepInEx' pour le télécharger automatiquement depuis GitHub.";
                InstallBepInExButton.Visibility = Visibility.Visible;
                InfoBarInstallBepInExButton.Visibility = Visibility.Visible;
            }
            else
            {
                InstallBepInExButton.Visibility = Visibility.Collapsed;
                InfoBarInstallBepInExButton.Visibility = Visibility.Collapsed;

                var isActive = _modToggleService.IsTranslationActive(gameDir);
                if (!isActive)
                {
                    StatusBadgeText.Text = "Désactivée (Vanilla)";
                    StatusBadgeIcon.Glyph = "\uE71A"; // Pause
                    StatusBadgeIcon.ClearValue(FontIcon.ForegroundProperty);
                    TranslationDetailsTextBlock.Text = "BepInEx est installé mais désactivé (winhttp.dll.disabled). Le jeu se lancera en version originale sans mod.";
                }
                else
                {
                    var transTag = localTrans != null ? $"Active (Trad {localTrans.Tag})" : "BepInEx Actif (Traduction prête)";
                    StatusBadgeText.Text = transTag;
                    StatusBadgeIcon.Glyph = "\uE73E"; // Checkmark
                    if (Application.Current.Resources.TryGetValue("P5CrimsonBrush", out var crimsonBrush) && crimsonBrush is Brush cb)
                    {
                        StatusBadgeIcon.Foreground = cb;
                    }
                    TranslationDetailsTextBlock.Text = localTrans != null
                        ? $"Mod de traduction FR ({localTrans.Tag}) installé. BepInEx sera injecté au démarrage du jeu."
                        : "BepInEx est installé et actif. Prêt à recevoir votre mod de traduction.";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Erreur dans RefreshUIState", ex);
        }
    }

    private void TranslationToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || _configService == null || _modToggleService == null)
            return;

        var isEnabled = TranslationToggleSwitch.IsOn;
        _configService.Config.TranslationEnabled = isEnabled;
        _configService.Save();

        var gameDir = _gameLocator.GetGameDirectory();

        if (_modToggleService.IsTranslationInstalled(gameDir))
        {
            try
            {
                _modToggleService.SetTranslationActive(gameDir, isEnabled);
            }
            catch (Exception ex)
            {
                MainInfoBar.Severity = InfoBarSeverity.Error;
                MainInfoBar.Title = "Erreur";
                MainInfoBar.Message = ex.Message;
                MainInfoBar.IsOpen = true;
            }
        }

        RefreshUIState();
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        var isTranslationEnabled = TranslationToggleSwitch.IsOn;

        try
        {
            _launcherService.LaunchGame(isTranslationEnabled);
        }
        catch (InvalidOperationException opEx)
        {
            MainInfoBar.Severity = InfoBarSeverity.Warning;
            MainInfoBar.Title = "Attention";
            MainInfoBar.Message = opEx.Message;
            MainInfoBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            MainInfoBar.Severity = InfoBarSeverity.Error;
            MainInfoBar.Title = "Impossible de lancer le jeu";
            MainInfoBar.Message = ex.Message;
            MainInfoBar.IsOpen = true;
        }
    }

    private async void InfoBarInstallBepInExButton_Click(object sender, RoutedEventArgs e)
    {
        await RunBepInExInstallationAsync();
    }

    private async void InstallBepInEx_Click(object sender, RoutedEventArgs e)
    {
        await RunBepInExInstallationAsync();
    }

    private async Task RunBepInExInstallationAsync()
    {
        var gameDir = _gameLocator.GetGameDirectory();
        if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir))
        {
            MainInfoBar.Severity = InfoBarSeverity.Error;
            MainInfoBar.Title = "Dossier introuvable";
            MainInfoBar.Message = "Veuillez d'abord configurer le dossier de jeu dans les paramètres.";
            MainInfoBar.IsOpen = true;
            return;
        }

        InstallBepInExButton.IsEnabled = false;
        InfoBarInstallBepInExButton.IsEnabled = false;
        DownloadProgressBar.Visibility = Visibility.Visible;
        DownloadProgressBar.IsIndeterminate = false;
        DownloadProgressBar.Value = 0;

        MainInfoBar.Severity = InfoBarSeverity.Informational;
        MainInfoBar.Title = "Installation de BepInEx en cours";
        MainInfoBar.Message = "Téléchargement de BepInEx depuis GitHub...";
        MainInfoBar.IsOpen = true;

        var progress = new Progress<DownloadProgressInfo>(p =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                DownloadProgressBar.Value = p.Percentage;
                MainInfoBar.Message = p.StatusMessage;
            });
        });

        try
        {
            var (success, message) = await _bepInExService.InstallBepInExAsync(gameDir, progress);
            if (success)
            {
                MainInfoBar.Severity = InfoBarSeverity.Success;
                MainInfoBar.Title = "Installation réussie";
                MainInfoBar.Message = message;
                MainInfoBar.IsOpen = true;
                InfoBarInstallBepInExButton.Visibility = Visibility.Collapsed;
                RefreshUIState();
            }
            else
            {
                MainInfoBar.Severity = InfoBarSeverity.Error;
                MainInfoBar.Title = "Échec de l'installation";
                MainInfoBar.Message = message;
                MainInfoBar.IsOpen = true;
            }
        }
        catch (Exception ex)
        {
            MainInfoBar.Severity = InfoBarSeverity.Error;
            MainInfoBar.Title = "Erreur inattendue";
            MainInfoBar.Message = ex.Message;
            MainInfoBar.IsOpen = true;
        }
        finally
        {
            DownloadProgressBar.Visibility = Visibility.Collapsed;
            InstallBepInExButton.IsEnabled = true;
            InfoBarInstallBepInExButton.IsEnabled = true;
        }
    }

    private async void CheckTranslationUpdate_Click(object sender, RoutedEventArgs e)
    {
        var gameDir = _gameLocator.GetGameDirectory();
        if (string.IsNullOrEmpty(gameDir))
        {
            MainInfoBar.Severity = InfoBarSeverity.Warning;
            MainInfoBar.Title = "Attention";
            MainInfoBar.Message = "Veuillez d'abord configurer le dossier du jeu.";
            MainInfoBar.IsOpen = true;
            return;
        }

        // 1. Ensure BepInEx is installed first
        if (!_bepInExService.IsBepInExInstalled(gameDir))
        {
            MainInfoBar.Severity = InfoBarSeverity.Informational;
            MainInfoBar.Title = "Installation préalable requise";
            MainInfoBar.Message = "Installation de BepInEx requise avant d'appliquer la traduction. Démarrage de l'installation...";
            MainInfoBar.IsOpen = true;
            await RunBepInExInstallationAsync();

            if (!_bepInExService.IsBepInExInstalled(gameDir))
            {
                return; // BepInEx install failed or was cancelled
            }
        }

        // 2. Check and update translation from TranslationRepo
        var repo = !string.IsNullOrWhiteSpace(_configService.Config.TranslationRepo) && !_configService.Config.TranslationRepo.Contains("[OWNER/REPO]")
            ? _configService.Config.TranslationRepo.Trim()
            : AppConstants.DefaultTranslationRepo;

        DownloadProgressBar.Visibility = Visibility.Visible;
        DownloadProgressBar.IsIndeterminate = true;
        MainInfoBar.Severity = InfoBarSeverity.Informational;
        MainInfoBar.Title = "Vérification";
        MainInfoBar.Message = "Recherche de mises à jour de la traduction...";
        MainInfoBar.IsOpen = true;

        try
        {
            var (isUpdateAvailable, release, statusMessage) = await _updateService.CheckTranslationUpdateAsync(gameDir);

            if (isUpdateAvailable && release != null)
            {
                MainInfoBar.Severity = InfoBarSeverity.Informational;
                MainInfoBar.Title = "Mise à jour disponible";
                MainInfoBar.Message = $"Téléchargement du mod de traduction ({release.TagName})...";

                DownloadProgressBar.IsIndeterminate = false;
                var progress = new Progress<DownloadProgressInfo>(p =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        DownloadProgressBar.Value = p.Percentage;
                        MainInfoBar.Message = p.StatusMessage;
                    });
                });

                await _updateService.UpdateTranslationAsync(gameDir, release, progress);

                MainInfoBar.Severity = InfoBarSeverity.Success;
                MainInfoBar.Title = "Traduction à jour";
                MainInfoBar.Message = $"Le mod de traduction ({release.TagName}) a été installé avec succès !";
                RefreshUIState();
            }
            else
            {
                MainInfoBar.Severity = InfoBarSeverity.Informational;
                MainInfoBar.Title = "Traduction";
                MainInfoBar.Message = statusMessage ?? "Votre traduction est à jour.";
            }
        }
        catch (Exception ex)
        {
            MainInfoBar.Severity = InfoBarSeverity.Error;
            MainInfoBar.Title = "Erreur de mise à jour";
            MainInfoBar.Message = ex.Message;
        }
        finally
        {
            DownloadProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private async Task CheckTranslationUpdateSilentlyAsync(string gameDir)
    {
        try
        {
            var (isUpdate, release, _) = await _updateService.CheckTranslationUpdateAsync(gameDir);
            if (isUpdate && release != null)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    MainInfoBar.Severity = InfoBarSeverity.Informational;
                    MainInfoBar.Title = "Mise à jour de traduction disponible";
                    MainInfoBar.Message = $"Une nouvelle version ({release.TagName}) de la traduction est disponible. Cliquez sur 'Mettre à jour la traduction'.";
                    MainInfoBar.IsOpen = true;
                });
            }
        }
        catch
        {
            // Silent background check
        }
    }

    private void OpenGameFolder_Click(object sender, RoutedEventArgs e)
    {
        var gameDir = _gameLocator.GetGameDirectory();

        if (!string.IsNullOrEmpty(gameDir) && Directory.Exists(gameDir))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{gameDir}\"",
                UseShellExecute = true
            });
        }
        else
        {
            MainInfoBar.Severity = InfoBarSeverity.Warning;
            MainInfoBar.Title = "Dossier introuvable";
            MainInfoBar.Message = "Le dossier du jeu n'existe pas ou n'a pas encore été configuré.";
            MainInfoBar.IsOpen = true;
        }
    }

    private async void ChangeGameFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
        picker.FileTypeFilter.Add("*");

        var hWnd = WindowNative.GetWindowHandle(App.MainWindowInstance);
        InitializeWithWindow.Initialize(picker, hWnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            var path = folder.Path;
            if (_gameLocator.IsValidGameDirectory(path, out var exeName))
            {
                _configService.Config.GamePath = path;
                _configService.Save();
                RefreshUIState();

                MainInfoBar.Severity = InfoBarSeverity.Success;
                MainInfoBar.Title = "Dossier validé";
                MainInfoBar.Message = $"Exécutable détecté : {exeName} dans {path}";
                MainInfoBar.IsOpen = true;
            }
            else
            {
                MainInfoBar.Severity = InfoBarSeverity.Error;
                MainInfoBar.Title = "Dossier invalide";
                MainInfoBar.Message = "Aucun exécutable de jeu Persona 5 The Phantom X (P5X.exe) valide n'a été trouvé dans ce dossier.";
                MainInfoBar.IsOpen = true;
            }
        }
    }

    private void AutoDetectGame_Click(object sender, RoutedEventArgs e)
    {
        _gameLocator.AutoDetectGameInstallation();
        var gameDir = _gameLocator.GetGameDirectory();

        if (!string.IsNullOrEmpty(gameDir))
        {
            RefreshUIState();
            MainInfoBar.Severity = InfoBarSeverity.Success;
            MainInfoBar.Title = "Détection réussie";
            MainInfoBar.Message = $"Installation trouvée : {gameDir}";
            MainInfoBar.IsOpen = true;
        }
        else
        {
            MainInfoBar.Severity = InfoBarSeverity.Warning;
            MainInfoBar.Title = "Non trouvé";
            MainInfoBar.Message = "Aucune installation Steam ou locale n'a été détectée automatiquement. Veuillez sélectionner le dossier manuellement avec 'Changer...'.";
            MainInfoBar.IsOpen = true;
        }
    }
}
