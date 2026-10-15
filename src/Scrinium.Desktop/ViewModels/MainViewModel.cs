using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Scrinium.Core.Settings;
using Scrinium.Core.Workspaces;

namespace Scrinium.Desktop.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private const string DefaultWorkspaceName = "Personal";

    private readonly WorkspaceCatalog _catalog;
    private readonly WorkspaceViewModelFactory _workspaces;
    private readonly SettingsService _settings;
    private readonly ILogger<MainViewModel> _logger;
    private readonly SemaphoreSlim _switching = new(1, 1);

    public MainViewModel(
        WorkspaceCatalog catalog,
        WorkspaceViewModelFactory workspaces,
        OllamaBannerViewModel banner,
        SettingsService settings,
        ILogger<MainViewModel> logger)
    {
        _catalog = catalog;
        _workspaces = workspaces;
        _settings = settings;
        _logger = logger;
        Banner = banner;
        settings.Changed += _ => OnPropertyChanged(nameof(ModelLine));
    }

    public OllamaBannerViewModel Banner { get; }

    public ObservableCollection<Workspace> Workspaces { get; } = [];

    [ObservableProperty]
    private WorkspaceViewModel? _active;

    [ObservableProperty]
    private Workspace? _selectedWorkspace;

    partial void OnSelectedWorkspaceChanged(Workspace? value)
    {
        if (value is not null)
            _ = SwitchToAsync(value);
    }

    [ObservableProperty]
    private bool _isNamingWorkspace;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateWorkspaceCommand))]
    private string _newWorkspaceName = "";

    [ObservableProperty]
    private string _workspaceError = "";

    public string ModelLine => $"Local · {_settings.Current.ChatModel}";

    public async Task InitializeAsync()
    {
        await OpenInitialWorkspaceAsync();
        Banner.Start();
    }

    public async Task OpenInitialWorkspaceAsync()
    {
        var fallback = _catalog.EnsureDefault(DefaultWorkspaceName);
        Reload();

        var remembered = WorkspaceId.TryParse(_settings.Current.Workspace, out var id) ? _catalog.Find(id) : null;
        await SwitchToAsync(remembered ?? fallback);
    }

    public async Task SwitchToAsync(Workspace target)
    {
        await _switching.WaitAsync();
        try
        {
            if (Active?.Workspace == target)
                return;

            Active?.Chat.StopCommand.Execute(null);
            var previous = Active;
            try
            {
                Active = await _workspaces.OpenAsync(target);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Data.Common.DbException)
            {
                _logger.LogError(ex, "Could not open workspace {Workspace}", target.Id);
                WorkspaceError = $"Could not open {target.Name}: {ex.Message}";
                SelectedWorkspace = previous?.Workspace;
                return;
            }

            SelectedWorkspace = target;
            WorkspaceError = "";
            _settings.Update(s => s with { Workspace = target.Id.Value });
            if (previous is not null)
                await previous.DisposeAsync();
        }
        finally
        {
            _switching.Release();
        }
    }

    [RelayCommand]
    private void StartNamingWorkspace()
    {
        NewWorkspaceName = "";
        WorkspaceError = "";
        IsNamingWorkspace = true;
    }

    [RelayCommand]
    private void CancelNamingWorkspace()
    {
        IsNamingWorkspace = false;
        WorkspaceError = "";
    }

    private bool CanCreateWorkspace() => !string.IsNullOrWhiteSpace(NewWorkspaceName);

    [RelayCommand(CanExecute = nameof(CanCreateWorkspace))]
    private async Task CreateWorkspaceAsync()
    {
        Workspace created;
        try
        {
            created = _catalog.Create(NewWorkspaceName);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            WorkspaceError = ex.Message;
            return;
        }

        Reload();
        IsNamingWorkspace = false;
        await SwitchToAsync(created);
    }

    [RelayCommand]
    private void NewQuestion() => Active?.Chat.NewQuestionCommand.Execute(null);

    [RelayCommand]
    private void ToggleSettings() => Active?.Panel.ShowSettings();

    private void Reload()
    {
        var selected = SelectedWorkspace;
        Workspaces.Clear();
        foreach (var workspace in _catalog.List())
            Workspaces.Add(workspace);

        if (selected is not null && !Workspaces.Contains(selected))
            SelectedWorkspace = null;
    }
}
