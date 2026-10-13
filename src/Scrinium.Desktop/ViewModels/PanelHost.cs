using CommunityToolkit.Mvvm.ComponentModel;
using Scrinium.Core.Documents;
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
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    private RightPanelViewModel? _current;

    public bool IsOpen => Current is not null;

    public async Task ShowPreviewAsync(SourceLocation location, string fallbackTitle)
    {
        DocumentPreview? preview = null;
        try
        {
            preview = await previews.LoadAsync(location);
        }
        catch (Exception ex) when (ex is IOException or Core.Extraction.TextExtractionException)
        {
            preview = null;
        }

        Open(new PreviewPanelViewModel(preview, fallbackTitle));
    }

    public void ShowSettings()
    {
        if (Current is SettingsPanelViewModel)
            Close();
        else
            OpenSettings();
    }

    private void OpenSettings()
    {
        var panel = new SettingsPanelViewModel(settings, library, monitor, paths.Root);
        Open(panel);
        _ = panel.RefreshModelsAsync();
    }

    public void Close() => Current = null;

    private void Open(RightPanelViewModel panel)
    {
        panel.CloseRequested += Close;
        Current = panel;
    }
}
