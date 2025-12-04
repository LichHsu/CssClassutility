using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CssClassutility.Models;

namespace CssClassutility;

/// <summary>
/// CssParser 擴充功能：診斷、重構與進階操作
/// </summary>
public partial class CssParser
{
    #region 診斷功能

    /// <summary>
    /// 診斷 CSS 結構完整性
    /// </summary>
    public static CssDiagnosisResult DiagnosisCssStruct(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("找不到檔案", path);

        string content = File.ReadAllText(path);
        var result = new CssDiagnosisResult();

        // 計算大括號 (排除字串和註解內的)
        int openCount = 0, closeCount = 0;
        bool inComment = false, inString = false;
        char stringChar = ' ';

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];

            // 處理註解
            if (!inString && !inComment && c == '/' && i + 1 < content.Length && content[i + 1] == '*')
            {
                inComment = true;
                i++;
                continue;
            }
            if (inComment && c == '*' && i + 1 < content.Length && content[i + 1] == '/')
            {
                inComment = false;
                i++;
                continue;
            }
            if (inComment) continue;

            // 處理字串
            if (!inString && (c == '"' || c == '\''))
            {
                inString = true;
                stringChar = c;
                continue;
            }
            if (inString && c == stringChar)
            {
                // 檢查轉義
                int backslashCount = 0;
                int j = i - 1;
                while (j >= 0 && content[j] == '\\') { backslashCount++; j--; }
                if (backslashCount % 2 == 0) inString = false;
                continue;
            }
            if (inString) continue;

