using System.CommandLine;
using CSharpMcp.Core;
using CSharpMcp.Models;

namespace CSharpMcp.Commands.Organization;

public class NamespaceTreeCommand : ICommandHandler
{
    public string Name => "namespace-tree";
    public string Description => "Show the namespace hierarchy of the solution";

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

    private async Task<NamespaceTreeResult> ExecuteAsync(CommandContext context)
    {
        await context.GetSolutionAsync();
        var allTypes = await context.Client.GetAllTypesAsync();

        var tree = new Dictionary<string, object>();
        foreach (var type in allTypes.Take(50)) // Limit for performance
        {
            var ns = type.ContainingNamespace?.ToDisplayString() ?? "Global";
            if (!tree.ContainsKey(ns))
            {
                tree[ns] = new List<string>();
            }
            ((List<string>)tree[ns]).Add(type.Name);
        }

        return new NamespaceTreeResult("Root", tree);
    }
}
