using CommunityToolkit.Mvvm.ComponentModel;
using Scrinium.Core.Documents;
using Scrinium.Core.Ingestion;
using Scrinium.Core.Preview;
using Scrinium.Core.Settings;
using Scrinium.Core.Storage;

namespace Scrinium.Desktop.ViewModels;

public sealed partial class PanelHost(
    DocumentPreviewService previews,
    SettingsService settings,
    DocumentLibrary library,
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
            Open(new SettingsPanelViewModel(settings, library, paths.Root));
    }

    public void Close() => Current = null;

    private void Open(RightPanelViewModel panel)
    {
        panel.CloseRequested += Close;
        Current = panel;
    }
}
