using System.Threading.Tasks;
using CSharpMcp.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Xunit;

namespace CSharpMcp.Tests.Commands
{
    public class CompilationCommandsTests
    {
        [Fact]
        public async Task GetDiagnostics_NoErrorsInValidProject()
        {
            using var testProject = new TestProjectHelper();
            await testProject.OpenSolutionAsync();

            var diagnostics = await testProject.Client.GetDiagnosticsAsync(null, DiagnosticSeverity.Error);

            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.Empty(errors);
        }

        [Fact]
        public async Task GetDiagnostics_FindsErrorsInBrokenCode()
        {
            using var testProject = new TestProjectHelper();

            var brokenCode = @"namespace MasterProject.Services
{
    public class BrokenClass
    {
        public void BrokenMethod()
        {
            NonExistentClass x = new NonExistentClass();
        }
    }
}";
            testProject.ModifyFile("Services/BrokenClass.cs", brokenCode);

            await testProject.OpenSolutionAsync();

            var diagnostics = await testProject.Client.GetDiagnosticsAsync(null, null);

            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.NotEmpty(errors);
            Assert.Contains(errors, d => d.Id == "CS0246");
        }

        [Fact]
        public async Task SymbolExists_FindsExistingClass()
        {
            using var testProject = new TestProjectHelper();
            await testProject.OpenSolutionAsync();

            var exists = await testProject.Client.SymbolExistsAsync("UserService", SymbolKind.NamedType);

            Assert.True(exists);
        }

        [Fact]
        public async Task SymbolExists_ReturnsFalseForNonExistent()
        {
            using var testProject = new TestProjectHelper();
            await testProject.OpenSolutionAsync();

            var exists = await testProject.Client.SymbolExistsAsync("NonExistentClass", SymbolKind.NamedType);

            Assert.False(exists);
        }
    }
}
