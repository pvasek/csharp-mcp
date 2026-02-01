using System.CommandLine;
using CSharpMcp.Core;
using CSharpMcp.Commands;
using CSharpMcp.Commands.Symbol;
using CSharpMcp.Commands.Compilation;
using CSharpMcp.Commands.TypeHierarchy;
using CSharpMcp.Commands.CallAnalysis;
using CSharpMcp.Commands.Dependency;
using CSharpMcp.Commands.CodeGeneration;
using CSharpMcp.Commands.Organization;
using CSharpMcp.Mcp;

namespace CSharpMcp;

class Program
{
    // Known command names for detecting CLI mode
    private static readonly HashSet<string> CommandNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "find-definition", "find-references", "rename", "signature", "list-members",
        "find-implementations", "inheritance-tree",
        "find-callers", "find-callees",
        "diagnostics", "check-symbol-exists",
        "dependencies", "unused-code",
        "generate-interface", "implement-interface",
        "list-types", "namespace-tree", "analyze-file"
    };

    static async Task<int> Main(string[] args)
    {
        // Determine mode based on arguments:
        // - If a known command is present → CLI mode
        // - If only global options or no args → MCP Server mode
        var mode = DetectMode(args);

        if (mode == ExecutionMode.McpServer)
        {
            return await RunMcpServerAsync(args);
        }
        else
        {
            return await RunCliAsync(args);
        }
    }

    private static ExecutionMode DetectMode(string[] args)
    {
        // No args → MCP Server mode
        if (args.Length == 0)
        {
            return ExecutionMode.McpServer;
        }

        // Check for help flags → CLI mode
        if (args.Any(a => a == "--help" || a == "-h" || a == "-?"))
        {
            return ExecutionMode.Cli;
        }

        // Check if any arg is a known command
        foreach (var arg in args)
        {
            if (CommandNames.Contains(arg))
            {
                return ExecutionMode.Cli;
            }
        }

        // If no known command is found, assume MCP Server mode
        // This handles cases like: --solution path/to/solution.sln
        // where the path doesn't start with - but is not a command
        return ExecutionMode.McpServer;
    }

    private static async Task<int> RunMcpServerAsync(string[] args)
    {
        // Parse solution option from args
        string? solutionPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--solution" || args[i] == "-s") && i + 1 < args.Length)
            {
                solutionPath = args[i + 1];
                break;
            }
        }

        try
        {
            await McpServerHost.RunAsync(solutionPath);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[MCP Server] Fatal error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunCliAsync(string[] args)
    {
        // Build root command
        var rootCommand = new RootCommand("C# MCP - Roslyn-powered code analysis MCP server and CLI tool");

        // Add global options
        rootCommand.AddGlobalOption(GlobalOptions.SolutionOption);
        rootCommand.AddGlobalOption(GlobalOptions.OutputOption);
        rootCommand.AddGlobalOption(GlobalOptions.VerboseOption);

        // Create command context
        await using var context = new CommandContext();

        // Register all 18 commands
        var commandHandlers = new ICommandHandler[]
        {
            // Symbol commands (5)
            new FindDefinitionCommand(),
            new FindReferencesCommand(),
            new SignatureCommand(),
            new ListMembersCommand(),
            new RenameCommand(),

            // Compilation commands (2)
            new DiagnosticsCommand(),
            new CheckSymbolExistsCommand(),

            // Type hierarchy commands (2)
            new FindImplementationsCommand(),
            new InheritanceTreeCommand(),

            // Call analysis commands (2)
            new FindCallersCommand(),
            new FindCalleesCommand(),

            // Dependency commands (2)
            new DependenciesCommand(),
            new UnusedCodeCommand(),

            // Code generation commands (2)
            new GenerateInterfaceCommand(),
            new ImplementInterfaceCommand(),

            // Organization commands (3)
            new ListTypesCommand(),
            new NamespaceTreeCommand(),
            new AnalyzeFileCommand(),
        };

        foreach (var handler in commandHandlers)
        {
            rootCommand.AddCommand(handler.BuildCommand(context));
        }

        // Parse and invoke
        return await rootCommand.InvokeAsync(args);
    }

    private enum ExecutionMode
    {
        McpServer,
        Cli
    }
}
