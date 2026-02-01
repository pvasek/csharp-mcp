using System.CommandLine;
using Microsoft.CodeAnalysis;
using CSharpMcp.Core;
using CSharpMcp.Models;
using CSharpMcp.Extensions;

namespace CSharpMcp.Commands.TypeHierarchy;

/// <summary>
/// Command to find all implementations of an interface or abstract class.
/// </summary>
public class FindImplementationsCommand : ICommandHandler
{
    public string Name => "find-implementations";
    public string Description => "Find all implementations of an interface or abstract class";

    public Command BuildCommand(CommandContext context)
    {
        var command = new Command(Name, Description);

        // Arguments
        var symbolArg = new Argument<string>("symbol-name", "Name of the interface or abstract class");
        command.AddArgument(symbolArg);

        // Handler
        command.SetHandler(async (symbolName, solution, output, verbose) =>
        {
            context.InitializeFromGlobalOptions(solution, output, verbose);

            try
            {
                var result = await ExecuteAsync(context, symbolName);

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
        }, symbolArg,
           GlobalOptions.SolutionOption, GlobalOptions.OutputOption, GlobalOptions.VerboseOption);

        return command;
    }

    private async Task<ImplementationsResult?> ExecuteAsync(
        CommandContext context,
        string symbolName)
    {
        await context.GetSolutionAsync();

        context.LogVerbose($"Finding interface/abstract class: {symbolName}");

        // Find the symbol
        var symbols = await context.Client.FindSymbolsByNameAsync(symbolName, SymbolKind.NamedType);
        var typeSymbol = symbols.OfType<INamedTypeSymbol>().FirstOrDefault();

        if (typeSymbol == null)
        {
            return null;
        }

        if (typeSymbol.TypeKind != TypeKind.Interface && !typeSymbol.IsAbstract)
        {
            await Console.Error.WriteLineAsync($"Warning: {symbolName} is not an interface or abstract class");
        }

        context.LogVerbose($"Finding implementations...");

        // Find implementations
        var implementations = await context.Client.FindImplementationsAsync(typeSymbol);
        var implList = implementations.ToList();

        context.LogVerbose($"Found {implList.Count} implementation(s)");

        // Convert to output model
        var typeInfos = implList.Select(impl =>
        {
            var location = context.Client.GetSymbolDefinitionLocation(impl);
            var lineSpan = location?.GetLineSpan();

            return new TypeLocationInfo(
                Name: impl.Name,
                Kind: impl.TypeKind.ToString().ToLowerInvariant(),
                File: location?.SourceTree?.FilePath ?? "unknown",
                Line: lineSpan?.StartLinePosition.Line + 1 ?? 0,
                Namespace: impl.GetNamespaceName()
            );
        }).ToList();

        return new ImplementationsResult(
            Interface: typeSymbol.Name,
            Implementations: typeInfos,
            TotalImplementations: typeInfos.Count
        );
    }
}
