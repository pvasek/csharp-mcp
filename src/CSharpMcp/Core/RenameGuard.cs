using Microsoft.CodeAnalysis.CSharp;

namespace CSharpMcp.Core;

/// <summary>
/// Input validation shared by the rename CLI command and the MCP rename tool.
/// </summary>
public static class RenameGuard
{
    /// <summary>
    /// Throws if <paramref name="newName"/> is not a plain C# identifier.
    /// This also rules out path separators, dots and other characters that
    /// could escape the file's directory when the file is renamed.
    /// </summary>
    public static void ValidateNewName(string newName)
    {
        if (string.IsNullOrEmpty(newName)
            || !SyntaxFacts.IsValidIdentifier(newName)
            || SyntaxFacts.GetKeywordKind(newName) != SyntaxKind.None)
        {
            throw new ArgumentException($"'{newName}' is not a valid C# identifier.");
        }
    }

    /// <summary>
    /// Computes the destination path for a file rename and verifies that it stays in
    /// the original file's directory and does not overwrite another file.
    /// Call this before any changes are written so a failure leaves the disk untouched.
    /// </summary>
    public static string GetSafeRenamedPath(string oldPath, string newFileName)
    {
        if (newFileName != Path.GetFileName(newFileName))
        {
            throw new InvalidOperationException($"Refusing to rename file: '{newFileName}' is not a plain file name.");
        }

        var oldFull = Path.GetFullPath(oldPath);
        var directory = Path.GetDirectoryName(oldFull)!;
        var newFull = Path.GetFullPath(Path.Combine(directory, newFileName));

        var sameDirectory = string.Equals(
            Path.GetDirectoryName(newFull), directory, StringComparison.OrdinalIgnoreCase);
        if (!sameDirectory)
        {
            throw new InvalidOperationException($"Refusing to rename file: '{newFileName}' would leave '{directory}'.");
        }

        // Case-only renames resolve to the same file, so they are not collisions.
        var isSameFile = string.Equals(oldFull, newFull, StringComparison.OrdinalIgnoreCase);
        if (!isSameFile && (File.Exists(newFull) || Directory.Exists(newFull)))
        {
            throw new InvalidOperationException($"Refusing to rename file: '{newFull}' already exists.");
        }

        return newFull;
    }
}
