namespace CSharpMcp.Models;

/// <summary>
/// Type dependency information.
/// </summary>
public record TypeDependency(
    string Name,
    string Namespace,
    int UsageCount
);

/// <summary>
/// Result of analyzing dependencies.
/// </summary>
public record DependenciesResult(
    string Target,
    List<string> Namespaces,
    List<TypeDependency> Types,
    List<string>? ExternalPackages
);

/// <summary>
/// Unused symbol information.
/// </summary>
public record UnusedSymbol(
    string Name,
    string Kind,
    string File,
    int Line,
    string Accessibility,
    string Reason
);

/// <summary>
/// Result of finding unused code.
/// </summary>
public record UnusedCodeResult(
    List<UnusedSymbol> UnusedSymbols,
    int TotalUnused
);
