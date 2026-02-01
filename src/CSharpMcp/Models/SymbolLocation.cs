namespace CSharpMcp.Models;

/// <summary>
/// Information about a symbol's location in the source code.
/// </summary>
public record LocationInfo(
    string File,
    int Line,
    int Column
);

/// <summary>
/// Result of finding a symbol's definition.
/// </summary>
public record SymbolLocation(
    string Symbol,
    string Kind,
    LocationInfo Location,
    string Namespace,
    string Accessibility
);
