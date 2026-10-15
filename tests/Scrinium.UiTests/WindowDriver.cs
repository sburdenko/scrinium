using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Scrinium.Desktop.Views;
using Scrinium.Tests.Support;

namespace Scrinium.UiTests;

public sealed class WindowDriver
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private WindowDriver(Window window) => Window = window;

    public Window Window { get; }

    public static WindowDriver Open(DesktopHarness harness)
    {
        var window = new MainWindow { DataContext = harness.Main, Width = 1320, Height = 820 };
        window.Show();
        var driver = new WindowDriver(window);
        driver.Settle();
        return driver;
    }

    public void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    public async Task UntilAsync(Func<bool> condition, string what)
    {
        var started = DateTime.UtcNow;
        while (true)
        {
            Settle();
            if (condition())
                return;

            if (DateTime.UtcNow - started > Timeout)
                throw new TimeoutException($"Timed out waiting for {what}.");

            await Task.Delay(10);
        }
    }

    public IEnumerable<T> All<T>() where T : Visual =>
        Window.GetVisualDescendants().OfType<T>().Where(v => v.IsEffectivelyVisible);

    public T Find<T>(Func<T, bool> match, string what) where T : Visual =>
        All<T>().FirstOrDefault(match) ?? throw new InvalidOperationException($"No visible {what}.");

    public Button Button(string name) => Find<Button>(b => NameOf(b) == name, $"button '{name}'");

    public bool Shows(string text) => All<TextBlock>().Any(t => t.Text == text);

    public void Click(Visual target)
    {
        var point = Center(target);
        Window.MouseMove(point);
        Window.MouseDown(point, MouseButton.Left);
        Window.MouseUp(point, MouseButton.Left);
        Settle();
    }

    public void Hover(Visual target)
    {
        Window.MouseMove(Center(target));
        Settle();
    }

    public void HoverAway()
    {
        Window.MouseMove(new Point(1, 1));
        Settle();
    }

    public void Type(TextBox box, string text)
    {
        Click(box);
        Window.KeyTextInput(text);
        Settle();
    }

    public void Press(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPress(key, modifiers, PhysicalKey.None, null);
        Window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        Settle();
    }

    public async Task DropFilesAsync(Visual target, params string[] paths)
    {
        var data = new DataTransfer();
        foreach (var path in paths)
        {
            var file = await Window.StorageProvider.TryGetFileFromPathAsync(new Uri(path))
                ?? throw new InvalidOperationException($"The storage provider cannot open {path}.");
            data.Add(DataTransferItem.CreateFile(file));
        }

        var point = Center(target);
        Window.DragDrop(point, RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.None);
        Window.DragDrop(point, RawDragEventType.DragOver, data, DragDropEffects.Copy, RawInputModifiers.None);
        Window.DragDrop(point, RawDragEventType.Drop, data, DragDropEffects.Copy, RawInputModifiers.None);
        Settle();
    }

    public static string? NameOf(Control control) =>
        ControlAutomationPeer.CreatePeerForElement(control).GetName();

    private Point Center(Visual target) =>
        target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), Window)
        ?? throw new InvalidOperationException("The target is not in the window.");
}
