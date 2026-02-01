namespace CSharpMcp.Models;

/// <summary>
/// Information about a method in the call graph.
/// </summary>
public record MethodCallInfo(
    string Method,
    string File,
    int Line,
    string? CallLocation
);

/// <summary>
/// Result of finding callers of a method.
/// </summary>
public record CallersResult(
    string Method,
    List<MethodCallInfo> Callers,
    int TotalCallers
);

/// <summary>
/// Result of finding callees (methods called by a method).
/// </summary>
public record CalleesResult(
    string Method,
    List<MethodCallInfo> Callees,
    int TotalCallees
);
