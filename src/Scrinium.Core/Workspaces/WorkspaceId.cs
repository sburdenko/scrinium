using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Scrinium.Core.Workspaces;

/// <summary>
/// The name of a workspace folder: lowercase letters, digits and dashes, at most 40 characters,
/// so it can never point outside the workspaces folder.
/// </summary>
public sealed partial record WorkspaceId
{
    private const int MaxLength = 40;
    private const string Fallback = "workspace";

    private WorkspaceId(string value) => Value = value;

    public string Value { get; }

    public static WorkspaceId Default { get; } = new("default");

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,39}$")]
    private static partial Regex Pattern();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NotSlug();

    public static bool TryParse(string? value, out WorkspaceId id)
    {
        id = Default;
        if (value is null || !Pattern().IsMatch(value))
            return false;

        id = new WorkspaceId(value);
        return true;
    }

    /// <exception cref="FormatException">The value is not a valid workspace id.</exception>
    public static WorkspaceId Parse(string value) =>
        TryParse(value, out var id) ? id : throw new FormatException($"'{value}' is not a valid workspace id.");

    /// <summary>A readable id derived from a display name, such as "kestrel-analytics" for "Kestrel Analytics".</summary>
    public static WorkspaceId Suggest(string name)
    {
        var slug = NotSlug().Replace(WithoutAccents(name).ToLowerInvariant(), "-").Trim('-');
        if (slug.Length > MaxLength)
            slug = slug[..MaxLength].TrimEnd('-');

        return new WorkspaceId(slug.Length == 0 ? Fallback : slug);
    }

    /// <summary>This id with a numeric suffix, shortened so the result stays a valid id.</summary>
    public WorkspaceId WithSuffix(int number)
    {
        var suffix = "-" + number.ToString(CultureInfo.InvariantCulture);
        var stem = Value.Length + suffix.Length > MaxLength ? Value[..(MaxLength - suffix.Length)].TrimEnd('-') : Value;
        return new WorkspaceId(stem + suffix);
    }

    public override string ToString() => Value;

    private static string WithoutAccents(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString();
    }
}
