using System;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace LauncherP5XFr.Services;

public interface INotificationService
{
    void ShowToast(string title, string message);
}

public sealed class NotificationService : INotificationService
{
    private static readonly Lazy<NotificationService> _instance = new(() => new NotificationService());
    public static NotificationService Instance => _instance.Value;

    private readonly ILoggerService _logger;
    private bool _isRegistered = false;

    public NotificationService() : this(LoggerService.Instance)
    {
    }

    public NotificationService(ILoggerService logger)
    {
        _logger = logger;
        Initialize();
    }

    private void Initialize()
    {
        try
        {
            if (AppNotificationManager.IsSupported())
            {
                AppNotificationManager.Default.Register();
                _isRegistered = true;
                _logger.Info("AppNotificationManager enregistré avec succès.");
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"AppNotificationManager non disponible ou échec d'enregistrement : {ex.Message}");
        }
    }

    public void ShowToast(string title, string message)
    {
        try
        {
            if (!_isRegistered)
            {
                _logger.Info($"[Toast Notification Fallback] {title} : {message}");
                return;
            }

            var notification = new AppNotificationBuilder()
                .AddText(title)
                .AddText(message)
                .BuildNotification();

            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Impossible d'afficher la notification toast : {ex.Message}");
        }
    }
}
