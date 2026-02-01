using System.CommandLine;
using Microsoft.CodeAnalysis;
using CSharpMcp.Core;
using CSharpMcp.Models;

namespace CSharpMcp.Commands.Dependency;

public class DependenciesCommand : ICommandHandler
{
    public string Name => "dependencies";
    public string Description => "Analyze what types/namespaces a file or type depends on";

    public Command BuildCommand(CommandContext context)
    {
        var command = new Command(Name, Description);
        var targetArg = new Argument<string>("target", "File path or type name");
        command.AddArgument(targetArg);

        command.SetHandler(async (target, solution, output, verbose) =>
        {
            context.InitializeFromGlobalOptions(solution, output, verbose);

            try
            {
                var result = await ExecuteAsync(context, target);
                Console.WriteLine(context.Formatter.Format(result, context.OutputFormat));
                Environment.Exit(ExitCodes.Success);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
                if (context.Verbose) await Console.Error.WriteLineAsync(ex.StackTrace);
                Environment.Exit(ExitCodes.Error);
            }
        }, targetArg,
           GlobalOptions.SolutionOption, GlobalOptions.OutputOption, GlobalOptions.VerboseOption);

        return command;
    }

    private async Task<DependenciesResult> ExecuteAsync(CommandContext context, string target)
    {
        await context.GetSolutionAsync();

        // Simple implementation: collect unique namespaces from all symbols
        var allSymbols = await context.Client.GetAllSymbolsAsync();
        var namespaces = allSymbols
            .Select(s => s.ContainingNamespace?.ToDisplayString())
            .Where(ns => !string.IsNullOrEmpty(ns))
            .Distinct()
            .ToList();

        return new DependenciesResult(
            Target: target,
            Namespaces: namespaces!,
            Types: new List<TypeDependency>(),
            ExternalPackages: new List<string>()
        );
    }
}
