using System.Threading.Tasks;
using CSharpMcp;
using CSharpMcp.Tests.Helpers;
using Xunit;

namespace CSharpMcp.Tests
{
    public class RoslynApiClientTests
    {
        [Fact]
        public async Task OpenSolution_LoadsSuccessfully()
        {
            using var testProject = new TestProjectHelper();

            var solution = await testProject.Client.OpenSolutionAsync(testProject.SolutionPath);

            Assert.NotNull(solution);
            Assert.NotEmpty(solution.Projects);
        }

        [Fact]
        public async Task OpenSolution_ThrowsForInvalidPath()
        {
            var client = new RoslynApiClient();

            await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            {
                await client.OpenSolutionAsync("invalid_path.sln");
            });
        }

        [Fact]
        public async Task FindSymbolsByName_FindsSymbols()
        {
            using var testProject = new TestProjectHelper();
            await testProject.OpenSolutionAsync();

            var symbols = await testProject.Client.FindSymbolsByNameAsync("User", Microsoft.CodeAnalysis.SymbolKind.NamedType, null, null);

            Assert.NotEmpty(symbols);
        }
    }
}
