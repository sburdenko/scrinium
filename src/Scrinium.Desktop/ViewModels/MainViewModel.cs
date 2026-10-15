using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scrinium.Core.Settings;

namespace Scrinium.Desktop.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly SettingsService _settings;

    public MainViewModel(
        SourcesViewModel sources,
        ChatViewModel chat,
        OllamaBannerViewModel banner,
        PanelHost panel,
        SettingsService settings)
    {
        Sources = sources;
        Chat = chat;
        Banner = banner;
        Panel = panel;

        _settings = settings;
        settings.Changed += _ => OnPropertyChanged(nameof(ModelLine));

        chat.CitationStateChanged += RefreshArchiveMarks;
        panel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PanelHost.Current))
                RefreshArchiveMarks();
        };
    }

    public SourcesViewModel Sources { get; }

    public ChatViewModel Chat { get; }

    public OllamaBannerViewModel Banner { get; }

    public PanelHost Panel { get; }

    public string ModelLine => $"Local · {_settings.Current.ChatModel}";

    public async Task InitializeAsync()
    {
        await Sources.InitializeAsync();
        Banner.Start();
    }

    [RelayCommand]
    private void NewQuestion() => Chat.NewQuestionCommand.Execute(null);

    [RelayCommand]
    private void ToggleSettings() => Panel.ShowSettings();

    private void RefreshArchiveMarks()
    {
        var open = (Panel.Current as PreviewPanelViewModel)?.DocumentId;
        Sources.Mark(Chat.FocusedExchange?.Answer.CitedCitations ?? [], open);
    }
}
