using System.Text.RegularExpressions;

namespace Scrinium.Core.Chat;

public abstract record AnswerPart
{
    private AnswerPart() { }

    public sealed record Plain(string Text) : AnswerPart;

    public sealed record Marker(string Raw, IReadOnlyList<int> Numbers) : AnswerPart;
}
