using System.CommandLine;
using Microsoft.CodeAnalysis;
using CSharpMcp.Core;
using CSharpMcp.Models;

namespace CSharpMcp.Commands.Compilation;

/// <summary>
/// Command to get compilation diagnostics (errors, warnings, info).
/// </summary>
public class DiagnosticsCommand : ICommandHandler
{
    public string Name => "diagnostics";
    public string Description => "Get all compilation errors, warnings, and info messages";

    public Command BuildCommand(CommandContext context)
    {
        var command = new Command(Name, Description);

        // Options
        var severityOption = new Option<string?>(
            aliases: ["--severity", "-s"],
            description: "Filter by severity: error, warning, info");

        var fileOption = new Option<string?>(
            aliases: ["--file", "-f"],
            description: "Get diagnostics only for specific file");

        var codeOption = new Option<string?>(
            aliases: ["--code", "-c"],
            description: "Filter by diagnostic code (e.g., CS0246)");

        command.AddOption(severityOption);
        command.AddOption(fileOption);
        command.AddOption(codeOption);

        // Handler
        command.SetHandler(async (severity, file, code, solution, output, verbose) =>
        {
            context.InitializeFromGlobalOptions(solution, output, verbose);

            try
            {
                var result = await ExecuteAsync(context, severity, file, code);
                var formattedOutput = context.Formatter.Format(result, context.OutputFormat);
                Console.WriteLine(formattedOutput);
                Environment.Exit(ExitCodes.Success);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
                if (context.Verbose)
                {
                    await Console.Error.WriteLineAsync(ex.StackTrace);
                }
                Environment.Exit(ExitCodes.Error);
            }
        }, severityOption, fileOption, codeOption,
           GlobalOptions.SolutionOption, GlobalOptions.OutputOption, GlobalOptions.VerboseOption);

        return command;
    }

    private async Task<DiagnosticResult> ExecuteAsync(
        CommandContext context,
        string? severityFilter,
        string? fileFilter,
        string? codeFilter)
    {
        // Load solution
        await context.GetSolutionAsync();

        // Parse severity filter
        DiagnosticSeverity? severity = severityFilter?.ToLowerInvariant() switch
        {
            "error" => DiagnosticSeverity.Error,
            "warning" => DiagnosticSeverity.Warning,
            "info" => DiagnosticSeverity.Info,
            _ => null
        };

        context.LogVerbose($"Getting diagnostics (severity: {severityFilter ?? "all"})");

        // Get diagnostics
        var diagnostics = await context.Client.GetDiagnosticsAsync(fileFilter, severity);
        var diagnosticsList = diagnostics.ToList();

        // Apply code filter if specified
        if (!string.IsNullOrEmpty(codeFilter))
        {
            diagnosticsList = diagnosticsList
                .Where(d => d.Id.Equals(codeFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Count by severity
        var errors = diagnosticsList.Count(d => d.Severity == DiagnosticSeverity.Error);
        var warnings = diagnosticsList.Count(d => d.Severity == DiagnosticSeverity.Warning);
        var infos = diagnosticsList.Count(d => d.Severity == DiagnosticSeverity.Info);

        context.LogVerbose($"Found {diagnosticsList.Count} diagnostic(s): {errors} errors, {warnings} warnings, {infos} info");

        // Convert to output model
        var items = diagnosticsList.Select(d =>
        {
            var lineSpan = d.Location.GetLineSpan();
            return new DiagnosticItem(
                Id: d.Id,
                Severity: d.Severity.ToString().ToLowerInvariant(),
                Message: d.GetMessage(),
                File: d.Location.SourceTree?.FilePath ?? "unknown",
                Line: lineSpan.StartLinePosition.Line + 1,
                Column: lineSpan.StartLinePosition.Character + 1,
                EndLine: lineSpan.EndLinePosition.Line + 1,
                EndColumn: lineSpan.EndLinePosition.Character + 1
            );
        }).ToList();

        return new DiagnosticResult(
            TotalErrors: errors,
            TotalWarnings: warnings,
            TotalInfo: infos,
            Diagnostics: items
        );
    }
}
