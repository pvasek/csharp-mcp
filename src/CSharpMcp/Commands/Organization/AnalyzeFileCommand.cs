using System.CommandLine;
using Microsoft.CodeAnalysis;
using CSharpMcp.Core;
using CSharpMcp.Models;
using CSharpMcp.Extensions;

namespace CSharpMcp.Commands.Organization;

public class AnalyzeFileCommand : ICommandHandler
{
    public string Name => "analyze-file";
    public string Description => "Quick comprehensive analysis of a single file";

    public Command BuildCommand(CommandContext context)
    {
        var command = new Command(Name, Description);
        var filePathArg = new Argument<string>("file-path", "Path to the file");
        command.AddArgument(filePathArg);

        command.SetHandler(async (filePath, solution, output, verbose) =>
        {
            context.InitializeFromGlobalOptions(solution, output, verbose);

            try
            {
                var result = await ExecuteAsync(context, filePath);
                Console.WriteLine(context.Formatter.Format(result, context.OutputFormat));
                Environment.Exit(ExitCodes.Success);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
                if (context.Verbose) await Console.Error.WriteLineAsync(ex.StackTrace);
                Environment.Exit(ExitCodes.Error);
            }
        }, filePathArg,
           GlobalOptions.SolutionOption, GlobalOptions.OutputOption, GlobalOptions.VerboseOption);

        return command;
    }

    private async Task<FileAnalysisResult> ExecuteAsync(CommandContext context, string filePath)
    {
        await context.GetSolutionAsync();

        // Get types in the file
        var allSymbols = await context.Client.FindSymbolsByNameAsync("*", inFile: filePath);
        var types = allSymbols.OfType<INamedTypeSymbol>().Take(10).ToList();

        var typeInfos = types.Select(t =>
        {
            var location = context.Client.GetSymbolDefinitionLocation(t);
            var lineSpan = location?.GetLineSpan();
            return new TypeLocationInfo(
                Name: t.Name,
                Kind: t.TypeKind.ToString().ToLowerInvariant(),
                File: location?.SourceTree?.FilePath ?? "unknown",
                Line: lineSpan?.StartLinePosition.Line + 1 ?? 0,
                Namespace: t.GetNamespaceName()
            );
        }).ToList();

        // Get diagnostics for the file
        var diagnostics = await context.Client.GetDiagnosticsAsync(filePath);
        var diagnosticItems = diagnostics.Take(10).Select(d =>
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

        var namespaces = types.Select(t => t.GetNamespaceName()).Distinct().ToList();

        return new FileAnalysisResult(
            File: filePath,
            Types: typeInfos,
            Namespaces: namespaces,
            Usings: new List<string>(),
            Dependencies: new List<string>(),
            Diagnostics: diagnosticItems,
            Metrics: new FileMetrics(0, types.Count, 0, "low")
        );
    }
}
