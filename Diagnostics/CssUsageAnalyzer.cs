using CssClassUtility.Models;
using System.Text.RegularExpressions;

namespace CssClassUtility.Diagnostics;

public static class CssUsageAnalyzer
{
    public static CssUsageAnalysisResult AnalyzeUsage(string cssPath, string projectRoot, string[]? extensions = null, string[]? ignorePaths = null)
    {
        // 1. Get Defined Classes
        var definedClasses = CssParser.GetClasses(cssPath)
            .Select(c => c.ClassName)
            .Distinct()
            .ToHashSet();

        // 2. Scan Files
        var searchExtensions = extensions ?? new[] { ".razor", ".html" };
        var ignoreDetails = ignorePaths ?? new[] { "bin", "obj", "node_modules", ".git", ".vs" };

        var files = Directory.GetFiles(projectRoot, "*.*", SearchOption.AllDirectories)
            .Where(f =>
            {
                var relativePath = Path.GetRelativePath(projectRoot, f);
                var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return searchExtensions.Contains(Path.GetExtension(f).ToLower()) &&
                       !segments.Any(s => ignoreDetails.Contains(s, StringComparer.OrdinalIgnoreCase));
            });

        var usedClasses = new HashSet<string>();
        // Regex to match class="..." or Class="..."
        // This is a simple regex, might not cover all edge cases but matches the PS script logic
        var classRegex = new Regex(@"(?:class|Class)\s*=\s*""([^""]*)""", RegexOptions.Compiled);

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var matches = classRegex.Matches(content);
            foreach (Match match in matches)
            {
                var classStr = match.Groups[1].Value;
                var names = classStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var name in names)
                {
                    usedClasses.Add(name);
                }
            }
        }

        // 3. Compare
        var unused = definedClasses.Where(d => !usedClasses.Contains(d)).OrderBy(x => x).ToList();
        var undefined = usedClasses.Where(u => !definedClasses.Contains(u)).OrderBy(x => x).ToList();

        return new CssUsageAnalysisResult
        {
            DefinedCount = definedClasses.Count,
            UsedCount = usedClasses.Count,
            UnusedClasses = unused,
            UndefinedClasses = undefined
        };
    }
}
