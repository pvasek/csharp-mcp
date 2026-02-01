namespace CSharpMcp.Core;

/// <summary>
/// Standard exit codes for the CLI application.
/// </summary>
public static class ExitCodes
{
    /// <summary>
    /// Operation completed successfully.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// General error occurred (exception, invalid arguments, etc.).
    /// </summary>
    public const int Error = 1;

    /// <summary>
    /// Requested resource not found (symbol, file, etc.).
    /// </summary>
    public const int NotFound = 2;
}
