using CommunityToolkit.Mvvm.ComponentModel;
using Scrinium.Core.Ingestion;
using Scrinium.Core.Preview;
using Scrinium.Core.Settings;
using Scrinium.Core.Storage;
using Scrinium.Ollama;

namespace Scrinium.Desktop.ViewModels;

public sealed partial class PanelHost(
    DocumentPreviewService previews,
    SettingsService settings,
    DocumentLibrary library,
    OllamaMonitor monitor,
    DataPaths paths) : ObservableObject
{
    private PreviewPanelViewModel? _lastPreview;
    private RightPanelViewModel? _underSettings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen), nameof(IsCollapsed))]
    private RightPanelViewModel? _current;

    public bool IsOpen => Current is not null;

    public bool IsCollapsed => Current is null;

    public bool CanReopen => _lastPreview is not null;

    public async Task ShowPreviewAsync(PreviewRequest request)
    {
        DocumentPreview? preview = null;
        try
        {
            preview = await previews.LoadAsync(request.Location);
        }
        catch (Exception ex) when (ex is IOException or Core.Extraction.TextExtractionException)
        {
            preview = null;
        }

        var panel = new PreviewPanelViewModel(
            preview,
            request,
            citation => ShowPreviewAsync(PreviewRequest.For(citation, request.Citations)));

        panel.CloseRequested += Close;
        _underSettings = null;
        _lastPreview = panel;
        OnPropertyChanged(nameof(CanReopen));
        Current = panel;
    }

    public void ShowSettings()
    {
        if (Current is SettingsPanelViewModel)
        {
            Close();
            return;
        }

        var panel = new SettingsPanelViewModel(settings, library, monitor, paths.Root);
        panel.CloseRequested += Close;
        _underSettings = Current;
        Current = panel;
        _ = panel.RefreshModelsAsync();
    }

    public void Close()
    {
        if (Current is SettingsPanelViewModel)
        {
            Current = _underSettings;
            _underSettings = null;
            return;
        }

        Current = null;
    }

    public void Expand()
    {
        if (_lastPreview is not null)
            Current = _lastPreview;
    }
}
