using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scrinium.Core.Documents;
using System.Collections.ObjectModel;
using Scrinium.Core.Ingestion;
using Scrinium.Core.Models;
using Scrinium.Core.Preview;
using Scrinium.Core.Settings;
using Scrinium.Desktop.Ollama;
using Scrinium.Desktop.Services;

namespace Scrinium.Desktop.ViewModels;

public abstract partial class RightPanelViewModel : ObservableObject
{
    public event Action? CloseRequested;

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();
}
