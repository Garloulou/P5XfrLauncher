using System;
using System.IO;
using System.Text.Json;
using LauncherP5XFr.Models;

namespace LauncherP5XFr.Services;

public interface IConfigService
{
    AppConfig Config { get; }
    void Save();
    event Action? ConfigChanged;
}

public sealed class ConfigService : IConfigService
{
    private static readonly Lazy<ConfigService> _instance = new(() => new ConfigService());
    public static ConfigService Instance => _instance.Value;

    private readonly string _configFilePath;
    private readonly object _lock = new();

    public AppConfig Config { get; private set; }

    public event Action? ConfigChanged;

    public ConfigService()
    {
        _configFilePath = Path.Combine(Helpers.AppEnvironment.AppDirectory, "config.json");
        Config = LoadOrCreate();
    }

    private AppConfig LoadOrCreate()
    {
        lock (_lock)
        {
            if (File.Exists(_configFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_configFilePath);
                    var loaded = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppConfig);
                    if (loaded != null)
                    {
                        LoggerService.Instance.Info("Configuration chargée avec succès depuis config.json");
                        return loaded;
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.Instance.Error("Erreur lors de la lecture de config.json, utilisation des valeurs par défaut.", ex);
                }
            }

            var defaultConfig = new AppConfig();
            try
            {
                var json = JsonSerializer.Serialize(defaultConfig, AppJsonContext.Default.AppConfig);
                File.WriteAllText(_configFilePath, json);
                LoggerService.Instance.Info("Nouveau fichier config.json généré avec les valeurs par défaut.");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error("Impossible d'écrire le fichier config.json initial.", ex);
            }

            return defaultConfig;
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            try
            {
                var json = JsonSerializer.Serialize(Config, AppJsonContext.Default.AppConfig);
                File.WriteAllText(_configFilePath, json);
                LoggerService.Instance.Info("Configuration enregistrée dans config.json.");
                ConfigChanged?.Invoke();
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error("Erreur lors de l'enregistrement de config.json.", ex);
            }
        }
    }
}
