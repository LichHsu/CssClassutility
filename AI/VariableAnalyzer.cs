using CssClassUtility.Models;
using System.Text.RegularExpressions;

namespace CssClassUtility.AI;

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
        if (!File.Exists(cssPath))
            throw new FileNotFoundException("找不到檔案", cssPath);

        // 確保變數名稱格式正確
        if (!variableName.StartsWith("--"))
            variableName = "--" + variableName;

        var analysis = new VariableImpactAnalysis
        {
            VariableName = variableName
        };

        var classes = CssParser.GetClasses(cssPath);
        var variableDefinitions = new Dictionary<string, string>(); // 變數名稱 -> 值

        // 第一階段：找出所有變數定義
        foreach (var cssClass in classes)
        {
            // 檢查是否為 :root 或其他可能定義變數的選擇器
            if (cssClass.Selector.Contains(":root") || cssClass.Selector.Contains("--"))
            {
                var props = CssParser.ContentToPropertiesPublic(cssClass.Content);
                foreach (var prop in props)
                {
                    if (prop.Key.StartsWith("--"))
                    {
                        variableDefinitions[prop.Key] = prop.Value;

                        // 如果找到目標變數的定義
                        if (prop.Key == variableName)
                        {
                            analysis.IsDefined = true;
                            analysis.DefinedValue = prop.Value;
                        }
                    }
                }
            }
        }

        // 第二階段：找出所有使用此變數的地方
        var varPattern = new Regex($@"var\(\s*{Regex.Escape(variableName)}(?:\s*,\s*([^)]+))?\s*\)", RegexOptions.Compiled);

        foreach (var cssClass in classes)
        {
            var props = CssParser.ContentToPropertiesPublic(cssClass.Content);

            foreach (var prop in props)
            {
                var match = varPattern.Match(prop.Value);
                if (match.Success)
                {
                    // 直接使用此變數
                    analysis.DirectUsages.Add(new VariableUsage
                    {
                        ClassName = cssClass.ClassName,
                        Property = prop.Key,
                        Value = prop.Value,
                        Level = 0
                    });
                }
                else if (prop.Key.StartsWith("--") && prop.Value.Contains("var("))
                {
                    // 這是一個變數定義，檢查是否間接引用目標變數
                    if (prop.Value.Contains($"var({variableName}"))
                    {
                        // 這個變數引用了目標變數，現在找出誰使用這個變數
                        string intermediateVar = prop.Key;
                        var intermediatePattern = new Regex($@"var\(\s*{Regex.Escape(intermediateVar)}(?:\s*,\s*([^)]+))?\s*\)", RegexOptions.Compiled);

                        foreach (var otherClass in classes)
                        {
                            var otherProps = CssParser.ContentToPropertiesPublic(otherClass.Content);
                            foreach (var otherProp in otherProps)
                            {
                                if (intermediatePattern.IsMatch(otherProp.Value) && otherClass.ClassName != cssClass.ClassName)
                                {
                                    analysis.IndirectUsages.Add(new VariableUsage
                                    {
                                        ClassName = otherClass.ClassName,
                                        Property = otherProp.Key,
                                        Value = otherProp.Value,
                                        Level = 1
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }

        analysis.TotalImpact = analysis.DirectUsages.Count + analysis.IndirectUsages.Count;
        return analysis;
    }
}
