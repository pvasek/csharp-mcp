namespace CSharpMcp.Models;

/// <summary>
/// Information about a single member of a type.
/// </summary>
public record MemberInfo(
    string Name,
    string Kind,
    string Accessibility,
    string? Signature,
    bool IsStatic,
    bool IsAbstract,
    bool IsVirtual,
    bool IsOverride,
    string? Type
);

/// <summary>
/// Result of listing members of a type.
/// </summary>
public record ListMembersResult(
    string Type,
    string Namespace,
    List<MemberInfo> Members,
    int TotalMembers
);
