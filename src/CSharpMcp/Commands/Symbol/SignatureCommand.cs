using System.CommandLine;
using Microsoft.CodeAnalysis;
using CSharpMcp.Core;
using CSharpMcp.Models;
using CSharpMcp.Extensions;

namespace CSharpMcp.Commands.Symbol;

/// <summary>
/// Command to get the signature and documentation of a symbol.
/// </summary>
public class SignatureCommand : ICommandHandler
{
    public string Name => "signature";
    public string Description => "Get the signature and documentation of a symbol";

    public Command BuildCommand(CommandContext context)
    {
        var command = new Command(Name, Description);

        // Arguments
        var symbolArg = new Argument<string>("symbol-name", "Name of the symbol");
        command.AddArgument(symbolArg);

        // Options
        var typeOption = new Option<string?>(
            aliases: ["--type", "-t"],
            description: "Type of symbol: class, method, property, field");

        var includeOverloadsOption = new Option<bool>(
            aliases: ["--include-overloads"],
            getDefaultValue: () => false,
            description: "Show all overloads for methods");

        var includeDocsOption = new Option<bool>(
            aliases: ["--include-docs"],
            getDefaultValue: () => true,
            description: "Include XML documentation comments");

        command.AddOption(typeOption);
        command.AddOption(includeOverloadsOption);
        command.AddOption(includeDocsOption);

        // Handler
        command.SetHandler(async (symbolName, type, includeOverloads, includeDocs, solution, output, verbose) =>
        {
            context.InitializeFromGlobalOptions(solution, output, verbose);

            try
            {
                var result = await ExecuteAsync(context, symbolName, type, includeOverloads, includeDocs);

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
        }, symbolArg, typeOption, includeOverloadsOption, includeDocsOption,
           GlobalOptions.SolutionOption, GlobalOptions.OutputOption, GlobalOptions.VerboseOption);

        return command;
    }

    private async Task<SymbolSignatureResult?> ExecuteAsync(
        CommandContext context,
        string symbolName,
        string? typeFilter,
        bool includeOverloads,
        bool includeDocs)
    {
        await context.GetSolutionAsync();

        // Parse type filter
        SymbolKind? kind = typeFilter?.ToLowerInvariant() switch
        {
            "class" => SymbolKind.NamedType,
            "method" => SymbolKind.Method,
            "property" => SymbolKind.Property,
            "field" => SymbolKind.Field,
            _ => null
        };

        context.LogVerbose($"Finding symbol: {symbolName}");

        // Find the symbol
        var symbols = await context.Client.FindSymbolsByNameAsync(symbolName, kind);
        var symbolsList = symbols.ToList();

        if (!symbolsList.Any())
        {
            return null;
        }

        // Get all matching symbols (for overloads)
        var targetSymbols = includeOverloads ? symbolsList : new List<ISymbol> { symbolsList.First() };

        var signatures = new List<SignatureInfo>();

        foreach (var symbol in targetSymbols)
        {
            SignatureInfo sig;

            if (symbol is IMethodSymbol method)
            {
                var parameters = method.Parameters.Select(p => new ParameterInfo(
                    Name: p.Name,
                    Type: p.Type.ToDisplayString(),
                    IsOptional: p.IsOptional,
                    DefaultValue: p.HasExplicitDefaultValue ? p.ExplicitDefaultValue?.ToString() : null
                )).ToList();

                sig = new SignatureInfo(
                    Declaration: method.ToDisplayString(),
                    ReturnType: method.ReturnType.ToDisplayString(),
                    Parameters: parameters,
                    Accessibility: method.GetAccessibilityString(),
                    IsStatic: method.IsStatic,
                    IsAsync: method.IsAsync,
                    IsVirtual: method.IsVirtual,
                    IsAbstract: method.IsAbstract,
                    Documentation: includeDocs ? method.GetDocumentationSummary() : null
                );
            }
            else if (symbol is IPropertySymbol property)
            {
                sig = new SignatureInfo(
                    Declaration: property.ToDisplayString(),
                    ReturnType: property.Type.ToDisplayString(),
                    Parameters: null,
                    Accessibility: property.GetAccessibilityString(),
                    IsStatic: property.IsStatic,
                    IsAsync: false,
                    IsVirtual: property.IsVirtual,
                    IsAbstract: property.IsAbstract,
                    Documentation: includeDocs ? property.GetDocumentationSummary() : null
                );
            }
            else
            {
                sig = new SignatureInfo(
                    Declaration: symbol.ToDisplayString(),
                    ReturnType: null,
                    Parameters: null,
                    Accessibility: symbol.GetAccessibilityString(),
                    IsStatic: symbol.IsStatic,
                    IsAsync: false,
                    IsVirtual: false,
                    IsAbstract: false,
                    Documentation: includeDocs ? symbol.GetDocumentationSummary() : null
                );
            }

            signatures.Add(sig);
        }

        return new SymbolSignatureResult(
            Symbol: symbolName,
            Kind: symbolsList.First().GetKindString(),
            Signatures: signatures
        );
    }
}
