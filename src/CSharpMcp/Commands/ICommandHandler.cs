using System.CommandLine;
using CSharpMcp.Core;

namespace CSharpMcp.Commands;

/// <summary>
/// Interface for command handlers that build System.CommandLine commands.
/// </summary>
public interface ICommandHandler
{
    /// <summary>
    /// The name of the command as it appears on the command line.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description of what the command does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Builds the System.CommandLine Command with all options and handlers.
    /// </summary>
    Command BuildCommand(CommandContext context);
}
