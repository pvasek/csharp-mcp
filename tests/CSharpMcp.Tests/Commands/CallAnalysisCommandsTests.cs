using System.Threading.Tasks;
using CSharpMcp.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Xunit;

namespace CSharpMcp.Tests.Commands
{
    public class CallAnalysisCommandsTests
    {
        [Fact]
        public async Task FindCallers_FindsMethodCallers()
        {
            using var testProject = new TestProjectHelper();
            await testProject.OpenSolutionAsync();

            var symbols = await testProject.Client.FindSymbolsByNameAsync("GetById", SymbolKind.Method, null, null);
            var getByIdMethod = symbols.FirstOrDefault(s => s.ContainingType.Name == "UserService") as IMethodSymbol;
            Assert.NotNull(getByIdMethod);

            var callers = await testProject.Client.FindCallersAsync(getByIdMethod);

            var callerList = callers.ToList();
            Assert.NotEmpty(callerList);
            Assert.Contains(callerList, c => c.ContainingType.Name == "UserController" || c.ContainingType.Name == "AdminUserService");
        }

        [Fact]
        public async Task FindCallees_FindsMethodCalls()
        {
            using var testProject = new TestProjectHelper();
            await testProject.OpenSolutionAsync();

            var symbols = await testProject.Client.FindSymbolsByNameAsync("GetUser", SymbolKind.Method, null, null);
            var getUserMethod = symbols.FirstOrDefault(s => s.ContainingType.Name == "UserController") as IMethodSymbol;
            Assert.NotNull(getUserMethod);

            var callees = await testProject.Client.FindCalleesAsync(getUserMethod);

            var calleeList = callees.ToList();
            Assert.NotEmpty(calleeList);
            Assert.Contains(calleeList, c => c.Name == "GetById");
        }

        [Fact]
        public async Task FindCallees_FindsMultipleCalls()
        {
            using var testProject = new TestProjectHelper();
            await testProject.OpenSolutionAsync();

            var symbols = await testProject.Client.FindSymbolsByNameAsync("ActivateUser", SymbolKind.Method, null, null);
            var activateMethod = symbols.FirstOrDefault() as IMethodSymbol;
            Assert.NotNull(activateMethod);

            var callees = await testProject.Client.FindCalleesAsync(activateMethod);

            var calleeList = callees.ToList();
            Assert.NotEmpty(calleeList);
            // ActivateUser calls GetById, Activate, and Update
            Assert.Contains(calleeList, c => c.Name == "GetById");
        }
    }
}
