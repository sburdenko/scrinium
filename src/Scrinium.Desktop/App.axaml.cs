using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scrinium.Core;
using Scrinium.Core.Ingestion;
using Scrinium.Core.Settings;
using Scrinium.Ollama;
using Scrinium.Desktop.Platform;
using Scrinium.Desktop.Services;
using Scrinium.Desktop.ViewModels;
using Scrinium.Desktop.Views;
using Scrinium.Extraction;
using Scrinium.Storage;

namespace Scrinium.Desktop;

public sealed class App : Application
{
    private IHost? _host;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Name = "Scrinium";
            _host = BuildHost();
            _host.Start();
            SetDockIcon(_host.Services.GetRequiredService<ILogger<App>>());

            var main = _host.Services.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = main };
            desktop.ShutdownRequested += (_, _) =>
            {
                main.Active?.Scope.StopWorker();
                _host.StopAsync().GetAwaiter().GetResult();
            };
            _ = main.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void SetDockIcon(ILogger logger)
    {
        if (!OperatingSystem.IsMacOS() || MacDockIcon.RunsInsideBundle)
            return;

        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://Scrinium.Desktop/Assets/Icon/scrinium.png"));
            using var png = new MemoryStream();
            stream.CopyTo(png);
            MacDockIcon.Apply(png.ToArray());
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException or IOException)
        {
            logger.LogWarning(ex, "Could not set the Dock icon");
        }
    }

    private static IHost BuildHost()
    {
        var dataDirectory = Environment.GetEnvironmentVariable("SCRINIUM_DATA")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Scrinium", "data");
        var settingsPath = Path.Combine(Path.GetDirectoryName(dataDirectory)!, "settings.json");

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddScriniumCore(dataDirectory, settingsPath);
        builder.Services.AddScriniumSqliteStorage();
        builder.Services.AddScriniumTextExtractors();

        builder.Services.AddScriniumOllama();

        builder.Services.AddSingleton<IFilePicker, AvaloniaFilePicker>();
        builder.Services.AddSingleton<WorkspaceViewModelFactory>();
        builder.Services.AddSingleton<OllamaBannerViewModel>();
        builder.Services.AddSingleton<MainViewModel>();

        return builder.Build();
    }
}
