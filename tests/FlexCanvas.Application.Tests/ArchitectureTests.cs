using System.Xml.Linq;
using System.Runtime.CompilerServices;
using FlexCanvas.Application.Poc;
using Xunit;

namespace FlexCanvas.Application.Tests;

public sealed class ArchitectureTests
{
    [Theory]
    [InlineData("FlexCanvas.Domain", "")]
    [InlineData("FlexCanvas.Application", "FlexCanvas.Domain")]
    [InlineData("FlexCanvas.Infrastructure", "FlexCanvas.Application,FlexCanvas.Domain")]
    [InlineData("FlexCanvas.App", "FlexCanvas.Application,FlexCanvas.Domain,FlexCanvas.Infrastructure")]
    public void ProductionProjectsRespectTheDependencyGraph(string project, string expected)
    {
        string root = FindRoot();
        var document = XDocument.Load(Path.Combine(root, "src", project, $"{project}.csproj"));
        string actual = string.Join(",", document.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension((string)reference.Attribute("Include")!))
            .Order(StringComparer.Ordinal));
        Assert.Equal(expected, actual);
        if (project is "FlexCanvas.Domain" or "FlexCanvas.Application")
        {
            Assert.DoesNotContain(document.Descendants(), e => e.Name.LocalName is "UseWPF" or "UseWindowsForms" or "FrameworkReference");
            Assert.Equal("net8.0", document.Descendants("TargetFramework").Single().Value);
        }
        Assert.Equal("enable", XDocument.Load(Path.Combine(root, "Directory.Build.props")).Descendants("Nullable").Single().Value);
    }

    [Fact]
    public void ApplicationAssemblyDoesNotReferencePresentationOrInfrastructure()
    {
        var references = typeof(IProbeWindowApi).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        Assert.Contains("FlexCanvas.Domain", references);
        Assert.DoesNotContain("FlexCanvas.Infrastructure", references);
        Assert.DoesNotContain("PresentationFramework", references);
        Assert.DoesNotContain("PresentationCore", references);
    }

    private static string FindRoot([CallerFilePath] string sourcePath = "")
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
            if (File.Exists(Path.Combine(folder.FullName, "FlexCanvas.sln"))) return folder.FullName;
        // Isolated build artifacts can live outside the checkout; still inspect the actual source projects.
        for (var folder = new DirectoryInfo(Path.GetDirectoryName(sourcePath)!); folder is not null; folder = folder.Parent)
            if (File.Exists(Path.Combine(folder.FullName, "FlexCanvas.sln"))) return folder.FullName;
        throw new InvalidOperationException("Test source checkout was not found.");
    }
}
