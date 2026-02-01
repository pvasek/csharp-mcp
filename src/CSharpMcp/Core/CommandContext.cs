using Microsoft.CodeAnalysis;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace CSharpMcp.Core;

/// <summary>
/// Shared context for all commands, providing access to the Roslyn API client
/// and global options.
/// </summary>
public class CommandContext : IAsyncDisposable
{
    private RoslynApiClient? _client;
    private Solution? _loadedSolution;

    public string? SolutionPath { get; set; }
    public OutputFormat OutputFormat { get; set; } = OutputFormat.Json;
    public bool Verbose { get; set; }
    public OutputFormatter Formatter { get; } = new();

    /// <summary>
    /// Gets the Roslyn API client, creating it if necessary.
    /// </summary>
    public RoslynApiClient Client
    {
        get
        {
            _client ??= new RoslynApiClient();
            return _client;
        }
    }

    /// <summary>
    /// Gets the loaded solution, loading it if necessary.
    /// If no solution path is specified, attempts to auto-discover in the current directory.
    /// </summary>
    public async Task<Solution> GetSolutionAsync()
    {
        if (_loadedSolution != null)
        {
            return _loadedSolution;
        }

        string? path = SolutionPath;

        // If no path specified, try to auto-discover
        if (string.IsNullOrEmpty(path))
        {
            LogVerbose("No solution specified, attempting auto-discovery...");

            path = SolutionDiscovery.FindSolution();

            if (string.IsNullOrEmpty(path))
            {
                var currentDir = Directory.GetCurrentDirectory();
                var message = SolutionDiscovery.GetDiscoveryMessage(currentDir);
                throw new InvalidOperationException(message);
            }

            if (Verbose)
            {
                Console.Error.WriteLine($"Auto-discovered: {path}");
            }
        }

        if (Verbose)
        {
            Console.Error.WriteLine($"Loading: {path}");
        }

        _loadedSolution = await Client.OpenSolutionAsync(path);
        return _loadedSolution;
    }

    /// <summary>
    /// Logs a message to stderr if verbose mode is enabled.
    /// </summary>
    public void LogVerbose(string message)
    {
        if (Verbose)
        {
            Console.Error.WriteLine($"[Verbose] {message}");
        }
    }

    /// <summary>
    /// Initializes the context with global options from the command invocation.
    /// Call this at the start of every command handler.
    /// </summary>
    public void InitializeFromGlobalOptions(string? solution, OutputFormat output, bool verbose)
    {
        SolutionPath = solution;
        OutputFormat = output;
        Verbose = verbose;
    }

    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            await _client.DisposeAsync();
        }
    }
}
