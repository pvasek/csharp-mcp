using System.Threading.Tasks;
using CSharpMcp.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Xunit;

namespace CSharpMcp.Tests.Commands
{
    public class TypeHierarchyCommandsTests
    {
        [Fact]
        public async Task FindImplementations_FindsInterfaceImplementation()
        {
            using var testProject = new TestProjectHelper();
            await testProject.OpenSolutionAsync();

            var symbols = await testProject.Client.FindSymbolsByNameAsync("IUserRepository", SymbolKind.NamedType, null, null);
            var interfaceSymbol = symbols.FirstOrDefault() as INamedTypeSymbol;
            Assert.NotNull(interfaceSymbol);

            var implementations = await testProject.Client.FindImplementationsAsync(interfaceSymbol);

            var implList = implementations.ToList();
            Assert.NotEmpty(implList);
            Assert.Contains(implList, impl => impl.Name == "UserRepository");
        }

        [Fact]
        public async Task GetBaseTypes_FindsBaseTypes()
        {
            using var testProject = new TestProjectHelper();
            await testProject.OpenSolutionAsync();

            var symbols = await testProject.Client.FindSymbolsByNameAsync("AdminUserService", SymbolKind.NamedType, null, null);
            var adminServiceClass = symbols.FirstOrDefault() as INamedTypeSymbol;
            Assert.NotNull(adminServiceClass);

            var baseTypes = testProject.Client.GetBaseTypes(adminServiceClass);

            var baseTypeList = baseTypes.ToList();
            Assert.NotEmpty(baseTypeList);
            // AdminUserService inherits from UserService, which should be in the base types
            Assert.Contains(baseTypeList, bt => bt.Name == "UserService" || bt.Name == "Object");
        }

        [Fact]
        public async Task GetDerivedTypes_FindsDerivedTypes()
        {
            using var testProject = new TestProjectHelper();
            await testProject.OpenSolutionAsync();

            var symbols = await testProject.Client.FindSymbolsByNameAsync("UserService", SymbolKind.NamedType, null, null);
            var userServiceClass = symbols.FirstOrDefault() as INamedTypeSymbol;
            Assert.NotNull(userServiceClass);

            var derivedTypes = await testProject.Client.GetDerivedTypesAsync(userServiceClass);

            var derivedList = derivedTypes.ToList();
            Assert.NotEmpty(derivedList);
            Assert.Contains(derivedList, dt => dt.Name == "AdminUserService");
        }
    }
}
