using System.CommandLine;
using Microsoft.CodeAnalysis;
using CSharpMcp.Core;
using CSharpMcp.Models;

namespace CSharpMcp.Commands.TypeHierarchy;

/// <summary>
/// Command to show the inheritance hierarchy for a type.
/// </summary>
public class InheritanceTreeCommand : ICommandHandler
{
    public string Name => "inheritance-tree";
    public string Description => "Show inheritance hierarchy (ancestors and descendants)";

    public Command BuildCommand(CommandContext context)
    {
        var command = new Command(Name, Description);

        // Arguments
        var typeNameArg = new Argument<string>("type-name", "Name of the type");
        command.AddArgument(typeNameArg);

        // Options
        var directionOption = new Option<string>(
            aliases: ["--direction", "-d"],
            getDefaultValue: () => "both",
            description: "Show ancestors, descendants, or both");

        command.AddOption(directionOption);

        // Handler
        command.SetHandler(async (typeName, direction, solution, output, verbose) =>
        {
            context.InitializeFromGlobalOptions(solution, output, verbose);

            try
            {
                var result = await ExecuteAsync(context, typeName, direction);

                if (result == null)
                {
                    await Console.Error.WriteLineAsync($"Type not found: {typeName}");
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
        }, typeNameArg, directionOption,
           GlobalOptions.SolutionOption, GlobalOptions.OutputOption, GlobalOptions.VerboseOption);

        return command;
    }

    private async Task<InheritanceTreeResult?> ExecuteAsync(
        CommandContext context,
        string typeName,
        string direction)
    {
        await context.GetSolutionAsync();

        context.LogVerbose($"Finding type: {typeName}");

        // Find the type
        var symbols = await context.Client.FindSymbolsByNameAsync(typeName, SymbolKind.NamedType);
        var typeSymbol = symbols.OfType<INamedTypeSymbol>().FirstOrDefault();

        if (typeSymbol == null)
        {
            return null;
        }

        var ancestors = new List<string>();
        var descendants = new List<string>();
        var interfaces = new List<string>();

        // Get ancestors (base types)
        if (direction == "up" || direction == "both")
        {
            var baseTypes = context.Client.GetBaseTypes(typeSymbol);
            ancestors = baseTypes.Select(t => t.ToDisplayString()).ToList();
        }

        // Get descendants (derived types)
        if (direction == "down" || direction == "both")
        {
            var derivedTypes = await context.Client.GetDerivedTypesAsync(typeSymbol);
            descendants = derivedTypes.Select(t => t.ToDisplayString()).ToList();
        }

        // Get interfaces
        interfaces = typeSymbol.AllInterfaces.Select(i => i.ToDisplayString()).ToList();

        context.LogVerbose($"Found {ancestors.Count} ancestor(s), {descendants.Count} descendant(s), {interfaces.Count} interface(s)");

        return new InheritanceTreeResult(
            Type: typeSymbol.ToDisplayString(),
            Ancestors: ancestors,
            Descendants: descendants,
            Interfaces: interfaces
        );
    }
}
