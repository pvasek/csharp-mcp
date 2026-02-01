# C# MCP - Roslyn-Powered Code Analysis MCP Server & CLI

A dual-mode tool for C# code analysis and refactoring using Roslyn APIs. Operates as an **MCP server** for AI assistants or as a traditional **CLI tool**.

## Installation

```bash
# Install as global tool (after published to NuGet)
dotnet tool install -g CSharpMcp

# Or build from source
git clone <repository-url>
cd csharp-mcp
dotnet build
```

## MCP Server Mode (Recommended)

The solution is loaded once at startup, making subsequent tool calls fast.

```bash
csharp-mcp                                    # Auto-discover solution
csharp-mcp --solution /path/to/Solution.sln   # Explicit path
```

### MCP Configuration

```json
{
  "mcpServers": {
    "csharp-mcp": {
      "command": "/path/to/csharp-mcp",
      "args": ["--solution", "/path/to/your/Solution.sln"]
    }
  }
}
```

### Available MCP Tools

| Tool | Description |
|------|-------------|
| `csharp_find_definition` | Find where a symbol is defined |
| `csharp_find_references` | Find all usages of a symbol |
| `csharp_rename` | Safely rename a symbol across the solution |
| `csharp_signature` | Get method/type signatures |
| `csharp_list_members` | List members of a type |
| `csharp_find_implementations` | Find implementations of interface/abstract class |
| `csharp_inheritance_tree` | Show inheritance hierarchy |
| `csharp_find_callers` | Find methods that call a specific method |
| `csharp_find_callees` | Find methods called by a specific method |
| `csharp_diagnostics` | Get compilation errors/warnings |
| `csharp_check_symbol_exists` | Check if a symbol exists |
| `csharp_dependencies` | Analyze type dependencies |
| `csharp_unused_code` | Find potentially unused code |
| `csharp_generate_interface` | Extract interface from class |
| `csharp_implement_interface` | Generate implementation stubs |
| `csharp_list_types` | List types in namespace/file |
| `csharp_namespace_tree` | Show namespace hierarchy |
| `csharp_analyze_file` | Comprehensive file analysis |

---

## CLI Mode

```bash
csharp-mcp [command] [options]
csharp-mcp --solution MySolution.sln find-definition UserService --type class
```

### Global Options

| Option | Description |
|--------|-------------|
| `--solution, -s` | Path to .sln file (auto-discovers if not specified) |
| `--output, -o` | Output format: json, text, markdown (default: json) |
| `--verbose, -v` | Enable verbose logging |

### Commands Reference

| Command | Options |
|---------|---------|
| `find-definition <symbol>` | `--type, -t` (class/method/property/field/interface/enum), `--in-file, -f`, `--in-namespace, -n` |
| `find-references <symbol>` | `--type, -t`, `--in-namespace, -n` |
| `rename <old> <new>` | `--type, -t`, `--in-namespace, -n`, `--preview`, `--rename-file` |
| `signature <symbol>` | `--type, -t`, `--include-overloads`, `--include-docs` |
| `list-members <type>` | `--kind, -k` (method/property/field/event), `--accessibility, -a`, `--include-inherited` |
| `find-implementations <symbol>` | - |
| `inheritance-tree <type>` | `--direction, -d` (up/down/both) |
| `find-callers <method>` | - |
| `find-callees <method>` | - |
| `diagnostics` | `--severity, -s` (error/warning/info), `--file, -f`, `--code, -c` |
| `check-symbol-exists <symbol>` | `--type, -t`, `--in-namespace, -n` |
| `dependencies <target>` | - |
| `unused-code` | - |
| `generate-interface <class>` | - |
| `implement-interface <interface>` | - |
| `list-types` | `--namespace` |
| `namespace-tree` | - |
| `analyze-file <path>` | - |

---

## Requirements

- **.NET 10.0 Runtime** - https://dotnet.microsoft.com/download/dotnet/10.0
- Solution or project file to analyze

## Technical Details

**Dependencies:** ModelContextProtocol (v0.7.0-preview.1), Microsoft.CodeAnalysis.CSharp.Workspaces (v5.0.0), Microsoft.Build.Locator (v1.8.1), System.CommandLine (v2.0.0-beta4)

See [SPEC.md](SPEC.md) for full specification.

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.
