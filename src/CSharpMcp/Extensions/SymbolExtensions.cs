using Microsoft.CodeAnalysis;
using System.Text;

namespace CSharpMcp.Extensions;

/// <summary>
/// Extension methods for ISymbol to provide common functionality.
/// </summary>
public static class SymbolExtensions
{
    /// <summary>
    /// Gets the fully qualified name of a symbol.
    /// </summary>
    public static string GetFullyQualifiedName(this ISymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    /// <summary>
    /// Gets a readable signature for a symbol.
    /// </summary>
    public static string GetSignature(this ISymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
    }

    /// <summary>
    /// Gets the accessibility as a string (public, private, protected, internal).
    /// </summary>
    public static string GetAccessibilityString(this ISymbol symbol)
    {
        return symbol.DeclaredAccessibility.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// Gets the kind of symbol as a string (class, method, property, etc.).
    /// </summary>
    public static string GetKindString(this ISymbol symbol)
    {
        return symbol.Kind.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// Checks if a symbol is part of the public API surface.
    /// </summary>
    public static bool IsPublicApi(this ISymbol symbol)
    {
        return symbol.DeclaredAccessibility == Accessibility.Public;
    }

    /// <summary>
    /// Gets the namespace name for a symbol, or empty string if no namespace.
    /// </summary>
    public static string GetNamespaceName(this ISymbol symbol)
    {
        return symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
    }

    /// <summary>
    /// Gets method parameters as a formatted string.
    /// </summary>
    public static string GetParametersString(this IMethodSymbol method)
    {
        var parameters = method.Parameters.Select(p =>
            $"{p.Type.ToDisplayString()} {p.Name}" + (p.IsOptional ? " = " + (p.ExplicitDefaultValue ?? "null") : ""));
        return string.Join(", ", parameters);
    }

    /// <summary>
    /// Gets XML documentation summary for a symbol, if available.
    /// </summary>
    public static string? GetDocumentationSummary(this ISymbol symbol)
    {
        var xml = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml)) return null;

        // Simple extraction of summary tag content
        var summaryStart = xml.IndexOf("<summary>", StringComparison.Ordinal);
        var summaryEnd = xml.IndexOf("</summary>", StringComparison.Ordinal);

        if (summaryStart >= 0 && summaryEnd > summaryStart)
        {
            var summary = xml.Substring(summaryStart + 9, summaryEnd - summaryStart - 9);
            return summary.Trim();
        }

        return null;
    }
}
