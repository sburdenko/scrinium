using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Scrinium.Desktop.ViewModels;

namespace Scrinium.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DragDrop.SetAllowDrop(Frame, true);
        Frame.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        Frame.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private static void OnDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel main)
            return;

        var paths = e.DataTransfer.TryGetFiles()?
            .Select(f => f.TryGetLocalPath())
            .OfType<string>()
            .ToList();

        if (paths is not { Count: > 0 })
            return;

        await main.Sources.ImportPathsAsync(paths);
    }
}
