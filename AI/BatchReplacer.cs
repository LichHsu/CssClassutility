using System.Text;
using System.Text.RegularExpressions;
using CssClassutility.Models;

namespace CssClassutility.AI;

/// <summary>
/// 批次屬性替換器
/// </summary>
public static class BatchReplacer
{
    /// <summary>
    /// 批次替換CSS屬性值 (In-Memory Reverse Replacement)
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
        
        // 2. 讀取與解析
        string originalContent = File.ReadAllText(cssPath);
        var classes = CssParser.GetClassesFromContent(originalContent, cssPath);
        var affectedClassNames = new HashSet<string>();

        // 3. 收集所有需要的替換操作
        var replacements = new List<(int StartIndex, int BlockEnd, string NewContent)>();

        foreach (var cssClass in classes)
        {
            var props = CssParser.ContentToPropertiesPublic(cssClass.Content);
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

            // 如果這個 class 有修改，加入待替換列表
            if (classModified)
            {
                string newCssContent = CssParser.PropertiesToContentPublic(props, cssClass.Selector);
                replacements.Add((cssClass.StartIndex, cssClass.BlockEnd, newCssContent));
            }
        }

        // 4. 執行替換 (從後往前，避免索引偏移)
        // Sort by StartIndex Descending
        replacements.Sort((a, b) => b.StartIndex.CompareTo(a.StartIndex));

        var sb = new StringBuilder(originalContent);

        foreach (var (start, end, content) in replacements)
        {
            // 移除舊區塊
            // Use block Length (+1 is implied by end - start + 1 logic used elsewhere, verifying...)
            // CssParser.GetClasses logic sets BlockEnd = index of '}'
            // Length = BlockEnd - StartIndex + 1
            sb.Remove(start, end - start + 1); 
            
            // 插入新區塊
            sb.Insert(start, content);
        }

        // 5. 寫回檔案
        File.WriteAllText(cssPath, sb.ToString());

        result.AffectedClasses = affectedClassNames.ToList();
        return result;
    }
}
