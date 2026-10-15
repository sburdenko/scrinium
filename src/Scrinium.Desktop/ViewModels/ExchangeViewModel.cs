using CommunityToolkit.Mvvm.ComponentModel;
using Scrinium.Core.Chat;

namespace Scrinium.Desktop.ViewModels;

public sealed partial class ExchangeViewModel : ObservableObject
{
    private const int ShortAnswerLimit = 110;

    public ExchangeViewModel(string question, DateTimeOffset asked)
    {
        Question = question;
        Asked = asked;
        Answer.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AnswerViewModel.Text) or nameof(AnswerViewModel.Phase))
                OnPropertyChanged(nameof(ShortAnswer));
        };
    }

    public string Question { get; }

    public DateTimeOffset Asked { get; }

    public AnswerViewModel Answer { get; } = new();

    public string TimeText => Asked.ToString("HH:mm");

    public string MetaLine => $"You asked · {TimeText}";

    [ObservableProperty]
    private bool _isFocused;

    public bool IsCompressed => !IsFocused;

    partial void OnIsFocusedChanged(bool value) => OnPropertyChanged(nameof(IsCompressed));

    public string ShortAnswer
    {
        get
        {
            if (Answer.IsFailed)
                return "Could not answer";

            var collapsed = CitationMarkers.Strip(Answer.Text);
            var cut = collapsed.IndexOfAny(['.', '!', '?']);
            var sentence = cut >= 0 ? collapsed[..(cut + 1)] : collapsed;
            return sentence.Length <= ShortAnswerLimit ? sentence : sentence[..ShortAnswerLimit].TrimEnd() + "…";
        }
    }
}
