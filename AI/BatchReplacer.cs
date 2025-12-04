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
        // 直接使用 CssParser 的擴充方法
        return CssParser.BatchReplacePropertyValues(cssPath, oldValue, newValue, propertyFilter, useRegex);
    }
}
