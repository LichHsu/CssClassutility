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
            var classes = CssParser.GetClasses(cssPath);
            string content = File.ReadAllText(cssPath);
            var affectedClassNames = new HashSet<string>();

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

                // 如果這個 class 有修改，更新檔案
                if (classModified)
                {
                    string newCssContent = CssParser.PropertiesToContentPublic(props, cssClass.Selector);
                    CssParser.ReplaceBlockPublic(cssPath, cssClass.StartIndex, cssClass.BlockEnd, newCssContent);
                    
                    // 重新讀取檔案（因為位置可能改變）
                    content = File.ReadAllText(cssPath);
                    classes = CssParser.GetClasses(cssPath);
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
}
