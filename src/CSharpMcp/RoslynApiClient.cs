using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.FindSymbols;

namespace CSharpMcp;

/// <summary>
/// Client that uses Roslyn APIs directly (without LSP) for refactoring operations.
/// Provides higher-level refactoring capabilities like rename, extract method, etc.
/// </summary>
public class RoslynApiClient : IAsyncDisposable
{
    private MSBuildWorkspace? _workspace;
    private Solution? _solution;

    /// <summary>
    /// Opens a solution file and loads it into the Roslyn workspace.
    /// Must be called before using any other methods.
    /// </summary>
    public async Task<Solution> OpenSolutionAsync(string solutionPath)
    {
        // Register MSBuild - required for loading projects (only register once)
        if (!Microsoft.Build.Locator.MSBuildLocator.IsRegistered)
        {
            Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults();
        }

        _workspace = MSBuildWorkspace.Create();

        // Subscribe to workspace failures for diagnostics
        _workspace.RegisterWorkspaceFailedHandler((args) =>
        {
            Console.WriteLine($"[Workspace] Failed: {args.Diagnostic.Message}");
        });

        Console.WriteLine($"[RoslynApi] Loading solution: {solutionPath}");
        _solution = await _workspace.OpenSolutionAsync(solutionPath);
        Console.WriteLine($"[RoslynApi] Loaded {_solution.Projects.Count()} project(s)");

        return _solution;
    }

    /// <summary>
    /// Gets all symbols in the solution (types, methods, properties, etc.)
    /// </summary>
    public async Task<IEnumerable<ISymbol>> GetAllSymbolsAsync()
    {
        EnsureSolutionLoaded();

        var allSymbols = new List<ISymbol>();

        foreach (var project in _solution!.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation == null) continue;

            // Get all type symbols from the compilation
            var symbols = GetAllSymbolsFromNamespace(compilation.GlobalNamespace);
            allSymbols.AddRange(symbols);
        }

