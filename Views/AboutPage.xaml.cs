using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Controls;

namespace LauncherP5XFr.Views;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();

        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        LauncherVersionTextBlock.Text = ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "v1.0.0";

        var osDesc = RuntimeInformation.OSDescription;
        var arch = RuntimeInformation.ProcessArchitecture;
        var dotNetVer = Environment.Version;

        SysInfoTextBlock.Text = $"OS : {osDesc} ({arch})\nRuntime : .NET {dotNetVer}\nWindows App SDK : 2.6.5 (WinUI 3)\nEmplacement : {AppContext.BaseDirectory}";
    }
}
