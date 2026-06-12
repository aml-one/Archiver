using Archiver.Core.Configuration;

namespace Archiver.Core.Services;

/// <summary>
/// Reads and writes the Archiver configuration.
/// </summary>
public interface IConfigurationStore
{
    ArchiverOptions LoadConfig();
    void SaveConfig(ArchiverOptions config);
    string ConfigFilePath { get; }

    event EventHandler<ArchiverOptions>? ConfigurationChanged;
}
