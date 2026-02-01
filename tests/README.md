# CSharpMcp Tests

This directory contains the xUnit test suite for the CSharpMcp project.

## Test Structure

```
tests/
├── CSharpMcp.Tests/          # xUnit test project
│   ├── Helpers/
│   │   └── TestProjectHelper.cs    # Helper for managing test projects
│   ├── Commands/
│   │   ├── SymbolCommandsTests.cs        # Tests for symbol operations
│   │   ├── CompilationCommandsTests.cs   # Tests for compilation/diagnostics
│   │   ├── TypeHierarchyCommandsTests.cs # Tests for type hierarchy
│   │   └── CallAnalysisCommandsTests.cs  # Tests for call analysis
│   └── RoslynApiClientTests.cs           # Tests for core API client
└── TestData/
    └── MasterProject/          # Master test C# project (read-only template)
        ├── MasterProject.sln
        ├── Models/
        ├── Services/
        ├── Repositories/
        ├── Interfaces/
        └── Controllers/
```

## Test Data Pattern

The test suite uses a **master project copy pattern**:

1. **Master Project** (`TestData/MasterProject/`): A complete C# solution that serves as clean test data
   - Contains realistic code: models, services, repositories, interfaces, controllers
   - Features inheritance, interfaces, method calls - all the patterns we need to test
   - Never modified during tests

2. **TestProjectHelper**: Copies the master project to a temporary directory for each test
   - Each test gets its own isolated copy
   - Tests can modify files without affecting other tests
   - Automatic cleanup after test completion

## Running Tests

```bash
# Run all tests
cd tests/CSharpMcp.Tests
dotnet test

# Run with verbose output
dotnet test --verbosity normal

# Run specific test class
dotnet test --filter "FullyQualifiedName~SymbolCommandsTests"

# Run specific test
dotnet test --filter "FullyQualifiedName~FindSymbolsByName_FindsClass"
```

## Test Coverage

### RoslynApiClientTests (3 tests)
- OpenSolution_LoadsSuccessfully
- OpenSolution_ThrowsForInvalidPath
- FindSymbolsByName_FindsSymbols

### SymbolCommandsTests (6 tests)
- FindSymbolsByName_FindsClass
- FindSymbolsByName_FindsMethod
- FindSymbolsByName_FindsInterface
- FindSymbolsByName_FiltersByNamespace
- FindReferences_FindsMethodReferences
- RenameSymbol_RenamesMethod

### CompilationCommandsTests (4 tests)
- GetDiagnostics_NoErrorsInValidProject
- GetDiagnostics_FindsErrorsInBrokenCode
- SymbolExists_FindsExistingClass
- SymbolExists_ReturnsFalseForNonExistent

### TypeHierarchyCommandsTests (3 tests)
- FindImplementations_FindsInterfaceImplementation
- GetBaseTypes_FindsBaseTypes
- GetDerivedTypes_FindsDerivedTypes

### CallAnalysisCommandsTests (3 tests)
- FindCallers_FindsMethodCallers
- FindCallees_FindsMethodCalls
- FindCallees_FindsMultipleCalls

**Total: 19 tests, all passing ✓**

## Adding New Tests

1. Add test method to appropriate test class
2. Use `TestProjectHelper` to get a fresh copy of the test project:
   ```csharp
   [Fact]
   public async Task YourTest()
   {
       using var testProject = new TestProjectHelper();
       await testProject.OpenSolutionAsync();

       // Use testProject.Client to test RoslynApiClient methods
       var symbols = await testProject.Client.FindSymbolsByNameAsync(...);

       Assert.NotEmpty(symbols);
   }
   ```

3. Optionally modify test files if needed:
   ```csharp
   testProject.ModifyFile("Services/UserService.cs", newContent);
   ```

## Master Project Contents

The master test project includes:

- **Models**: `User`, `UserDto`
- **Interfaces**: `IUserRepository`
- **Repositories**: `UserRepository` (implements `IUserRepository`)
- **Services**: `BaseService`, `UserService`, `AdminUserService` (inheritance)
- **Controllers**: `UserController`

This provides rich test scenarios for:
- Finding definitions and references
- Type hierarchy (base/derived classes, interfaces)
- Call analysis (callers/callees)
- Renaming symbols
- Compilation diagnostics

## Notes

- Tests run in isolation - each gets its own project copy
- MSBuild locator is registered once per test run (handled automatically)
- Temporary test directories are cleaned up automatically
- All tests verify actual Roslyn API behavior, not mocked
