using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Scrinium.Core.Chat;
using Scrinium.Desktop.ViewModels;

namespace Scrinium.Desktop.Controls;

public sealed class AnswerTextBlock : SelectableTextBlock
{
    public static readonly StyledProperty<string?> AnswerProperty =
        AvaloniaProperty.Register<AnswerTextBlock, string?>(nameof(Answer));

    public static readonly StyledProperty<IEnumerable<CitationViewModel>?> CitationsProperty =
        AvaloniaProperty.Register<AnswerTextBlock, IEnumerable<CitationViewModel>?>(nameof(Citations));

    public static readonly StyledProperty<ICommand?> MarkerCommandProperty =
        AvaloniaProperty.Register<AnswerTextBlock, ICommand?>(nameof(MarkerCommand));

    public static readonly StyledProperty<int?> ActiveNumberProperty =
        AvaloniaProperty.Register<AnswerTextBlock, int?>(nameof(ActiveNumber));

    static AnswerTextBlock()
    {
        AnswerProperty.Changed.AddClassHandler<AnswerTextBlock>((block, _) => block.Rebuild());
        CitationsProperty.Changed.AddClassHandler<AnswerTextBlock>((block, _) => block.Rebuild());
        ActiveNumberProperty.Changed.AddClassHandler<AnswerTextBlock>((block, _) => block.Rebuild());
    }

    public string? Answer
    {
        get => GetValue(AnswerProperty);
        set => SetValue(AnswerProperty, value);
    }

    public IEnumerable<CitationViewModel>? Citations
    {
        get => GetValue(CitationsProperty);
        set => SetValue(CitationsProperty, value);
    }

    public ICommand? MarkerCommand
    {
        get => GetValue(MarkerCommandProperty);
        set => SetValue(MarkerCommandProperty, value);
    }

    public int? ActiveNumber
    {
        get => GetValue(ActiveNumberProperty);
        set => SetValue(ActiveNumberProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(SelectableTextBlock);

    private readonly List<MarkerSpan> _markers = [];

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var marker = MarkerAt(e.GetPosition(this));
        Cursor = marker is null ? null : new Cursor(StandardCursorType.Hand);
        ToolTip.SetTip(this, marker?.Citation.DocumentTitle);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton != MouseButton.Left || !string.IsNullOrEmpty(SelectedText))
            return;

        if (MarkerAt(e.GetPosition(this)) is { } marker && MarkerCommand?.CanExecute(marker.Citation) == true)
            MarkerCommand.Execute(marker.Citation);
    }

    private MarkerSpan? MarkerAt(Point point)
    {
        if (_markers.Count == 0)
            return null;

        var hit = TextLayout.HitTestPoint(point);
        if (!hit.IsInside)
            return null;

        return _markers.Cast<MarkerSpan?>().FirstOrDefault(m => hit.TextPosition >= m!.Value.Start && hit.TextPosition < m.Value.End);
    }

    private void Rebuild()
    {
        Inlines ??= new InlineCollection();
        Inlines.Clear();
        _markers.Clear();

        var byNumber = (Citations ?? []).ToDictionary(c => c.Number);
        var parts = CitationMarkers.Split(Answer ?? "");
        var highlight = this.TryFindResource("AccentSoft", ActualThemeVariant, out var soft) ? soft as IBrush : null;
        var position = 0;

        for (var i = 0; i < parts.Count; i++)
        {
            switch (parts[i])
            {
                case AnswerPart.Plain plain:
                    var followedByActive = i + 1 < parts.Count
                        && parts[i + 1] is AnswerPart.Marker next
                        && ActiveNumber is { } active
                        && next.Numbers.Contains(active)
                        && next.Numbers.All(byNumber.ContainsKey);
                    position = AddPlain(plain.Text, followedByActive ? highlight : null, position);
                    break;
                case AnswerPart.Marker marker:
                    position = AddMarker(marker, byNumber, position);
                    break;
            }
        }
    }

    private int AddPlain(string text, IBrush? sentenceBackground, int position)
    {
        if (sentenceBackground is null)
        {
            Inlines!.Add(new Run(text));
            return position + text.Length;
        }

        var cut = text.TrimEnd().LastIndexOfAny(['.', '!', '?', '\n']) + 1;
        if (cut > 0)
            Inlines!.Add(new Run(text[..cut]));

        Inlines!.Add(new Run(text[cut..]) { Background = sentenceBackground });
        return position + text.Length;
    }

    private int AddMarker(AnswerPart.Marker marker, Dictionary<int, CitationViewModel> byNumber, int position)
    {
        if (marker.Numbers.Any(n => !byNumber.ContainsKey(n)))
        {
            Inlines!.Add(new Run(marker.Raw));
            return position + marker.Raw.Length;
        }

        var accent = this.TryFindResource("Accent", ActualThemeVariant, out var a) ? a as IBrush : null;
        for (var index = 0; index < marker.Numbers.Count; index++)
        {
            var number = marker.Numbers[index];
            var label = index < marker.Numbers.Count - 1 ? $"{number}," : number.ToString();
            Inlines!.Add(new Run(label)
            {
                FontSize = FontSize * 0.7,
                FontWeight = FontWeight.Bold,
                Foreground = accent,
                BaselineAlignment = BaselineAlignment.Superscript
            });
            _markers.Add(new MarkerSpan(position, position + label.Length, byNumber[number]));
            position += label.Length;
        }

        return position;
    }

    private readonly record struct MarkerSpan(int Start, int End, CitationViewModel Citation);
}
