namespace CSharpMcp.Models;

/// <summary>
/// Information about a single diagnostic (error, warning, info).
/// </summary>
public record DiagnosticItem(
    string Id,
    string Severity,
    string Message,
    string File,
    int Line,
    int Column,
    int EndLine,
    int EndColumn
);

/// <summary>
/// Result of getting diagnostics from the solution.
/// </summary>
public record DiagnosticResult(
    int TotalErrors,
    int TotalWarnings,
    int TotalInfo,
    List<DiagnosticItem> Diagnostics
);
