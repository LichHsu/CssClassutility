using CssClassUtility.Models;

namespace CssClassUtility.Diagnostics;

/// <summary>
/// 重複 Class 檢測器
/// </summary>
public static class DuplicateDetector
{
    /// <summary>
    /// 取得重複的 Class 列表
    /// </summary>
    public static List<DuplicateClassInfo> GetDuplicateClasses(string path)
    {
        var classes = CssParser.GetClasses(path);
        return [.. classes.GroupBy(c => c.ClassName)
            .Where(g => g.Count() > 1)
            .Select(g => new DuplicateClassInfo
            {
                ClassName = g.Key,
                Count = g.Count(),
                Selectors = [.. g.Select(c => c.Selector).Distinct()]
            })];
    }
}
