using CssClassUtility.Core;

namespace CssClassUtility.Operations;

public static class CssAnalyzer
{
    /// <summary>
    /// 找出那些「有被使用，但未在 CSS 中定義」的 Class
    /// </summary>
    /// <param name="cssPath">CSS 檔案路徑</param>
    /// <param name="classesToCheck">被使用的 Class 列表</param>
    /// <returns>未定義的 Class 列表</returns>
    public static List<string> FindMissingClasses(string cssPath, List<string> classesToCheck)
    {
        if (!File.Exists(cssPath)) return classesToCheck; // 如果 CSS 檔不存在，所有都是 Missing

        // 取得 CSS 中定義的所有類別名稱
        var definedClasses = CssParser.GetClasses(cssPath)
            .Select(c => c.ClassName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 過濾出未定義的
        var missing = classesToCheck
            .Where(c => !string.IsNullOrEmpty(c) && !definedClasses.Contains(c))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        return missing;
    }
}
