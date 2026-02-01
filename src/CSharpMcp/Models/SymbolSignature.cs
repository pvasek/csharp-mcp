namespace CSharpMcp.Models;

/// <summary>
/// Parameter information for a method.
/// </summary>
public record ParameterInfo(
    string Name,
    string Type,
    bool IsOptional,
    string? DefaultValue
);

/// <summary>
/// Single method/type signature.
/// </summary>
public record SignatureInfo(
    string Declaration,
    string? ReturnType,
    List<ParameterInfo>? Parameters,
    string Accessibility,
    bool IsStatic,
    bool IsAsync,
    bool IsVirtual,
    bool IsAbstract,
    string? Documentation
);

/// <summary>
/// Result of getting symbol signature(s).
/// </summary>
public record SymbolSignatureResult(
    string Symbol,
    string Kind,
    List<SignatureInfo> Signatures
);
