using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Scrinium.Desktop.ViewModels;

namespace Scrinium.Desktop.Views;

public partial class SourcesView : UserControl
{
    public SourcesView()
    {
        InitializeComponent();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private static void OnDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not SourcesViewModel sources)
            return;

        var paths = e.DataTransfer.TryGetFiles()?
            .Select(f => f.TryGetLocalPath())
            .OfType<string>()
            .ToList();

        if (paths is { Count: > 0 })
            await sources.ImportPathsAsync(paths);
    }
}
