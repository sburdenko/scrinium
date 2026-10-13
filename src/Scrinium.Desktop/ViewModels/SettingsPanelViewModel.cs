using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scrinium.Core.Documents;
using System.Collections.ObjectModel;
using Scrinium.Core.Ingestion;
using Scrinium.Core.Models;
using Scrinium.Core.Preview;
using Scrinium.Core.Settings;
using Scrinium.Ollama;
using Scrinium.Desktop.Services;

namespace Scrinium.Desktop.ViewModels;

public sealed partial class SettingsPanelViewModel : RightPanelViewModel
{
    private readonly SettingsService _settings;
    private readonly DocumentLibrary _library;
    private readonly OllamaMonitor _monitor;

    public SettingsPanelViewModel(
        SettingsService settings,
        DocumentLibrary library,
        OllamaMonitor monitor,
        string dataDirectory)
    {
        _settings = settings;
        _library = library;
        _monitor = monitor;
        DataDirectory = dataDirectory;
        ChatModel = settings.Current.ChatModel;
        EmbeddingModel = settings.Current.EmbeddingModel;
        OllamaUrl = settings.Current.OllamaUrl;
        ApplyChoices(new ModelCatalog.Unavailable());
    }

    public string DataDirectory { get; }

    public ObservableCollection<string> ChatModels { get; } = [];

    public ObservableCollection<string> EmbeddingModels { get; } = [];

    [ObservableProperty]
    private string _modelsNote = "Checking installed models…";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _chatModel = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _embeddingModel = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string _ollamaUrl = "";

    [ObservableProperty]
    private string _savedNote = "";

    public bool HasChanges =>
        Differs(ChatModel, _settings.Current.ChatModel)
        || Differs(EmbeddingModel, _settings.Current.EmbeddingModel)
        || Differs(OllamaUrl, _settings.Current.OllamaUrl);

    private static string Keep(string? edited, string saved) =>
        string.IsNullOrWhiteSpace(edited) ? saved : edited.Trim();

    private static bool Differs(string? edited, string saved) => (edited ?? "").Trim() != saved;

    [RelayCommand]
    public async Task RefreshModelsAsync()
    {
        ModelsNote = "Checking installed models…";
        ApplyChoices(await _monitor.ListModelsAsync());
    }

    private void ApplyChoices(ModelCatalog catalog)
    {
        var choices = ModelChoices.From(catalog, _settings.Current.ChatModel, _settings.Current.EmbeddingModel);
        Replace(ChatModels, choices.Chat);
        Replace(EmbeddingModels, choices.Embedding);

        ChatModel = _settings.Current.ChatModel;
        EmbeddingModel = _settings.Current.EmbeddingModel;

        ModelsNote = catalog switch
        {
            ModelCatalog.Available a when a.Models.Count == 0 => "Ollama has no models installed yet.",
            ModelCatalog.Available a => $"{a.Models.Count} models installed in Ollama.",
            _ => "Ollama is not reachable, so only the configured models are shown."
        };
    }

    private static void Replace(ObservableCollection<string> target, IReadOnlyList<string> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(item);
    }

    [RelayCommand]
    private void Save()
    {
        var urlChanged = Differs(OllamaUrl, _settings.Current.OllamaUrl);
        _settings.Update(s => s with
        {
            ChatModel = Keep(ChatModel, s.ChatModel),
            EmbeddingModel = Keep(EmbeddingModel, s.EmbeddingModel),
            OllamaUrl = Keep(OllamaUrl, s.OllamaUrl)
        });

        SavedNote = urlChanged
            ? "Saved. The new Ollama address applies after a restart."
            : "Saved.";
        OnPropertyChanged(nameof(HasChanges));
    }

    [RelayCommand]
    private async Task ReindexAllAsync()
    {
        await _library.ReindexAllAsync();
        SavedNote = "Reindexing started. Progress is shown in the sources list.";
    }
}
