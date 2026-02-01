using System.CommandLine;
using Microsoft.CodeAnalysis;
using CSharpMcp.Core;
using CSharpMcp.Extensions;

namespace CSharpMcp.Commands.Compilation;

/// <summary>
/// Result of checking if a symbol exists.
/// </summary>
public record SymbolExistsResult(
    string Symbol,
    bool Exists,
    bool? Accessible,
    string? Location,
    string? Kind,
    string? Namespace
);

/// <summary>
/// Command to quickly check if a symbol exists and is accessible.
/// </summary>
public class CheckSymbolExistsCommand : ICommandHandler
{
    public string Name => "check-symbol-exists";
    public string Description => "Quickly verify if a symbol exists and is accessible";

    public Command BuildCommand(CommandContext context)
    {
        var command = new Command(Name, Description);

        // Arguments
        var symbolArg = new Argument<string>("symbol-name", "Name of the symbol to check");
        command.AddArgument(symbolArg);

        // Options
        var typeOption = new Option<string?>(
            aliases: ["--type", "-t"],
            description: "Expected symbol type");

        var inNamespaceOption = new Option<string?>(
            aliases: ["--in-namespace", "-n"],
            description: "Expected namespace");

        command.AddOption(typeOption);
        command.AddOption(inNamespaceOption);

        // Handler
        command.SetHandler(async (symbolName, type, inNamespace, solution, output, verbose) =>
        {
            context.InitializeFromGlobalOptions(solution, output, verbose);

            try
            {
                var result = await ExecuteAsync(context, symbolName, type, inNamespace);
                var formattedOutput = context.Formatter.Format(result, context.OutputFormat);
                Console.WriteLine(formattedOutput);

                // Exit with NotFound if symbol doesn't exist
                Environment.Exit(result.Exists ? ExitCodes.Success : ExitCodes.NotFound);
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

    private async Task<SymbolExistsResult> ExecuteAsync(
        CommandContext context,
        string symbolName,
        string? typeFilter,
        string? inNamespace)
    {
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

        context.LogVerbose($"Checking if symbol exists: {symbolName}");

        // Find the symbol
        var symbols = await context.Client.FindSymbolsByNameAsync(symbolName, kind, inNamespace);
        var symbol = symbols.FirstOrDefault();

        if (symbol == null)
        {
            return new SymbolExistsResult(
                Symbol: symbolName,
                Exists: false,
                Accessible: null,
                Location: null,
                Kind: null,
                Namespace: null
            );
        }

        var location = context.Client.GetSymbolDefinitionLocation(symbol);
        var lineSpan = location?.GetLineSpan();

        return new SymbolExistsResult(
            Symbol: symbolName,
            Exists: true,
            Accessible: symbol.DeclaredAccessibility == Accessibility.Public,
            Location: location != null ? $"{location.SourceTree?.FilePath}:{lineSpan?.StartLinePosition.Line + 1}" : null,
            Kind: symbol.GetKindString(),
            Namespace: symbol.GetNamespaceName()
        );
    }
}
