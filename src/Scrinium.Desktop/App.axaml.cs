using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scrinium.Core;
using Scrinium.Core.Ingestion;
using Scrinium.Core.Settings;
using Scrinium.Ollama;
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
            _host = BuildHost();
            _host.Start();

            var main = _host.Services.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = main };
            desktop.ShutdownRequested += (_, _) => _host.StopAsync().GetAwaiter().GetResult();
            _ = main.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
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
        builder.Services.AddSingleton<PanelHost>();
        builder.Services.AddSingleton<SourcesViewModel>();
        builder.Services.AddSingleton<ChatViewModel>();
        builder.Services.AddSingleton<OllamaBannerViewModel>();
        builder.Services.AddSingleton<MainViewModel>();

        return builder.Build();
    }
}
