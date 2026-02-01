using System.Text.Json;
using CSharpMcp.Tests.Helpers;
using ModelContextProtocol.Protocol;

namespace CSharpMcp.Tests;

/// <summary>
/// Integration tests that verify the MCP server layer works correctly by using
/// an MCP client to communicate with the MCP server via stdio transport.
/// </summary>
public class McpIntegrationTests : IAsyncLifetime
{
    private TestProjectHelper _testProject = null!;
    private McpTestServer _server = null!;

    public async Task InitializeAsync()
    {
        _testProject = new TestProjectHelper();
        _server = await McpTestServer.StartAsync(_testProject.SolutionPath);
    }

    public async Task DisposeAsync()
    {
        await _server.DisposeAsync();
        _testProject.Dispose();
    }

    [Fact]
    public async Task ListTools_Returns18Tools()
    {
        var tools = await _server.ListToolsAsync();

        Assert.Equal(18, tools.Count);
        Assert.Contains(tools, t => t.Name == "csharp_find_definition");
        Assert.Contains(tools, t => t.Name == "csharp_find_references");
        Assert.Contains(tools, t => t.Name == "csharp_rename");
        Assert.Contains(tools, t => t.Name == "csharp_signature");
        Assert.Contains(tools, t => t.Name == "csharp_list_members");
        Assert.Contains(tools, t => t.Name == "csharp_find_implementations");
        Assert.Contains(tools, t => t.Name == "csharp_inheritance_tree");
        Assert.Contains(tools, t => t.Name == "csharp_find_callers");
        Assert.Contains(tools, t => t.Name == "csharp_find_callees");
        Assert.Contains(tools, t => t.Name == "csharp_diagnostics");
        Assert.Contains(tools, t => t.Name == "csharp_check_symbol_exists");
        Assert.Contains(tools, t => t.Name == "csharp_dependencies");
        Assert.Contains(tools, t => t.Name == "csharp_unused_code");
        Assert.Contains(tools, t => t.Name == "csharp_generate_interface");
        Assert.Contains(tools, t => t.Name == "csharp_implement_interface");
        Assert.Contains(tools, t => t.Name == "csharp_list_types");
        Assert.Contains(tools, t => t.Name == "csharp_namespace_tree");
        Assert.Contains(tools, t => t.Name == "csharp_analyze_file");
    }

