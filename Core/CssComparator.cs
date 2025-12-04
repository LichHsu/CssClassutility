using CssClassutility.Models;
using System.Text.RegularExpressions;

namespace CssClassutility.Core;

/// <summary>
/// CSS 樣式比較器
/// </summary>
public static class CssComparator
{
    /// <summary>
    /// 語義化比較兩個 CSS 樣式區塊是否相同
    /// </summary>
    public static CssCompareResult CompareCssStyle(string styleA, string styleB)
    {
        string normA = NormalizeCss(styleA);
        string normB = NormalizeCss(styleB);

        return new CssCompareResult
        {
            IsIdentical = normA == normB,
            NormalizedA = normA,
            NormalizedB = normB
        };
    }

    private static string NormalizeCss(string css)
    {
        // 移除註解
        css = Regex.Replace(css, @"/\*[\s\S]*?\*/", "");
        
        // 正規化空白
        css = Regex.Replace(css, @"\s+", " ");
        
        // 移除前後空白
        css = css.Trim();
        
        // 移除分號前的空白
        css = css.Replace(" ;", ";");
        
        // 移除冒號後的空白
        css = css.Replace(": ", ":");
        
        // 排序屬性（簡化版）
        var props = css.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .OrderBy(p => p)
            .ToList();
        
        return string.Join(";", props);
    }
}

public class CssCompareResult
{
    public bool IsIdentical { get; set; }
    public string NormalizedA { get; set; } = string.Empty;
    public string NormalizedB { get; set; } = string.Empty;
}
