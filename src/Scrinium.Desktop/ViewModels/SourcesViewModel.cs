using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scrinium.Core.Documents;
using Scrinium.Core.Ingestion;
using Scrinium.Core.Settings;
using Scrinium.Desktop.Services;

namespace Scrinium.Desktop.ViewModels;

public sealed partial class SourcesViewModel : ObservableObject
{
    private readonly DocumentLibrary _library;
    private readonly ISettingsProvider _settings;
    private readonly IFilePicker _picker;
    private readonly PanelHost _panel;
    private IReadOnlyList<Document> _documents = [];
    private IReadOnlyList<CitationViewModel> _markedCitations = [];
    private Guid? _openDocumentId;
    private int _reloadScheduled;

    public SourcesViewModel(DocumentLibrary library, ISettingsProvider settings, IFilePicker picker, PanelHost panel)
    {
        _library = library;
        _settings = settings;
        _picker = picker;
        _panel = panel;
        _library.DocumentChanged += _ => ScheduleReload();
    }

    public ObservableCollection<SourceItemViewModel> Items { get; } = [];

    [ObservableProperty]
    private string _filter = "";

    [ObservableProperty]
    private string _summary = "";

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private string _lastError = "";

    partial void OnFilterChanged(string value) => Rebuild();

    public Task InitializeAsync() => ReloadAsync();

    public async Task ImportPathsAsync(IEnumerable<string> paths)
    {
        var errors = new List<string>();
        foreach (var path in paths)
        {
            if (!DocumentKinds.IsSupported(path))
            {
                errors.Add($"{Path.GetFileName(path)}: unsupported format (txt, md and pdf only for now)");
                continue;
            }

            try
            {
                await _library.ImportAsync(path);
            }
            catch (IOException ex)
            {
                errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }

        LastError = string.Join("\n", errors);
    }

    [RelayCommand]
    private async Task AddFilesAsync() => await ImportPathsAsync(await _picker.PickDocumentsAsync());

    [RelayCommand]
    private async Task DeleteAsync(SourceItemViewModel item)
    {
        await _library.DeleteAsync(item.Id);
        _panel.ResetOriginalIfShowing(item.Id);
    }

    [RelayCommand]
    private Task RetryAsync(SourceItemViewModel item) => _library.RetryAsync(item.Id);

    [RelayCommand]
    private Task OpenAsync(SourceItemViewModel item) =>
        _panel.ShowPreviewAsync(PreviewRequest.ForDocument(item.Id, item.Title));

    [RelayCommand]
    private void DismissError() => LastError = "";

    public void Mark(IReadOnlyList<CitationViewModel> cited, Guid? openDocumentId)
    {
        _markedCitations = cited;
        _openDocumentId = openDocumentId;
        foreach (var item in Items)
            ApplyMarks(item);
    }

    private void ApplyMarks(SourceItemViewModel item)
    {
        item.CitationMarks = string.Join(" · ", _markedCitations
            .Where(c => c.Location.DocumentId == item.Id)
            .Select(c => c.Number)
            .Distinct()
            .Order());
        item.IsOpenInOriginal = _openDocumentId == item.Id;
    }

    private void ScheduleReload()
    {
        if (Interlocked.Exchange(ref _reloadScheduled, 1) == 1)
            return;

        Dispatcher.UIThread.Post(async () =>
        {
            Interlocked.Exchange(ref _reloadScheduled, 0);
            await ReloadAsync();
        });
    }

    private async Task ReloadAsync()
    {
        _documents = await _library.ListAsync();
        Rebuild();
    }

    private void Rebuild()
    {
        var model = _settings.Current.EmbeddingModel;
        var visible = _documents
            .Select((d, index) => new SourceItemViewModel(d, model, index))
            .Where(i => string.IsNullOrWhiteSpace(Filter) || i.Title.Contains(Filter.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        Items.Clear();
        foreach (var item in visible)
        {
            ApplyMarks(item);
            Items.Add(item);
        }

        IsEmpty = _documents.Count == 0;
        Summary = $"{_documents.Count} · {model}";
    }
}
