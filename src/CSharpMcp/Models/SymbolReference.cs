namespace CSharpMcp.Models;

/// <summary>
/// Information about a single reference to a symbol.
/// </summary>
public record ReferenceInfo(
    string File,
    int Line,
    int Column,
    string Context,
    string Kind
);

/// <summary>
/// Result of finding all references to a symbol.
/// </summary>
public record SymbolReferenceResult(
    string Symbol,
    int TotalReferences,
    List<ReferenceInfo> References
);
