using System.Text.Json;

namespace CSharpMcp.Core;

/// <summary>
/// Formats command output in JSON, text, or markdown format.
/// </summary>
public class OutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Formats data in the specified output format.
    /// </summary>
    public string Format<T>(T data, OutputFormat format)
    {
        return format switch
        {
            OutputFormat.Json => ToJson(data),
            OutputFormat.Text => ToText(data),
            OutputFormat.Markdown => ToMarkdown(data),
            _ => throw new ArgumentException($"Unknown output format: {format}")
        };
    }

    /// <summary>
    /// Converts data to JSON format.
    /// </summary>
    private string ToJson<T>(T data)
    {
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    /// <summary>
    /// Converts data to human-readable text format.
    /// </summary>
    private string ToText<T>(T data)
    {
        // For now, use a simple ToString-based approach
        // Individual commands can override this for better formatting
        if (data == null) return string.Empty;

        var type = data.GetType();
        if (type.IsPrimitive || type == typeof(string))
        {
            return data.ToString() ?? string.Empty;
        }

        // Use reflection to format as key-value pairs
        var properties = type.GetProperties();
        var lines = new List<string>();

        foreach (var prop in properties)
        {
            var value = prop.GetValue(data);
            lines.Add($"{prop.Name}: {value}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Converts data to markdown format.
    /// </summary>
    private string ToMarkdown<T>(T data)
    {
        // For now, use a simple markdown table
        // Individual commands can override this for better formatting
        if (data == null) return string.Empty;

        var type = data.GetType();
        if (type.IsPrimitive || type == typeof(string))
        {
            return data.ToString() ?? string.Empty;
        }

        var properties = type.GetProperties();
        var lines = new List<string>();

        foreach (var prop in properties)
        {
            var value = prop.GetValue(data);
            lines.Add($"**{prop.Name}**: {value}");
        }

        return string.Join("  " + Environment.NewLine, lines);
    }
}
