namespace CSharpMcp.Models;

/// <summary>
/// Result of finding implementations of an interface or abstract class.
/// </summary>
public record ImplementationsResult(
    string Interface,
    List<TypeLocationInfo> Implementations,
    int TotalImplementations
);

/// <summary>
/// Information about a type in the hierarchy.
/// </summary>
public record TypeLocationInfo(
    string Name,
    string Kind,
    string File,
    int Line,
    string Namespace
);

/// <summary>
/// Result of getting the inheritance tree for a type.
/// </summary>
public record InheritanceTreeResult(
    string Type,
    List<string> Ancestors,
    List<string> Descendants,
    List<string> Interfaces
);
