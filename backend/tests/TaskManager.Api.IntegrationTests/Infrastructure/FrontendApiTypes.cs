using System.Text.RegularExpressions;

namespace TaskManager.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Reads the shared TypeScript contract (<c>frontend/types/api.ts</c>) so contract tests can compare real
/// responses with the types the frontend compiles against, not only with a hand-written list.
/// </summary>
public static partial class FrontendApiTypes
{
    private static readonly Lazy<string> Source = new(() => File.ReadAllText(FindApiTs()));

    /// <summary>The property names of <c>export interface {name} { ... }</c>, in declaration order.</summary>
    public static IReadOnlyList<string> InterfaceKeys(string name)
    {
        var match = Regex.Match(Source.Value, $@"export interface {Regex.Escape(name)}\s*\{{(?<body>[^}}]*)\}}");
        Assert.True(match.Success, $"interface {name} not found in frontend/types/api.ts");
        return PropertyLine().Matches(match.Groups["body"].Value).Select(m => m.Groups["key"].Value).ToList();
    }

    /// <summary>The string literals of <c>export type {name} = "a" | "b" ...;</c>.</summary>
    public static IReadOnlyList<string> UnionValues(string name)
    {
        var match = Regex.Match(Source.Value, $@"export type {Regex.Escape(name)}\s*=(?<body>[^;]*);");
        Assert.True(match.Success, $"type {name} not found in frontend/types/api.ts");
        return StringLiteral().Matches(match.Groups["body"].Value).Select(m => m.Groups["value"].Value).ToList();
    }

    private static string FindApiTs()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "frontend", "types", "api.ts");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("frontend/types/api.ts was not found above the test output folder.");
    }

    // "  description?: string | null;" -> description. Comment lines start with "/**" or "*" and never match.
    [GeneratedRegex(@"^\s*(?<key>[A-Za-z_][A-Za-z0-9_]*)\??\s*:", RegexOptions.Multiline)]
    private static partial Regex PropertyLine();

    [GeneratedRegex("\"(?<value>[^\"]+)\"")]
    private static partial Regex StringLiteral();
}
