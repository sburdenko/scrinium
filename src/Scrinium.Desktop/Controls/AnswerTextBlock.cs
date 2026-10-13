using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
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

    static AnswerTextBlock()
    {
        AnswerProperty.Changed.AddClassHandler<AnswerTextBlock>((block, _) => block.Rebuild());
        CitationsProperty.Changed.AddClassHandler<AnswerTextBlock>((block, _) => block.Rebuild());
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

    protected override Type StyleKeyOverride => typeof(SelectableTextBlock);

    private void Rebuild()
    {
        Inlines ??= new InlineCollection();
        Inlines.Clear();

        var byNumber = (Citations ?? []).ToDictionary(c => c.Number);

        foreach (var part in CitationMarkers.Split(Answer ?? ""))
        {
            switch (part)
            {
                case AnswerPart.Plain plain:
                    Inlines.Add(new Run(plain.Text));
                    break;
                case AnswerPart.Marker marker:
                    AddMarker(marker, byNumber);
                    break;
            }
        }
    }

    private void AddMarker(AnswerPart.Marker marker, Dictionary<int, CitationViewModel> byNumber)
    {
        if (marker.Numbers.Any(n => !byNumber.ContainsKey(n)))
        {
            Inlines!.Add(new Run(marker.Raw));
            return;
        }

        foreach (var number in marker.Numbers)
        {
            var citation = byNumber[number];
            var chip = new Button
            {
                Content = number.ToString(),
                Command = MarkerCommand,
                CommandParameter = citation,
                Classes = { "cite" }
            };
            ToolTip.SetTip(chip, citation.DocumentTitle);
            Inlines!.Add(new InlineUIContainer(chip));
        }
    }
}
