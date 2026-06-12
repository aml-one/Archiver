using Archiver.Core;
using Archiver.Core.Configuration;
using Archiver.Core.Logging;
using Archiver.Core.Services;
using Archiver.Service.Scheduling;

var builder = Host.CreateApplicationBuilder(args);

// ── Configure Windows Service ──────────────────────────────────────
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Archiver";
});

// ── Register Core Services ─────────────────────────────────────────
builder.Services.AddSingleton<ISystemClock, SystemClock>();
builder.Services.AddSingleton<ICredentialProtector, CredentialProtector>();
builder.Services.AddSingleton<JsonConfigurationStore>();
builder.Services.AddSingleton<IConfigurationStore>(sp =>
    sp.GetRequiredService<JsonConfigurationStore>());
builder.Services.AddSingleton<INetworkShareAuthenticator, NetworkShareAuthenticator>();
builder.Services.AddSingleton<IFileCopier, FileCopier>();

// ── Register Hosted Services ───────────────────────────────────────
builder.Services.AddSingleton<JobScheduler>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<JobScheduler>());

// ── Logging: File-based (rolling daily) + Windows EventLog ─────────
builder.Logging.ClearProviders();
builder.Logging.AddProvider(new FileWriterLoggerProvider(
    logDirectory: Constants.DefaultLogDirectory,
    retentionDays: Constants.DefaultLogRetentionDays));
builder.Logging.AddEventLog(settings =>
{
    settings.LogName = "Archiver";
    settings.SourceName = "Archiver";
});

var host = builder.Build();
host.Run();