    [Fact]
    public async Task FindDefinition_FindsClass()
    {
        var result = await _server.CallToolAsync("csharp_find_definition", new Dictionary<string, object?>
        {
            ["symbolName"] = "UserService",
            ["type"] = "class"
        });

        var textContent = GetTextContent(result);
        var json = JsonDocument.Parse(textContent);

        Assert.Equal("UserService", json.RootElement.GetProperty("symbol").GetString());
        Assert.Contains("UserService.cs", json.RootElement.GetProperty("location").GetProperty("file").GetString());
        // Roslyn returns "namedtype" for classes (SymbolKind.NamedType)
        Assert.Equal("namedtype", json.RootElement.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task FindDefinition_FindsMethod()
    {
        var result = await _server.CallToolAsync("csharp_find_definition", new Dictionary<string, object?>
        {
            ["symbolName"] = "GetById",
            ["type"] = "method"
        });

        var textContent = GetTextContent(result);
        var json = JsonDocument.Parse(textContent);

        Assert.Equal("GetById", json.RootElement.GetProperty("symbol").GetString());
        Assert.Equal("method", json.RootElement.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task FindDefinition_ReturnsErrorForNonExistentSymbol()
    {
        var result = await _server.CallToolAsync("csharp_find_definition", new Dictionary<string, object?>
        {
            ["symbolName"] = "NonExistentClass"
        });

        var textContent = GetTextContent(result);
        var json = JsonDocument.Parse(textContent);

        Assert.True(json.RootElement.TryGetProperty("error", out var errorProp));
        Assert.Contains("not found", errorProp.GetString());
    }

    [Fact]
    public async Task FindReferences_FindsMethodReferences()
    {
        var result = await _server.CallToolAsync("csharp_find_references", new Dictionary<string, object?>
        {
            ["symbolName"] = "GetById",
            ["type"] = "method"
        });

        var textContent = GetTextContent(result);
        var json = JsonDocument.Parse(textContent);

        Assert.True(json.RootElement.GetProperty("totalReferences").GetInt32() > 0);
        Assert.NotEmpty(json.RootElement.GetProperty("references").EnumerateArray().ToList());
    }

    [Fact]
    public async Task Diagnostics_ReturnsCompilationInfo()
    {
        var result = await _server.CallToolAsync("csharp_diagnostics", new Dictionary<string, object?>());

        var textContent = GetTextContent(result);
        var json = JsonDocument.Parse(textContent);

        Assert.True(json.RootElement.TryGetProperty("totalErrors", out _));
        Assert.True(json.RootElement.TryGetProperty("totalWarnings", out _));
        Assert.True(json.RootElement.TryGetProperty("diagnostics", out _));
    }

    [Fact]
    public async Task CheckSymbolExists_FindsExistingSymbol()
    {
        var result = await _server.CallToolAsync("csharp_check_symbol_exists", new Dictionary<string, object?>
        {
            ["symbolName"] = "User",
            ["type"] = "class"
        });

        var textContent = GetTextContent(result);
        var json = JsonDocument.Parse(textContent);

        Assert.True(json.RootElement.GetProperty("exists").GetBoolean());
        Assert.Equal("User", json.RootElement.GetProperty("symbol").GetString());
        Assert.NotNull(json.RootElement.GetProperty("location").GetString());
    }

    [Fact]
    public async Task CheckSymbolExists_ReturnsFalseForNonExistentSymbol()
    {
        var result = await _server.CallToolAsync("csharp_check_symbol_exists", new Dictionary<string, object?>
        {
            ["symbolName"] = "NonExistentSymbol",
            ["type"] = "class"
        });

        var textContent = GetTextContent(result);
        var json = JsonDocument.Parse(textContent);

        Assert.False(json.RootElement.GetProperty("exists").GetBoolean());
    }

    [Fact]
    public async Task ListTypes_ReturnsAllTypes()
    {
        var result = await _server.CallToolAsync("csharp_list_types", new Dictionary<string, object?>());

        var textContent = GetTextContent(result);
        var json = JsonDocument.Parse(textContent);

        var types = json.RootElement.GetProperty("types").EnumerateArray().ToList();
        Assert.NotEmpty(types);

        // Verify known types exist
        var typeNames = types.Select(t => t.GetProperty("name").GetString()).ToList();
        Assert.Contains("User", typeNames);
        Assert.Contains("UserService", typeNames);
        Assert.Contains("IUserRepository", typeNames);
    }

    [Fact]
    public async Task ListTypes_FiltersNamespace()
    {
        var result = await _server.CallToolAsync("csharp_list_types", new Dictionary<string, object?>
        {
            ["namespaceFilter"] = "MasterProject.Services"
        });

        var textContent = GetTextContent(result);
        var json = JsonDocument.Parse(textContent);

        var types = json.RootElement.GetProperty("types").EnumerateArray().ToList();
        Assert.NotEmpty(types);

        // All types should be in the Services namespace
        Assert.All(types, t =>
        {
            var ns = t.GetProperty("namespace").GetString();
            Assert.Equal("MasterProject.Services", ns);
        });
    }

    [Fact]
    public async Task FindImplementations_FindsInterfaceImplementations()
    {
        var result = await _server.CallToolAsync("csharp_find_implementations", new Dictionary<string, object?>
        {
            ["symbolName"] = "IUserRepository"
        });

        var textContent = GetTextContent(result);
        var json = JsonDocument.Parse(textContent);

        var implementations = json.RootElement.GetProperty("implementations").EnumerateArray().ToList();
        Assert.NotEmpty(implementations);

        var implNames = implementations.Select(i => i.GetProperty("name").GetString()).ToList();
        Assert.Contains("UserRepository", implNames);
    }

    [Fact]
    public async Task InheritanceTree_ShowsClassHierarchy()
    {
        var result = await _server.CallToolAsync("csharp_inheritance_tree", new Dictionary<string, object?>
        {
            ["typeName"] = "AdminUserService"
        });

        var textContent = GetTextContent(result);
        var json = JsonDocument.Parse(textContent);

        // AdminUserService extends UserService, so ancestors should include UserService
        var ancestors = json.RootElement.GetProperty("ancestors").EnumerateArray()
            .Select(a => a.GetString()).ToList();

        Assert.Contains("MasterProject.Services.UserService", ancestors);
    }

    [Fact]
    public async Task ListMembers_ReturnsTypeMembers()
    {
        var result = await _server.CallToolAsync("csharp_list_members", new Dictionary<string, object?>
        {
            ["typeName"] = "User"
        });

        var textContent = GetTextContent(result);
        var json = JsonDocument.Parse(textContent);

        var members = json.RootElement.GetProperty("members").EnumerateArray().ToList();
        Assert.NotEmpty(members);

        var memberNames = members.Select(m => m.GetProperty("name").GetString()).ToList();
        // User class should have some properties/methods
        Assert.True(memberNames.Count > 0);
    }

    [Fact]
    public async Task Signature_ReturnsMethodSignature()
    {
        var result = await _server.CallToolAsync("csharp_signature", new Dictionary<string, object?>
        {
            ["symbolName"] = "GetById",
            ["type"] = "method"
        });

        var textContent = GetTextContent(result);
        var json = JsonDocument.Parse(textContent);

        Assert.Equal("GetById", json.RootElement.GetProperty("symbol").GetString());
        Assert.Equal("method", json.RootElement.GetProperty("kind").GetString());

        var signatures = json.RootElement.GetProperty("signatures").EnumerateArray().ToList();
        Assert.NotEmpty(signatures);
        Assert.NotNull(signatures[0].GetProperty("declaration").GetString());
    }

    private static string GetTextContent(CallToolResult response)
    {
        var textContent = response.Content
            .OfType<TextContentBlock>()
            .FirstOrDefault();

        if (textContent == null)
            throw new InvalidOperationException("Response did not contain text content");

        return textContent.Text;
    }
}
