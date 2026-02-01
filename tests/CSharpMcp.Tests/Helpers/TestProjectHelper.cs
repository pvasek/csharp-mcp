using System;
using System.IO;
using CSharpMcp;

namespace CSharpMcp.Tests.Helpers
{
    /// <summary>
    /// Helper class for managing test projects by copying the master project
    /// </summary>
    public class TestProjectHelper : IDisposable
    {
        private static readonly string MasterProjectPath = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "TestData", "MasterProject"));

        public string TestProjectPath { get; private set; }
        public string SolutionPath { get; private set; }
        public RoslynApiClient Client { get; private set; }

        public TestProjectHelper()
        {
            // Create a unique test directory
            var testId = Guid.NewGuid().ToString("N")[..8];
            TestProjectPath = Path.Combine(Path.GetTempPath(), $"CSharpMcpTest_{testId}");

            // Copy master project to test directory
            CopyDirectory(MasterProjectPath, TestProjectPath);

            // Set solution path
            SolutionPath = Path.Combine(TestProjectPath, "MasterProject.sln");

            // Create client
            Client = new RoslynApiClient();
        }

        public async Task OpenSolutionAsync()
        {
            await Client.OpenSolutionAsync(SolutionPath);
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            if (!Directory.Exists(sourceDir))
                throw new DirectoryNotFoundException($"Master project not found at: {sourceDir}");

            Directory.CreateDirectory(destDir);

            // Copy all files
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            // Copy all subdirectories
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                // Skip bin and obj directories
                var dirName = Path.GetFileName(subDir);
                if (dirName == "bin" || dirName == "obj")
                    continue;

                var destSubDir = Path.Combine(destDir, dirName);
                CopyDirectory(subDir, destSubDir);
            }
        }

        public void ModifyFile(string relativePath, string newContent)
        {
            var fullPath = Path.Combine(TestProjectPath, relativePath);
            File.WriteAllText(fullPath, newContent);
        }

        public string ReadFile(string relativePath)
        {
            var fullPath = Path.Combine(TestProjectPath, relativePath);
            return File.ReadAllText(fullPath);
        }

        public string GetFullPath(string relativePath)
        {
            return Path.Combine(TestProjectPath, relativePath);
        }

        public void Dispose()
        {
            // Clean up test directory
            if (Directory.Exists(TestProjectPath))
            {
                try
                {
                    Directory.Delete(TestProjectPath, recursive: true);
                }
                catch
                {
                    // Ignore cleanup failures
                }
            }
        }
    }
}
