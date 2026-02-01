# C# MCP - Roslyn-Powered Code Analysis MCP Server & CLI

A dual-mode tool for C# code analysis and refactoring using Roslyn APIs. Operates as an **MCP (Model Context Protocol) server** for AI assistants or as a traditional **CLI tool**. Provides fast, accurate, token-efficient operations for working with C# codebases.

## Features

- **MCP Server Mode** - Persistent server with pre-loaded solution for fast tool calls
- **CLI Mode** - Traditional command-line interface for scripting and testing
- **18 Tools/Commands** across 7 categories for comprehensive code analysis
- **Symbol-aware operations** that understand C# semantics
- **Safe refactoring** with preview mode
- **Fast and accurate** using Roslyn compiler APIs

## Installation

```bash
git clone <repository-url>
cd csharp-mcp
dotnet build
```

---

## MCP Server Mode (Recommended)

The tool operates primarily as an MCP server, providing fast, persistent access to C# code analysis. The solution is loaded once at startup, making subsequent tool calls significantly faster.

### Starting the MCP Server

```bash
# Start MCP server with auto-discovered solution
csharp-mcp

# Start MCP server with explicit solution path
csharp-mcp --solution /path/to/Solution.sln
```

### MCP Configuration

Add to your MCP client configuration:

**Claude Desktop** (`claude_desktop_config.json`):
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

**Claude Code** (`.claude/settings.json` or via MCP settings):
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

All 18 commands are exposed as MCP tools:

| Tool Name | Description |
|-----------|-------------|
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

### Performance Benefits

| Operation | CLI Mode | MCP Server Mode |
|-----------|----------|-----------------|
| Solution load | Every call (~2-10s) | Once at startup |
| First tool call | Slow | Fast (pre-loaded) |
| Subsequent calls | Slow (reload) | Fast (cached) |

---

## CLI Mode

The tool supports traditional CLI mode when invoked with a specific command. Useful for testing, scripting, and backwards compatibility.

### CLI Usage

```bash
# Run via dotnet
dotnet run --project src/CSharpMcp -- [command] [options]

# Or use the built executable directly
./bin/Debug/net10.0/csharp-mcp [command] [options]

# Examples
csharp-mcp find-definition UserService --type class
csharp-mcp --solution MySolution.sln find-references GetById
csharp-mcp diagnostics --severity error
```

### Global Options

- `--solution, -s <path>` - Path to .sln file (optional - auto-discovers if not specified)
- `--project, -p <path>` - Path to .csproj file (alternative to solution, also auto-discovers)
- `--output, -o <format>` - Output format: json, text, or markdown (default: json)
- `--verbose, -v` - Enable verbose logging

**Auto-Discovery:** If you don't specify `-s` or `-p`, the tool automatically searches the current directory for a `.sln` file (preferred) or `.csproj` file. Perfect for running commands from your project root!

## Commands

### Symbol Commands

#### find-definition
Find where a symbol (class, method, property, etc.) is defined.

```bash
csharp-mcp -s MySolution.sln find-definition UserService --type class
csharp-mcp -s MySolution.sln find-definition GetById --type method --in-namespace MyApp.Services
```

**Options:**
- `--type, -t` - Filter by symbol type: class, method, property, field, interface, enum
- `--in-file, -f` - Search only in specific file
- `--in-namespace, -n` - Search only in specific namespace

#### find-references
Find all references/usages of a symbol throughout the solution.

```bash
csharp-mcp -s MySolution.sln find-references GetById --type method
csharp-mcp -s MySolution.sln find-references IUserRepository --type interface
```

**Options:**
- `--type, -t` - Symbol type to search for
- `--in-namespace, -n` - Symbol namespace

#### signature
Get the signature and documentation of a symbol.

```bash
csharp-mcp -s MySolution.sln signature GetById --type method --include-overloads
csharp-mcp -s MySolution.sln signature UserService --type class
```

**Options:**
- `--type, -t` - Type of symbol
- `--include-overloads` - Show all overloads for methods
- `--include-docs` - Include XML documentation comments (default: true)

#### list-members
List all members (methods, properties, fields) of a type.

