using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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
}
