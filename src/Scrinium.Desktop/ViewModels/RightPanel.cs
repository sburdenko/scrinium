using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scrinium.Core.Documents;
using Scrinium.Core.Ingestion;
using Scrinium.Core.Preview;
using Scrinium.Core.Settings;
using Scrinium.Desktop.Services;

namespace Scrinium.Desktop.ViewModels;

public abstract partial class RightPanelViewModel : ObservableObject
{
    public event Action? CloseRequested;

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();
}

public sealed partial class PreviewPanelViewModel(DocumentPreview? preview, string fallbackTitle) : RightPanelViewModel
{
    public string Title { get; } = preview?.Title ?? fallbackTitle;

    public string PageLabel { get; } = preview?.Page is { } page ? $"Page {page}" : "";

    public string Before { get; } = preview?.Before ?? "";

    public string Highlight { get; } = preview?.Highlight ?? "";

    public string After { get; } = preview?.After ?? "";

    public bool HasMoreBefore { get; } = preview?.HasMoreBefore ?? false;

    public bool HasMoreAfter { get; } = preview?.HasMoreAfter ?? false;

    public bool IsAvailable { get; } = preview is not null;

    private string? OriginalPath { get; } = preview?.OriginalPath;

    [RelayCommand]
    private void OpenOriginal()
    {
        if (OriginalPath is not null)
            ExternalOpener.Open(OriginalPath);
    }
}

public sealed partial class SettingsPanelViewModel : RightPanelViewModel
{
    private readonly SettingsService _settings;
    private readonly DocumentLibrary _library;

    public SettingsPanelViewModel(SettingsService settings, DocumentLibrary library, string dataDirectory)
    {
        _settings = settings;
        _library = library;
        DataDirectory = dataDirectory;
        ChatModel = settings.Current.ChatModel;
        EmbeddingModel = settings.Current.EmbeddingModel;
        OllamaUrl = settings.Current.OllamaUrl;
    }

    public string DataDirectory { get; }

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
        ChatModel.Trim() != _settings.Current.ChatModel
        || EmbeddingModel.Trim() != _settings.Current.EmbeddingModel
        || OllamaUrl.Trim() != _settings.Current.OllamaUrl;

    public bool EmbeddingModelChanged => EmbeddingModel.Trim() != _settings.Current.EmbeddingModel;

    [RelayCommand]
    private void Save()
    {
        var urlChanged = OllamaUrl.Trim() != _settings.Current.OllamaUrl;
        _settings.Update(s => s with
        {
            ChatModel = ChatModel.Trim(),
            EmbeddingModel = EmbeddingModel.Trim(),
            OllamaUrl = OllamaUrl.Trim()
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
