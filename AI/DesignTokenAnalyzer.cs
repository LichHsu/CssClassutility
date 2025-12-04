using CssClassutility.Models;
using System.Text.RegularExpressions;

namespace CssClassutility.AI;

/// <summary>
/// 設計 Token 識別分析器
/// </summary>
public static class DesignTokenAnalyzer
{
    /// <summary>
    /// 識別 CSS 檔案中可轉換為設計 token 的值
    /// </summary>
    public static DesignTokenAnalysis IdentifyDesignTokens(string cssPath, int minOccurrences = 2)
    {
        var analysis = new DesignTokenAnalysis();
        var classes = CssParser.GetClasses(cssPath);

        // 用於追蹤值的出現
        var colorMap = new Dictionary<string, List<string>>();
        var spacingMap = new Dictionary<string, List<string>>();
        var fontSizeMap = new Dictionary<string, List<string>>();
        var lineHeightMap = new Dictionary<string, List<string>>();
        var borderRadiusMap = new Dictionary<string, List<string>>();
        var shadowMap = new Dictionary<string, List<string>>();

        // 正則表達式模式
        var colorPattern = new Regex(@"#[0-9a-fA-F]{3,6}\b|(?:rgb|rgba|hsl|hsla)\([^)]+\)", RegexOptions.Compiled);
        var spacingPattern = new Regex(@"\b(\d+(?:\.\d+)?(?:px|rem|em|%))\b", RegexOptions.Compiled);
        var fontSizePattern = new Regex(@"font-size\s*:\s*([^;]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var lineHeightPattern = new Regex(@"line-height\s*:\s*([^;]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var borderRadiusPattern = new Regex(@"border-radius\s*:\s*([^;]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var shadowPattern = new Regex(@"box-shadow\s*:\s*([^;]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        foreach (var cssClass in classes)
        {
            var content = cssClass.Content;
            var className = cssClass.ClassName;

            // 提取顏色
            var colorMatches = colorPattern.Matches(content);
            foreach (Match match in colorMatches)
            {
                var color = NormalizeColorValue(match.Value);
                if (!colorMap.ContainsKey(color))
                    colorMap[color] = new List<string>();
                if (!colorMap[color].Contains(className))
                    colorMap[color].Add(className);
            }

            // 提取間距值（從 padding, margin 等屬性）
            var spacingProps = new[] { "padding", "margin", "gap", "top", "right", "bottom", "left", "width", "height" };
            foreach (var prop in spacingProps)
            {
                var propPattern = new Regex($@"{prop}\s*:\s*([^;]+)", RegexOptions.IgnoreCase);
                var propMatch = propPattern.Match(content);
                if (propMatch.Success)
                {
                    var spacingMatches = spacingPattern.Matches(propMatch.Groups[1].Value);
                    foreach (Match match in spacingMatches)
                    {
                        var spacing = match.Value;
                        if (!spacingMap.ContainsKey(spacing))
                            spacingMap[spacing] = new List<string>();
                        if (!spacingMap[spacing].Contains(className))
                            spacingMap[spacing].Add(className);
                    }
                }
            }

            // 提取字體大小
            var fontSizeMatch = fontSizePattern.Match(content);
            if (fontSizeMatch.Success)
            {
                var fontSize = fontSizeMatch.Groups[1].Value.Trim();
                if (!fontSizeMap.ContainsKey(fontSize))
                    fontSizeMap[fontSize] = new List<string>();
                if (!fontSizeMap[fontSize].Contains(className))
                    fontSizeMap[fontSize].Add(className);
            }

            // 提取行高
            var lineHeightMatch = lineHeightPattern.Match(content);
            if (lineHeightMatch.Success)
            {
                var lineHeight = lineHeightMatch.Groups[1].Value.Trim();
                if (!lineHeightMap.ContainsKey(lineHeight))
                    lineHeightMap[lineHeight] = new List<string>();
                if (!lineHeightMap[lineHeight].Contains(className))
                    lineHeightMap[lineHeight].Add(className);
            }

            // 提取圓角
            var borderRadiusMatch = borderRadiusPattern.Match(content);
            if (borderRadiusMatch.Success)
            {
                var borderRadius = borderRadiusMatch.Groups[1].Value.Trim();
                if (!borderRadiusMap.ContainsKey(borderRadius))
                    borderRadiusMap[borderRadius] = new List<string>();
                if (!borderRadiusMap[borderRadius].Contains(className))
                    borderRadiusMap[borderRadius].Add(className);
            }

            // 提取陰影
            var shadowMatch = shadowPattern.Match(content);
            if (shadowMatch.Success)
            {
                var shadow = shadowMatch.Groups[1].Value.Trim();
                if (!shadowMap.ContainsKey(shadow))
                    shadowMap[shadow] = new List<string>();
                if (!shadowMap[shadow].Contains(className))
                    shadowMap[shadow].Add(className);
            }
        }

        // 建立建議（只包含出現次數 >= minOccurrences 的值）
        BuildTokenSuggestions(analysis.Colors, colorMap, minOccurrences, "color");
        BuildTokenSuggestions(analysis.Spacings, spacingMap, minOccurrences, "spacing");
        BuildTokenSuggestions(analysis.FontSizes, fontSizeMap, minOccurrences, "font-size");
        BuildTokenSuggestions(analysis.LineHeights, lineHeightMap, minOccurrences, "line-height");
        BuildTokenSuggestions(analysis.BorderRadius, borderRadiusMap, minOccurrences, "radius");
        BuildTokenSuggestions(analysis.Shadows, shadowMap, minOccurrences, "shadow");

        return analysis;
    }

    private static void BuildTokenSuggestions(
        Dictionary<string, TokenSuggestion> target,
        Dictionary<string, List<string>> source,
        int minOccurrences,
        string tokenType)
    {
        foreach (var kvp in source.Where(x => x.Value.Count >= minOccurrences))
        {
            target[kvp.Key] = new TokenSuggestion
            {
                Value = kvp.Key,
                Occurrences = kvp.Value.Count,
                SuggestedTokenName = GenerateTokenName(kvp.Key, tokenType, kvp.Value.Count),
                UsedInClasses = kvp.Value
            };
        }
    }

    private static string GenerateTokenName(string value, string tokenType, int occurrences)
    {
        return tokenType switch
        {
            "color" => $"--color-{GetColorName(value)}",
            "spacing" => $"--spacing-{GetSpacingName(value)}",
            "font-size" => $"--font-size-{GetSizeName(value)}",
            "line-height" => $"--line-height-{GetSizeName(value)}",
            "radius" => $"--radius-{GetSizeName(value)}",
            "shadow" => $"--shadow-{occurrences}",
            _ => $"--{tokenType}-{occurrences}"
        };
    }

    private static string GetColorName(string color)
    {
        var normalized = NormalizeColorValue(color).ToLower();
        if (normalized.Contains("fff")) return "white";
        if (normalized.Contains("000")) return "black";
        // 使用 Hex 值作為名稱後綴，移除特定顏色判斷
        return normalized.StartsWith("#") ? normalized.Substring(1) : normalized;
    }

    private static string GetSpacingName(string spacing)
    {
        // 直接使用原始數值，替換特殊字符
        return spacing.Replace(".", "_").Replace("%", "pct");
    }

    private static string GetSizeName(string size)
    {
        // 直接使用原始數值，替換特殊字符
        return size.Replace(".", "_").Replace("%", "pct");
    }

    private static string NormalizeColorValue(string color)
    {
        color = color.Trim().ToLower();
        // 將 3 位 hex 轉為 6 位
        if (color.StartsWith('#') && color.Length == 4)
        {
            color = $"#{color[1]}{color[1]}{color[2]}{color[2]}{color[3]}{color[3]}";
        }
        return color;
    }
}
