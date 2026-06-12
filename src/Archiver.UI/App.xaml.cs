using System.Windows;
using Archiver.Core;
using Archiver.Core.Services;
using Archiver.UI.Services;
using Archiver.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Archiver.UI;

public partial class App : Application
{
    private readonly ServiceProvider _serviceProvider;

    public App()
    {
        _serviceProvider = ConfigureServices();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        var mainWindow = new Views.MainWindow { DataContext = mainViewModel };
        mainWindow.Show();

        // Initialize the first page
        _ = mainViewModel.ShowServiceStatus();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider.Dispose();
        base.OnExit(e);
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Core services
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<ICredentialProtector, CredentialProtector>();

        // JsonConfigurationStore needs a logger — use NullLogger
        var configStore = new JsonConfigurationStore(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<JsonConfigurationStore>.Instance);
        services.AddSingleton<JsonConfigurationStore>(configStore);
        services.AddSingleton<IConfigurationStore>(configStore);

        // NetworkShareAuthenticator also needs a logger — construct manually
        var networkAuth = new NetworkShareAuthenticator(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<NetworkShareAuthenticator>.Instance);
        services.AddSingleton<INetworkShareAuthenticator>(networkAuth);

        // UI services
        services.AddSingleton<IServiceController, WindowsServiceController>();
        services.AddSingleton<ILogReader>(sp => new FileLogReader(Constants.DefaultLogDirectory));

        // ViewModels
        services.AddTransient<JobEditViewModel>();
        services.AddSingleton<ServiceStatusViewModel>();
        services.AddSingleton<JobsPageViewModel>();
        services.AddSingleton<LogsPageViewModel>();
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }
}
