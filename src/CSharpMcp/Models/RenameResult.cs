namespace CSharpMcp.Models;

/// <summary>
/// A single edit to be made in a file.
/// </summary>
public record FileEdit(
    int Line,
    string Old,
    string New
);

/// <summary>
/// Changes to a single file.
/// </summary>
public record FileChange(
    string File,
    string? NewFileName,
    List<FileEdit> Edits
);

/// <summary>
/// Result of a rename operation (preview or actual).
/// </summary>
public record RenameResult(
    string Symbol,
    string NewName,
    List<FileChange> Changes,
    int TotalChanges,
    int AffectedFiles
);
