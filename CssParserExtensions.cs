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
