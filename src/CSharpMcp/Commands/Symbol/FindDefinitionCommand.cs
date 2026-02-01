using System.CommandLine;
using Microsoft.CodeAnalysis;
using CSharpMcp.Core;
using CSharpMcp.Models;
using CSharpMcp.Extensions;

namespace CSharpMcp.Commands.Symbol;

/// <summary>
/// Command to find the definition location of a symbol.
/// </summary>
public class FindDefinitionCommand : ICommandHandler
{
    public string Name => "find-definition";
    public string Description => "Find where a symbol (class, method, property, etc.) is defined";

    public Command BuildCommand(CommandContext context)
    {
        var command = new Command(Name, Description);

        // Arguments
        var symbolArg = new Argument<string>("symbol-name", "Name of the symbol to find");
        command.AddArgument(symbolArg);

        // Options
        var typeOption = new Option<string?>(
            aliases: ["--type", "-t"],
            description: "Filter by symbol type: class, method, property, field, interface, enum");

        var inFileOption = new Option<string?>(
            aliases: ["--in-file", "-f"],
            description: "Search only in specific file");

        var inNamespaceOption = new Option<string?>(
            aliases: ["--in-namespace", "-n"],
            description: "Search only in specific namespace");

        command.AddOption(typeOption);
        command.AddOption(inFileOption);
        command.AddOption(inNamespaceOption);

        // Handler
        command.SetHandler(async (symbolName, type, inFile, inNamespace, solution, output, verbose) =>
        {
            context.InitializeFromGlobalOptions(solution, output, verbose);

            try
            {
                var result = await ExecuteAsync(context, symbolName, type, inFile, inNamespace);

                if (result == null)
                {
                    await Console.Error.WriteLineAsync($"Symbol not found: {symbolName}");
                    Environment.Exit(ExitCodes.NotFound);
                }

                var outputText = context.Formatter.Format(result, context.OutputFormat);
                Console.WriteLine(outputText);
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
        }, symbolArg, typeOption, inFileOption, inNamespaceOption,
           GlobalOptions.SolutionOption, GlobalOptions.OutputOption, GlobalOptions.VerboseOption);

        return command;
    }

    private async Task<SymbolLocation?> ExecuteAsync(
        CommandContext context,
        string symbolName,
        string? typeFilter,
        string? inFile,
        string? inNamespace)
    {
        // Load solution
        await context.GetSolutionAsync();

        // Parse type filter to SymbolKind
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

        context.LogVerbose($"Searching for symbol: {symbolName}");

        // Find symbols matching the criteria
        var symbols = await context.Client.FindSymbolsByNameAsync(
            symbolName,
            kind,
            inNamespace,
            inFile);

        var symbolsList = symbols.ToList();

        if (!symbolsList.Any())
        {
            return null;
        }

        // If multiple matches, take the first one (we could enhance this later)
        var symbol = symbolsList.First();

        if (symbolsList.Count > 1)
        {
            context.LogVerbose($"Found {symbolsList.Count} matches, returning first one");
        }

        // Get the definition location
        var location = context.Client.GetSymbolDefinitionLocation(symbol);

        if (location == null)
        {
            return null;
        }

        var lineSpan = location.GetLineSpan();

        return new SymbolLocation(
            Symbol: symbol.Name,
            Kind: symbol.GetKindString(),
            Location: new LocationInfo(
                File: location.SourceTree?.FilePath ?? "unknown",
                Line: lineSpan.StartLinePosition.Line + 1,
                Column: lineSpan.StartLinePosition.Character + 1
            ),
            Namespace: symbol.GetNamespaceName(),
            Accessibility: symbol.GetAccessibilityString()
        );
    }
}
