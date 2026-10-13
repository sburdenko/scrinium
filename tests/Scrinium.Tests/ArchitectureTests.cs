using System.Reflection;

namespace Scrinium.Tests;

public sealed class ArchitectureTests
{
    private static readonly Assembly Core = typeof(Scrinium.Core.Documents.Document).Assembly;
    private static readonly Assembly Storage = typeof(Scrinium.Storage.SqliteStore).Assembly;
    private static readonly Assembly Extraction = typeof(Scrinium.Extraction.PdfTextExtractor).Assembly;
    private static readonly Assembly Ollama = typeof(Scrinium.Ollama.OllamaMonitor).Assembly;
    private static readonly Assembly Desktop = typeof(Scrinium.Desktop.App).Assembly;

    [Fact]
    public void Core_depends_on_no_other_Scrinium_assembly() =>
        AssertScriniumReferences(Core);

    [Fact]
    public void Storage_Extraction_and_Ollama_depend_only_on_Core()
    {
        AssertScriniumReferences(Storage, "Scrinium.Core");
        AssertScriniumReferences(Extraction, "Scrinium.Core");
        AssertScriniumReferences(Ollama, "Scrinium.Core");
    }

    [Fact]
    public void Desktop_composes_Core_Storage_Extraction_and_Ollama() =>
        AssertScriniumReferences(Desktop, "Scrinium.Core", "Scrinium.Storage", "Scrinium.Extraction", "Scrinium.Ollama");

    [Fact]
    public void Only_Storage_references_Sqlite() =>
        AssertOnlyReferencedBy("Microsoft.Data.Sqlite", Storage);

    [Fact]
    public void Only_Extraction_references_PdfPig() =>
        AssertOnlyReferencedBy("UglyToad.PdfPig", Extraction);

    [Fact]
    public void Only_Desktop_references_Avalonia() =>
        AssertOnlyReferencedBy("Avalonia", Desktop);

    [Fact]
    public void Only_Ollama_references_OllamaSharp() =>
        AssertOnlyReferencedBy("OllamaSharp", Ollama);

    [Fact]
    public void Core_names_outcomes_as_states_not_boolean_flags()
    {
        var flags = PublicInstanceProperties(Core).Where(p => p.PropertyType == typeof(bool));

        Assert.Empty(flags.Select(Describe));
    }

    [Fact]
    public void Core_values_cannot_change_after_construction()
    {
        var mutable = PublicInstanceProperties(Core).Where(IsMutable);

        Assert.Empty(mutable.Select(Describe));
    }

    private static void AssertScriniumReferences(Assembly assembly, params string[] allowed)
    {
        var actual = assembly.GetReferencedAssemblies()
            .Select(name => name.Name!)
            .Where(name => name.StartsWith("Scrinium.", StringComparison.Ordinal))
            .Order()
            .ToList();

        Assert.Equal(allowed.Order(), actual);
    }

    private static void AssertOnlyReferencedBy(string prefix, Assembly owner)
    {
        foreach (var assembly in new[] { Core, Storage, Extraction, Ollama, Desktop })
        {
            var references = assembly.GetReferencedAssemblies()
                .Any(name => name.Name!.StartsWith(prefix, StringComparison.Ordinal));

            Assert.True(
                references == (assembly == owner),
                $"{assembly.GetName().Name} {(references ? "references" : "must reference")} {prefix}");
        }
    }

    private static IEnumerable<PropertyInfo> PublicInstanceProperties(Assembly assembly) =>
        assembly.GetExportedTypes()
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(property => property.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() is null);

    private static bool IsMutable(PropertyInfo property)
    {
        var setter = property.SetMethod;
        if (setter is null || !setter.IsPublic)
            return false;

        var initOnly = setter.ReturnParameter
            .GetRequiredCustomModifiers()
            .Any(modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit");
        return !initOnly;
    }

    private static string Describe(PropertyInfo property) => $"{property.DeclaringType!.FullName}.{property.Name}";
}
