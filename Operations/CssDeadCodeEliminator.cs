using System.Text.RegularExpressions;
using System.Text.Json;
using CssClassutility.Core;
using CssClassutility.Models;

namespace CssClassutility.Operations;

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
            if (allowSet.Contains(cls.Name))
            {
                keep = true;
            }
            // 2. Check Usage
            else if (IsUsed(cls, usedSelectors))
            {
                keep = true;
            }
            // 3. Keep Special/Root/Keyframes (Usually start with @ or :)
            else if (cls.Name.StartsWith("@") || cls.Name.StartsWith(":"))
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
        // cls.Name might be "btn" or "btn:hover"
        
        // Extract base name from cls.Name
        string baseName = cls.Name.Split(':')[0].Split('.')[0].Trim(); // Simplified
        // Actually cls.Name from GetClasses is usually just the selector string found in file.
        // We need to parse valid CSS class names from selector string.
        
        var classNamesInSelector = ExtractClassNames(cls.Name);
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

    private static string GetRawBlock(string path, CssClass cls)
    {
        // In a real implementation, we would probably have the content in memory 
        // or read it directly. CssParser.ConvertToCssJson reads it.
        // For efficiency, CssClasses List usually should contain content if we parsed carefully.
        // But the current CssClassDef only has Start/End indices.
        
        // Quick read using stream/indices would be better, but let's just read all text for MVP
        // Optimization: Read file once outside loop.
        
        // Since we don't have the file content passed in, we fall back to a slower generic read
        // or assume the caller handles IO.
        // For this tool, let's assume we read the file once.
        return ""; // Logic needs to be handled in the Tool wrapper to avoid multiple reads
    }
    
    // Improved signature to accept content
    public static string FilterContent(string cssContent, List<CssClass> classes, List<string> usedSelectors, List<string> allowList)
    {
        var sb = new System.Text.StringBuilder();
        // Since classes are sorted by line, we can just append
        
        // Need to sort classes by StartIndex to ensure order
        classes.Sort((a, b) => a.StartIndex.CompareTo(b.StartIndex));

        // Note: This simple logic skips comments or non-class content between blocks
        // A better approach for "Dead Code Elimination" usually means "Rebuild from AST".
        // But "Minification" often implies preserving everything capable.
        
        // Let's use the Entity approach: Convert kept classes to Entities, then toString.
        
        return ""; // Wrapper will handle this via CssParser
    }
}
