using System.CommandLine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using CSharpMcp.Core;
using CSharpMcp.Models;

namespace CSharpMcp.Commands.Symbol;

/// <summary>
/// Command to safely rename a symbol across the entire solution.
/// </summary>
public class RenameCommand : ICommandHandler
{
    public string Name => "rename";
    public string Description => "Safely rename a symbol across the entire solution";

    public Command BuildCommand(CommandContext context)
    {
        var command = new Command(Name, Description);

        // Arguments
        var oldNameArg = new Argument<string>("old-name", "Current name of the symbol");
        var newNameArg = new Argument<string>("new-name", "New name for the symbol");
        command.AddArgument(oldNameArg);
        command.AddArgument(newNameArg);

        // Options
        var typeOption = new Option<string?>(
            aliases: ["--type", "-t"],
            description: "Type of symbol being renamed");

        var inNamespaceOption = new Option<string?>(
            aliases: ["--in-namespace", "-n"],
            description: "Limit scope to namespace");

        var previewOption = new Option<bool>(
            aliases: ["--preview"],
            getDefaultValue: () => false,
            description: "Show changes without applying them");

        var renameFileOption = new Option<bool>(
            aliases: ["--rename-file"],
            getDefaultValue: () => false,
            description: "Also rename the file if renaming a type");

        command.AddOption(typeOption);
        command.AddOption(inNamespaceOption);
        command.AddOption(previewOption);
        command.AddOption(renameFileOption);

        // Handler
        command.SetHandler(async (invocationContext) =>
        {
            var oldName = invocationContext.ParseResult.GetValueForArgument(oldNameArg);
            var newName = invocationContext.ParseResult.GetValueForArgument(newNameArg);
            var type = invocationContext.ParseResult.GetValueForOption(typeOption);
            var inNamespace = invocationContext.ParseResult.GetValueForOption(inNamespaceOption);
            var preview = invocationContext.ParseResult.GetValueForOption(previewOption);
            var renameFile = invocationContext.ParseResult.GetValueForOption(renameFileOption);
            var solution = invocationContext.ParseResult.GetValueForOption(GlobalOptions.SolutionOption);
            var output = invocationContext.ParseResult.GetValueForOption(GlobalOptions.OutputOption);
            var verbose = invocationContext.ParseResult.GetValueForOption(GlobalOptions.VerboseOption);

            context.InitializeFromGlobalOptions(solution, output, verbose);

            try
            {
                var result = await ExecuteAsync(context, oldName, newName, type, inNamespace, preview, renameFile);

                if (result == null)
                {
                    await Console.Error.WriteLineAsync($"Symbol not found: {oldName}");
                    Environment.Exit(ExitCodes.NotFound);
                }

                var formattedOutput = context.Formatter.Format(result, context.OutputFormat);
                Console.WriteLine(formattedOutput);
                Environment.Exit(ExitCodes.Success);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
                if (context.Verbose)
                {
                    await Console.Error.WriteLineAsync(ex.StackTrace);
                }
                Environment.Exit(ExitCodes.Error);
            }
        });

        return command;
    }

    private async Task<RenameResult?> ExecuteAsync(
        CommandContext context,
        string oldName,
        string newName,
        string? typeFilter,
        string? inNamespace,
        bool preview,
        bool renameFile)
    {
        await context.GetSolutionAsync();

        // Parse type filter
        SymbolKind? kind = typeFilter?.ToLowerInvariant() switch
        {
            "class" => SymbolKind.NamedType,
            "method" => SymbolKind.Method,
            "property" => SymbolKind.Property,
            "field" => SymbolKind.Field,
            _ => null
        };

        context.LogVerbose($"Finding symbol to rename: {oldName}");

        // Find the symbol
        var symbols = await context.Client.FindSymbolsByNameAsync(oldName, kind, inNamespace);
        var symbol = symbols.FirstOrDefault();

        if (symbol == null)
        {
            return null;
        }

        context.LogVerbose($"Renaming '{symbol.Name}' to '{newName}'...");

        // Perform the rename
        var newSolution = await context.Client.RenameSymbolAsync(symbol, newName);

        // Get the original solution
        var originalSolution = await context.GetSolutionAsync();

        // Analyze changes
        var changes = newSolution.GetChanges(originalSolution);
        var fileChanges = new List<FileChange>();
        int totalChanges = 0;

        foreach (var projectChanges in changes.GetProjectChanges())
        {
            foreach (var documentId in projectChanges.GetChangedDocuments())
            {
                var oldDocument = originalSolution.GetDocument(documentId);
                var newDocument = newSolution.GetDocument(documentId);

                if (oldDocument == null || newDocument == null) continue;

                var oldText = await oldDocument.GetTextAsync();
                var newText = await newDocument.GetTextAsync();

                // Get line-by-line differences
                var edits = newText.GetTextChanges(oldText);
                var fileEdits = new List<FileEdit>();

                foreach (var edit in edits)
                {
                    var lineSpan = oldText.Lines.GetLinePositionSpan(edit.Span);
                    var oldLine = oldText.GetSubText(oldText.Lines[lineSpan.Start.Line].Span).ToString();
                    var newLine = edit.NewText ?? "";

                    fileEdits.Add(new FileEdit(
                        Line: lineSpan.Start.Line + 1,
                        Old: oldLine.Trim(),
                        New: newLine.Trim()
                    ));

                    totalChanges++;
                }

                var filePath = oldDocument.FilePath ?? "unknown";
                string? newFileName = null;

                // Handle file renaming for types
                if (renameFile && symbol.Kind == SymbolKind.NamedType)
                {
                    var fileName = Path.GetFileName(filePath);
                    if (fileName.StartsWith(oldName, StringComparison.OrdinalIgnoreCase))
                    {
                        newFileName = fileName.Replace(oldName, newName);
                    }
                }

                fileChanges.Add(new FileChange(
                    File: filePath,
                    NewFileName: newFileName,
                    Edits: fileEdits
                ));
            }
        }

        // Apply changes if not in preview mode
        if (!preview)
        {
            context.LogVerbose("Applying changes to disk...");
            await context.Client.ApplySolutionChangesAsync(newSolution);

            // Handle file renames
            foreach (var change in fileChanges.Where(c => c.NewFileName != null))
            {
                var oldPath = change.File;
                var directory = Path.GetDirectoryName(oldPath) ?? "";
                var newPath = Path.Combine(directory, change.NewFileName!);

                if (File.Exists(oldPath))
                {
                    File.Move(oldPath, newPath);
                    context.LogVerbose($"Renamed file: {change.NewFileName}");
                }
            }
        }

        return new RenameResult(
            Symbol: oldName,
            NewName: newName,
            Changes: fileChanges,
            TotalChanges: totalChanges,
            AffectedFiles: fileChanges.Count
        );
    }
}
