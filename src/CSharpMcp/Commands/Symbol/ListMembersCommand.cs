using System.CommandLine;
using Microsoft.CodeAnalysis;
using CSharpMcp.Core;
using CSharpMcp.Models;
using CSharpMcp.Extensions;

namespace CSharpMcp.Commands.Symbol;

/// <summary>
/// Command to list all members of a type.
/// </summary>
public class ListMembersCommand : ICommandHandler
{
    public string Name => "list-members";
    public string Description => "List all members (methods, properties, fields) of a type";

    public Command BuildCommand(CommandContext context)
    {
        var command = new Command(Name, Description);

        // Arguments
        var typeNameArg = new Argument<string>("type-name", "Name of the type");
        command.AddArgument(typeNameArg);

        // Options
        var kindOption = new Option<string?>(
            aliases: ["--kind", "-k"],
            description: "Filter by member kind: method, property, field, event");

        var accessibilityOption = new Option<string?>(
            aliases: ["--accessibility", "-a"],
            description: "Filter by accessibility: public, private, protected, internal");

        var includeInheritedOption = new Option<bool>(
            aliases: ["--include-inherited"],
            getDefaultValue: () => false,
            description: "Include inherited members");

        command.AddOption(kindOption);
        command.AddOption(accessibilityOption);
        command.AddOption(includeInheritedOption);

        // Handler
        command.SetHandler(async (typeName, kind, accessibility, includeInherited, solution, output, verbose) =>
        {
            context.InitializeFromGlobalOptions(solution, output, verbose);

            try
            {
                var result = await ExecuteAsync(context, typeName, kind, accessibility, includeInherited);

                if (result == null)
                {
                    await Console.Error.WriteLineAsync($"Type not found: {typeName}");
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
        }, typeNameArg, kindOption, accessibilityOption, includeInheritedOption,
           GlobalOptions.SolutionOption, GlobalOptions.OutputOption, GlobalOptions.VerboseOption);

        return command;
    }

    private async Task<ListMembersResult?> ExecuteAsync(
        CommandContext context,
        string typeName,
        string? kindFilter,
        string? accessibilityFilter,
        bool includeInherited)
    {
        await context.GetSolutionAsync();

        context.LogVerbose($"Finding type: {typeName}");

        // Find the type
        var symbols = await context.Client.FindSymbolsByNameAsync(typeName, SymbolKind.NamedType);
        var typeSymbol = symbols.OfType<INamedTypeSymbol>().FirstOrDefault();

        if (typeSymbol == null)
        {
            return null;
        }

        // Get members
        var members = includeInherited
            ? typeSymbol.GetMembers()
            : typeSymbol.GetMembers().Where(m => m.ContainingType?.Equals(typeSymbol, SymbolEqualityComparer.Default) == true);

        // Apply kind filter
        if (!string.IsNullOrEmpty(kindFilter))
        {
            var kind = kindFilter.ToLowerInvariant() switch
            {
                "method" => SymbolKind.Method,
                "property" => SymbolKind.Property,
                "field" => SymbolKind.Field,
                "event" => SymbolKind.Event,
                _ => (SymbolKind?)null
            };

            if (kind.HasValue)
            {
                members = members.Where(m => m.Kind == kind.Value);
            }
        }

        // Apply accessibility filter
        if (!string.IsNullOrEmpty(accessibilityFilter))
        {
            var accessibility = accessibilityFilter.ToLowerInvariant() switch
            {
                "public" => Accessibility.Public,
                "private" => Accessibility.Private,
                "protected" => Accessibility.Protected,
                "internal" => Accessibility.Internal,
                _ => (Accessibility?)null
            };

            if (accessibility.HasValue)
            {
                members = members.Where(m => m.DeclaredAccessibility == accessibility.Value);
            }
        }

        var membersList = members.ToList();
        context.LogVerbose($"Found {membersList.Count} member(s)");

        // Convert to output model
        var memberInfos = membersList.Select(m =>
        {
            string? type = null;
            if (m is IPropertySymbol prop)
            {
                type = prop.Type.ToDisplayString();
            }
            else if (m is IFieldSymbol field)
            {
                type = field.Type.ToDisplayString();
            }
            else if (m is IMethodSymbol method)
            {
                type = method.ReturnType.ToDisplayString();
            }

            return new MemberInfo(
                Name: m.Name,
                Kind: m.GetKindString(),
                Accessibility: m.GetAccessibilityString(),
                Signature: m.ToDisplayString(),
                IsStatic: m.IsStatic,
                IsAbstract: m.IsAbstract,
                IsVirtual: m.IsVirtual,
                IsOverride: m.IsOverride,
                Type: type
            );
        }).ToList();

        return new ListMembersResult(
            Type: typeSymbol.Name,
            Namespace: typeSymbol.GetNamespaceName(),
            Members: memberInfos,
            TotalMembers: memberInfos.Count
        );
    }
}
