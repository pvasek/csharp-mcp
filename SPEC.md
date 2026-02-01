# C# MCP Specification

## Purpose

Provide Claude Code with fast, accurate, token-efficient operations for C# refactoring and code analysis. This tool leverages Roslyn APIs to perform operations that are currently slow, error-prone, and token-intensive when done via text search and file operations.

## Core Value Proposition

**Without C# Skill:**
- Multiple grep/text searches with false positives
- Reading entire files to understand signatures
- Unsafe text-based renaming that breaks code
- Manual tracing of dependencies and call chains
- Compilation errors discovered late in the process

**With C# Skill:**
- Single command, precise results
- Symbol-aware operations that understand semantics
- Safe refactoring that respects language rules
- Instant access to type information and relationships
- Early error detection and validation

---

## Command Structure

The tool is a CLI application with a command/subcommand structure:

```bash
csharp-mcp [global-options] <command> [command-options] [arguments]
```

### Global Options
- `--solution <path>` - Path to .sln file (auto-discovers if not specified)
- `--output <format>` - Output format: json, text, markdown (default: json)
- `--verbose` - Enable verbose logging

---

## MCP Server Mode

The C# Skill tool operates primarily as an **MCP (Model Context Protocol) server**, providing fast, persistent access to C# code analysis. The solution is loaded once at startup, making subsequent tool calls significantly faster.

### Starting the MCP Server

```bash
# Start MCP server with auto-discovered solution
csharp-mcp

# Start MCP server with explicit solution path
csharp-mcp --solution path/to/Solution.sln

# Start MCP server with specific project
csharp-mcp --solution path/to/Project.csproj
```

### MCP Configuration

