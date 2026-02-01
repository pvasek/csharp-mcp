namespace CSharpMcp.Models;

/// <summary>
/// Result of generating an interface from a class.
/// </summary>
public record InterfaceGenerationResult(
    string InterfaceName,
    string Content,
    string? OutputFile
);

/// <summary>
/// Result of generating implementation stubs.
/// </summary>
public record ImplementationStubsResult(
    string ClassName,
    List<string> Methods
);
