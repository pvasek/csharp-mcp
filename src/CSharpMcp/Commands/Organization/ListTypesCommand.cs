using System.CommandLine;
using Microsoft.CodeAnalysis;
using CSharpMcp.Core;
using CSharpMcp.Models;
using CSharpMcp.Extensions;

namespace CSharpMcp.Commands.Organization;

public class ListTypesCommand : ICommandHandler
{
    public string Name => "list-types";
    public string Description => "List all types in a namespace or file";

    public Command BuildCommand(CommandContext context)
    {
        var command = new Command(Name, Description);

        var namespaceOption = new Option<string?>("--namespace", "Filter by namespace");
        command.AddOption(namespaceOption);

        command.SetHandler(async (namespaceFilter, solution, output, verbose) =>
        {
            context.InitializeFromGlobalOptions(solution, output, verbose);

            try
            {
                var result = await ExecuteAsync(context, namespaceFilter);
                Console.WriteLine(context.Formatter.Format(result, context.OutputFormat));
                Environment.Exit(ExitCodes.Success);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
                if (context.Verbose) await Console.Error.WriteLineAsync(ex.StackTrace);
                Environment.Exit(ExitCodes.Error);
            }
        }, namespaceOption,
           GlobalOptions.SolutionOption, GlobalOptions.OutputOption, GlobalOptions.VerboseOption);

        return command;
    }

    private async Task<ListTypesResult> ExecuteAsync(CommandContext context, string? namespaceFilter)
    {
        await context.GetSolutionAsync();
        var allTypes = await context.Client.GetAllTypesAsync();

        if (!string.IsNullOrEmpty(namespaceFilter))
        {
            allTypes = allTypes.Where(t => t.ContainingNamespace?.ToDisplayString() == namespaceFilter);
        }

        var typesList = allTypes.Take(100).ToList(); // Limit for performance

        var typeInfos = typesList.Select(t =>
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

        return new ListTypesResult(namespaceFilter, typeInfos, typeInfos.Count);
    }
}
