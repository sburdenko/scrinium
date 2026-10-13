using CommunityToolkit.Mvvm.Input;

namespace Scrinium.Desktop.ViewModels;

public sealed partial class MainViewModel(
    SourcesViewModel sources,
    ChatViewModel chat,
    OllamaBannerViewModel banner,
    PanelHost panel)
{
    public SourcesViewModel Sources { get; } = sources;

    public ChatViewModel Chat { get; } = chat;

    public OllamaBannerViewModel Banner { get; } = banner;

    public PanelHost Panel { get; } = panel;

    public async Task InitializeAsync()
    {
        await Sources.InitializeAsync();
        Banner.Start();
    }

    [RelayCommand]
    private void ToggleSettings() => Panel.ShowSettings();
}
