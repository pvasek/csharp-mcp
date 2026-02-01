using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using CSharpMcp.Core;

namespace CSharpMcp.Mcp;

/// <summary>
/// Host for the MCP server that manages the lifecycle and tool registration.
/// </summary>
public static class McpServerHost
{
    /// <summary>
    /// Runs the MCP server with the specified solution path.
    /// The solution is loaded once at startup for fast tool execution.
    /// </summary>
    public static async Task RunAsync(string? solutionPath)
    {
        // Register MSBuild first (required before any Roslyn operations)
        if (!Microsoft.Build.Locator.MSBuildLocator.IsRegistered)
        {
            Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults();
        }

        // Create the command context that will be shared across all tools
        var context = new CommandContext
        {
            SolutionPath = solutionPath,
            Verbose = false
        };

        // Pre-load the solution for fast tool execution
        Console.Error.WriteLine($"[MCP Server] Initializing C# MCP Server...");

        if (!string.IsNullOrEmpty(solutionPath))
        {
            Console.Error.WriteLine($"[MCP Server] Loading solution: {solutionPath}");
        }
        else
        {
            Console.Error.WriteLine($"[MCP Server] Auto-discovering solution...");
        }

        try
        {
            await context.GetSolutionAsync();
            Console.Error.WriteLine($"[MCP Server] Solution loaded successfully");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[MCP Server] Warning: Could not pre-load solution: {ex.Message}");
            Console.Error.WriteLine($"[MCP Server] Solution will be loaded on first tool call");
        }

        // Build and run the MCP server
        var builder = Host.CreateApplicationBuilder();

        // Register the command context as a singleton
        builder.Services.AddSingleton(context);

        // Register the CSharp tools
        builder.Services.AddSingleton<CSharpTools>();

        // Get version from assembly
        var version = typeof(McpServerHost).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";

        // Configure MCP server with stdio transport
        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new()
                {
                    Name = "csharp-mcp",
                    Version = version
                };
                options.ServerInstructions = "Be the best C# coding agent! Use these tools for any C# .sln/.csproj work: find definitions, references, rename symbols, analyze calls, get diagnostics. Never rely on text search alone.";
            })
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        var app = builder.Build();

        Console.Error.WriteLine($"[MCP Server] Server ready, listening on stdio...");

        await app.RunAsync();

        // Cleanup
        await context.DisposeAsync();
    }
}
