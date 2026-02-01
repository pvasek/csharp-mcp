using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CSharpMcp.Tests.Helpers;

/// <summary>
/// Helper class that starts the csharp-mcp executable as a child process
/// and connects via MCP client using stdio transport.
/// </summary>
public class McpTestServer : IAsyncDisposable
{
    private McpClient? _client;
    private readonly string _solutionPath;

    private McpTestServer(string solutionPath)
    {
        _solutionPath = solutionPath;
    }

    /// <summary>
    /// Starts the MCP server with the specified solution and returns a connected client.
    /// </summary>
    public static async Task<McpTestServer> StartAsync(string solutionPath)
    {
        var server = new McpTestServer(solutionPath);
        await server.InitializeAsync();
        return server;
    }

    private async Task InitializeAsync()
    {
        // Get path to the built executable
        // The test runs from bin/Debug/net10.0/, so we navigate to src/CSharpMcp/bin/Debug/net10.0/
        var exePath = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "CSharpMcp", "bin", "Debug", "net10.0", "csharp-mcp"));

        // On Windows, add .exe extension
        if (OperatingSystem.IsWindows())
        {
            exePath += ".exe";
        }

        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException(
                $"csharp-mcp executable not found at: {exePath}. " +
                "Make sure to build the project first with 'dotnet build'.");
        }

        // Create MCP client with stdio transport (starts the server process)
        var clientTransport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "csharp-mcp-test",
            Command = exePath,
            Arguments = ["--solution", _solutionPath],
        });

        _client = await McpClient.CreateAsync(clientTransport);
    }

    /// <summary>
    /// Lists all available tools from the MCP server.
    /// </summary>
    public async Task<IList<McpClientTool>> ListToolsAsync()
    {
        if (_client == null)
            throw new InvalidOperationException("Server not initialized");

        return await _client.ListToolsAsync();
    }

    /// <summary>
    /// Calls a tool with the specified name and arguments.
    /// </summary>
    public async Task<CallToolResult> CallToolAsync(string toolName, Dictionary<string, object?> arguments)
    {
        if (_client == null)
            throw new InvalidOperationException("Server not initialized");

        return await _client.CallToolAsync(toolName, arguments);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            await _client.DisposeAsync();
            _client = null;
        }
    }
}
