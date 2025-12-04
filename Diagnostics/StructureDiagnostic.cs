using CssClassutility.Models;

namespace CssClassutility.Diagnostics;

/// <summary>
/// CSS 結構診斷工具
/// </summary>
public static class StructureDiagnostic
{
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
        var classes = CssParser.GetClasses(path);
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
}