        return allSymbols;
    }

    /// <summary>
    /// Finds a symbol by its fully qualified name.
    /// Example: "RoslynLspExample.RoslynLspClient"
    /// </summary>
    public async Task<ISymbol?> FindSymbolAsync(string fullyQualifiedName)
    {
        EnsureSolutionLoaded();

        foreach (var project in _solution!.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation == null) continue;

            var symbol = compilation.GetTypeByMetadataName(fullyQualifiedName);
            if (symbol != null) return symbol;
        }

        return null;
    }

    /// <summary>
    /// Renames a symbol across the entire solution.
    /// Returns a new Solution with all changes applied (original solution is unchanged).
    /// </summary>
    public async Task<Solution> RenameSymbolAsync(ISymbol symbol, string newName)
    {
        EnsureSolutionLoaded();

        Console.WriteLine($"[RoslynApi] Renaming '{symbol.Name}' to '{newName}'...");

        // Use Roslyn's rename API with newer overload
        var renamedSolution = await Renamer.RenameSymbolAsync(
            _solution!,
            symbol,
            new Microsoft.CodeAnalysis.Rename.SymbolRenameOptions(),
            newName,
            CancellationToken.None
        );

        Console.WriteLine($"[RoslynApi] Rename complete");
        return renamedSolution;
    }

    /// <summary>
    /// Renames a type by its fully qualified name.
    /// Returns a new Solution with all changes applied.
    /// </summary>
    public async Task<Solution> RenameTypeAsync(string fullyQualifiedName, string newName)
    {
        var symbol = await FindSymbolAsync(fullyQualifiedName);
        if (symbol == null)
        {
            throw new InvalidOperationException($"Symbol not found: {fullyQualifiedName}");
        }

        return await RenameSymbolAsync(symbol, newName);
    }

    /// <summary>
    /// Applies solution changes to disk (writes modified files).
    /// WARNING: This modifies your source files!
    /// </summary>
    public async Task ApplySolutionChangesAsync(Solution newSolution)
    {
        EnsureSolutionLoaded();

        var changes = newSolution.GetChanges(_solution!);

        Console.WriteLine($"[RoslynApi] Applying changes...");
        var changedProjects = 0;
        var changedDocuments = 0;

        foreach (var projectChanges in changes.GetProjectChanges())
        {
            changedProjects++;

            // Handle changed documents
            foreach (var documentId in projectChanges.GetChangedDocuments())
            {
                changedDocuments++;
                var oldDocument = _solution!.GetDocument(documentId);
                var newDocument = newSolution.GetDocument(documentId);

                if (newDocument == null) continue;

                var newText = await newDocument.GetTextAsync();
                var filePath = newDocument.FilePath;

                if (filePath == null) continue;

                Console.WriteLine($"[RoslynApi]   Writing: {Path.GetFileName(filePath)}");
                await File.WriteAllTextAsync(filePath, newText.ToString());
            }

            // Handle added documents
            foreach (var documentId in projectChanges.GetAddedDocuments())
            {
                changedDocuments++;
                var newDocument = newSolution.GetDocument(documentId)!;
                var newText = await newDocument.GetTextAsync();
                var filePath = newDocument.FilePath!;

                Console.WriteLine($"[RoslynApi]   Creating: {Path.GetFileName(filePath)}");
                await File.WriteAllTextAsync(filePath, newText.ToString());
            }

            // Handle removed documents
            foreach (var documentId in projectChanges.GetRemovedDocuments())
            {
                changedDocuments++;
                var oldDocument = _solution!.GetDocument(documentId);
                if (oldDocument == null) continue;

                var filePath = oldDocument.FilePath;
                if (filePath == null) continue;

                Console.WriteLine($"[RoslynApi]   Deleting: {Path.GetFileName(filePath)}");
                File.Delete(filePath);
            }
        }

        Console.WriteLine($"[RoslynApi] Applied changes to {changedDocuments} document(s) in {changedProjects} project(s)");

        // Update our current solution
        _solution = newSolution;
    }

    /// <summary>
    /// Finds all references to a symbol across the solution.
    /// </summary>
    public async Task<IEnumerable<ReferenceLocation>> FindReferencesAsync(ISymbol symbol)
    {
        EnsureSolutionLoaded();

        Console.WriteLine($"[RoslynApi] Finding references to '{symbol.Name}'...");
        var references = await SymbolFinder.FindReferencesAsync(symbol, _solution!);

        var locations = references
            .SelectMany(r => r.Locations)
            .ToList();

        Console.WriteLine($"[RoslynApi] Found {locations.Count} reference(s)");
        return locations;
    }

    /// <summary>
    /// Gets all types (classes, interfaces, structs, enums) in the solution.
    /// </summary>
    public async Task<IEnumerable<INamedTypeSymbol>> GetAllTypesAsync()
    {
        var allSymbols = await GetAllSymbolsAsync();
        return allSymbols.OfType<INamedTypeSymbol>();
    }

    /// <summary>
    /// Finds a symbol by name with optional filters for kind and namespace.
    /// More flexible than FindSymbolAsync which requires fully qualified names.
    /// </summary>
    public async Task<IEnumerable<ISymbol>> FindSymbolsByNameAsync(
        string name,
        SymbolKind? kind = null,
        string? inNamespace = null,
        string? inFile = null)
    {
        EnsureSolutionLoaded();

        var results = new List<ISymbol>();
        var allSymbols = await GetAllSymbolsAsync();

        foreach (var symbol in allSymbols)
        {
            // Check name match
            if (!symbol.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                continue;

            // Check kind filter
            if (kind.HasValue && symbol.Kind != kind.Value)
                continue;

            // Check namespace filter
            if (!string.IsNullOrEmpty(inNamespace))
            {
                var symbolNamespace = symbol.ContainingNamespace?.ToDisplayString();
                if (symbolNamespace == null || !symbolNamespace.Equals(inNamespace, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            // Check file filter
            if (!string.IsNullOrEmpty(inFile))
            {
                var locations = symbol.Locations;
                if (!locations.Any(l => l.SourceTree?.FilePath?.EndsWith(inFile, StringComparison.OrdinalIgnoreCase) == true))
                    continue;
            }

            results.Add(symbol);
        }

        return results;
    }

    /// <summary>
    /// Gets the definition location of a symbol.
    /// </summary>
    public Location? GetSymbolDefinitionLocation(ISymbol symbol)
    {
        return symbol.Locations.FirstOrDefault(l => l.IsInSource);
    }

    /// <summary>
    /// Gets all compilation diagnostics (errors, warnings, info) from the solution.
    /// </summary>
    public async Task<IEnumerable<Diagnostic>> GetDiagnosticsAsync(
        string? filePath = null,
        DiagnosticSeverity? severity = null)
    {
        EnsureSolutionLoaded();

        var allDiagnostics = new List<Diagnostic>();

        foreach (var project in _solution!.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation == null) continue;

            var diagnostics = compilation.GetDiagnostics();

            // Apply filters
            var filtered = diagnostics.AsEnumerable();

            if (severity.HasValue)
            {
                filtered = filtered.Where(d => d.Severity == severity.Value);
            }

            if (!string.IsNullOrEmpty(filePath))
            {
                filtered = filtered.Where(d =>
                    d.Location.SourceTree?.FilePath?.EndsWith(filePath, StringComparison.OrdinalIgnoreCase) == true);
            }

            allDiagnostics.AddRange(filtered);
        }

        return allDiagnostics;
    }

    /// <summary>
    /// Checks if a symbol with the given name exists in the solution.
    /// </summary>
    public async Task<bool> SymbolExistsAsync(string symbolName, SymbolKind? kind = null)
    {
        var symbols = await FindSymbolsByNameAsync(symbolName, kind);
        return symbols.Any();
    }

    /// <summary>
    /// Finds all implementations of an interface or abstract class.
    /// </summary>
    public async Task<IEnumerable<INamedTypeSymbol>> FindImplementationsAsync(INamedTypeSymbol interfaceOrAbstractType)
    {
        EnsureSolutionLoaded();

        var implementations = await SymbolFinder.FindImplementationsAsync(interfaceOrAbstractType, _solution!);
        return implementations.OfType<INamedTypeSymbol>();
    }

    /// <summary>
    /// Gets the base types (inheritance chain) of a type.
    /// </summary>
    public IEnumerable<INamedTypeSymbol> GetBaseTypes(INamedTypeSymbol type)
    {
        var current = type.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            yield return current;
            current = current.BaseType;
        }
    }

    /// <summary>
    /// Gets all derived types (descendants) of a type.
    /// </summary>
    public async Task<IEnumerable<INamedTypeSymbol>> GetDerivedTypesAsync(INamedTypeSymbol type)
    {
        EnsureSolutionLoaded();

        var allTypes = await GetAllTypesAsync();
        return allTypes.Where(t => t.BaseType != null && t.BaseType.Equals(type, SymbolEqualityComparer.Default));
    }

    /// <summary>
    /// Finds all methods that call the specified method.
    /// </summary>
    public async Task<IEnumerable<IMethodSymbol>> FindCallersAsync(IMethodSymbol method)
    {
        EnsureSolutionLoaded();

        var callers = await SymbolFinder.FindCallersAsync(method, _solution!);
        return callers.Select(c => c.CallingSymbol).OfType<IMethodSymbol>();
    }

    /// <summary>
    /// Finds all methods called by the specified method.
    /// </summary>
    public async Task<IEnumerable<IMethodSymbol>> FindCalleesAsync(IMethodSymbol method)
    {
        EnsureSolutionLoaded();

        var callees = new List<IMethodSymbol>();

        foreach (var syntaxRef in method.DeclaringSyntaxReferences)
        {
            var syntax = await syntaxRef.GetSyntaxAsync();
            var semanticModel = await _solution!.GetDocument(syntaxRef.SyntaxTree)?.GetSemanticModelAsync()!;

            if (semanticModel == null) continue;

            var invocations = syntax.DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>();

            foreach (var invocation in invocations)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(invocation);
                if (symbolInfo.Symbol is IMethodSymbol invokedMethod)
                {
                    callees.Add(invokedMethod);
                }
            }
        }

        return callees.Distinct(SymbolEqualityComparer.Default).Cast<IMethodSymbol>();
    }

    public async ValueTask DisposeAsync()
    {
        _workspace?.Dispose();
    }

    private void EnsureSolutionLoaded()
    {
        if (_solution == null)
        {
            throw new InvalidOperationException("No solution loaded. Call OpenSolutionAsync first.");
        }
    }

    private static IEnumerable<ISymbol> GetAllSymbolsFromNamespace(INamespaceSymbol namespaceSymbol)
    {
        foreach (var member in namespaceSymbol.GetMembers())
        {
            if (member is INamespaceSymbol childNamespace)
            {
                // Recursively get symbols from nested namespaces
                foreach (var symbol in GetAllSymbolsFromNamespace(childNamespace))
                {
                    yield return symbol;
                }
            }
            else
            {
                // Return the symbol (type, method, property, etc.)
                yield return member;

                // If it's a type, also return its members
                if (member is INamedTypeSymbol typeSymbol)
                {
                    foreach (var typeMember in typeSymbol.GetMembers())
                    {
                        yield return typeMember;
                    }
                }
            }
        }
    }
}
