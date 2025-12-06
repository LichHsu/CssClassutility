using CssClassUtility.Models;
using CssClassUtility.Operations;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CssClassUtility;

public partial class CssParser
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex timeRule();
    [GeneratedRegex(@"(.+\.css):\.?(.+)")]
    private static partial Regex cssRule();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [GeneratedRegex(@"\.[a-zA-Z0-9_-]+")]
    private static partial Regex CssRulePattern();

    [GeneratedRegex(@"/\*[\s\S]*?\*/")]
    private static partial Regex CommentPattern();

    #region 核心解析

    /// <summary>
    /// 解析 CSS 檔案並回傳 Class 定義列表
    /// </summary>
    public static List<CssClass> GetClasses(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("找不到檔案", path);
        string content = File.ReadAllText(path);
        return GetClassesFromContent(content, path);
    }

    /// <summary>
    /// 從內容解析 CSS Class
    /// </summary>
    public static List<CssClass> GetClassesFromContent(string content, string filePath = "")
    {
        var results = new List<CssClass>();
        var length = content.Length;

        // Stack to track scopes (Root, Media Query, Class Block)
        var scopeStack = new Stack<ParsingScope>();
        scopeStack.Push(new ParsingScope { Type = ScopeType.Root, Selector = "", SelectorStart = 0 });

        int index = 0;
        bool inComment = false;
        bool inString = false;
        char stringChar = ' ';
        var buffer = new StringBuilder();
        int currentSelectorStart = -1;

        while (index < length)
        {
            char c = content[index];

            // 1. Handle Comments
            if (!inString)
            {
                if (!inComment && c == '/' && index + 1 < length && content[index + 1] == '*')
                {
                    inComment = true;
                    index++;
                    // Note: We don't advance buffer here, allowing comments to be skipped or handled if needed
                }
                else if (inComment && c == '*' && index + 1 < length && content[index + 1] == '/')
                {
                    inComment = false;
                    index += 2;
                    continue;
                }
            }

            if (inComment)
            {
                index++;
                continue;
            }

            // 2. Handle Strings
            if (!inComment)
            {
                if (!inString && (c == '"' || c == '\''))
                {
                    inString = true;
                    stringChar = c;
                }
                else if (inString && c == stringChar)
                {
                    // Check for escaping
                    bool isEscaped = false;
                    int backIndex = index - 1;
                    while (backIndex >= 0 && content[backIndex] == '\\')
                    {
                        isEscaped = !isEscaped;
                        backIndex--;
                    }

                    if (!isEscaped)
                    {
                        inString = false;
                        stringChar = ' ';
                    }
                }
            }

            // 3. Main Loop
            if (inString)
            {
                buffer.Append(c);
            }
            else if (c == '{')
            {
                string selector = buffer.ToString().Trim();
                buffer.Clear();

                ScopeType type = ScopeType.Other;
                if (!selector.StartsWith('@'))
                {
                    if (CssRulePattern().IsMatch(selector)) type = ScopeType.Class;
                }
                else
                {
                    type = ScopeType.Media; // Or other @rules like @supports, @keyframes
                }

                int selStart = currentSelectorStart != -1 ? currentSelectorStart : index; // Fallback
                scopeStack.Push(new ParsingScope { Type = type, Selector = selector, StartIndex = index, SelectorStart = selStart });
                currentSelectorStart = -1;
            }
            else if (c == '}')
            {
                if (scopeStack.Count > 1)
                {
                    var scope = scopeStack.Pop();

                    if (scope.Type == ScopeType.Class)
                    {
                        int blockStart = scope.StartIndex;
                        int blockEnd = index;
                        // Extract content excluding braces
                        string innerContent = content.Substring(blockStart + 1, blockEnd - blockStart - 1).Trim();

                        // Determine context (e.g., inside @media)
                        string? context = null;
                        if (scopeStack.Peek().Type == ScopeType.Media)
                        {
                            context = scopeStack.Peek().Selector;
                        }

                        // Extract all class names from the selector (e.g., ".a, .b")
                        var classMatches = Regex.Matches(scope.Selector, @"\.([a-zA-Z0-9_-]+)");
                        foreach (Match match in classMatches)
                        {
                            results.Add(new CssClass
                            {
                                ClassName = match.Groups[1].Value,
                                Selector = scope.Selector,
                                Content = innerContent,
                                Context = context,
                                File = filePath,
                                StartIndex = scope.SelectorStart,
                                BlockEnd = blockEnd
                            });
                        }
                    }
                }
                buffer.Clear();
                currentSelectorStart = -1;
            }
            else if (!char.IsWhiteSpace(c))
            {
                if (buffer.Length == 0 && currentSelectorStart == -1) currentSelectorStart = index;
                buffer.Append(c);
            }
            else
            {
                // Preserve needed whitespace in selectors (e.g. "div .class")
                if (buffer.Length > 0) buffer.Append(c);
            }
            index++;
        }
        return results;
    }

    private class ParsingScope
    {
        public ScopeType Type { get; set; }
        public string Selector { get; set; } = "";
        public int StartIndex { get; set; }
        public int SelectorStart { get; set; }
    }

    private enum ScopeType
    {
        Root,
        Media,
        Class,
        Other
    }

    #endregion

    #region 樣式比較

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
        css = CommentPattern().Replace(css, "");

        // 正規化空白
        css = timeRule().Replace(css, " ");

        // 解析屬性並排序
        var props = css.Split(';')
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p =>
            {
                var parts = p.Split(':', 2);
                return parts.Length == 2
                    ? new { Key = parts[0].Trim().ToLower(), Value = parts[1].Trim() }
                    : null;
            })
            .Where(p => p != null)
            .OrderBy(p => p!.Key)
            .Select(p => $"{p!.Key}:{p.Value}");

        return string.Join(";", props);
    }

    /// <summary>
    /// 將 CSS 內容轉換為屬性字典
    /// </summary>
    public static Dictionary<string, string> ContentToProperties(string content) => ContentToPropertiesPublic(content); // Use the public extension method

    #endregion

    #region Class 移除

    /// <summary>
    /// 安全地從檔案中移除 CSS Class 定義
    /// </summary>
    public static string RemoveCssClass(string path, string className)
    {
        if (!File.Exists(path)) return $"錯誤：找不到檔案 {path}";

        string content = File.ReadAllText(path);
        var classes = GetClasses(path);

        var target = classes.FirstOrDefault(c => c.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase));
        if (target == null)
        {
            return $"警告：在 {path} 中找不到 Class .{className}";
        }

        // 安全檢查：群組選擇器
        if (target.Selector.Contains(','))
        {
            return $"安全防護：Class .{className} 屬於群組選擇器 '{target.Selector}'。目前不支援部分移除群組選擇器，請手動處理。";
        }

        // 移除內容
        int removeStartIndex = target.StartIndex;
        int removeEndIndex = target.BlockEnd;

        // 檢查前導換行/空白以移除空行 (Lookbehind)
        while (removeStartIndex > 0 && char.IsWhiteSpace(content[removeStartIndex - 1]))
        {
            removeStartIndex--;
        }

        string newContent = content.Remove(removeStartIndex, removeEndIndex - removeStartIndex + 1);

        // 插入註解標記
        string comment = $"\n/* .{className} removed by CssClassManager */\n";
        newContent = newContent.Insert(removeStartIndex, comment);

        // 驗證完整性 (檢查大括號平衡)
        int openCount = newContent.Count(c => c == '{');
        int closeCount = newContent.Count(c => c == '}');

        if (openCount != closeCount)
        {
            throw new Exception($"移除後偵測到大括號不匹配！ (Open: {openCount}, Close: {closeCount})");
        }

        // 儲存檔案
        File.WriteAllText(path, newContent, Encoding.UTF8);
        return $"成功從 {path} 移除 .{className}";
    }

    #endregion

    #region JSON 轉換

    /// <summary>
    /// 將 CSS Class 轉換為 JSON 實體格式
    /// </summary>
    public static CssEntity ConvertToCssJson(string path, string className)
    {
        var classes = GetClasses(path);
        var target = classes.FirstOrDefault(c => c.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
            ?? throw new Exception($"找不到 Class .{className}");

        return ConvertToCssJson(target, Path.GetFileNameWithoutExtension(path));
    }

    public static CssEntity ConvertToCssJson(CssClass cssClass, string? sourceFileName = null)
    {
        var props = ContentToProperties(cssClass.Content);
        var sortedProps = new SortedDictionary<string, string>(props, StringComparer.OrdinalIgnoreCase);

        return new CssEntity
        {
            Name = cssClass.ClassName,
            Selector = cssClass.Selector,
            Properties = sortedProps,
            Metadata = new CssEntityMetadata
            {
                SourceFile = sourceFileName ?? Path.GetFileNameWithoutExtension(cssClass.File),
                Context = cssClass.Context
            }
        };
    }

    /// <summary>
    /// 將 JSON 實體轉換為 CSS 字串
    /// </summary>
    public static string ConvertFromCssJson(CssEntity entity)
    {
        var sb = new StringBuilder();
        sb.Append(entity.Selector).Append(" {\n");

        foreach (var kvp in entity.Properties)
        {
            sb.Append($"    {kvp.Key}: {kvp.Value};\n");
        }

        sb.Append('}');
        return sb.ToString();
    }

    #endregion

    #region Class 合併

    /// <summary>
    /// 將來源 Class 的屬性合併到目標 Class
    /// </summary>
    public static string MergeCssClass(string targetPath, string targetClassName, string sourceObject, MergeStrategy strategy)
    {
        var classes = GetClasses(targetPath);
        var targetClass = classes.FirstOrDefault(c => c.ClassName.Equals(targetClassName, StringComparison.OrdinalIgnoreCase))
            ?? throw new Exception($"目標 Class .{targetClassName} 不存在");

        var targetEntity = ConvertToCssJson(targetClass);
        var targetProps = targetEntity.Properties;

        // 取得來源屬性
        SortedDictionary<string, string>? sourceProps = null;

        if (sourceObject.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var sourceEntity = GetCssEntity(sourceObject);
            sourceProps = sourceEntity.Properties;
        }
        else if (cssRule().IsMatch(sourceObject))
        {
            var match = cssRule().Match(sourceObject);
            string srcFile = match.Groups[1].Value;
            string srcClass = match.Groups[2].Value;
            var srcClasses = GetClasses(srcFile);
            var srcTarget = srcClasses.FirstOrDefault(c => c.ClassName.Equals(srcClass, StringComparison.OrdinalIgnoreCase));
            if (srcTarget != null)
            {
                sourceProps = ConvertToCssJson(srcTarget).Properties;
            }
        }

        if (sourceProps == null)
            throw new Exception($"無法讀取來源物件: {sourceObject}");

        // 合併邏輯
        bool modified = MergePropertiesPublic(targetProps, sourceProps, strategy);

        if (modified)
        {
            string newCss = ConvertFromCssJson(targetEntity);
            ReplaceBlock(targetPath, targetClass.StartIndex, targetClass.BlockEnd, newCss);
            return $"已合併 CSS Class: .{targetClassName} ({strategy})";
        }

        return $"未變更: .{targetClassName}";
    }

    #endregion

    #region 實體管理 (JSON 檔案)

    /// <summary>
    /// 將 CSS 檔案實體化為 JSON 檔案集合
    /// </summary>
    public static string ExportCssToEntities(string cssPath, string outputRoot, string cleanMode)
    {
        if (!File.Exists(cssPath)) throw new FileNotFoundException("找不到檔案", cssPath);

        string fileName = Path.GetFileNameWithoutExtension(cssPath);
        string targetDir = Path.Combine(outputRoot, fileName);

        // 準備輸出目錄
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }
        else
        {
            // 處理清理模式
            if (cleanMode == "DeleteAll")
            {
                foreach (var f in Directory.GetFiles(targetDir, "*.json"))
                    File.Delete(f);
            }
            else if (cleanMode == "KeepSoftDeleted")
            {
                foreach (var f in Directory.GetFiles(targetDir, "*.json"))
                {
                    if (!Path.GetFileName(f).StartsWith('_'))
                        File.Delete(f);
                }
            }
        }

        // 解析 CSS
        var classes = GetClasses(cssPath);
        if (classes.Count == 0)
        {
            throw new Exception($"解析失敗或檔案為空：在 {cssPath} 中找不到任何 CSS Class。匯出已中止以保護數據。");
        }

        var createdFiles = new HashSet<string>();

        foreach (var cls in classes)
        {
            var entity = ConvertToCssJson(cls, fileName);
            string jsonFileName = $"{cls.ClassName}.json";
            string jsonPath = Path.Combine(targetDir, jsonFileName);

            // 處理重名衝突
            int counter = 1;
            while (createdFiles.Contains(jsonPath) || File.Exists(jsonPath))
            {
                jsonFileName = $"{cls.ClassName}_{counter}.json";
                jsonPath = Path.Combine(targetDir, jsonFileName);
                counter++;
            }

            createdFiles.Add(jsonPath);
            string json = JsonSerializer.Serialize(entity, _jsonOptions);
            File.WriteAllText(jsonPath, json, Encoding.UTF8);
        }

        return $"完成！共匯出 {classes.Count} 個實體至 {targetDir}";
    }

    /// <summary>
    /// 從 JSON 實體集合建置 CSS 檔案
    /// </summary>
    public static string ImportCssFromEntities(string sourceDir, string outputFile, bool includeSoftDeleted)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"找不到來源目錄: {sourceDir}");

        var files = Directory.GetFiles(sourceDir, "*.json");
        var entities = new List<CssEntity>();

        foreach (var file in files)
        {
            // 軟刪除檢查
            if (!includeSoftDeleted && Path.GetFileName(file).StartsWith('_'))
                continue;

            try
            {
                string json = File.ReadAllText(file);
                var entity = JsonSerializer.Deserialize<CssEntity>(json, _jsonOptions);
                if (entity != null) entities.Add(entity);
            }
            catch { /* 忽略解析錯誤的檔案 */ }
        }

        if (entities.Count == 0)
        {
            throw new Exception($"錯誤：在 {sourceDir} 中找不到任何有效的 CSS 實體 JSON。導入已中止以避免生成空檔案。");
        }

        // 分組處理
        var grouped = entities.GroupBy(e => e.Metadata?.Context ?? "");
        var cssOutput = new List<string>();

        // 處理 Root Context (無 Context)
        var rootGroup = grouped.FirstOrDefault(g => string.IsNullOrEmpty(g.Key));
        if (rootGroup != null)
        {
            foreach (var entity in rootGroup)
            {
                cssOutput.Add(ConvertFromCssJson(entity));
            }
        }

        // 處理其他 Context (Media Queries 等)
        foreach (var group in grouped.Where(g => !string.IsNullOrEmpty(g.Key)))
        {
            string context = group.Key;
            cssOutput.Add($"\n{context} {{");

            foreach (var entity in group)
            {
                // 縮排內部規則
                string rule = ConvertFromCssJson(entity);
                var indentedRule = string.Join("\n", rule.Split('\n').Select(l => "    " + l));
                cssOutput.Add(indentedRule);
            }

            cssOutput.Add("}");
        }

        // 寫入檔案
        string finalCss = string.Join("\n\n", cssOutput);
        File.WriteAllText(outputFile, finalCss, Encoding.UTF8);

        return $"建置完成！輸出至: {outputFile}（共處理 {entities.Count} 個實體）";
    }

    /// <summary>
    /// 讀取並解析 CSS 實體 JSON 檔案
    /// </summary>
    public static CssEntity GetCssEntity(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"找不到檔案: {path}");

        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<CssEntity>(json, _jsonOptions)
            ?? throw new Exception("無法解析 JSON 實體");
    }

    /// <summary>
    /// 修改 CSS 實體的屬性
    /// </summary>
    public static string UpdateCssEntityProperty(string path, string key, string value, string action)
    {
        var entity = GetCssEntity(path);
        var props = entity.Properties;
        bool modified = false;
        key = key.ToLower().Trim();

        if (action.Equals("Set", StringComparison.OrdinalIgnoreCase))
        {
            if (!props.TryGetValue(key, out string? oldVal) || oldVal != value)
            {
                props[key] = value;
                modified = true;
            }
        }
        else if (action.Equals("Remove", StringComparison.OrdinalIgnoreCase))
        {
            if (props.Remove(key))
                modified = true;
        }

        if (modified)
        {
            string json = JsonSerializer.Serialize(entity, _jsonOptions);
            File.WriteAllText(path, json, Encoding.UTF8);
            return $"已更新實體: {path}";
        }

        return $"實體未變更: {path}";
    }

    /// <summary>
    /// 合併兩個 CSS 實體
    /// </summary>
    public static string MergeCssEntity(string targetPath, string sourcePath, MergeStrategy strategy)
    {
        var target = GetCssEntity(targetPath);
        var source = GetCssEntity(sourcePath);

        bool modified = MergePropertiesPublic(target.Properties, source.Properties, strategy);

        if (modified)
        {
            string json = JsonSerializer.Serialize(target, _jsonOptions);
            File.WriteAllText(targetPath, json, Encoding.UTF8);
            return $"已合併實體: {targetPath} ({strategy})";
        }

        return $"實體未變更: {targetPath}";
    }

    #endregion

    #region 輔助方法

    private static void ReplaceBlock(string path, int start, int end, string newContent)
    {
        string content = File.ReadAllText(path);
        string newFullContent = ReplaceBlockInContent(content, start, end, newContent);
        File.WriteAllText(path, newFullContent, Encoding.UTF8);
    }

    /// <summary>
    /// 在內容中替換區塊
    /// </summary>
    public static string ReplaceBlockInContent(string content, int start, int end, string newBlock)
    {
        string before = content.Substring(0, start);
        string after = content.Substring(end + 1);
        return before + newBlock + after;
    }

    public static string UpdateClassProperty(string path, string className, string key, string value, string action)
    {
        return CssUpdater.UpdateClassProperty(path, className, key, value, action);
    }

    #endregion
}
