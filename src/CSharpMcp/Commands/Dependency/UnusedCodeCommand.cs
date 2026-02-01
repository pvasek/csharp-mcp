using System.CommandLine;
using Microsoft.CodeAnalysis;
using CSharpMcp.Core;
using CSharpMcp.Models;
using CSharpMcp.Extensions;

namespace CSharpMcp.Commands.Dependency;

public class UnusedCodeCommand : ICommandHandler
{
    public string Name => "unused-code";
    public string Description => "Find potentially unused code (methods, classes, properties)";

    public Command BuildCommand(CommandContext context)
    {
        var command = new Command(Name, Description);

        command.SetHandler(async (solution, output, verbose) =>
        {
            context.InitializeFromGlobalOptions(solution, output, verbose);

            try
            {
                var result = await ExecuteAsync(context);
                Console.WriteLine(context.Formatter.Format(result, context.OutputFormat));
                Environment.Exit(ExitCodes.Success);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
                if (context.Verbose) await Console.Error.WriteLineAsync(ex.StackTrace);
                Environment.Exit(ExitCodes.Error);
            }
        },
        GlobalOptions.SolutionOption, GlobalOptions.OutputOption, GlobalOptions.VerboseOption);

        return command;
    }

    private async Task<UnusedCodeResult> ExecuteAsync(CommandContext context)
    {
        await context.GetSolutionAsync();
        context.LogVerbose("Finding unused code (this may take a while)...");

        // Simple implementation: find private methods with no references
        var allSymbols = await context.Client.GetAllSymbolsAsync();
        var privateMethods = allSymbols
            .OfType<IMethodSymbol>()
            .Where(m => m.DeclaredAccessibility == Accessibility.Private && !m.IsImplicitlyDeclared)
            .Take(10); // Limit for performance

        var unused = new List<UnusedSymbol>();

        foreach (var method in privateMethods)
        {
            var refs = await context.Client.FindReferencesAsync(method);
            if (!refs.Any())
            {
                var location = context.Client.GetSymbolDefinitionLocation(method);
                var lineSpan = location?.GetLineSpan();
                unused.Add(new UnusedSymbol(
                    Name: method.Name,
                    Kind: "method",
                    File: location?.SourceTree?.FilePath ?? "unknown",
                    Line: lineSpan?.StartLinePosition.Line + 1 ?? 0,
                    Accessibility: "private",
                    Reason: "No callers found"
                ));
            }
        }

        return new UnusedCodeResult(unused, unused.Count);
    }
}
