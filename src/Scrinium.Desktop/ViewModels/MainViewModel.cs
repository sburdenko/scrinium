using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Scrinium.Desktop.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    public MainViewModel(
        SourcesViewModel sources,
        ChatViewModel chat,
        OllamaBannerViewModel banner,
        PanelHost panel)
    {
        Sources = sources;
        Chat = chat;
        Banner = banner;
        Panel = panel;

        sources.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SourcesViewModel.FailedCount))
                OnPropertyChanged(nameof(FailureBadge));
        };
    }

    public SourcesViewModel Sources { get; }

    public ChatViewModel Chat { get; }

    public OllamaBannerViewModel Banner { get; }

    public PanelHost Panel { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAsk), nameof(IsArchive))]
    private AppSection _section = AppSection.Ask;

    public bool IsAsk => Section == AppSection.Ask;

    public bool IsArchive => Section == AppSection.Archive;

    public string? FailureBadge => Sources.FailedCount > 0 ? $"{Sources.FailedCount} failed" : null;

    public async Task InitializeAsync()
    {
        await Sources.InitializeAsync();
        if (Sources.IsEmpty)
            Section = AppSection.Archive;

        Banner.Start();
    }

    [RelayCommand]
    private void ShowAsk() => Section = AppSection.Ask;

    [RelayCommand]
    private void ShowArchive() => Section = AppSection.Archive;

    [RelayCommand]
    private void NewChat()
    {
        Chat.NewChatCommand.Execute(null);
        Section = AppSection.Ask;
    }

    [RelayCommand]
    private void ToggleSettings()
    {
        Section = AppSection.Ask;
        Panel.ShowSettings();
    }
}
