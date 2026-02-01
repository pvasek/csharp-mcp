namespace CSharpMcp.Models;

/// <summary>
/// Result of listing types.
/// </summary>
public record ListTypesResult(
    string? Namespace,
    List<TypeLocationInfo> Types,
    int TotalTypes
);

/// <summary>
/// Namespace tree node.
/// </summary>
public record NamespaceNode(
    string Name,
    List<string> Types,
    Dictionary<string, NamespaceNode>? Children
);

/// <summary>
/// Result of getting namespace tree.
/// </summary>
public record NamespaceTreeResult(
    string Root,
    Dictionary<string, object> Tree
);

/// <summary>
/// Metrics for a file.
/// </summary>
public record FileMetrics(
    int Lines,
    int Methods,
    int Properties,
    string Complexity
);

/// <summary>
/// Result of analyzing a file.
/// </summary>
public record FileAnalysisResult(
    string File,
    List<TypeLocationInfo> Types,
    List<string> Namespaces,
    List<string> Usings,
    List<string> Dependencies,
    List<DiagnosticItem> Diagnostics,
    FileMetrics Metrics
);
