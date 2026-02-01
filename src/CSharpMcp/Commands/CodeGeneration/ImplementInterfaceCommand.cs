using System.CommandLine;
using Microsoft.CodeAnalysis;
using CSharpMcp.Core;
using CSharpMcp.Models;

namespace CSharpMcp.Commands.CodeGeneration;

public class ImplementInterfaceCommand : ICommandHandler
{
    public string Name => "implement-interface";
    public string Description => "Generate implementation stubs for an interface";

    public Command BuildCommand(CommandContext context)
    {
        var command = new Command(Name, Description);
        var interfaceNameArg = new Argument<string>("interface-name", "Name of the interface");
        command.AddArgument(interfaceNameArg);

        command.SetHandler(async (interfaceName, solution, output, verbose) =>
        {
            context.InitializeFromGlobalOptions(solution, output, verbose);

            try
            {
                var result = await ExecuteAsync(context, interfaceName);
                if (result == null)
                {
                    await Console.Error.WriteLineAsync($"Interface not found: {interfaceName}");
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
        }, interfaceNameArg,
           GlobalOptions.SolutionOption, GlobalOptions.OutputOption, GlobalOptions.VerboseOption);

        return command;
    }

    private async Task<ImplementationStubsResult?> ExecuteAsync(CommandContext context, string interfaceName)
    {
        await context.GetSolutionAsync();
        var symbols = await context.Client.FindSymbolsByNameAsync(interfaceName, SymbolKind.NamedType);
        var interfaceSymbol = symbols.OfType<INamedTypeSymbol>().FirstOrDefault();
        if (interfaceSymbol == null || interfaceSymbol.TypeKind != TypeKind.Interface) return null;

        var methods = new List<string>();
        foreach (var member in interfaceSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var stub = $"public {member.ReturnType.ToDisplayString()} {member.Name}({string.Join(", ", member.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"))})\n{{\n    throw new NotImplementedException();\n}}";
            methods.Add(stub);
        }

        return new ImplementationStubsResult(interfaceSymbol.Name + "Implementation", methods);
    }
}
