using System.Reflection;
using System.Xml.Linq;
using Scrinium.Tests.Support;

namespace Scrinium.Tests;

public sealed class ArchitectureTests
{
    private static readonly Assembly Core = typeof(Scrinium.Core.Documents.Document).Assembly;
    private static readonly Assembly Storage = typeof(Scrinium.Storage.SqliteStore).Assembly;
    private static readonly Assembly Extraction = typeof(Scrinium.Extraction.PdfTextExtractor).Assembly;
    private static readonly Assembly Ollama = typeof(Scrinium.Ollama.OllamaMonitor).Assembly;
    private static readonly Assembly Desktop = typeof(Scrinium.Desktop.App).Assembly;
    private static readonly Assembly Server = typeof(Scrinium.Server.ServerApp).Assembly;
    private static readonly Assembly Eval = typeof(Scrinium.Eval.EvaluationRunner).Assembly;

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
    public void Desktop_and_Server_compose_Core_Storage_Extraction_and_Ollama()
    {
        string[] parts = ["Scrinium.Core", "Scrinium.Storage", "Scrinium.Extraction", "Scrinium.Ollama"];
        AssertScriniumReferences(Desktop, parts);
        AssertScriniumReferences(Server, parts);
        AssertScriniumReferences(Eval, parts);
    }

    [Fact]
    public void Only_Storage_references_Sqlite() =>
        AssertOnlyReferencedBy("Microsoft.Data.Sqlite", Storage);

    [Fact]
    public void Only_Extraction_references_PdfPig() =>
        AssertOnlyReferencedBy("UglyToad.PdfPig", Extraction);

    [Fact]
    public void Only_Extraction_references_HtmlAgilityPack() =>
        AssertOnlyReferencedBy("HtmlAgilityPack", Extraction);

    [Fact]
    public void Only_Extraction_references_OpenXml() =>
        AssertOnlyReferencedBy("DocumentFormat.OpenXml", Extraction);

    [Fact]
    public void Only_Desktop_references_Avalonia() =>
        AssertOnlyReferencedBy("Avalonia", Desktop);

    [Fact]
    public void Only_Server_references_AspNetCore() =>
        AssertOnlyReferencedBy("Microsoft.AspNetCore", Server);

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

    [Theory]
    [InlineData("Microsoft.Data.Sqlite", "Scrinium.Storage")]
    [InlineData("PdfPig", "Scrinium.Extraction")]
    [InlineData("HtmlAgilityPack", "Scrinium.Extraction")]
    [InlineData("DocumentFormat.OpenXml", "Scrinium.Extraction")]
    [InlineData("Avalonia", "Scrinium.Desktop")]
    [InlineData("OllamaSharp", "Scrinium.Ollama")]
    public void A_package_is_declared_only_by_the_project_that_owns_it(string package, string owner)
    {
        var declaring = Directory.GetFiles(Path.Combine(RepositoryPaths.Root(), "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(project => DeclaresPackage(project, package))
            .Select(project => Path.GetFileNameWithoutExtension(project))
            .ToList();

        Assert.Equal([owner], declaring);
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
        foreach (var assembly in new[] { Core, Storage, Extraction, Ollama, Desktop, Server })
        {
            var references = assembly.GetReferencedAssemblies()
                .Any(name => name.Name!.StartsWith(prefix, StringComparison.Ordinal));

            Assert.True(
                references == (assembly == owner),
                $"{assembly.GetName().Name} {(references ? "references" : "must reference")} {prefix}");
        }
    }

    private static bool DeclaresPackage(string projectFile, string package) =>
        XDocument.Load(projectFile)
            .Descendants("PackageReference")
            .Any(reference => ((string?)reference.Attribute("Include"))?.StartsWith(package, StringComparison.Ordinal) == true);

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
