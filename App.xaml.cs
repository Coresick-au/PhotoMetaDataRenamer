using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PhotoRenamer.Services;
using PhotoRenamer.Services.Interfaces;
using PhotoRenamer.ViewModels;

namespace PhotoRenamer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            var mainWindow = new MainWindow
            {
                DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
            };
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Startup Error: {ex.Message}\n\nInner: {ex.InnerException?.Message}\n\nStack: {ex.StackTrace}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Register services
        services.AddSingleton<IMetadataReader, MetadataReader>();
        services.AddSingleton<IGeocodingService, GeocodingService>();
        services.AddSingleton<IFileManager, FileManager>();
        services.AddSingleton<ISuggestionEngine, SuggestionEngine>();
        services.AddSingleton<ThumbnailService>();
        services.AddSingleton<SettingsService>();

        // Register ViewModels
        services.AddTransient<MainViewModel>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
