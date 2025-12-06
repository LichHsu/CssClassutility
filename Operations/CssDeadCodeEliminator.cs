using System.Text.RegularExpressions;
using System.Text.Json;
using CssClassUtility.Core;
using CssClassUtility.Models;

namespace CssClassUtility.Operations;

public static class CssDeadCodeEliminator
{
    /// <summary>
    /// 生成最小化的 CSS (移除未使用的 Class)
    /// </summary>
    /// <param name="cssPath">原始 CSS 路徑</param>
    /// <param name="usedSelectors">已使用的選擇器列表 (來自 Razor 分析)</param>
    /// <returns>清理後的 CSS 內容</returns>
    public static string GenerateMinimalCss(string cssPath, List<string> usedSelectors, List<string> allowList)
    {
        var allClasses = CssParser.GetClasses(cssPath);
        string content = File.ReadAllText(cssPath);
        var keptClasses = new List<string>();

        // Pre-process allowList to Regex if needed, simple string match for now
        var allowSet = new HashSet<string>(allowList);

        foreach (var cls in allClasses)
        {
            bool keep = false;

            // 1. Check AllowList (Always keep these)
            if (allowSet.Contains(cls.ClassName))
            {
                keep = true;
            }
            // 2. Check Usage
            else if (IsUsed(cls, usedSelectors))
            {
                keep = true;
            }
            // 3. Keep Special/Root/Keyframes (Usually start with @ or :)
            else if (cls.ClassName.StartsWith("@") || cls.ClassName.StartsWith(":"))
            {
                keep = true; // Conservative approach for now
            }

            if (keep)
            {
                // Reconstruct the CSS block
                if (cls.StartIndex >= 0 && cls.BlockEnd < content.Length && cls.BlockEnd >= cls.StartIndex)
                {
                     int len = cls.BlockEnd - cls.StartIndex + 1;
                     keptClasses.Add(content.Substring(cls.StartIndex, len));
                }
            }
        }

        return string.Join("\n", keptClasses);
    }

    private static bool IsUsed(CssClass cls, List<string> usedSelectors)
    {
        // Simple name match
        // usedSelectors contains simple class names like "btn", "btn-primary"
        // cls.ClassName might be "btn" or "btn:hover"
        
        var classNamesInSelector = ExtractClassNames(cls.Selector); // Use selector to find all classes involved
        foreach (var name in classNamesInSelector)
        {
            if (usedSelectors.Contains(name)) return true;
        }

        return false;
    }

    private static List<string> ExtractClassNames(string selector)
    {
        var list = new List<string>();
        var matches = Regex.Matches(selector, @"\.([a-zA-Z0-9_-]+)");
        foreach (Match m in matches)
        {
            list.Add(m.Groups[1].Value);
        }
        return list;
    }

    // Helper placeholders if referenced elsewhere, but GenerateMinimalCss is self-contained now.
    public static string FilterContent(string cssContent, List<CssClass> classes, List<string> usedSelectors, List<string> allowList)
    {
        // Placeholder implementation if needed by other parts, essentially same logic but in-memory
         var keptClasses = new List<string>();
         var allowSet = new HashSet<string>(allowList);
         
         foreach (var cls in classes)
         {
             bool keep = false;
             if (allowSet.Contains(cls.ClassName)) keep = true;
             else if (IsUsed(cls, usedSelectors)) keep = true;
             else if (cls.ClassName.StartsWith("@") || cls.ClassName.StartsWith(":")) keep = true;

            if (keep)
            {
                // We don't have full content here effectively unless passed in cssContent matches indices
                 int len = cls.BlockEnd - cls.StartIndex + 1;
                 if (cls.StartIndex >= 0 && cls.StartIndex + len <= cssContent.Length)
                 {
                    keptClasses.Add(cssContent.Substring(cls.StartIndex, len));
                 }
            }
         }
         return string.Join("\n", keptClasses);
    }
}
