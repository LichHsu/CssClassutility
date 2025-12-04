using CssClassutility.Models;

namespace CssClassutility.AI;

/// <summary>
/// CSS 變數影響分析器
/// </summary>
public static class VariableAnalyzer
{
    /// <summary>
    /// 分析CSS變數的影響範圍
    /// </summary>
    public static VariableImpactAnalysis AnalyzeVariableImpact(string cssPath, string variableName)
    {
        // 直接使用 CssParser 的擴充方法
        return CssParser.AnalyzeVariableImpact(cssPath, variableName);
    }
}