```bash
csharp-mcp -s MySolution.sln list-members UserService
csharp-mcp -s MySolution.sln list-members User --kind method --accessibility public
```

**Options:**
- `--kind, -k` - Filter by member kind: method, property, field, event
- `--accessibility, -a` - Filter by accessibility: public, private, protected, internal
- `--include-inherited` - Include inherited members

#### rename
Safely rename a symbol across the entire solution.

```bash
# Preview mode (show changes without applying)
csharp-mcp -s MySolution.sln rename UserService UserManager --preview

# Apply rename
csharp-mcp -s MySolution.sln rename UserService UserManager --type class --rename-file

# Rename method
csharp-mcp -s MySolution.sln rename GetById FindById --type method
```

**Options:**
- `--type, -t` - Type of symbol being renamed
- `--in-namespace, -n` - Limit scope to namespace
- `--preview` - Show changes without applying them
- `--rename-file` - Also rename the file if renaming a type

### Compilation Commands

#### diagnostics
Get all compilation errors, warnings, and info messages.

```bash
csharp-mcp -s MySolution.sln diagnostics --severity error
csharp-mcp -s MySolution.sln diagnostics --file src/UserService.cs --severity warning
csharp-mcp -s MySolution.sln diagnostics --code CS0246
```

**Options:**
- `--severity, -s` - Filter by severity: error, warning, info
- `--file, -f` - Get diagnostics only for specific file
- `--code, -c` - Filter by diagnostic code (e.g., CS0246)

#### check-symbol-exists
Quickly verify if a symbol exists and is accessible.

```bash
csharp-mcp -s MySolution.sln check-symbol-exists UserDto --type class
csharp-mcp -s MySolution.sln check-symbol-exists GetById --type method --in-namespace MyApp.Services
```

**Options:**
- `--type, -t` - Expected symbol type
- `--in-namespace, -n` - Expected namespace

### Type Hierarchy Commands

#### find-implementations
Find all implementations of an interface or abstract class.

```bash
csharp-mcp -s MySolution.sln find-implementations IUserRepository
csharp-mcp -s MySolution.sln find-implementations IDisposable
```

#### inheritance-tree
Show inheritance hierarchy (ancestors and descendants).

```bash
csharp-mcp -s MySolution.sln inheritance-tree UserService
csharp-mcp -s MySolution.sln inheritance-tree BaseService --direction down
```

**Options:**
- `--direction, -d` - Show ancestors, descendants, or both (default: both)

### Call Analysis Commands

#### find-callers
Find all methods that call a specific method.

```bash
csharp-mcp -s MySolution.sln find-callers GetById
csharp-mcp -s MySolution.sln find-callers ProcessOrder
```

#### find-callees
Find all methods called by a specific method.

```bash
csharp-mcp -s MySolution.sln find-callees GetUser
csharp-mcp -s MySolution.sln find-callees ProcessOrder
```

### Dependency Analysis Commands

#### dependencies
Analyze what types/namespaces a file or type depends on.

```bash
csharp-mcp -s MySolution.sln dependencies src/Controllers/UserController.cs
csharp-mcp -s MySolution.sln dependencies UserService
```

#### unused-code
Find potentially unused code (methods, classes, properties).

```bash
csharp-mcp -s MySolution.sln unused-code
```

### Code Generation Commands

#### generate-interface
Extract an interface from a class.

```bash
csharp-mcp -s MySolution.sln generate-interface UserService
csharp-mcp -s MySolution.sln generate-interface UserService -o text
```

#### implement-interface
Generate implementation stubs for an interface.

```bash
csharp-mcp -s MySolution.sln implement-interface IUserRepository
csharp-mcp -s MySolution.sln implement-interface IDisposable
```

### Organization Commands

#### list-types
List all types in a namespace or file.

```bash
csharp-mcp -s MySolution.sln list-types --namespace MyApp.Services
csharp-mcp -s MySolution.sln list-types
```

**Options:**
- `--namespace` - Filter by namespace

#### namespace-tree
Show the namespace hierarchy of the solution.

```bash
csharp-mcp -s MySolution.sln namespace-tree
csharp-mcp -s MySolution.sln namespace-tree -o markdown
```

#### analyze-file
Quick comprehensive analysis of a single file.

