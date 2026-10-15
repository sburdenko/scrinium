using CommunityToolkit.Mvvm.ComponentModel;
using Scrinium.Core.Ingestion;
using Scrinium.Core.Preview;
using Scrinium.Core.Settings;
using Scrinium.Core.Storage;
using Scrinium.Ollama;

namespace Scrinium.Desktop.ViewModels;

public sealed partial class PanelHost : ObservableObject
{
    private readonly DocumentPreviewService _previews;
    private readonly SettingsService _settings;
    private readonly DocumentLibrary _library;
    private readonly OllamaMonitor _monitor;
    private readonly DataPaths _paths;
    private PreviewPanelViewModel _original;

    [ObservableProperty]
    private RightPanelViewModel _current;

    public PanelHost(
        DocumentPreviewService previews,
        SettingsService settings,
        DocumentLibrary library,
        OllamaMonitor monitor,
        DataPaths paths)
    {
        _previews = previews;
        _settings = settings;
        _library = library;
        _monitor = monitor;
        _paths = paths;
        _original = PreviewPanelViewModel.Empty();
        _current = _original;
    }

    public async Task ShowPreviewAsync(PreviewRequest request)
    {
        DocumentPreview? preview = null;
        try
        {
            preview = await _previews.LoadAsync(request.Location);
        }
        catch (Exception ex) when (ex is IOException or Core.Extraction.TextExtractionException)
        {
            preview = null;
        }

        _original = new PreviewPanelViewModel(
            preview,
            request,
            citation => ShowPreviewAsync(PreviewRequest.For(citation, request.Citations)));
        Current = _original;
    }

    public void ShowSettings()
    {
        if (Current is SettingsPanelViewModel)
        {
            Close();
            return;
        }

        var panel = new SettingsPanelViewModel(_settings, _library, _monitor, _paths.Root);
        panel.CloseRequested += Close;
        Current = panel;
        _ = panel.RefreshModelsAsync();
    }

    public void Close()
    {
        if (Current is SettingsPanelViewModel)
            Current = _original;
    }

    public void ResetOriginal()
    {
        _original = PreviewPanelViewModel.Empty();
        if (Current is not SettingsPanelViewModel)
            Current = _original;
    }

    public void ResetOriginalIfShowing(Guid documentId)
    {
        if (_original.DocumentId == documentId)
            ResetOriginal();
    }
}
