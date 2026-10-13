using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Scrinium.Desktop.Services;

public sealed class AvaloniaFilePicker : IFilePicker
{
    private static readonly FilePickerFileType Documents = new("Documents")
    {
        Patterns = ["*.txt", "*.md", "*.markdown", "*.pdf"]
    };

    public async Task<IReadOnlyList<string>> PickDocumentsAsync()
    {
        var window = (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        var top = window is null ? null : TopLevel.GetTopLevel(window);
        if (top is null)
            return [];

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add documents",
            AllowMultiple = true,
            FileTypeFilter = [Documents]
        });

        return files.Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
    }
}
