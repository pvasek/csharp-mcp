namespace CSharpMcp.Core;

/// <summary>
/// Utility class for discovering .sln files in the current directory.
/// </summary>
public static class SolutionDiscovery
{
    /// <summary>
    /// Attempts to find a solution file in the current directory.
    /// </summary>
    /// <param name="searchDirectory">Directory to search (defaults to current directory)</param>
    /// <returns>Path to the found solution file, or null if none found</returns>
    public static string? FindSolution(string? searchDirectory = null)
    {
        var directory = searchDirectory ?? Directory.GetCurrentDirectory();

        if (!Directory.Exists(directory))
        {
            return null;
        }

        var solutionFiles = Directory.GetFiles(directory, "*.sln", SearchOption.TopDirectoryOnly);

        if (solutionFiles.Length == 1)
        {
            return solutionFiles[0];
        }

        if (solutionFiles.Length > 1)
        {
            var fileNames = string.Join(", ", solutionFiles.Select(Path.GetFileName));
            throw new InvalidOperationException(
                $"Multiple solution files found in '{directory}': {fileNames}. " +
                "Please specify which one to use with --solution or -s.");
        }

        // Nothing found
        return null;
    }

    /// <summary>
    /// Gets a descriptive message about what was found or not found during discovery.
    /// </summary>
    public static string GetDiscoveryMessage(string directory)
    {
        var solutionFiles = Directory.GetFiles(directory, "*.sln", SearchOption.TopDirectoryOnly);

        if (solutionFiles.Length == 0)
        {
            return $"No .sln files found in '{directory}'. " +
                   "Please specify a solution file with --solution.";
        }

        if (solutionFiles.Length > 1)
        {
            return $"Multiple solution files found: {string.Join(", ", solutionFiles.Select(Path.GetFileName))}. " +
                   "Please specify which one to use with --solution.";
        }

        return ""; // Should not reach here if used correctly
    }
}
