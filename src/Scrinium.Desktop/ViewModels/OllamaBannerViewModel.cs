using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scrinium.Desktop.Ollama;
using Scrinium.Ollama;

namespace Scrinium.Desktop.ViewModels;

public sealed partial class OllamaBannerViewModel : ObservableObject
{
    private readonly OllamaMonitor _monitor;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(4) };

    public OllamaBannerViewModel(OllamaMonitor monitor)
    {
        _monitor = monitor;
        _timer.Tick += async (_, _) => await RecheckAsync();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisible), nameof(Message), nameof(CanStart), nameof(CanPull))]
    private OllamaStatus _status = new OllamaStatus.Checking();

    public bool IsVisible => Status is not OllamaStatus.Ready and not OllamaStatus.Checking;

    public bool CanStart => Status is OllamaStatus.Offline;

    public bool CanPull => Status is OllamaStatus.MissingModels;

    public string Message => Status switch
    {
        OllamaStatus.Offline => "Ollama is not running. Indexing and answering are unavailable without it.",
        OllamaStatus.MissingModels missing => $"Missing models: {string.Join(", ", missing.Models)}",
        OllamaStatus.Pulling pulling => $"Downloading {pulling.Model}: {pulling.Progress}",
        _ => ""
    };

    public void Start()
    {
        _timer.Start();
        _ = RecheckAsync();
    }

    [RelayCommand]
    private async Task RecheckAsync()
    {
        if (Status is OllamaStatus.Pulling)
            return;

        Status = await _monitor.CheckAsync();
    }

    [RelayCommand]
    private async Task StartOllamaAsync()
    {
        OllamaLauncher.TryStart();
        await Task.Delay(TimeSpan.FromSeconds(2));
        await RecheckAsync();
    }

    [RelayCommand]
    private async Task PullMissingAsync()
    {
        if (Status is not OllamaStatus.MissingModels missing)
            return;

        try
        {
            foreach (var model in missing.Models)
            {
                await foreach (var progress in _monitor.PullAsync(model))
                    Status = new OllamaStatus.Pulling(model, progress);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            Status = new OllamaStatus.Offline();
            return;
        }

        Status = await _monitor.CheckAsync();
    }
}
