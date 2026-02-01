using System.CommandLine;
using Microsoft.CodeAnalysis;
using CSharpMcp.Core;
using CSharpMcp.Models;
using CSharpMcp.Extensions;

namespace CSharpMcp.Commands.Symbol;

/// <summary>
/// Command to find all references/usages of a symbol.
/// </summary>
public class FindReferencesCommand : ICommandHandler
{
    public string Name => "find-references";
    public string Description => "Find all references/usages of a symbol throughout the solution";

    public Command BuildCommand(CommandContext context)
    {
        var command = new Command(Name, Description);

        // Arguments
        var symbolArg = new Argument<string>("symbol-name", "Name of the symbol to find references for");
        command.AddArgument(symbolArg);

        // Options
        var typeOption = new Option<string?>(
            aliases: ["--type", "-t"],
            description: "Symbol type: class, method, property, field, interface, enum");

        var inNamespaceOption = new Option<string?>(
            aliases: ["--in-namespace", "-n"],
            description: "Symbol namespace");

        command.AddOption(typeOption);
        command.AddOption(inNamespaceOption);

        // Handler
        command.SetHandler(async (symbolName, type, inNamespace, solution, output, verbose) =>
        {
            context.InitializeFromGlobalOptions(solution, output, verbose);

            try
            {
                var result = await ExecuteAsync(context, symbolName, type, inNamespace);

                if (result == null)
                {
                    await Console.Error.WriteLineAsync($"Symbol not found: {symbolName}");
                    Environment.Exit(ExitCodes.NotFound);
                }

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
        }, symbolArg, typeOption, inNamespaceOption,
           GlobalOptions.SolutionOption, GlobalOptions.OutputOption, GlobalOptions.VerboseOption);

        return command;
    }

    private async Task<SymbolReferenceResult?> ExecuteAsync(
        CommandContext context,
        string symbolName,
        string? typeFilter,
        string? inNamespace)
    {
        // Load solution
        await context.GetSolutionAsync();

        // Parse type filter
        SymbolKind? kind = typeFilter?.ToLowerInvariant() switch
        {
            "class" => SymbolKind.NamedType,
            "method" => SymbolKind.Method,
            "property" => SymbolKind.Property,
            "field" => SymbolKind.Field,
            "interface" => SymbolKind.NamedType,
            "enum" => SymbolKind.NamedType,
            _ => null
        };

        context.LogVerbose($"Finding symbol: {symbolName}");

        // Find the symbol
        var symbols = await context.Client.FindSymbolsByNameAsync(symbolName, kind, inNamespace);
        var symbol = symbols.FirstOrDefault();

        if (symbol == null)
        {
            return null;
        }

        context.LogVerbose($"Finding references to: {symbol.Name}");

        // Find all references
        var references = await context.Client.FindReferencesAsync(symbol);
        var referencesList = references.ToList();

        context.LogVerbose($"Found {referencesList.Count} reference(s)");

        // Convert to output model
        var items = referencesList.Select(r =>
        {
            var location = r.Location;
            var lineSpan = location.GetLineSpan();

            // Get context (the line of code containing the reference)
            var sourceText = location.SourceTree?.GetText().ToString();
            var line = sourceText?.Split('\n').ElementAtOrDefault(lineSpan.StartLinePosition.Line)?.Trim() ?? "";

            return new ReferenceInfo(
                File: location.SourceTree?.FilePath ?? "unknown",
                Line: lineSpan.StartLinePosition.Line + 1,
                Column: lineSpan.StartLinePosition.Character + 1,
                Context: line.Length > 100 ? line.Substring(0, 100) + "..." : line,
                Kind: r.IsImplicit ? "implicit" : "explicit"
            );
        }).ToList();

        return new SymbolReferenceResult(
            Symbol: $"{symbol.ContainingType?.Name}.{symbol.Name}" ?? symbol.Name,
            TotalReferences: items.Count,
            References: items
        );
    }
}
