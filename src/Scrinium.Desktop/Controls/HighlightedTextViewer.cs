using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;

namespace Scrinium.Desktop.Controls;

public sealed class HighlightedTextViewer : ScrollViewer
{

    public static readonly StyledProperty<string?> BeforeProperty =
        AvaloniaProperty.Register<HighlightedTextViewer, string?>(nameof(Before));

    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<HighlightedTextViewer, string?>(nameof(Highlight));

    public static readonly StyledProperty<string?> AfterProperty =
        AvaloniaProperty.Register<HighlightedTextViewer, string?>(nameof(After));

    private readonly SelectableTextBlock _text = new() { TextWrapping = TextWrapping.Wrap, LineHeight = 26, FontSize = 15, Margin = new Thickness(24) };

    static HighlightedTextViewer()
    {
        BeforeProperty.Changed.AddClassHandler<HighlightedTextViewer>((v, _) => v.Rebuild());
        HighlightProperty.Changed.AddClassHandler<HighlightedTextViewer>((v, _) => v.Rebuild());
        AfterProperty.Changed.AddClassHandler<HighlightedTextViewer>((v, _) => v.Rebuild());
    }

    public HighlightedTextViewer()
    {
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
        Content = _text;
    }

    public string? Before
    {
        get => GetValue(BeforeProperty);
        set => SetValue(BeforeProperty, value);
    }

    public string? Highlight
    {
        get => GetValue(HighlightProperty);
        set => SetValue(HighlightProperty, value);
    }

    public string? After
    {
        get => GetValue(AfterProperty);
        set => SetValue(AfterProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    private void Rebuild()
    {
        var before = Before ?? "";
        var highlight = Highlight ?? "";

        _text.Inlines ??= new InlineCollection();
        _text.Inlines.Clear();
        var muted = FindBrush("InkMuted");
        _text.Inlines.Add(new Run(before) { Foreground = muted });
        _text.Inlines.Add(new Run(highlight)
        {
            Background = FindBrush("Highlight"),
            Foreground = FindBrush("HighlightInk")
        });
        _text.Inlines.Add(new Run(After ?? "") { Foreground = muted });

        Offset = default;
        if (highlight.Length > 0)
            Dispatcher.UIThread.Post(() => ScrollTo(before.Length), DispatcherPriority.Loaded);
    }

    private void ScrollTo(int textPosition)
    {
        if (Bounds.Width <= 0)
            return;

        UpdateLayout();
        var caret = _text.TextLayout.HitTestTextPosition(textPosition);
        Offset = new Vector(0, Math.Max(0, caret.Top - Viewport.Height / 3));
    }

    private IBrush? FindBrush(string key) =>
        Application.Current is { } app && app.TryGetResource(key, ActualThemeVariant, out var value)
            ? value as IBrush
            : null;
}
