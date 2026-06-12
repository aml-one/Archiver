using System.Text.Json;
using Archiver.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace Archiver.Core.Services;

/// <summary>
/// Manages reading and writing the JSON configuration file in ProgramData.
/// Thread-safe for concurrent reads via ReaderWriterLockSlim.
/// Watches the file for external changes and raises ConfigurationChanged.
/// </summary>
public sealed class JsonConfigurationStore : IConfigurationStore, IDisposable
{
    private readonly ILogger<JsonConfigurationStore> _logger;
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _configDir;
    private FileSystemWatcher? _watcher;
    private DateTime _lastWriteTime = DateTime.MinValue; // debounce

    public string ConfigFilePath { get; }

    public event EventHandler<ArchiverOptions>? ConfigurationChanged;

    public JsonConfigurationStore(ILogger<JsonConfigurationStore> logger)
        : this(Constants.DefaultConfigFilePath, logger)
    {
    }

    public JsonConfigurationStore(string configFilePath, ILogger<JsonConfigurationStore> logger)
    {
        _logger = logger;
        ConfigFilePath = configFilePath;
        _configDir = Path.GetDirectoryName(configFilePath) ?? Constants.DefaultConfigDirectory;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        Directory.CreateDirectory(_configDir);

        if (!File.Exists(ConfigFilePath))
        {
            _logger.LogInformation("Config file not found at {Path}, creating default", ConfigFilePath);
            SaveConfig(CreateDefaultConfig());
        }

        StartWatching();
    }

    public ArchiverOptions LoadConfig()
    {
        _lock.EnterReadLock();
        try
        {
            var json = File.ReadAllText(ConfigFilePath);
            var config = JsonSerializer.Deserialize<ArchiverOptions>(json, _jsonOptions);
            return config ?? CreateDefaultConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load config from {Path}", ConfigFilePath);
            return CreateDefaultConfig();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void SaveConfig(ArchiverOptions config)
    {
        _lock.EnterWriteLock();
        try
        {
            // Stop watcher temporarily to avoid self-triggering
            if (_watcher != null)
                _watcher.EnableRaisingEvents = false;

            var json = JsonSerializer.Serialize(config, _jsonOptions);

            // Atomic write: write to temp file, then move
            var tempFile = ConfigFilePath + ".tmp";
            File.WriteAllText(tempFile, json);
            File.Move(tempFile, ConfigFilePath, overwrite: true);

            _lastWriteTime = File.GetLastWriteTimeUtc(ConfigFilePath);
            _logger.LogInformation("Config saved to {Path}", ConfigFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save config to {Path}", ConfigFilePath);
            throw;
        }
        finally
        {
            _lock.ExitWriteLock();
            if (_watcher != null)
                _watcher.EnableRaisingEvents = true;
        }
    }

    private void StartWatching()
    {
        if (_watcher != null)
            return; // already watching

        _watcher = new FileSystemWatcher(_configDir, Constants.DefaultConfigFileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnConfigFileChanged;
    }

    private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce — FileSystemWatcher often fires twice
        var lastWrite = File.GetLastWriteTimeUtc(ConfigFilePath);
        if (lastWrite <= _lastWriteTime.AddSeconds(1))
            return;

        _lastWriteTime = lastWrite;
        _logger.LogInformation("Config file changed externally, reloading");

        try
        {
            var config = LoadConfig();
            ConfigurationChanged?.Invoke(this, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling config file change");
        }
    }

    private static ArchiverOptions CreateDefaultConfig()
    {
        return new ArchiverOptions
        {
            Service = new ServiceOptions(),
            Credentials = new List<CredentialOptions>(),
            Jobs = new List<CopyJobOptions>
            {
                new CopyJobOptions
                {
                    Name = "Example Backup",
                    Description = "Example job — edit or remove it",
                    Enabled = false,
                    SourcePath = @"C:\SourceFolder",
                    IncludeSubdirectories = true,
                    FileFilter = "*.bak;*.zip",
                    DestinationNetworkPath = @"\\NAS\Backups",
                    Schedule = new ScheduleOptions
                    {
                        Type = ScheduleType.Daily,
                        Times = new List<string> { "02:00", "14:00" }
                    },
                    Cleanup = new CleanupOptions
                    {
                        Enabled = true,
                        RetentionDays = 60,
                        MinFreeDiskPercent = 5
                    }
                }
            }
        };
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _lock.Dispose();
    }
}
