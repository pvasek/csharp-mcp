using System.CommandLine;
using Microsoft.CodeAnalysis;
using CSharpMcp.Core;
using CSharpMcp.Models;

namespace CSharpMcp.Commands.CodeGeneration;

public class GenerateInterfaceCommand : ICommandHandler
{
    public string Name => "generate-interface";
    public string Description => "Extract an interface from a class";

    public Command BuildCommand(CommandContext context)
    {
        var command = new Command(Name, Description);
        var classNameArg = new Argument<string>("class-name", "Name of the class");
        command.AddArgument(classNameArg);

        command.SetHandler(async (className, solution, output, verbose) =>
        {
            context.InitializeFromGlobalOptions(solution, output, verbose);

            try
            {
                var result = await ExecuteAsync(context, className);
                if (result == null)
                {
                    await Console.Error.WriteLineAsync($"Class not found: {className}");
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
        }, classNameArg,
           GlobalOptions.SolutionOption, GlobalOptions.OutputOption, GlobalOptions.VerboseOption);

        return command;
    }

    private async Task<InterfaceGenerationResult?> ExecuteAsync(CommandContext context, string className)
    {
        await context.GetSolutionAsync();
        var symbols = await context.Client.FindSymbolsByNameAsync(className, SymbolKind.NamedType);
        var classSymbol = symbols.OfType<INamedTypeSymbol>().FirstOrDefault();
        if (classSymbol == null) return null;

        var interfaceName = "I" + className;
        var publicMethods = classSymbol.GetMembers()
            .Where(m => m.DeclaredAccessibility == Accessibility.Public && m.Kind == SymbolKind.Method)
            .OfType<IMethodSymbol>()
            .Where(m => !m.IsImplicitlyDeclared);

        var content = $"public interface {interfaceName}\n{{\n";
        foreach (var method in publicMethods)
        {
            content += $"    {method.ReturnType.ToDisplayString()} {method.Name}({string.Join(", ", method.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"))});\n";
        }
        content += "}";

        return new InterfaceGenerationResult(interfaceName, content, null);
    }
}