```bash
csharp-mcp -s MySolution.sln analyze-file src/Services/UserService.cs
csharp-mcp -s MySolution.sln analyze-file src/Program.cs -o markdown
```

## Output Formats

### JSON (default)
```bash
csharp-mcp -s MySolution.sln find-definition UserService -o json
```
```json
{
  "symbol": "UserService",
  "kind": "class",
  "location": {
    "file": "src/Services/UserService.cs",
    "line": 15,
    "column": 18
  },
  "namespace": "MyApp.Services",
  "accessibility": "public"
}
```

### Text
```bash
csharp-mcp -s MySolution.sln find-definition UserService -o text
```
```
Symbol: UserService
Kind: class
Location: File: src/Services/UserService.cs
          Line: 15
          Column: 18
Namespace: MyApp.Services
Accessibility: public
```

### Markdown
```bash
csharp-mcp -s MySolution.sln find-definition UserService -o markdown
```
```markdown
**Symbol**: UserService
**Kind**: class
**Location**: ...
```

## Example Workflows

### Workflow 1: Safe Refactoring
```bash
# 1. Check current usage
csharp-mcp -s MySolution.sln find-references GetById --type method

# 2. Preview rename
csharp-mcp -s MySolution.sln rename GetById FindById --type method --preview

# 3. Execute rename
csharp-mcp -s MySolution.sln rename GetById FindById --type method

# 4. Verify no errors
csharp-mcp -s MySolution.sln diagnostics --severity error
```

### Workflow 2: Understanding a Type
```bash
# 1. Find where it's defined
csharp-mcp -s MySolution.sln find-definition UserService

# 2. See its members
csharp-mcp -s MySolution.sln list-members UserService

# 3. Check inheritance
csharp-mcp -s MySolution.sln inheritance-tree UserService

# 4. See what it depends on
csharp-mcp -s MySolution.sln dependencies UserService
```

### Workflow 3: Code Quality
```bash
# 1. Check for compilation errors
csharp-mcp -s MySolution.sln diagnostics --severity error

# 2. Find unused code
csharp-mcp -s MySolution.sln unused-code

# 3. Analyze specific file
csharp-mcp -s MySolution.sln analyze-file src/Services/UserService.cs
```

## Exit Codes

- `0` - Success
- `1` - General error (exception, invalid arguments)
- `2` - Not found (symbol not found, file not found)

## Requirements

- **.NET 10.0 Runtime** - Required to run the tool
  - Download: https://dotnet.microsoft.com/download/dotnet/10.0
  - The binaries are framework-dependent and require .NET 10.0 to be installed
- Solution or project file to analyze

## Architecture

The tool is built on four main components:

1. **MCP Server Host** - Handles MCP protocol, tool registration, and server lifecycle
2. **RoslynApiClient** - Wrapper around Roslyn APIs for symbol operations
3. **Command Handlers** - 18 command implementations (shared between MCP and CLI)
4. **Output Formatters** - JSON/text/markdown output generation

All source code is located in `src/CSharpMcp/`.

## Technical Details

### Dependencies
- `ModelContextProtocol` (v0.7.0-preview.1) - MCP server SDK
- `Microsoft.Extensions.Hosting` (v9.0.0) - Host builder for MCP server
- `Microsoft.CodeAnalysis.CSharp.Workspaces` (v5.0.0) - Roslyn workspaces
- `Microsoft.CodeAnalysis.Workspaces.MSBuild` (v5.0.0) - MSBuild integration
- `Microsoft.Build.Locator` (v1.8.1) - MSBuild discovery
- `System.CommandLine` (v2.0.0-beta4.22272.1) - CLI framework

### Key Features
- MCP server with stdio transport for AI assistant integration
- Solution pre-loading for fast subsequent tool calls
- Uses Roslyn's `SymbolFinder` for accurate symbol lookups
- Leverages `SemanticModel` for type information
- Safe renaming via `Renamer` API
- Compilation diagnostics from full solution analysis

## Contributing

This tool implements all 18 commands specified in [SPEC.md](SPEC.md).

## License

[Your License Here]

## Support

For issues or questions, please refer to the specification document or open an issue.
