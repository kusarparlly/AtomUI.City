using System.Collections.ObjectModel;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace AtomUI.City.Testing;

/// <summary>
/// Represents source generation test case.
/// </summary>
public sealed class SourceGenerationTestCase
{
    private readonly List<ExpectedDiagnostic> _expectedDiagnostics = [];
    private readonly ReadOnlyCollection<ExpectedDiagnostic> _readOnlyExpectedDiagnostics;
    private readonly ReadOnlyCollection<SourceFile> _readOnlySources;
    private readonly List<SourceFile> _sources = [];

    private SourceGenerationTestCase(string name)
    {
        Name = name;
        _readOnlyExpectedDiagnostics = new ReadOnlyCollection<ExpectedDiagnostic>(_expectedDiagnostics);
        _readOnlySources = new ReadOnlyCollection<SourceFile>(_sources);
    }

    /// <summary>
    /// Gets name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets sources.
    /// </summary>
    public IReadOnlyList<SourceFile> Sources => _readOnlySources;

    /// <summary>
    /// Gets expected diagnostics.
    /// </summary>
    public IReadOnlyList<ExpectedDiagnostic> ExpectedDiagnostics => _readOnlyExpectedDiagnostics;

    /// <summary>
    /// Executes the create operation.
    /// </summary>
    public static SourceGenerationTestCase Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new SourceGenerationTestCase(name);
    }

    /// <summary>
    /// Executes the add source operation.
    /// </summary>
    public SourceGenerationTestCase AddSource(string path, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(text);

        _sources.Add(new SourceFile(path, text));

        return this;
    }

    /// <summary>
    /// Executes the expect diagnostic operation.
    /// </summary>
    public SourceGenerationTestCase ExpectDiagnostic(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        _expectedDiagnostics.Add(new ExpectedDiagnostic(id));

        return this;
    }

    /// <summary>
    /// Executes the run operation.
    /// </summary>
    public SourceGenerationTestResult Run(
        ISourceGenerator generator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(generator);
        cancellationToken.ThrowIfCancellationRequested();

        var syntaxTrees = _sources
            .Select(source => CSharpSyntaxTree.ParseText(
                SourceText.From(source.Text, Encoding.UTF8),
                path: source.Path,
                cancellationToken: cancellationToken))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            Name,
            syntaxTrees,
            CreateReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        cancellationToken.ThrowIfCancellationRequested();
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics,
            cancellationToken);

        var generatedSources = driver
            .GetRunResult()
            .Results
            .SelectMany(result => result.GeneratedSources)
            .Select(source => new GeneratedSource(source.HintName, source.SourceText.ToString()))
            .ToArray();

        return new SourceGenerationTestResult(
            GeneratedSourceSnapshot.Create(generatedSources),
            generatorDiagnostics,
            outputCompilation.GetDiagnostics(cancellationToken));
    }

    private static IReadOnlyList<MetadataReference> CreateReferences()
    {
        return AppDomain
            .CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Cast<MetadataReference>()
            .ToArray();
    }
}
