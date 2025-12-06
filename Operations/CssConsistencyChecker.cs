using CssClassutility.Core;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace CssClassutility.Operations;

public static class CssConsistencyChecker
{
    /// <summary>
    /// Checks a list of CSS classes against a global CSS file to find which ones are undefined.
    /// </summary>
    /// <param name="cssPath">Path to the global/theme CSS file.</param>
    /// <param name="classes">List of candidate classes to check.</param>
    /// <returns>Object containing lists of missing and found classes.</returns>
    public static object CheckMissingClasses(string cssPath, IEnumerable<string> classes)
    {
        if (!File.Exists(cssPath))
        {
            throw new FileNotFoundException($"CSS file not found: {cssPath}");
        }

        // 1. Parse global CSS to get all defined classes
        var definedClasses = CssParser.GetClasses(cssPath)
            .Select(c => c.ClassName)
            .ToHashSet();

        var missing = new List<string>();
        var found = new List<string>();

        foreach (var cls in classes)
        {
            if (string.IsNullOrWhiteSpace(cls)) continue;

            if (definedClasses.Contains(cls))
            {
                found.Add(cls);
            }
            else
            {
                missing.Add(cls);
            }
        }

        return new
        {
            missing = missing.OrderBy(x => x).ToList(),
            found = found.OrderBy(x => x).ToList(),
            totalChecked = missing.Count + found.Count,
            totalDefinedInCss = definedClasses.Count
        };
    }
}
