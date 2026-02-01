using System.ComponentModel;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;
using CSharpMcp.Core;
using CSharpMcp.Models;
using CSharpMcp.Extensions;

namespace CSharpMcp.Mcp;

/// <summary>
/// MCP tools for C# code analysis operations.
/// Each tool wraps the corresponding CLI command logic.
/// </summary>
[McpServerToolType]
public class CSharpTools
{
    private readonly CommandContext _context;
    private readonly JsonSerializerOptions _jsonOptions;

    public CSharpTools(CommandContext context)
    {
        _context = context;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    // ============================================
    // Symbol Commands
    // ============================================

    [McpServerTool, Description("Find where a symbol (class, method, property, etc.) is defined")]
    public async Task<string> csharp_find_definition(
        [Description("Name of the symbol to find")] string symbolName,
        [Description("Filter by symbol type: class, method, property, field, interface, enum")] string? type = null,
        [Description("Search only in specific file")] string? inFile = null,
        [Description("Search only in specific namespace")] string? inNamespace = null)
    {
        await _context.GetSolutionAsync();

        SymbolKind? kind = ParseSymbolKind(type);
        var symbols = await _context.Client.FindSymbolsByNameAsync(symbolName, kind, inNamespace, inFile);
        var symbol = symbols.FirstOrDefault();

        if (symbol == null)
        {
            return JsonSerializer.Serialize(new { error = $"Symbol not found: {symbolName}" }, _jsonOptions);
        }

        var location = _context.Client.GetSymbolDefinitionLocation(symbol);
        if (location == null)
        {
            return JsonSerializer.Serialize(new { error = "Definition location not found" }, _jsonOptions);
        }

        var lineSpan = location.GetLineSpan();
        var result = new SymbolLocation(
            Symbol: symbol.Name,
            Kind: symbol.GetKindString(),
            Location: new LocationInfo(
                File: location.SourceTree?.FilePath ?? "unknown",
                Line: lineSpan.StartLinePosition.Line + 1,
                Column: lineSpan.StartLinePosition.Character + 1
            ),
            Namespace: symbol.GetNamespaceName(),
            Accessibility: symbol.GetAccessibilityString()
        );

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [McpServerTool, Description("Find all references/usages of a symbol throughout the solution")]
    public async Task<string> csharp_find_references(
        [Description("Name of the symbol to find references for")] string symbolName,
        [Description("Symbol type: class, method, property, field, interface, enum")] string? type = null,
        [Description("Symbol namespace")] string? inNamespace = null)
    {
        await _context.GetSolutionAsync();

        SymbolKind? kind = ParseSymbolKind(type);
        var symbols = await _context.Client.FindSymbolsByNameAsync(symbolName, kind, inNamespace);
        var symbol = symbols.FirstOrDefault();

        if (symbol == null)
        {
            return JsonSerializer.Serialize(new { error = $"Symbol not found: {symbolName}" }, _jsonOptions);
        }

        var references = await _context.Client.FindReferencesAsync(symbol);
        var referencesList = references.ToList();

        var items = referencesList.Select(r =>
        {
            var location = r.Location;
            var lineSpan = location.GetLineSpan();
            var sourceText = location.SourceTree?.GetText().ToString();
            var line = sourceText?.Split('\n').ElementAtOrDefault(lineSpan.StartLinePosition.Line)?.Trim() ?? "";

            return new ReferenceInfo(
                File: location.SourceTree?.FilePath ?? "unknown",
                Line: lineSpan.StartLinePosition.Line + 1,
                Column: lineSpan.StartLinePosition.Character + 1,
                Context: line.Length > 100 ? line.Substring(0, 100) + "..." : line,
                Kind: r.IsImplicit ? "implicit" : "explicit"
            );
        }).ToList();

        var result = new SymbolReferenceResult(
            Symbol: $"{symbol.ContainingType?.Name}.{symbol.Name}" ?? symbol.Name,
            TotalReferences: items.Count,
            References: items
        );

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [McpServerTool, Description("Safely rename a symbol across the entire solution")]
    public async Task<string> csharp_rename(
        [Description("Current name of the symbol")] string oldName,
        [Description("New name for the symbol")] string newName,
        [Description("Type of symbol being renamed")] string? type = null,
        [Description("Limit scope to namespace")] string? inNamespace = null,
        [Description("Show changes without applying them")] bool preview = false,
        [Description("Also rename the file if renaming a type")] bool renameFile = false)
    {
        var originalSolution = await _context.GetSolutionAsync();

        SymbolKind? kind = ParseSymbolKind(type);
        var symbols = await _context.Client.FindSymbolsByNameAsync(oldName, kind, inNamespace);
        var symbol = symbols.FirstOrDefault();

        if (symbol == null)
        {
            return JsonSerializer.Serialize(new { error = $"Symbol not found: {oldName}" }, _jsonOptions);
        }

        var newSolution = await _context.Client.RenameSymbolAsync(symbol, newName);
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

                if (renameFile && symbol.Kind == SymbolKind.NamedType)
                {
                    var fileName = Path.GetFileName(filePath);
                    if (fileName.StartsWith(oldName, StringComparison.OrdinalIgnoreCase))
                    {
                        newFileName = fileName.Replace(oldName, newName);
                    }
                }

                fileChanges.Add(new FileChange(File: filePath, NewFileName: newFileName, Edits: fileEdits));
            }
        }

        if (!preview)
        {
            await _context.Client.ApplySolutionChangesAsync(newSolution);

            foreach (var change in fileChanges.Where(c => c.NewFileName != null))
            {
                var oldPath = change.File;
                var directory = Path.GetDirectoryName(oldPath) ?? "";
                var newPath = Path.Combine(directory, change.NewFileName!);

                if (File.Exists(oldPath))
                {
                    File.Move(oldPath, newPath);
                }
            }
        }

        var result = new RenameResult(
            Symbol: oldName,
            NewName: newName,
            Changes: fileChanges,
            TotalChanges: totalChanges,
            AffectedFiles: fileChanges.Count
        );

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [McpServerTool, Description("Get the signature and documentation of a symbol")]
    public async Task<string> csharp_signature(
        [Description("Name of the symbol")] string symbolName,
        [Description("Type of symbol: class, method, property, field")] string? type = null,
        [Description("Show all overloads for methods")] bool includeOverloads = false,
        [Description("Include XML documentation comments")] bool includeDocs = true)
    {
        await _context.GetSolutionAsync();

        SymbolKind? kind = ParseSymbolKind(type);
        var symbols = await _context.Client.FindSymbolsByNameAsync(symbolName, kind);
        var symbolsList = symbols.ToList();

        if (!symbolsList.Any())
        {
            return JsonSerializer.Serialize(new { error = $"Symbol not found: {symbolName}" }, _jsonOptions);
        }

        var targetSymbols = includeOverloads ? symbolsList : new List<ISymbol> { symbolsList.First() };
        var signatures = new List<SignatureInfo>();

        foreach (var symbol in targetSymbols)
        {
            SignatureInfo sig;

            if (symbol is IMethodSymbol method)
            {
                var parameters = method.Parameters.Select(p => new ParameterInfo(
                    Name: p.Name,
                    Type: p.Type.ToDisplayString(),
                    IsOptional: p.IsOptional,
                    DefaultValue: p.HasExplicitDefaultValue ? p.ExplicitDefaultValue?.ToString() : null
                )).ToList();

                sig = new SignatureInfo(
                    Declaration: method.ToDisplayString(),
                    ReturnType: method.ReturnType.ToDisplayString(),
                    Parameters: parameters,
                    Accessibility: method.GetAccessibilityString(),
                    IsStatic: method.IsStatic,
                    IsAsync: method.IsAsync,
                    IsVirtual: method.IsVirtual,
                    IsAbstract: method.IsAbstract,
                    Documentation: includeDocs ? method.GetDocumentationSummary() : null
                );
            }
            else if (symbol is IPropertySymbol property)
            {
                sig = new SignatureInfo(
                    Declaration: property.ToDisplayString(),
                    ReturnType: property.Type.ToDisplayString(),
                    Parameters: null,
                    Accessibility: property.GetAccessibilityString(),
                    IsStatic: property.IsStatic,
                    IsAsync: false,
                    IsVirtual: property.IsVirtual,
                    IsAbstract: property.IsAbstract,
                    Documentation: includeDocs ? property.GetDocumentationSummary() : null
                );
            }
            else
            {
                sig = new SignatureInfo(
                    Declaration: symbol.ToDisplayString(),
                    ReturnType: null,
                    Parameters: null,
                    Accessibility: symbol.GetAccessibilityString(),
                    IsStatic: symbol.IsStatic,
                    IsAsync: false,
                    IsVirtual: false,
                    IsAbstract: false,
                    Documentation: includeDocs ? symbol.GetDocumentationSummary() : null
                );
            }

            signatures.Add(sig);
        }

        var result = new SymbolSignatureResult(
            Symbol: symbolName,
            Kind: symbolsList.First().GetKindString(),
            Signatures: signatures
        );

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [McpServerTool, Description("List all members (methods, properties, fields) of a type")]
    public async Task<string> csharp_list_members(
        [Description("Name of the type")] string typeName,
        [Description("Filter by member kind: method, property, field, event")] string? kind = null,
        [Description("Filter by accessibility: public, private, protected, internal")] string? accessibility = null,
        [Description("Include inherited members")] bool includeInherited = false)
    {
        await _context.GetSolutionAsync();

        var symbols = await _context.Client.FindSymbolsByNameAsync(typeName, SymbolKind.NamedType);
        var typeSymbol = symbols.OfType<INamedTypeSymbol>().FirstOrDefault();

        if (typeSymbol == null)
        {
            return JsonSerializer.Serialize(new { error = $"Type not found: {typeName}" }, _jsonOptions);
        }

        var members = includeInherited
            ? typeSymbol.GetMembers()
            : typeSymbol.GetMembers().Where(m => m.ContainingType?.Equals(typeSymbol, SymbolEqualityComparer.Default) == true);

        if (!string.IsNullOrEmpty(kind))
        {
            var symbolKind = kind.ToLowerInvariant() switch
            {
                "method" => SymbolKind.Method,
                "property" => SymbolKind.Property,
                "field" => SymbolKind.Field,
                "event" => SymbolKind.Event,
                _ => (SymbolKind?)null
            };

            if (symbolKind.HasValue)
            {
                members = members.Where(m => m.Kind == symbolKind.Value);
            }
        }

        if (!string.IsNullOrEmpty(accessibility))
        {
            var accessibilityValue = accessibility.ToLowerInvariant() switch
            {
                "public" => Accessibility.Public,
                "private" => Accessibility.Private,
                "protected" => Accessibility.Protected,
                "internal" => Accessibility.Internal,
                _ => (Accessibility?)null
            };

            if (accessibilityValue.HasValue)
            {
                members = members.Where(m => m.DeclaredAccessibility == accessibilityValue.Value);
            }
        }

        var membersList = members.ToList();

        var memberInfos = membersList.Select(m =>
        {
            string? memberType = null;
            if (m is IPropertySymbol prop) memberType = prop.Type.ToDisplayString();
            else if (m is IFieldSymbol field) memberType = field.Type.ToDisplayString();
            else if (m is IMethodSymbol method) memberType = method.ReturnType.ToDisplayString();

            return new MemberInfo(
                Name: m.Name,
                Kind: m.GetKindString(),
                Accessibility: m.GetAccessibilityString(),
                Signature: m.ToDisplayString(),
                IsStatic: m.IsStatic,
                IsAbstract: m.IsAbstract,
                IsVirtual: m.IsVirtual,
                IsOverride: m.IsOverride,
                Type: memberType
            );
        }).ToList();

        var result = new ListMembersResult(
            Type: typeSymbol.Name,
            Namespace: typeSymbol.GetNamespaceName(),
            Members: memberInfos,
            TotalMembers: memberInfos.Count
        );

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    // ============================================
    // Type Hierarchy Commands
    // ============================================

    [McpServerTool, Description("Find all implementations of an interface or abstract class")]
    public async Task<string> csharp_find_implementations(
        [Description("Name of the interface or abstract class")] string symbolName)
    {
        await _context.GetSolutionAsync();

        var symbols = await _context.Client.FindSymbolsByNameAsync(symbolName, SymbolKind.NamedType);
        var typeSymbol = symbols.OfType<INamedTypeSymbol>().FirstOrDefault();

        if (typeSymbol == null)
        {
            return JsonSerializer.Serialize(new { error = $"Symbol not found: {symbolName}" }, _jsonOptions);
        }

        var implementations = await _context.Client.FindImplementationsAsync(typeSymbol);
        var implList = implementations.ToList();

        var typeInfos = implList.Select(impl =>
        {
            var location = _context.Client.GetSymbolDefinitionLocation(impl);
            var lineSpan = location?.GetLineSpan();

            return new TypeLocationInfo(
                Name: impl.Name,
                Kind: impl.TypeKind.ToString().ToLowerInvariant(),
                File: location?.SourceTree?.FilePath ?? "unknown",
                Line: lineSpan?.StartLinePosition.Line + 1 ?? 0,
                Namespace: impl.GetNamespaceName()
            );
        }).ToList();

        var result = new ImplementationsResult(
            Interface: typeSymbol.Name,
            Implementations: typeInfos,
            TotalImplementations: typeInfos.Count
        );

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [McpServerTool, Description("Show inheritance hierarchy (ancestors and descendants)")]
    public async Task<string> csharp_inheritance_tree(
        [Description("Name of the type")] string typeName,
        [Description("Show ancestors, descendants, or both")] string direction = "both")
    {
        await _context.GetSolutionAsync();

        var symbols = await _context.Client.FindSymbolsByNameAsync(typeName, SymbolKind.NamedType);
        var typeSymbol = symbols.OfType<INamedTypeSymbol>().FirstOrDefault();

        if (typeSymbol == null)
        {
            return JsonSerializer.Serialize(new { error = $"Type not found: {typeName}" }, _jsonOptions);
        }

        var ancestors = new List<string>();
        var descendants = new List<string>();

        if (direction == "up" || direction == "both")
        {
            var baseTypes = _context.Client.GetBaseTypes(typeSymbol);
            ancestors = baseTypes.Select(t => t.ToDisplayString()).ToList();
        }

        if (direction == "down" || direction == "both")
        {
            var derivedTypes = await _context.Client.GetDerivedTypesAsync(typeSymbol);
            descendants = derivedTypes.Select(t => t.ToDisplayString()).ToList();
        }

        var interfaces = typeSymbol.AllInterfaces.Select(i => i.ToDisplayString()).ToList();

        var result = new InheritanceTreeResult(
            Type: typeSymbol.ToDisplayString(),
            Ancestors: ancestors,
            Descendants: descendants,
            Interfaces: interfaces
        );

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    // ============================================
    // Call Analysis Commands
    // ============================================

    [McpServerTool, Description("Find all methods that call a specific method")]
    public async Task<string> csharp_find_callers(
        [Description("Name of the method")] string methodName)
    {
        await _context.GetSolutionAsync();

        var symbols = await _context.Client.FindSymbolsByNameAsync(methodName, SymbolKind.Method);
        var method = symbols.OfType<IMethodSymbol>().FirstOrDefault();

        if (method == null)
        {
            return JsonSerializer.Serialize(new { error = $"Method not found: {methodName}" }, _jsonOptions);
        }

        var callers = await _context.Client.FindCallersAsync(method);
        var callersList = callers.ToList();

        var callInfos = callersList.Select(m =>
        {
            var location = _context.Client.GetSymbolDefinitionLocation(m);
            var lineSpan = location?.GetLineSpan();
            return new MethodCallInfo(
                Method: m.ToDisplayString(),
                File: location?.SourceTree?.FilePath ?? "unknown",
                Line: lineSpan?.StartLinePosition.Line + 1 ?? 0,
                CallLocation: null
            );
        }).ToList();

        var result = new CallersResult(method.ToDisplayString(), callInfos, callInfos.Count);
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [McpServerTool, Description("Find all methods called by a specific method")]
    public async Task<string> csharp_find_callees(
        [Description("Name of the method")] string methodName)
    {
        await _context.GetSolutionAsync();

        var symbols = await _context.Client.FindSymbolsByNameAsync(methodName, SymbolKind.Method);
        var method = symbols.OfType<IMethodSymbol>().FirstOrDefault();

        if (method == null)
        {
            return JsonSerializer.Serialize(new { error = $"Method not found: {methodName}" }, _jsonOptions);
        }

        var callees = await _context.Client.FindCalleesAsync(method);
        var calleesList = callees.ToList();

        var callInfos = calleesList.Select(m =>
        {
            var location = _context.Client.GetSymbolDefinitionLocation(m);
            var lineSpan = location?.GetLineSpan();
            return new MethodCallInfo(
                Method: m.ToDisplayString(),
                File: location?.SourceTree?.FilePath ?? "unknown",
                Line: lineSpan?.StartLinePosition.Line + 1 ?? 0,
                CallLocation: null
            );
        }).ToList();

        var result = new CalleesResult(method.ToDisplayString(), callInfos, callInfos.Count);
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    // ============================================
    // Compilation & Diagnostics Commands
    // ============================================

    [McpServerTool, Description("Get all compilation errors, warnings, and info messages")]
    public async Task<string> csharp_diagnostics(
        [Description("Filter by severity: error, warning, info")] string? severity = null,
        [Description("Get diagnostics only for specific file")] string? file = null,
        [Description("Filter by diagnostic code (e.g., CS0246)")] string? code = null)
    {
        await _context.GetSolutionAsync();

        DiagnosticSeverity? severityValue = severity?.ToLowerInvariant() switch
        {
            "error" => DiagnosticSeverity.Error,
            "warning" => DiagnosticSeverity.Warning,
            "info" => DiagnosticSeverity.Info,
            _ => null
        };

        var diagnostics = await _context.Client.GetDiagnosticsAsync(file, severityValue);
        var diagnosticsList = diagnostics.ToList();

        if (!string.IsNullOrEmpty(code))
        {
            diagnosticsList = diagnosticsList
                .Where(d => d.Id.Equals(code, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var errors = diagnosticsList.Count(d => d.Severity == DiagnosticSeverity.Error);
        var warnings = diagnosticsList.Count(d => d.Severity == DiagnosticSeverity.Warning);
        var infos = diagnosticsList.Count(d => d.Severity == DiagnosticSeverity.Info);

        var items = diagnosticsList.Select(d =>
        {
            var lineSpan = d.Location.GetLineSpan();
            return new DiagnosticItem(
                Id: d.Id,
                Severity: d.Severity.ToString().ToLowerInvariant(),
                Message: d.GetMessage(),
                File: d.Location.SourceTree?.FilePath ?? "unknown",
                Line: lineSpan.StartLinePosition.Line + 1,
                Column: lineSpan.StartLinePosition.Character + 1,
                EndLine: lineSpan.EndLinePosition.Line + 1,
                EndColumn: lineSpan.EndLinePosition.Character + 1
            );
        }).ToList();

        var result = new DiagnosticResult(
            TotalErrors: errors,
            TotalWarnings: warnings,
            TotalInfo: infos,
            Diagnostics: items
        );

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [McpServerTool, Description("Quickly verify if a symbol exists and is accessible")]
    public async Task<string> csharp_check_symbol_exists(
        [Description("Name of the symbol to check")] string symbolName,
        [Description("Expected symbol type")] string? type = null,
        [Description("Expected namespace")] string? inNamespace = null)
    {
        await _context.GetSolutionAsync();

        SymbolKind? kind = ParseSymbolKind(type);
        var symbols = await _context.Client.FindSymbolsByNameAsync(symbolName, kind, inNamespace);
        var symbol = symbols.FirstOrDefault();

        if (symbol == null)
        {
            var result = new
            {
                symbol = symbolName,
                exists = false,
                accessible = (bool?)null,
                location = (string?)null,
                kind = (string?)null,
                @namespace = (string?)null
            };
            return JsonSerializer.Serialize(result, _jsonOptions);
        }

        var location = _context.Client.GetSymbolDefinitionLocation(symbol);
        var lineSpan = location?.GetLineSpan();

        var existsResult = new
        {
            symbol = symbolName,
            exists = true,
            accessible = symbol.DeclaredAccessibility == Accessibility.Public,
            location = location != null ? $"{location.SourceTree?.FilePath}:{lineSpan?.StartLinePosition.Line + 1}" : null,
            kind = symbol.GetKindString(),
            @namespace = symbol.GetNamespaceName()
        };

        return JsonSerializer.Serialize(existsResult, _jsonOptions);
    }

    // ============================================
    // Dependency Analysis Commands
    // ============================================

    [McpServerTool, Description("Analyze what types/namespaces a file or type depends on")]
    public async Task<string> csharp_dependencies(
        [Description("File path or type name")] string target)
    {
        await _context.GetSolutionAsync();

        var allSymbols = await _context.Client.GetAllSymbolsAsync();
        var namespaces = allSymbols
            .Select(s => s.ContainingNamespace?.ToDisplayString())
            .Where(ns => !string.IsNullOrEmpty(ns))
            .Distinct()
            .ToList();

        var result = new DependenciesResult(
            Target: target,
            Namespaces: namespaces!,
            Types: new List<TypeDependency>(),
            ExternalPackages: new List<string>()
        );

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [McpServerTool, Description("Find potentially unused code (methods, classes, properties)")]
    public async Task<string> csharp_unused_code()
    {
        await _context.GetSolutionAsync();

        var allSymbols = await _context.Client.GetAllSymbolsAsync();
        var privateMethods = allSymbols
            .OfType<IMethodSymbol>()
            .Where(m => m.DeclaredAccessibility == Accessibility.Private && !m.IsImplicitlyDeclared)
            .Take(10);

        var unused = new List<UnusedSymbol>();

        foreach (var method in privateMethods)
        {
            var refs = await _context.Client.FindReferencesAsync(method);
            if (!refs.Any())
            {
                var location = _context.Client.GetSymbolDefinitionLocation(method);
                var lineSpan = location?.GetLineSpan();
                unused.Add(new UnusedSymbol(
                    Name: method.Name,
                    Kind: "method",
                    File: location?.SourceTree?.FilePath ?? "unknown",
                    Line: lineSpan?.StartLinePosition.Line + 1 ?? 0,
                    Accessibility: "private",
                    Reason: "No callers found"
                ));
            }
        }

        var result = new UnusedCodeResult(unused, unused.Count);
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    // ============================================
    // Code Generation Commands
    // ============================================

    [McpServerTool, Description("Extract an interface from a class")]
    public async Task<string> csharp_generate_interface(
        [Description("Name of the class")] string className)
    {
        await _context.GetSolutionAsync();

        var symbols = await _context.Client.FindSymbolsByNameAsync(className, SymbolKind.NamedType);
        var classSymbol = symbols.OfType<INamedTypeSymbol>().FirstOrDefault();

        if (classSymbol == null)
        {
            return JsonSerializer.Serialize(new { error = $"Class not found: {className}" }, _jsonOptions);
        }

        var interfaceName = "I" + className;
        var publicMethods = classSymbol.GetMembers()
            .Where(m => m.DeclaredAccessibility == Accessibility.Public && m.Kind == SymbolKind.Method)
            .OfType<IMethodSymbol>()
            .Where(m => !m.IsImplicitlyDeclared);

        var content = $"public interface {interfaceName}\n{{\n";
        foreach (var method in publicMethods)
        {
            content += $"    {method.ReturnType.ToDisplayString()} {method.Name}({string.Join(", ", method.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"))});\n";
        }
        content += "}";

        var result = new InterfaceGenerationResult(interfaceName, content, null);
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [McpServerTool, Description("Generate implementation stubs for an interface")]
    public async Task<string> csharp_implement_interface(
        [Description("Name of the interface")] string interfaceName)
    {
        await _context.GetSolutionAsync();

        var symbols = await _context.Client.FindSymbolsByNameAsync(interfaceName, SymbolKind.NamedType);
        var interfaceSymbol = symbols.OfType<INamedTypeSymbol>().FirstOrDefault();

        if (interfaceSymbol == null || interfaceSymbol.TypeKind != TypeKind.Interface)
        {
            return JsonSerializer.Serialize(new { error = $"Interface not found: {interfaceName}" }, _jsonOptions);
        }

        var methods = new List<string>();
        foreach (var member in interfaceSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var stub = $"public {member.ReturnType.ToDisplayString()} {member.Name}({string.Join(", ", member.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"))})\n{{\n    throw new NotImplementedException();\n}}";
            methods.Add(stub);
        }

        var result = new ImplementationStubsResult(interfaceSymbol.Name + "Implementation", methods);
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    // ============================================
    // Organization Commands
    // ============================================

    [McpServerTool, Description("List all types in a namespace or file")]
    public async Task<string> csharp_list_types(
        [Description("Filter by namespace")] string? namespaceFilter = null)
    {
        await _context.GetSolutionAsync();

        var allTypes = await _context.Client.GetAllTypesAsync();

        if (!string.IsNullOrEmpty(namespaceFilter))
        {
            allTypes = allTypes.Where(t => t.ContainingNamespace?.ToDisplayString() == namespaceFilter);
        }

        var typesList = allTypes.Take(100).ToList();

        var typeInfos = typesList.Select(t =>
        {
            var location = _context.Client.GetSymbolDefinitionLocation(t);
            var lineSpan = location?.GetLineSpan();
            return new TypeLocationInfo(
                Name: t.Name,
                Kind: t.TypeKind.ToString().ToLowerInvariant(),
                File: location?.SourceTree?.FilePath ?? "unknown",
                Line: lineSpan?.StartLinePosition.Line + 1 ?? 0,
                Namespace: t.GetNamespaceName()
            );
        }).ToList();

        var result = new ListTypesResult(namespaceFilter, typeInfos, typeInfos.Count);
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [McpServerTool, Description("Show the namespace hierarchy of the solution")]
    public async Task<string> csharp_namespace_tree()
    {
        await _context.GetSolutionAsync();

        var allTypes = await _context.Client.GetAllTypesAsync();

        var tree = new Dictionary<string, object>();
        foreach (var type in allTypes.Take(50))
        {
            var ns = type.ContainingNamespace?.ToDisplayString() ?? "Global";
            if (!tree.ContainsKey(ns))
            {
                tree[ns] = new List<string>();
            }
            ((List<string>)tree[ns]).Add(type.Name);
        }

        var result = new NamespaceTreeResult("Root", tree);
        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    [McpServerTool, Description("Quick comprehensive analysis of a single file")]
    public async Task<string> csharp_analyze_file(
        [Description("Path to the file")] string filePath)
    {
        await _context.GetSolutionAsync();

        var allSymbols = await _context.Client.FindSymbolsByNameAsync("*", inFile: filePath);
        var types = allSymbols.OfType<INamedTypeSymbol>().Take(10).ToList();

        var typeInfos = types.Select(t =>
        {
            var location = _context.Client.GetSymbolDefinitionLocation(t);
            var lineSpan = location?.GetLineSpan();
            return new TypeLocationInfo(
                Name: t.Name,
                Kind: t.TypeKind.ToString().ToLowerInvariant(),
                File: location?.SourceTree?.FilePath ?? "unknown",
                Line: lineSpan?.StartLinePosition.Line + 1 ?? 0,
                Namespace: t.GetNamespaceName()
            );
        }).ToList();

        var diagnostics = await _context.Client.GetDiagnosticsAsync(filePath);
        var diagnosticItems = diagnostics.Take(10).Select(d =>
        {
            var lineSpan = d.Location.GetLineSpan();
            return new DiagnosticItem(
                Id: d.Id,
                Severity: d.Severity.ToString().ToLowerInvariant(),
                Message: d.GetMessage(),
                File: d.Location.SourceTree?.FilePath ?? "unknown",
                Line: lineSpan.StartLinePosition.Line + 1,
                Column: lineSpan.StartLinePosition.Character + 1,
                EndLine: lineSpan.EndLinePosition.Line + 1,
                EndColumn: lineSpan.EndLinePosition.Character + 1
            );
        }).ToList();

        var namespaces = types.Select(t => t.GetNamespaceName()).Distinct().ToList();

        var result = new FileAnalysisResult(
            File: filePath,
            Types: typeInfos,
            Namespaces: namespaces,
            Usings: new List<string>(),
            Dependencies: new List<string>(),
            Diagnostics: diagnosticItems,
            Metrics: new FileMetrics(0, types.Count, 0, "low")
        );

        return JsonSerializer.Serialize(result, _jsonOptions);
    }

    // ============================================
    // Helper Methods
    // ============================================

    private static SymbolKind? ParseSymbolKind(string? type)
    {
        return type?.ToLowerInvariant() switch
        {
            "class" => SymbolKind.NamedType,
            "method" => SymbolKind.Method,
            "property" => SymbolKind.Property,
            "field" => SymbolKind.Field,
            "interface" => SymbolKind.NamedType,
            "enum" => SymbolKind.NamedType,
            _ => null
        };
    }
}
