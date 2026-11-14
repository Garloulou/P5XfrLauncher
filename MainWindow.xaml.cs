using System;
using System.IO;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;
using LauncherP5XFr.Services;
using LauncherP5XFr.Views;
using WinRT.Interop;

namespace LauncherP5XFr;

public sealed partial class MainWindow : Window
{
    private readonly IConfigService _configService;
    private readonly ILocalizationService _locService;
    private AppWindow? _appWindow;

    public MainWindow()
    {
        InitializeComponent();

        _configService = ConfigService.Instance;
        _locService = LocalizationService.Instance;

        // Custom TitleBar integration
        try
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warn($"SetTitleBar non appliqué : {ex.Message}");
        }

        ConfigureWindow();
        ApplyTheme(_configService.Config.Theme);

        // Immediate navigation to HomePage on creation
        try
        {
            ContentFrame.Navigate(typeof(HomePage));
            NavView.SelectedItem = NavHome;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error("Erreur lors de la navigation initiale vers HomePage", ex);
        }

        _configService.ConfigChanged += OnConfigChanged;
        _locService.LanguageChanged += OnLanguageChanged;
    }

    private void ConfigureWindow()
    {
        var hWnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow != null)
        {
            _appWindow.Title = "Persona 5: The Phantom X - Launcher FR";
            _appWindow.Resize(new SizeInt32(1040, 680));

            try
            {
                var iconPath = Path.Combine(Helpers.AppEnvironment.AppDirectory, "Assets", "AppIcon.ico");
                if (!File.Exists(iconPath))
                {
                    iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
                }

                if (File.Exists(iconPath))
                {
                    _appWindow.SetIcon(iconPath);
                }
            }
            catch { }
        }

        try
        {
            SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
            {
                Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt
            };
        }
        catch
        {
            try
            {
                SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
            }
            catch
            {
                // Fallback to default
            }
        }
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateLocalizedText();
        NavView.SelectedItem = NavHome;
        NavigateTo("Home");
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem selectedItem && selectedItem.Tag is string tag)
        {
            NavigateTo(tag);
        }
    }

    public void NavigateTo(string tag)
    {
        try
        {
            Type? pageType = tag switch
            {
                "Home" => typeof(HomePage),
                "Settings" => typeof(SettingsPage),
                "About" => typeof(AboutPage),
                _ => typeof(HomePage)
            };

            if (ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType, null, new SlideNavigationTransitionInfo());
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Erreur lors de la navigation vers '{tag}'", ex);
        }
    }

    public void ApplyTheme(string? themeName)
    {
        RootGrid.RequestedTheme = themeName switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    private void UpdateLocalizedText()
    {
        NavHome.Content = _locService.GetString("NavHome");
        NavSettings.Content = _locService.GetString("NavSettings");
        NavAbout.Content = _locService.GetString("NavAbout");
    }

    private void OnConfigChanged()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ApplyTheme(_configService.Config.Theme);
        });
    }

    private void OnLanguageChanged()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateLocalizedText();
        });
    }
}
