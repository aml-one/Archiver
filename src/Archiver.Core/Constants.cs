namespace Archiver.Core;

public static class Constants
{
    public const string DefaultConfigDirectory = @"C:\ProgramData\Archiver";
    public const string DefaultConfigFileName = "config.json";
    public const string DefaultLogDirectory = @"C:\ProgramData\Archiver\logs";
    public const string DefaultStatusFileName = "status.json";
    public const string TriggerFileExtension = ".trigger";
    public const int DefaultLogRetentionDays = 30;
    public const int DefaultCopyRetryCount = 3;
    public const int DefaultCopyRetryDelaySeconds = 10;
    public const double DefaultCopyRetryBackoffMultiplier = 2.0;
    public const int DefaultFileLockTimeoutSeconds = 30;
    public const int DefaultSimultaneousJobCount = 1;

    public static string DefaultConfigFilePath => Path.Combine(DefaultConfigDirectory, DefaultConfigFileName);
    public static string DefaultStatusFilePath => Path.Combine(DefaultConfigDirectory, DefaultStatusFileName);
}