Add to your MCP client configuration (e.g., `claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "csharp-mcp": {
      "command": "csharp-mcp",
      "args": ["--solution", "/path/to/your/Solution.sln"]
    }
  }
}
```

### Server Lifecycle

1. **Initialization**: On startup, the server:
   - Registers MSBuild instance
   - Loads the specified solution (or auto-discovers)
   - Compiles and caches the semantic model
   - Registers all tools with the MCP runtime

2. **Tool Execution**: Each tool call:
   - Reuses the cached solution/workspace
   - Performs semantic analysis on the pre-loaded model
   - Returns JSON results

3. **Shutdown**: Server releases all resources gracefully

### Performance Benefits

| Operation | CLI Mode | MCP Server Mode |
|-----------|----------|-----------------|
| Solution load | Every call (~2-10s) | Once at startup |
| First tool call | Slow | Fast (pre-loaded) |
| Subsequent calls | Slow (reload) | Fast (cached) |

---

## CLI Mode

The tool supports traditional CLI mode when invoked with a specific command. This is useful for:

- **Testing & Development**: Quickly test commands without setting up an MCP client
- **Scripting**: Use in shell scripts and automation pipelines
- **Debugging**: Verify tool behavior before MCP integration
- **Backwards Compatibility**: Existing scripts continue to work

```bash
# CLI mode - explicit command invocation
csharp-mcp find-definition UserService --type class

# CLI mode with options
csharp-mcp --solution path/to/Solution.sln find-references GetById

# Quick test of a command
csharp-mcp diagnostics --severity error
```

**Note**: CLI mode loads the solution on each invocation. For interactive workflows with multiple operations, MCP server mode provides better performance.

---

## Command Summary

### Symbol Commands
- **`find-definition`** - Find where a symbol (class, method, property, etc.) is defined
- **`find-references`** - Find all usages of a symbol throughout the solution
- **`rename`** - Safely rename a symbol across the entire solution with preview
- **`signature`** - Get method/type signatures with parameters and return types
- **`list-members`** - List all members (methods, properties, fields) of a type

### Type Hierarchy Commands
- **`find-implementations`** - Find all implementations of an interface or abstract class
- **`inheritance-tree`** - Show inheritance hierarchy (ancestors and descendants)

### Call Analysis Commands
- **`find-callers`** - Find all methods that call a specific method (who uses this?)
- **`find-callees`** - Find all methods called by a specific method (what does this use?)

### Compilation & Diagnostics Commands
- **`diagnostics`** - Get all compilation errors, warnings, and messages
- **`check-symbol-exists`** - Quickly verify if a symbol exists and is accessible

### Dependency Analysis Commands
- **`dependencies`** - Show what types/namespaces a file or type depends on
- **`unused-code`** - Find potentially unused methods, classes, and properties

### Code Generation Commands
- **`generate-interface`** - Extract an interface from a class
- **`implement-interface`** - Generate implementation stubs for an interface

### Namespace & Organization Commands
- **`list-types`** - List all types in a namespace or file
- **`namespace-tree`** - Show the namespace hierarchy of the solution
- **`analyze-file`** - Quick comprehensive analysis of a single file

---

## Commands

### 1. Symbol Commands

#### 1.1 `find-definition`
Find the definition location of a symbol.

```bash
csharp-mcp find-definition <symbol-name> [options]
```

**Options:**
- `--type <type>` - Filter by symbol type: class, method, property, field, interface, enum
- `--in-file <path>` - Search only in specific file
- `--in-namespace <namespace>` - Search only in specific namespace

**Output:**
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

**Use Cases:**
- "Where is UserService defined?" → `find-definition UserService --type class`
- "Find the GetById method" → `find-definition GetById --type method`

---

#### 1.2 `find-references`
Find all references/usages of a symbol throughout the solution.

```bash
csharp-mcp find-references <symbol-name> [options]
```

**Options:**
- `--type <type>` - Symbol type to search for
- `--include-indirect` - Include indirect references (via inheritance, interfaces)
- `--exclude-comments` - Exclude references in comments/strings

**Output:**
```json
{
  "symbol": "UserService.GetById",
  "totalReferences": 23,
  "references": [
    {
      "file": "src/Controllers/UserController.cs",
      "line": 45,
      "column": 28,
      "context": "var user = _userService.GetById(id);",
      "kind": "invocation"
    },
    {
      "file": "src/Services/AdminService.cs",
      "line": 102,
      "column": 15,
      "context": "return userService.GetById(adminId);",
      "kind": "invocation"
    }
  ]
}
```

**Use Cases:**
- "What uses this method?" → `find-references GetById --type method`
- "Find all usages of IUserRepository" → `find-references IUserRepository --type interface --include-indirect`

---

#### 1.3 `rename`
Safely rename a symbol across the entire solution.

```bash
csharp-mcp rename <old-name> <new-name> [options]
```

**Options:**
- `--type <type>` - Type of symbol being renamed
- `--in-namespace <namespace>` - Limit scope to namespace
- `--preview` - Show changes without applying them
- `--rename-file` - Also rename the file if renaming a type

**Output (Preview Mode):**
```json
{
  "symbol": "UserService",
  "newName": "UserManager",
  "changes": [
    {
      "file": "src/Services/UserService.cs",
      "fileName": "UserManager.cs",
      "edits": [
        {"line": 15, "old": "class UserService", "new": "class UserManager"},
        {"line": 20, "old": "public UserService(", "new": "public UserManager("}
      ]
    },
    {
      "file": "src/Controllers/UserController.cs",
      "edits": [
        {"line": 12, "old": "private readonly UserService", "new": "private readonly UserManager"},
        {"line": 18, "old": "UserService userService", "new": "UserManager userManager"}
      ]
    }
  ],
  "totalChanges": 47,
  "affectedFiles": 12
}
```

**Use Cases:**
- "Rename UserService to UserManager" → `rename UserService UserManager --type class --rename-file`
- "Rename GetById to FindById" → `rename GetById FindById --type method --preview`

---

#### 1.4 `signature`
Get the signature and documentation of a symbol.

```bash
csharp-mcp signature <symbol-name> [options]
```

**Options:**
- `--type <type>` - Type of symbol
- `--include-overloads` - Show all overloads for methods
- `--include-docs` - Include XML documentation comments

**Output:**
```json
{
  "symbol": "UserService.GetById",
  "kind": "method",
  "signatures": [
    {
      "declaration": "public User GetById(int id)",
      "returnType": "User",
      "parameters": [
        {"name": "id", "type": "int", "isOptional": false}
      ],
      "accessibility": "public",
      "isStatic": false,
      "isAsync": false,
      "documentation": "Retrieves a user by their unique identifier."
    },
    {
      "declaration": "public async Task<User> GetById(int id, CancellationToken ct)",
      "returnType": "Task<User>",
      "parameters": [
        {"name": "id", "type": "int", "isOptional": false},
        {"name": "ct", "type": "CancellationToken", "isOptional": false}
      ],
      "accessibility": "public",
      "isStatic": false,
      "isAsync": true
    }
  ]
}
```

**Use Cases:**
- "What parameters does GetById take?" → `signature GetById --type method --include-overloads`
- "Show me the UserService constructor" → `signature UserService --type class`

---

#### 1.5 `list-members`
List all members of a type (class, interface, struct, enum).

```bash
csharp-mcp list-members <type-name> [options]
```

**Options:**
- `--kind <kind>` - Filter by member kind: method, property, field, event
- `--accessibility <level>` - Filter by accessibility: public, private, protected, internal
- `--include-inherited` - Include inherited members
- `--include-signatures` - Include full signatures

**Output:**
```json
{
  "type": "UserService",
  "namespace": "MyApp.Services",
  "members": [
    {
      "name": "GetById",
      "kind": "method",
      "accessibility": "public",
      "signature": "public User GetById(int id)",
      "isStatic": false,
      "isAbstract": false,
      "isVirtual": false
    },
    {
      "name": "Repository",
      "kind": "property",
      "accessibility": "private",
      "type": "IUserRepository",
      "hasGetter": true,
      "hasSetter": false
    }
  ],
  "totalMembers": 15
}
```

**Use Cases:**
- "What methods does UserService have?" → `list-members UserService --kind method --accessibility public`
- "Show all properties of User class" → `list-members User --kind property`

---

### 2. Type Hierarchy Commands

#### 2.1 `find-implementations`
Find all implementations of an interface or abstract class/method.

```bash
csharp-mcp find-implementations <symbol-name> [options]
```

**Options:**
- `--include-indirect` - Include indirect implementations (implementations of derived interfaces)

**Output:**
```json
{
  "interface": "IUserRepository",
  "implementations": [
    {
      "type": "SqlUserRepository",
      "file": "src/Repositories/SqlUserRepository.cs",
      "line": 10,
      "namespace": "MyApp.Repositories"
    },
    {
      "type": "InMemoryUserRepository",
      "file": "src/Repositories/InMemoryUserRepository.cs",
      "line": 8,
      "namespace": "MyApp.Repositories"
    }
  ],
  "totalImplementations": 2
}
```

**Use Cases:**
- "What implements IUserRepository?" → `find-implementations IUserRepository`
- "Find all implementations of ProcessAsync" → `find-implementations ProcessAsync --type method`

---

#### 2.2 `inheritance-tree`
Show the inheritance hierarchy for a type.

```bash
csharp-mcp inheritance-tree <type-name> [options]
```

**Options:**
- `--direction <up|down|both>` - Show ancestors, descendants, or both
- `--depth <n>` - Limit depth of tree

**Output:**
```json
{
  "type": "UserService",
  "ancestors": [
    "BaseService",
    "Object"
  ],
  "descendants": [
    "AdminUserService",
    "GuestUserService"
  ],
  "interfaces": [
    "IUserService",
    "IDisposable"
  ]
}
```

**Use Cases:**
- "What does UserService inherit from?" → `inheritance-tree UserService --direction up`
- "What classes derive from BaseService?" → `inheritance-tree BaseService --direction down`

---

### 3. Call Analysis Commands

#### 3.1 `find-callers`
Find all methods that call a specific method (call hierarchy up).

```bash
csharp-mcp find-callers <method-name> [options]
```

**Options:**
- `--in-type <type>` - Specify the containing type if method name is ambiguous
- `--depth <n>` - Show nested callers up to N levels

**Output:**
```json
{
  "method": "UserService.GetById",
  "callers": [
    {
      "method": "UserController.GetUser",
      "file": "src/Controllers/UserController.cs",
      "line": 45,
      "callLocation": {"line": 47, "column": 28}
    },
    {
      "method": "AdminService.GetUserDetails",
      "file": "src/Services/AdminService.cs",
      "line": 100,
      "callLocation": {"line": 102, "column": 15}
    }
  ],
  "totalCallers": 8
}
```

**Use Cases:**
- "What calls GetById?" → `find-callers GetById --in-type UserService`
- "Show call chain for this method" → `find-callers ProcessOrder --depth 3`

---

#### 3.2 `find-callees`
Find all methods called by a specific method (call hierarchy down).

```bash
csharp-mcp find-callees <method-name> [options]
```

**Options:**
- `--in-type <type>` - Specify the containing type
- `--depth <n>` - Show nested callees up to N levels
- `--include-external` - Include calls to external assemblies

**Output:**
```json
{
  "method": "UserController.GetUser",
  "callees": [
    {
      "method": "UserService.GetById",
      "file": "src/Services/UserService.cs",
      "line": 45,
      "namespace": "MyApp.Services"
    },
    {
      "method": "UserMapper.MapToDto",
      "file": "src/Mappers/UserMapper.cs",
      "line": 12,
      "namespace": "MyApp.Mappers"
    }
  ],
  "totalCallees": 5
}
```

**Use Cases:**
- "What does GetUser call?" → `find-callees GetUser --in-type UserController`
- "Show all dependencies of ProcessOrder" → `find-callees ProcessOrder --depth 2`

---

### 4. Compilation & Diagnostics Commands

#### 4.1 `diagnostics`
Get all compilation errors, warnings, and info messages.

```bash
csharp-mcp diagnostics [options]
```

**Options:**
- `--severity <level>` - Filter by severity: error, warning, info
- `--file <path>` - Get diagnostics only for specific file
- `--code <code>` - Filter by diagnostic code (e.g., CS0246)
- `--include-hidden` - Include hidden/suggestion diagnostics

**Output:**
```json
{
  "totalErrors": 3,
  "totalWarnings": 12,
  "diagnostics": [
    {
      "id": "CS0246",
      "severity": "error",
      "message": "The type or namespace name 'UserDto' could not be found",
      "file": "src/Controllers/UserController.cs",
      "line": 23,
      "column": 16,
      "endLine": 23,
      "endColumn": 23
    },
    {
      "id": "CS8602",
      "severity": "warning",
      "message": "Dereference of a possibly null reference",
      "file": "src/Services/UserService.cs",
      "line": 45,
      "column": 20,
      "endLine": 45,
      "endColumn": 24
    }
  ]
}
```

**Use Cases:**
- "Check for compilation errors" → `diagnostics --severity error`
- "What warnings in UserService.cs?" → `diagnostics --file src/Services/UserService.cs --severity warning`

---

#### 4.2 `check-symbol-exists`
Quickly check if a symbol exists and is accessible.

```bash
csharp-mcp check-symbol-exists <symbol-name> [options]
```

**Options:**
- `--type <type>` - Expected symbol type
- `--in-namespace <namespace>` - Expected namespace
- `--from-file <path>` - Check accessibility from specific file context

**Output:**
```json
{
  "symbol": "UserDto",
  "exists": true,
  "accessible": true,
  "location": {
    "file": "src/DTOs/UserDto.cs",
    "line": 5
  },
  "kind": "class",
  "namespace": "MyApp.DTOs"
}
```

**Use Cases:**
- "Does UserDto exist?" → `check-symbol-exists UserDto --type class`
- "Can I use IUserRepository from UserController.cs?" → `check-symbol-exists IUserRepository --from-file src/Controllers/UserController.cs`

---

### 5. Dependency Analysis Commands

#### 5.1 `dependencies`
Analyze what types/namespaces a file or type depends on.

```bash
csharp-mcp dependencies <target> [options]
```

**Options:**
- `--type <file|class|namespace>` - What kind of target
- `--include-external` - Include external assembly dependencies
- `--transitive` - Show transitive dependencies

**Output:**
```json
{
  "target": "src/Controllers/UserController.cs",
  "namespaces": [
    "MyApp.Services",
    "MyApp.DTOs",
    "MyApp.Models",
    "Microsoft.AspNetCore.Mvc",
    "System.Threading.Tasks"
  ],
  "types": [
    {
      "name": "UserService",
      "namespace": "MyApp.Services",
      "usageCount": 5
    },
    {
      "name": "UserDto",
      "namespace": "MyApp.DTOs",
      "usageCount": 3
    }
  ],
  "externalPackages": [
    "Microsoft.AspNetCore.Mvc.Core"
  ]
}
```

**Use Cases:**
- "What does UserController depend on?" → `dependencies src/Controllers/UserController.cs --type file`
- "What namespaces does UserService use?" → `dependencies UserService --type class`

---

#### 5.2 `unused-code`
Find potentially unused code (methods, classes, properties).

```bash
csharp-mcp unused-code [options]
```

**Options:**
- `--kind <kind>` - Filter by: method, class, property, field
- `--accessibility <level>` - Only check specific accessibility levels
- `--exclude-entry-points` - Exclude Program.Main and similar
- `--file <path>` - Check specific file only

**Output:**
```json
{
  "unusedSymbols": [
    {
      "name": "OldUserService",
      "kind": "class",
      "file": "src/Services/OldUserService.cs",
      "line": 10,
      "accessibility": "public",
      "reason": "No references found"
    },
    {
      "name": "GetUserByEmail",
      "kind": "method",
      "file": "src/Services/UserService.cs",
      "line": 67,
      "accessibility": "private",
      "reason": "No callers found"
    }
  ],
  "totalUnused": 15
}
```

**Use Cases:**
- "Find unused methods" → `unused-code --kind method`
- "What private methods are unused in UserService.cs?" → `unused-code --file src/Services/UserService.cs --kind method --accessibility private`

---

### 6. Code Generation & Scaffolding Commands

#### 6.1 `generate-interface`
Extract an interface from a class.

```bash
csharp-mcp generate-interface <class-name> [options]
```

**Options:**
- `--name <interface-name>` - Name for the interface (default: I + ClassName)
- `--members <members>` - Comma-separated list of members to include (default: all public)
- `--output-file <path>` - Where to save the interface

**Output:**
```json
{
  "interface": "IUserService",
  "content": "public interface IUserService\n{\n    User GetById(int id);\n    Task<User> CreateAsync(UserDto dto);\n    void Delete(int id);\n}",
  "outputFile": "src/Interfaces/IUserService.cs"
}
```

**Use Cases:**
- "Extract interface from UserService" → `generate-interface UserService`
- "Create interface with specific methods" → `generate-interface UserService --members GetById,CreateAsync`

---

#### 6.2 `implement-interface`
Generate implementation stubs for an interface.

```bash
csharp-mcp implement-interface <interface-name> [options]
```

**Options:**
- `--in-class <class-name>` - Add to existing class
- `--new-class <class-name>` - Create new class
- `--throw-not-implemented` - Throw NotImplementedException in stubs

**Output:**
```json
{
  "class": "UserService",
  "methods": [
    {
      "signature": "public User GetById(int id)",
      "body": "{\n    throw new NotImplementedException();\n}"
    },
    {
      "signature": "public Task<User> CreateAsync(UserDto dto)",
      "body": "{\n    throw new NotImplementedException();\n}"
    }
  ]
}
```

**Use Cases:**
- "Implement IUserRepository in UserService" → `implement-interface IUserRepository --in-class UserService`

---

### 7. Namespace & Organization Commands

#### 7.1 `list-types`
List all types in a namespace or file.

```bash
csharp-mcp list-types [options]
```

**Options:**
- `--namespace <namespace>` - Filter by namespace
- `--file <path>` - List types in specific file
- `--kind <kind>` - Filter by: class, interface, struct, enum, delegate
- `--accessibility <level>` - Filter by accessibility

**Output:**
```json
{
  "namespace": "MyApp.Services",
  "types": [
    {
      "name": "UserService",
      "kind": "class",
      "accessibility": "public",
      "file": "src/Services/UserService.cs",
      "line": 15
    },
    {
      "name": "AdminService",
      "kind": "class",
      "accessibility": "internal",
      "file": "src/Services/AdminService.cs",
      "line": 10
    }
  ],
  "totalTypes": 12
}
```

**Use Cases:**
- "List all classes in MyApp.Services" → `list-types --namespace MyApp.Services --kind class`
- "What types are in this file?" → `list-types --file src/Services/UserService.cs`

---

#### 7.2 `namespace-tree`
Show the namespace hierarchy of the solution.

```bash
csharp-mcp namespace-tree [options]
```

**Options:**
- `--root <namespace>` - Show tree starting from specific namespace
- `--depth <n>` - Limit depth

**Output:**
```json
{
  "root": "MyApp",
  "tree": {
    "MyApp": {
      "Controllers": ["UserController", "AdminController"],
      "Services": ["UserService", "AdminService"],
      "Models": {
        "Entities": ["User", "Role"],
        "DTOs": ["UserDto", "RoleDto"]
      },
      "Repositories": ["UserRepository", "RoleRepository"]
    }
  }
}
```

**Use Cases:**
- "Show namespace structure" → `namespace-tree`
- "Show structure of MyApp.Services" → `namespace-tree --root MyApp.Services`

---

### 8. Utility Commands

#### 8.1 `analyze-file`
Quick comprehensive analysis of a file.

```bash
csharp-mcp analyze-file <file-path>
```

**Output:**
```json
{
  "file": "src/Services/UserService.cs",
  "types": [
    {"name": "UserService", "kind": "class", "line": 15}
  ],
  "namespaces": ["MyApp.Services"],
  "usings": [
    "System",
    "System.Threading.Tasks",
    "MyApp.Models",
    "MyApp.Repositories"
  ],
  "dependencies": ["IUserRepository", "User", "UserDto"],
  "diagnostics": [
    {"severity": "warning", "line": 45, "message": "Possible null reference"}
  ],
  "metrics": {
    "lines": 234,
    "methods": 12,
    "properties": 5,
    "complexity": "medium"
  }
}
```

**Use Cases:**
- "Analyze UserService.cs" → `analyze-file src/Services/UserService.cs`

---

## Implementation Priority

### Phase 1: Core Symbol Operations (Highest Value)
1. `find-definition` - Essential for navigation
2. `find-references` - Critical for refactoring
3. `rename` - Safest way to rename symbols
4. `signature` - Quick method/type information
5. `diagnostics` - Early error detection

### Phase 2: Type & Hierarchy
6. `find-implementations` - Interface/abstract navigation
7. `inheritance-tree` - Understanding type relationships
8. `list-members` - Quick type exploration

### Phase 3: Call Analysis
9. `find-callers` - Understanding impact
10. `find-callees` - Understanding dependencies

### Phase 4: Dependencies & Quality
11. `dependencies` - Dependency analysis
12. `unused-code` - Code cleanup
13. `check-symbol-exists` - Validation

### Phase 5: Code Generation
14. `generate-interface` - Scaffolding
15. `implement-interface` - Implementation help

### Phase 6: Organization & Utilities
16. `list-types` - Namespace exploration
17. `namespace-tree` - Solution structure
18. `analyze-file` - Quick file overview

---

## MCP Tools

All commands are exposed as MCP tools with the following naming convention:

| Command | MCP Tool Name |
|---------|---------------|
| `find-definition` | `csharp_find_definition` |
| `find-references` | `csharp_find_references` |
| `rename` | `csharp_rename` |
| `signature` | `csharp_signature` |
| `list-members` | `csharp_list_members` |
| `find-implementations` | `csharp_find_implementations` |
| `inheritance-tree` | `csharp_inheritance_tree` |
| `find-callers` | `csharp_find_callers` |
| `find-callees` | `csharp_find_callees` |
| `diagnostics` | `csharp_diagnostics` |
| `check-symbol-exists` | `csharp_check_symbol_exists` |
| `dependencies` | `csharp_dependencies` |
| `unused-code` | `csharp_unused_code` |
| `generate-interface` | `csharp_generate_interface` |
| `implement-interface` | `csharp_implement_interface` |
| `list-types` | `csharp_list_types` |
| `namespace-tree` | `csharp_namespace_tree` |
| `analyze-file` | `csharp_analyze_file` |

### Tool Parameter Mapping

CLI options become tool parameters:

```json
// CLI: csharp-mcp find-definition UserService --type class --in-namespace MyApp
// MCP Tool call:
{
  "name": "csharp_find_definition",
  "arguments": {
    "symbolName": "UserService",
    "type": "class",
    "inNamespace": "MyApp"
  }
}
```

### Tool Response Format

All tools return JSON matching the existing CLI JSON output format.

---

## Technical Implementation Notes

### MCP SDK Integration

- **Package**: `ModelContextProtocol` (NuGet)
- **Transport**: stdio (standard MCP transport)
- **Tool Registration**: Attribute-based using `[McpServerTool]`
- **Dependency Injection**: CommandContext injected into tools

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      MCP Server Host                         │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐          │
│  │ Tool:       │  │ Tool:       │  │ Tool:       │   ...    │
│  │ find_def    │  │ find_refs   │  │ rename      │          │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘          │
│         │                │                │                  │
│         └────────────────┼────────────────┘                  │
│                          ▼                                   │
│              ┌───────────────────────┐                       │
│              │   RoslynApiClient     │ (singleton, cached)   │
│              │   - Loaded Solution   │                       │
│              │   - SemanticModels    │                       │
│              └───────────────────────┘                       │
└─────────────────────────────────────────────────────────────┘
```

### Dual-Mode Program.cs Structure

```csharp
// If no command specified → MCP Server mode
// If command specified → CLI mode

if (args.Length == 0 || args.All(a => a.StartsWith("--")))
{
    // MCP Server mode
    await RunMcpServer(args);
}
else
{
    // CLI mode (legacy)
    await RunCli(args);
}
```

### Roslyn APIs to Use

- **ISymbol hierarchy** - For all symbol operations
- **SymbolFinder** - For find-references, find-implementations
- **Renamer** - For safe renaming
- **Solution/Project/Document model** - For workspace operations
- **SemanticModel** - For type information and symbol resolution
- **SyntaxTree** - For source code analysis
- **Diagnostic APIs** - For compilation errors/warnings
- **CallGraph APIs** - For caller/callee analysis

### Performance Considerations

- Cache loaded solutions/workspaces
- Use incremental compilation when possible
- Provide `--file` filters to limit scope
- Support JSON output for easy parsing
- Consider parallel processing for batch operations

### Error Handling

- Clear error messages when symbol not found
- Suggest alternatives for ambiguous symbols
- Handle incomplete/broken solutions gracefully
- Validate paths and symbol names early

### Integration with Claude Code

- JSON output for easy parsing
- Exit codes: 0 = success, 1 = error, 2 = not found
- Stderr for errors/warnings, stdout for results
- Support for relative paths from workspace root

---

## Example Workflows

### Workflow 1: Renaming a Method
```bash
# 1. Check current usage
csharp-mcp find-references GetById --type method

# 2. Preview rename
csharp-mcp rename GetById FindById --type method --preview

# 3. Execute rename
csharp-mcp rename GetById FindById --type method

# 4. Verify no errors
csharp-mcp diagnostics --severity error
```

### Workflow 2: Understanding a Type
```bash
# 1. Find where it's defined
csharp-mcp find-definition UserService

# 2. See its members
csharp-mcp list-members UserService --include-signatures

# 3. Check inheritance
csharp-mcp inheritance-tree UserService --direction both

# 4. See what it depends on
csharp-mcp dependencies UserService --type class
```

### Workflow 3: Refactoring
```bash
# 1. Find all usages
csharp-mcp find-references OldMethod --type method

# 2. Check what calls it
csharp-mcp find-callers OldMethod

# 3. Understand what it does
csharp-mcp find-callees OldMethod --depth 2

# 4. Safe rename
csharp-mcp rename OldMethod NewMethod --preview
```

---

## Success Metrics

**Token Savings:**
- Find operation: ~500-2000 tokens saved per search
- Rename operation: ~1000-5000 tokens saved per rename
- Signature lookup: ~200-500 tokens saved per lookup

**Time Savings:**
- Find operations: 5-10 tool calls → 1 call
- Rename operations: 10-30 edits → 1 command
- Type exploration: Multiple file reads → 1 command

**Accuracy Improvements:**
- Rename: 100% accuracy vs ~85% with text search
- Find references: Catches indirect references text search misses
- Symbol resolution: No false positives

---

## Future Enhancements

### Solution Reloading (Planned)

A future version will support solution reloading without server restart:

```json
// Planned MCP tool
{
  "name": "csharp_reload_solution",
  "arguments": {
    "path": "/optional/new/path.sln"  // Optional, reloads current if omitted
  }
}
```

### File Change Notifications (Planned)

Integration with file system watchers for automatic incremental updates.