            // 計算大括號
            if (c == '{') openCount++;
            else if (c == '}') closeCount++;
        }

        result.OpenBraceCount = openCount;
        result.CloseBraceCount = closeCount;

        if (openCount != closeCount)
        {
            result.Errors.Add($"大括號不匹配：開 {openCount} 個，閉 {closeCount} 個");
        }

        // 取得所有 Class 並檢查重複
        var classes = GetClasses(path);
        result.TotalClasses = classes.Count;

        var grouped = classes.GroupBy(c => c.ClassName)
            .Where(g => g.Count() > 1);

        foreach (var group in grouped)
        {
            result.DuplicateClasses.Add(new DuplicateClassInfo
            {
                ClassName = group.Key,
                Count = group.Count(),
                Selectors = group.Select(c => c.Selector).Distinct().ToList()
            });
        }

        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    /// <summary>
    /// 取得重複的 Class 列表
    /// </summary>
    public static List<DuplicateClassInfo> GetDuplicateClasses(string path)
    {
        var classes = GetClasses(path);
        return classes.GroupBy(c => c.ClassName)
            .Where(g => g.Count() > 1)
            .Select(g => new DuplicateClassInfo
            {
                ClassName = g.Key,
                Count = g.Count(),
                Selectors = g.Select(c => c.Selector).Distinct().ToList()
            })
            .ToList();
    }

    #endregion

    #region 重構功能

    /// <summary>
    /// 重構 CSS：去除多餘空行、按名稱排序
    /// </summary>
    public static string RestructureCss(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("找不到檔案", path);

        // 建立備份
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string backupPath = $"{path}.restructure_backup_{timestamp}";
        File.Copy(path, backupPath, true);

        try
        {
            string content = File.ReadAllText(path);
            var classes = GetClasses(path);

            // 按 ClassName 排序 (穩定排序保持同名 Class 的原始順序)
            var sortedClasses = classes
                .Select((c, index) => new { Class = c, OriginalIndex = index })
                .OrderBy(x => x.Class.ClassName)
                .ThenBy(x => x.OriginalIndex)
                .Select(x => x.Class)
                .ToList();

            // 重建 CSS
            var sb = new StringBuilder();
            string? lastContext = null;

            foreach (var cls in sortedClasses)
            {
                // 處理 Media Query 上下文
                if (cls.Context != lastContext)
                {
                    if (lastContext != null)
                    {
                        sb.AppendLine("}");
                        sb.AppendLine();
                    }

                    if (!string.IsNullOrEmpty(cls.Context))
                    {
                        sb.AppendLine(cls.Context + " {");
                    }

                    lastContext = cls.Context;
                }

                // 格式化 Class
                string indent = string.IsNullOrEmpty(cls.Context) ? "" : "    ";
                sb.AppendLine($"{indent}{cls.Selector} {{");

                // 排序屬性
                var props = ContentToPropertiesPublic(cls.Content);
                foreach (var key in props.Keys.OrderBy(k => k))
                {
                    sb.AppendLine($"{indent}    {key}: {props[key]};");
                }

                sb.AppendLine($"{indent}}}");
                sb.AppendLine();
            }

            // 關閉最後的 Media Query
            if (lastContext != null && !string.IsNullOrEmpty(lastContext))
            {
                sb.AppendLine("}");
            }

            string newContent = sb.ToString().TrimEnd() + "\n";
            File.WriteAllText(path, newContent, Encoding.UTF8);

            return $"重構完成！共處理 {sortedClasses.Count} 個 Class，備份於 {backupPath}";
        }
        catch (Exception ex)
        {
            File.Copy(backupPath, path, true);
            return $"重構失敗：{ex.Message}（已從備份還原）";
        }
    }

    #endregion

    #region 擷取功能

    /// <summary>
    /// 回傳指定 Class 的原始文字
    /// </summary>
    public static string TakeCssClass(string path, string className, int index = 0)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("找不到檔案", path);

        string content = File.ReadAllText(path);
        var classes = GetClasses(path)
            .Where(c => c.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (classes.Count == 0)
            throw new Exception($"找不到 Class .{className}");

        if (index >= classes.Count)
            throw new Exception($"索引 {index} 超出範圍，共有 {classes.Count} 個同名 Class");

        var target = classes[index];
        return content.Substring(target.StartIndex, target.BlockEnd - target.StartIndex + 1);
    }

    #endregion

    #region 合併功能

    /// <summary>
    /// 從另一個 CSS 檔合併 Class
    /// </summary>
    public static string MergeCssClassFromFile(
        string targetPath,
        string targetClassName,
        string sourcePath,
        string sourceClassName,
        MergeStrategy strategy = MergeStrategy.Overwrite,
        int targetIndex = 0,
        int sourceIndex = 0)
    {
        // 取得來源 Class
        var sourceClasses = GetClasses(sourcePath)
            .Where(c => c.ClassName.Equals(sourceClassName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (sourceClasses.Count == 0)
            throw new Exception($"來源檔案中找不到 Class .{sourceClassName}");

        if (sourceIndex >= sourceClasses.Count)
            throw new Exception($"來源索引 {sourceIndex} 超出範圍");

        var sourceClass = sourceClasses[sourceIndex];
        var sourceEntity = ConvertToCssJson(sourceClass);

        // 取得目標 Class
        var targetClasses = GetClasses(targetPath)
            .Where(c => c.ClassName.Equals(targetClassName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetClasses.Count == 0)
            throw new Exception($"目標檔案中找不到 Class .{targetClassName}");

        if (targetIndex >= targetClasses.Count)
            throw new Exception($"目標索引 {targetIndex} 超出範圍");

        var targetClass = targetClasses[targetIndex];
        var targetEntity = ConvertToCssJson(targetClass);

        // 合併屬性
        bool modified = MergePropertiesPublic(targetEntity.Properties, sourceEntity.Properties, strategy);

        if (modified)
        {
            string newCss = ConvertFromCssJson(targetEntity);
            ReplaceBlockPublic(targetPath, targetClass.StartIndex, targetClass.BlockEnd, newCss);
            return $"已合併 .{sourceClassName} → .{targetClassName} ({strategy})";
        }

        return $"未變更: .{targetClassName}";
    }

    #endregion

    #region 支援重複 Class 的更新/刪除

    /// <summary>
    /// 更新 Class 屬性 (支援索引選擇重複 Class)
    /// </summary>
    public static string UpdateClassPropertyByIndex(
        string path, string className, string key, string value, string action, int index = 0)
    {
        var classes = GetClasses(path)
            .Where(c => c.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (classes.Count == 0)
            return $"警告: 在 {path} 中找不到 Class .{className}";

        if (index >= classes.Count)
            return $"警告: 索引 {index} 超出範圍，共有 {classes.Count} 個同名 Class";

        var target = classes[index];
        var props = ContentToPropertiesPublic(target.Content);
        bool modified = false;
        string lowerKey = key.ToLower().Trim();

        if (action.Equals("Set", StringComparison.OrdinalIgnoreCase))
        {
            if (!props.TryGetValue(lowerKey, out string? oldVal) || oldVal != value)
            {
                props[lowerKey] = value;
                modified = true;
            }
        }
        else if (action.Equals("Remove", StringComparison.OrdinalIgnoreCase))
        {
            if (props.Remove(lowerKey)) modified = true;
        }

        if (!modified) return $"未變更: .{className}";

        string newCssContent = PropertiesToContentPublic(props, target.Selector);
        ReplaceBlockPublic(path, target.StartIndex, target.BlockEnd, newCssContent);

        return $"已成功更新 CSS Class: .{className} (索引 {index})";
    }

    /// <summary>
    /// 移除 Class (支援索引選擇重複 Class)
    /// </summary>
    public static string RemoveCssClassByIndex(string path, string className, int index = 0)
    {
        if (!File.Exists(path)) return $"錯誤：找不到檔案 {path}";

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string backupPath = $"{path}.safe_backup_{timestamp}";
        File.Copy(path, backupPath, true);

        try
        {
            string content = File.ReadAllText(path);
            var classes = GetClasses(path)
                .Where(c => c.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (classes.Count == 0)
                return $"警告：在 {path} 中找不到 Class .{className}";

            if (index >= classes.Count)
                return $"警告: 索引 {index} 超出範圍，共有 {classes.Count} 個同名 Class";

            var target = classes[index];

            if (target.Selector.Contains(','))
                return $"安全防護：Class .{className} 屬於群組選擇器，請手動處理";

            int removeStartIndex = target.StartIndex;
            int removeEndIndex = target.BlockEnd;

            while (removeStartIndex > 0 && char.IsWhiteSpace(content[removeStartIndex - 1]))
                removeStartIndex--;

            string newContent = content.Remove(removeStartIndex, removeEndIndex - removeStartIndex + 1);
            string comment = $"\n/* .{className} (index {index}) removed by CssClassManager */\n";
            newContent = newContent.Insert(removeStartIndex, comment);

            int openCount = newContent.Count(c => c == '{');
            int closeCount = newContent.Count(c => c == '}');

            if (openCount != closeCount)
                throw new Exception($"大括號不匹配 (Open: {openCount}, Close: {closeCount})");

            File.WriteAllText(path, newContent, Encoding.UTF8);
            return $"成功移除 .{className} (索引 {index})，備份於 {backupPath}";
        }
        catch (Exception ex)
        {
            File.Copy(backupPath, path, true);
            return $"移除失敗：{ex.Message}（已從備份還原）";
        }
    }

    #endregion

    #region 公開的輔助方法 (供擴充功能使用)

    public static Dictionary<string, string> ContentToPropertiesPublic(string content)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var cleanContent = CommentPattern().Replace(content, "");

        foreach (var prop in cleanContent.Split(';'))
        {
            var parts = prop.Split([':', ], 2);
            if (parts.Length == 2)
            {
                string key = parts[0].Trim().ToLower();
                string val = parts[1].Trim();
                if (!string.IsNullOrEmpty(key)) props[key] = val;
            }
        }
        return props;
    }

    public static string PropertiesToContentPublic(Dictionary<string, string> props, string selector)
    {
        var sb = new StringBuilder();
        sb.Append(selector).Append(" {\n");
        foreach (var key in props.Keys.OrderBy(k => k))
        {
            sb.Append($"    {key}: {props[key]};\n");
        }
        sb.Append('}');
        return sb.ToString();
    }

    public static void ReplaceBlockPublic(string path, int startIndex, int endIndex, string newContent)
    {
        string backupPath = $"{path}.bak";
        File.Copy(path, backupPath, true);

        string content = File.ReadAllText(path);
        int lengthToRemove = endIndex - startIndex + 1;

        if (startIndex < 0 || startIndex + lengthToRemove > content.Length)
            throw new IndexOutOfRangeException("檔案內容在處理過程中發生變化");

        string tempContent = content.Remove(startIndex, lengthToRemove);
        string finalContent = tempContent.Insert(startIndex, newContent);
        File.WriteAllText(path, finalContent, Encoding.UTF8);
    }

    public static bool MergePropertiesPublic(
        SortedDictionary<string, string> target,
        SortedDictionary<string, string> source,
        MergeStrategy strategy)
    {
        bool modified = false;

        foreach (var kvp in source)
        {
            string key = kvp.Key;
            string val = kvp.Value;

            switch (strategy)
            {
                case MergeStrategy.Overwrite:
                    if (!target.TryGetValue(key, out string? existingVal) || existingVal != val)
                    {
                        target[key] = val;
                        modified = true;
                    }
                    break;

                case MergeStrategy.FillMissing:
                    if (!target.ContainsKey(key))
                    {
                        target[key] = val;
                        modified = true;
                    }
                    break;

                case MergeStrategy.PruneDuplicate:
                    if (target.TryGetValue(key, out string? targetVal) && targetVal == val)
                    {
                        target.Remove(key);
                        modified = true;
                    }
                    break;
            }
        }

        return modified;
    }

    #endregion

    #region AI 輔助功能

    /// <summary>
    /// 識別 CSS 檔案中可轉換為設計 token 的值
    /// </summary>
    public static DesignTokenAnalysis IdentifyDesignTokens(string cssPath, int minOccurrences = 2)
    {
        var analysis = new DesignTokenAnalysis();
        var classes = GetClasses(cssPath);

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

    /// <summary>
    /// 追蹤 CSS class 在專案中的使用位置
    /// </summary>
    public static CssUsageTrace TraceCssUsage(string className, string projectRoot, string[]? fileExtensions = null)
    {
        var trace = new CssUsageTrace
        {
            ClassName = className,
            TotalOccurrences = 0,
            Locations = new List<UsageLocation>()
        };

        // 預設搜尋的副檔名
        fileExtensions ??= new[] { ".html", ".razor", ".jsx", ".tsx", ".vue", ".cshtml", ".aspx" };

        // 排除的目錄
        var excludeDirs = new[] { "node_modules", "bin", "obj", ".git", ".vs", "wwwroot\\lib" };

        // 正則表達式模式
        var patterns = new[]
        {
            new Regex($@"\bclass\s*=\s*[""']([^""']*\b{Regex.Escape(className)}\b[^""']*)[""']", RegexOptions.Compiled),
            new Regex($@"\bclassName\s*=\s*[""']([^""']*\b{Regex.Escape(className)}\b[^""']*)[""']", RegexOptions.Compiled),
            new Regex($@"classList\.(?:add|remove|toggle)\([""']{Regex.Escape(className)}[""']\)", RegexOptions.Compiled),
            new Regex($@"@class\s*=\s*[""']([^""']*\b{Regex.Escape(className)}\b[^""']*)[""']", RegexOptions.Compiled) // Blazor
        };

        if (!Directory.Exists(projectRoot))
        {
            return trace;
        }

        // 遞迴搜尋檔案
        SearchDirectory(projectRoot, fileExtensions, excludeDirs, patterns, className, trace);

        trace.TotalOccurrences = trace.Locations.Count;
        return trace;
    }

    /// <summary>
    /// 分析 CSS 檔案並提供重構建議
    /// </summary>
    public static RefactoringAnalysis SuggestRefactoring(string cssPath, int minPriority = 1)
    {
        var analysis = new RefactoringAnalysis
        {
            FilePath = cssPath,
            Suggestions = new List<RefactoringSuggestion>(),
            Statistics = new Dictionary<string, int>()
        };

        var classes = GetClasses(cssPath);
        analysis.Statistics["totalClasses"] = classes.Count;

        // 1. 找出重複的屬性組合（建議提取共用 class）
        FindCommonProperties(classes, analysis);

        // 2. 找出可替換為 token 的硬編碼值
        FindHardcodedValues(classes, analysis, cssPath);

        // 3. 找出相似度高的 class（建議合併）
        FindSimilarClasses(classes, analysis);

        // 4. 找出過度複雜的 class（建議拆分）
        FindComplexClasses(classes, analysis);

        // 5. 找出重複定義的屬性
        FindDuplicatePropertiesInClass(classes, analysis);

        // 依優先級排序並過濾
        analysis.Suggestions = analysis.Suggestions
            .Where(s => s.Priority >= minPriority)
            .OrderByDescending(s => s.Priority)
            .ToList();

        analysis.Statistics["totalSuggestions"] = analysis.Suggestions.Count;
        return analysis;
    }

    #endregion

    #region AI 輔助功能 - 輔助方法

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
        // 簡化的顏色命名（實際應使用更複雜的邏輯）
        var normalized = NormalizeColorValue(color).ToLower();
        if (normalized.Contains("fff")) return "white";
        if (normalized.Contains("000")) return "black";
        if (normalized.StartsWith("#3b82f6") || normalized.StartsWith("#3b8")) return "primary-500";
        if (normalized.StartsWith("#6b7280") || normalized.StartsWith("#6b7")) return "gray-500";
        return normalized.Length > 7 ? normalized.Substring(1, 6) : normalized.Substring(1);
    }

    private static string GetSpacingName(string spacing)
    {
        // 提取數值
        var match = Regex.Match(spacing, @"(\d+(?:\.\d+)?)");
        if (match.Success)
        {
            var value = double.Parse(match.Groups[1].Value);
            if (spacing.Contains("rem")) return ((int)(value * 4)).ToString();
            if (spacing.Contains("px")) return ((int)(value / 4)).ToString();
        }
        return "custom";
    }

    private static string GetSizeName(string size)
    {
        var match = Regex.Match(size, @"(\d+(?:\.\d+)?)");
        if (match.Success)
        {
            var value = double.Parse(match.Groups[1].Value);
            return value switch
            {
                <= 12 => "xs",
                <= 14 => "sm",
                <= 16 => "base",
                <= 18 => "lg",
                <= 24 => "xl",
                _ => "2xl"
            };
        }
        return "custom";
    }

    private static string NormalizeColorValue(string color)
    {
        color = color.Trim().ToLower();
        // 將 3 位 hex 轉為 6 位
        if (color.StartsWith("#") && color.Length == 4)
        {
            color = $"#{color[1]}{color[1]}{color[2]}{color[2]}{color[3]}{color[3]}";
        }
        return color;
    }

    private static void SearchDirectory(
        string directory,
        string[] extensions,
        string[] excludeDirs,
        Regex[] patterns,
        string className,
        CssUsageTrace trace)
    {
        try
        {
            // 檢查是否為排除目錄
            if (excludeDirs.Any(ex => directory.Contains(ex, StringComparison.OrdinalIgnoreCase)))
                return;

            // 搜尋檔案
            foreach (var ext in extensions)
            {
                var files = Directory.GetFiles(directory, $"*{ext}", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    SearchFile(file, patterns, className, trace);
                }
            }

            // 遞迴搜尋子目錄
            foreach (var subDir in Directory.GetDirectories(directory))
            {
                SearchDirectory(subDir, extensions, excludeDirs, patterns, className, trace);
            }
        }
        catch
        {
            // 忽略無法存取的目錄
        }
    }

    private static void SearchFile(string filePath, Regex[] patterns, string className, CssUsageTrace trace)
    {
        try
        {
            var lines = File.ReadAllLines(filePath);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                foreach (var pattern in patterns)
                {
                    if (pattern.IsMatch(line))
                    {
                        trace.Locations.Add(new UsageLocation
                        {
                            FilePath = filePath,
                            LineNumber = i + 1,
                            Context = line.Trim()
                        });
                        break; // 每行只記錄一次
                    }
                }
            }
        }
        catch
        {
            // 忽略無法讀取的檔案
        }
    }

    private static void FindCommonProperties(List<CssClass> classes, RefactoringAnalysis analysis)
    {
        // 找出共同的屬性組合
        var propertyGroups = new Dictionary<string, List<string>>();

        foreach (var cssClass in classes)
        {
            var props = ContentToPropertiesPublic(cssClass.Content);
            foreach (var prop in props)
            {
                var key = $"{prop.Key}:{prop.Value}";
                if (!propertyGroups.ContainsKey(key))
                    propertyGroups[key] = new List<string>();
                propertyGroups[key].Add(cssClass.ClassName);
            }
        }

        // 找出出現在多個 class 中的屬性
        var commonProps = propertyGroups.Where(x => x.Value.Count >= 3).ToList();
        if (commonProps.Any())
        {
            foreach (var group in commonProps.Take(5)) // 最多顯示 5 個建議
            {
                analysis.Suggestions.Add(new RefactoringSuggestion
                {
                    Type = "extract-common-properties",
                    Description = $"屬性 '{group.Key}' 在 {group.Value.Count} 個 class 中重複出現",
                    AffectedClasses = group.Value,
                    Details = new Dictionary<string, object> { { "property", group.Key } },
                    Priority = Math.Min(10, group.Value.Count)
                });
            }
        }
    }

    private static void FindHardcodedValues(List<CssClass> classes, RefactoringAnalysis analysis, string cssPath)
    {
        var tokenAnalysis = IdentifyDesignTokens(cssPath, 3);
        var totalTokens = tokenAnalysis.Colors.Count + tokenAnalysis.Spacings.Count;

        if (totalTokens > 0)
        {
            analysis.Suggestions.Add(new RefactoringSuggestion
            {
                Type = "use-design-token",
                Description = $"發現 {totalTokens} 個可轉換為設計 token 的硬編碼值",
                AffectedClasses = new List<string>(),
                Details = new Dictionary<string, object>
                {
                    { "colorTokens", tokenAnalysis.Colors.Count },
                    { "spacingTokens", tokenAnalysis.Spacings.Count }
                },
                Priority = 7
            });
        }
    }

    private static void FindSimilarClasses(List<CssClass> classes, RefactoringAnalysis analysis)
    {
        for (int i = 0; i < classes.Count - 1; i++)
        {
            for (int j = i + 1; j < classes.Count; j++)
            {
                var similarity = CalculateSimilarity(classes[i].Content, classes[j].Content);
                if (similarity >= 0.8) // 80% 相似度
                {
                    analysis.Suggestions.Add(new RefactoringSuggestion
                    {
                        Type = "merge-similar-classes",
                        Description = $"Class '{classes[i].ClassName}' 和 '{classes[j].ClassName}' 相似度 {similarity:P0}",
                        AffectedClasses = new List<string> { classes[i].ClassName, classes[j].ClassName },
                        Details = new Dictionary<string, object> { { "similarity", similarity } },
                        Priority = (int)(similarity * 10)
                    });
                }
            }
        }
    }

    private static void FindComplexClasses(List<CssClass> classes, RefactoringAnalysis analysis)
    {
        foreach (var cssClass in classes)
        {
            var props = ContentToPropertiesPublic(cssClass.Content);
            if (props.Count > 15)
            {
                analysis.Suggestions.Add(new RefactoringSuggestion
                {
                    Type = "split-complex-class",
                    Description = $"Class '{cssClass.ClassName}' 包含 {props.Count} 個屬性，建議拆分",
                    AffectedClasses = new List<string> { cssClass.ClassName },
                    Details = new Dictionary<string, object> { { "propertyCount", props.Count } },
                    Priority = Math.Min(10, props.Count / 5)
                });
            }
        }
    }

    private static void FindDuplicatePropertiesInClass(List<CssClass> classes, RefactoringAnalysis analysis)
    {
        foreach (var cssClass in classes)
        {
            var props = ContentToPropertiesPublic(cssClass.Content);
            var duplicates = props.GroupBy(x => x.Key).Where(g => g.Count() > 1).ToList();
            
            if (duplicates.Any())
            {
                analysis.Suggestions.Add(new RefactoringSuggestion
                {
                    Type = "remove-duplicate-properties",
                    Description = $"Class '{cssClass.ClassName}' 包含重複定義的屬性",
                    AffectedClasses = new List<string> { cssClass.ClassName },
                    Details = new Dictionary<string, object>
                    {
                        { "duplicateProperties", duplicates.Select(g => g.Key).ToList() }
                    },
                    Priority = 6
                });
            }
        }
    }

    private static double CalculateSimilarity(string content1, string content2)
    {
        var props1 = ContentToPropertiesPublic(content1);
        var props2 = ContentToPropertiesPublic(content2);

        if (props1.Count == 0 && props2.Count == 0) return 1.0;
        if (props1.Count == 0 || props2.Count == 0) return 0.0;

        var common = props1.Keys.Intersect(props2.Keys).Count(key => props1[key] == props2[key]);
        var total = Math.Max(props1.Count, props2.Count);

        return (double)common / total;
    }

    #endregion

    #region 批次操作與變數分析

    /// <summary>
    /// 批次替換CSS屬性值
    /// </summary>
    public static BatchReplaceResult BatchReplacePropertyValues(
        string cssPath,
        string oldValue,
        string newValue,
        string? propertyFilter = null,
        bool useRegex = false)
    {
        if (!File.Exists(cssPath))
            throw new FileNotFoundException("找不到檔案", cssPath);

        var result = new BatchReplaceResult();
        
        // 建立備份
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string backupPath = $"{cssPath}.batch_backup_{timestamp}";
        File.Copy(cssPath, backupPath, true);
        result.BackupPath = backupPath;

        try
        {
            var classes = GetClasses(cssPath);
            string content = File.ReadAllText(cssPath);
            var affectedClassNames = new HashSet<string>();

            foreach (var cssClass in classes)
            {
                var props = ContentToPropertiesPublic(cssClass.Content);
                bool classModified = false;

                foreach (var prop in props.ToList())
                {
                    // 檢查屬性篩選
                    if (propertyFilter != null && !prop.Key.Equals(propertyFilter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    bool shouldReplace = false;
                    string actualOldValue = prop.Value;

                    // 判斷是否需要替換
                    if (useRegex)
                    {
                        if (Regex.IsMatch(prop.Value, oldValue))
                        {
                            shouldReplace = true;
                        }
                    }
                    else
                    {
                        if (prop.Value == oldValue)
                        {
                            shouldReplace = true;
                        }
                    }

                    if (shouldReplace)
                    {
                        // 執行替換
                        string replacedValue = useRegex
                            ? Regex.Replace(prop.Value, oldValue, newValue)
                            : newValue;

                        props[prop.Key] = replacedValue;
                        classModified = true;

                        // 記錄替換詳情
                        result.Replacements.Add(new ReplacementDetail
                        {
                            ClassName = cssClass.ClassName,
                            Property = prop.Key,
                            OldValue = actualOldValue,
                            NewValue = replacedValue
                        });

                        result.TotalMatches++;
                        affectedClassNames.Add(cssClass.ClassName);
                    }
                }

                // 如果這個 class 有修改，更新檔案
                if (classModified)
                {
                    string newCssContent = PropertiesToContentPublic(props, cssClass.Selector);
                    ReplaceBlockPublic(cssPath, cssClass.StartIndex, cssClass.BlockEnd, newCssContent);
                    
                    // 重新讀取檔案（因為位置可能改變）
                    content = File.ReadAllText(cssPath);
                    classes = GetClasses(cssPath);
                }
            }

            result.AffectedClasses = affectedClassNames.ToList();
            return result;
        }
        catch (Exception ex)
        {
            // 發生錯誤，恢復備份
            File.Copy(backupPath, cssPath, true);
            throw new Exception($"批次替換失敗：{ex.Message}（已從備份還原）", ex);
        }
    }

    /// <summary>
    /// 分析CSS變數的影響範圍
    /// </summary>
    public static VariableImpactAnalysis AnalyzeVariableImpact(string cssPath, string variableName)
    {
        if (!File.Exists(cssPath))
            throw new FileNotFoundException("找不到檔案", cssPath);

        // 確保變數名稱格式正確
        if (!variableName.StartsWith("--"))
            variableName = "--" + variableName;

        var analysis = new VariableImpactAnalysis
        {
            VariableName = variableName
        };

        var classes = GetClasses(cssPath);
        var variableDefinitions = new Dictionary<string, string>(); // 變數名稱 -> 值

        // 第一階段：找出所有變數定義
        foreach (var cssClass in classes)
        {
            // 檢查是否為 :root 或其他可能定義變數的選擇器
            if (cssClass.Selector.Contains(":root") || cssClass.Selector.Contains("--"))
            {
                var props = ContentToPropertiesPublic(cssClass.Content);
                foreach (var prop in props)
                {
                    if (prop.Key.StartsWith("--"))
                    {
                        variableDefinitions[prop.Key] = prop.Value;
                        
                        // 如果找到目標變數的定義
                        if (prop.Key == variableName)
                        {
                            analysis.IsDefined = true;
                            analysis.DefinedValue = prop.Value;
                        }
                    }
                }
            }
        }

        // 第二階段：找出所有使用此變數的地方
        var varPattern = new Regex($@"var\(\s*{Regex.Escape(variableName)}(?:\s*,\s*([^)]+))?\s*\)", RegexOptions.Compiled);
        
        foreach (var cssClass in classes)
        {
            var props = ContentToPropertiesPublic(cssClass.Content);
            
            foreach (var prop in props)
            {
                var match = varPattern.Match(prop.Value);
                if (match.Success)
                {
                    // 直接使用此變數
                    analysis.DirectUsages.Add(new VariableUsage
                    {
                        ClassName = cssClass.ClassName,
                        Property = prop.Key,
                        Value = prop.Value,
                        Level = 0
                    });
                }
                else if (prop.Key.StartsWith("--") && prop.Value.Contains("var("))
                {
                    // 這是一個變數定義，檢查是否間接引用目標變數
                    if (prop.Value.Contains($"var({variableName}"))
                    {
                        // 這個變數引用了目標變數，現在找出誰使用這個變數
                        string intermediateVar = prop.Key;
                        var intermediatePattern = new Regex($@"var\(\s*{Regex.Escape(intermediateVar)}(?:\s*,\s*([^)]+))?\s*\)", RegexOptions.Compiled);
                        
                        foreach (var otherClass in classes)
                        {
                            var otherProps = ContentToPropertiesPublic(otherClass.Content);
                            foreach (var otherProp in otherProps)
                            {
                                if (intermediatePattern.IsMatch(otherProp.Value) && otherClass.ClassName != cssClass.ClassName)
                                {
                                    analysis.IndirectUsages.Add(new VariableUsage
                                    {
                                        ClassName = otherClass.ClassName,
                                        Property = otherProp.Key,
                                        Value = otherProp.Value,
                                        Level = 1
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }

        analysis.TotalImpact = analysis.DirectUsages.Count + analysis.IndirectUsages.Count;
        return analysis;
    }

    #endregion
}
