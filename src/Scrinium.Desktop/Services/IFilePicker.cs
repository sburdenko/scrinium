using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Scrinium.Desktop.Services;

public interface IFilePicker
{
    Task<IReadOnlyList<string>> PickDocumentsAsync();
}
