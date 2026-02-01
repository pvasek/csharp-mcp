using System.CommandLine;

namespace CSharpMcp.Core;

/// <summary>
/// Output format for command results.
/// </summary>
public enum OutputFormat
{
    Json,
    Text,
    Markdown
}

/// <summary>
/// Global command-line options shared across all commands.
/// </summary>
public static class GlobalOptions
{
    /// <summary>
    /// Path to the .sln file to analyze.
    /// Optional - if not specified, will auto-discover in current directory.
    /// </summary>
    public static readonly Option<string?> SolutionOption = new(
        aliases: ["--solution", "-s"],
        description: "Path to the .sln file (optional - auto-discovers if not specified)"
    );

    /// <summary>
    /// Output format for results.
    /// </summary>
    public static readonly Option<OutputFormat> OutputOption = new(
        aliases: ["--output", "-o"],
        getDefaultValue: () => OutputFormat.Json,
        description: "Output format: json, text, or markdown"
    );

    /// <summary>
    /// Enable verbose logging.
    /// </summary>
    public static readonly Option<bool> VerboseOption = new(
        aliases: ["--verbose", "-v"],
        getDefaultValue: () => false,
        description: "Enable verbose logging"
    );
}
