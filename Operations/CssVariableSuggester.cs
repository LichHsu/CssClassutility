using System.Text.RegularExpressions;
using CssClassUtility.Models;
using CssClassUtility.Core;

namespace CssClassUtility.Operations;

public static class CssVariableSuggester
{
    public class VariableSuggestion
    {
        public string Value { get; set; } = "";
        public int Count { get; set; }
        public string SuggestedName { get; set; } = "";
        public string Type { get; set; } = ""; // Color, Size, etc.
        public List<string> UsedInClasses { get; set; } = new();
    }

    public static List<VariableSuggestion> SuggestVariables(string cssPath, int threshold = 3)
    {
        var classes = CssParser.GetClasses(cssPath);
        var valueCounts = new Dictionary<string, VariableSuggestion>();

        foreach (var cls in classes)
        {
            var entity = CssParser.ConvertToCssJson(cls);
            foreach (var prop in entity.Properties)
            {
                string val = prop.Value;

                // 忽略已是變數的值
                if (val.Contains("var(--")) continue;

                // 識別顏色 (Hex, RGB, HSL)
                if (IsColor(val))
                {
                    AddUsage(valueCounts, val, "color", cls.ClassName);
                }
                // 識別尺寸 (px, rem)
                else if (IsSize(val))
                {
                    AddUsage(valueCounts, val, "size", cls.ClassName);
                }
            }
        }

        var results = valueCounts.Values
            .Where(x => x.Count >= threshold)
            .OrderByDescending(x => x.Count)
            .ToList();

        // Generate names
        foreach (var res in results)
        {
            res.SuggestedName = GenerateName(res.Type, res.Value);
        }

        return results;
    }

    private static void AddUsage(Dictionary<string, VariableSuggestion> dict, string val, string type, string className)
    {
        if (!dict.ContainsKey(val))
        {
            dict[val] = new VariableSuggestion { Value = val, Type = type };
        }
        dict[val].Count++;
        if (dict[val].UsedInClasses.Count < 5) // Limit sample size
        {
            dict[val].UsedInClasses.Add(className);
        }
    }

    private static bool IsColor(string val)
    {
        return val.StartsWith("#") || val.StartsWith("rgb") || val.StartsWith("hsl");
    }

    private static bool IsSize(string val)
    {
        return val.EndsWith("px") || val.EndsWith("rem") || val.EndsWith("em");
    }

    private static string GenerateName(string type, string val)
    {
        // Simple heuristic
        if (type == "color") return $"--color-{val.Replace("#", "").Replace("(", "-").Replace(")", "").Replace(",", "-")}";
        if (type == "size") return $"--size-{val.Replace(".", "-")}";
        return $"--var-{val}";
    }
}
