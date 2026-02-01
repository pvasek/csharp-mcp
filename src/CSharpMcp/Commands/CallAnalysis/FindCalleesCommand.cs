using System.CommandLine;
using Microsoft.CodeAnalysis;
using CSharpMcp.Core;
using CSharpMcp.Models;
using CSharpMcp.Extensions;

namespace CSharpMcp.Commands.CallAnalysis;

public class FindCalleesCommand : ICommandHandler
{
    public string Name => "find-callees";
    public string Description => "Find all methods called by a specific method";

    public Command BuildCommand(CommandContext context)
    {
        var command = new Command(Name, Description);
        var methodArg = new Argument<string>("method-name", "Name of the method");
        command.AddArgument(methodArg);

        command.SetHandler(async (methodName, solution, output, verbose) =>
        {
            context.InitializeFromGlobalOptions(solution, output, verbose);

            try
            {
                var result = await ExecuteAsync(context, methodName);
                if (result == null)
                {
                    await Console.Error.WriteLineAsync($"Method not found: {methodName}");
                    Environment.Exit(ExitCodes.NotFound);
                }
                Console.WriteLine(context.Formatter.Format(result, context.OutputFormat));
                Environment.Exit(ExitCodes.Success);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
                if (context.Verbose) await Console.Error.WriteLineAsync(ex.StackTrace);
                Environment.Exit(ExitCodes.Error);
            }
        }, methodArg,
           GlobalOptions.SolutionOption, GlobalOptions.OutputOption, GlobalOptions.VerboseOption);

        return command;
    }

    private async Task<CalleesResult?> ExecuteAsync(CommandContext context, string methodName)
    {
        await context.GetSolutionAsync();
        var symbols = await context.Client.FindSymbolsByNameAsync(methodName, SymbolKind.Method);
        var method = symbols.OfType<IMethodSymbol>().FirstOrDefault();
        if (method == null) return null;

        var callees = await context.Client.FindCalleesAsync(method);
        var calleesList = callees.ToList();

        var callInfos = calleesList.Select(m =>
        {
            var location = context.Client.GetSymbolDefinitionLocation(m);
            var lineSpan = location?.GetLineSpan();
            return new MethodCallInfo(
                Method: m.ToDisplayString(),
                File: location?.SourceTree?.FilePath ?? "unknown",
                Line: lineSpan?.StartLinePosition.Line + 1 ?? 0,
                CallLocation: null
            );
        }).ToList();

        return new CalleesResult(method.ToDisplayString(), callInfos, callInfos.Count);
    }
}
