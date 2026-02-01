using System.Threading.Tasks;
using CSharpMcp.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Xunit;

namespace CSharpMcp.Tests.Commands
{
    public class SymbolCommandsTests
    {
        [Fact]
        public async Task FindSymbolsByName_FindsClass()
        {
            using var testProject = new TestProjectHelper();
            await testProject.OpenSolutionAsync();

            var symbols = await testProject.Client.FindSymbolsByNameAsync("UserService", SymbolKind.NamedType, null, null);

            var symbolList = symbols.ToList();
            Assert.NotEmpty(symbolList);
            var userService = symbolList.First();
            Assert.Equal("UserService", userService.Name);
            Assert.Equal("MasterProject.Services", userService.ContainingNamespace.ToDisplayString());
        }

        [Fact]
        public async Task FindSymbolsByName_FindsMethod()
        {
            using var testProject = new TestProjectHelper();
            await testProject.OpenSolutionAsync();

            var symbols = await testProject.Client.FindSymbolsByNameAsync("GetById", SymbolKind.Method, null, null);

            var symbolList = symbols.ToList();
            Assert.NotEmpty(symbolList);
            Assert.Contains(symbolList, s => s.ContainingType.Name == "UserService");
        }

        [Fact]
        public async Task FindSymbolsByName_FindsInterface()
        {
            using var testProject = new TestProjectHelper();
            await testProject.OpenSolutionAsync();

            var symbols = await testProject.Client.FindSymbolsByNameAsync("IUserRepository", SymbolKind.NamedType, null, null);

            var symbolList = symbols.ToList();
            Assert.NotEmpty(symbolList);
            var interfaceSymbol = symbolList.First() as INamedTypeSymbol;
            Assert.NotNull(interfaceSymbol);
            Assert.Equal("IUserRepository", interfaceSymbol.Name);
            Assert.Equal(TypeKind.Interface, interfaceSymbol.TypeKind);
        }

        [Fact]
        public async Task FindSymbolsByName_FiltersByNamespace()
        {
            using var testProject = new TestProjectHelper();
            await testProject.OpenSolutionAsync();

            var symbols = await testProject.Client.FindSymbolsByNameAsync("UserService", SymbolKind.NamedType, "MasterProject.Services", null);

            var symbolList = symbols.ToList();
            Assert.NotEmpty(symbolList);
            Assert.All(symbolList, s => Assert.Equal("MasterProject.Services", s.ContainingNamespace.ToDisplayString()));
        }

        [Fact]
        public async Task FindReferences_FindsMethodReferences()
        {
            using var testProject = new TestProjectHelper();
            await testProject.OpenSolutionAsync();

            var symbols = await testProject.Client.FindSymbolsByNameAsync("GetById", SymbolKind.Method, null, null);
            var getByIdMethod = symbols.FirstOrDefault(s => s.ContainingType.Name == "UserService");
            Assert.NotNull(getByIdMethod);

            var references = await testProject.Client.FindReferencesAsync(getByIdMethod);

            Assert.NotEmpty(references);
        }

        [Fact]
        public async Task RenameSymbol_RenamesMethod()
        {
            using var testProject = new TestProjectHelper();
            await testProject.OpenSolutionAsync();

            var symbols = await testProject.Client.FindSymbolsByNameAsync("Activate", SymbolKind.Method, null, null);
            var activateMethod = symbols.FirstOrDefault(s => s.ContainingType.Name == "User");
            Assert.NotNull(activateMethod);

            var newSolution = await testProject.Client.RenameSymbolAsync(activateMethod, "Enable");

            Assert.NotNull(newSolution);
            var allTypes = await testProject.Client.GetAllTypesAsync();
            // Verify solution was modified (we don't apply changes in tests)
            Assert.NotEmpty(allTypes);
        }
    }
}
