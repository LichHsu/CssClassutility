using CssClassUtility.Models;

namespace CssClassUtility.AI;

/// <summary>
/// 重構建議引擎
/// </summary>
public static class RefactoringAdvisor
{
    /// <summary>
    /// 分析 CSS 檔案並提供重構建議
    /// </summary>
    public static RefactoringAnalysis SuggestRefactoring(string cssPath, int minPriority = 1)
    {
        var analysis = new RefactoringAnalysis
        {
            FilePath = cssPath,
            Suggestions = new List<RefactoringSuggestion>(),
            Statistics = new Dictionary<string, int>()
        };

        var classes = CssParser.GetClasses(cssPath);
        analysis.Statistics["totalClasses"] = classes.Count;

        // 1. 找出重複的屬性組合（建議提取共用 class）
        FindCommonProperties(classes, analysis);

        // 2. 找出可替換為 token 的硬編碼值
        FindHardcodedValues(classes, analysis, cssPath);

        // 3. 找出相似度高的 class（建議合併）
        FindSimilarClasses(classes, analysis);

        // 4. 找出過度複雜的 class（建議拆分）
        FindComplexClasses(classes, analysis);

        // 5. 找出重複定義的屬性
        FindDuplicatePropertiesInClass(classes, analysis);

        // 依優先級排序並過濾
        analysis.Suggestions = analysis.Suggestions
            .Where(s => s.Priority >= minPriority)
            .OrderByDescending(s => s.Priority)
            .ToList();

        analysis.Statistics["totalSuggestions"] = analysis.Suggestions.Count;
        return analysis;
    }

    private static void FindCommonProperties(List<CssClass> classes, RefactoringAnalysis analysis)
    {
        var propertyGroups = new Dictionary<string, List<string>>();

        foreach (var cssClass in classes)
        {
            var props = CssParser.ContentToPropertiesPublic(cssClass.Content);
            foreach (var prop in props)
            {
                var key = $"{prop.Key}:{prop.Value}";
                if (!propertyGroups.ContainsKey(key))
                    propertyGroups[key] = new List<string>();
                propertyGroups[key].Add(cssClass.ClassName);
            }
        }

        var commonProps = propertyGroups.Where(x => x.Value.Count >= 3).ToList();
        if (commonProps.Any())
        {
            foreach (var group in commonProps.Take(5))
            {
                analysis.Suggestions.Add(new RefactoringSuggestion
                {
                    Type = "extract-common-properties",
                    Description = $"屬性 '{group.Key}' 在 {group.Value.Count} 個 class 中重複出現",
                    AffectedClasses = group.Value,
                    Details = new Dictionary<string, object> { { "property", group.Key } },
                    Priority = Math.Min(10, group.Value.Count)
                });
            }
        }
    }

    private static void FindHardcodedValues(List<CssClass> classes, RefactoringAnalysis analysis, string cssPath)
    {
        var tokenAnalysis = DesignTokenAnalyzer.IdentifyDesignTokens(cssPath, 3);
        var totalTokens = tokenAnalysis.Colors.Count + tokenAnalysis.Spacings.Count;

        if (totalTokens > 0)
        {
            analysis.Suggestions.Add(new RefactoringSuggestion
            {
                Type = "use-design-token",
                Description = $"發現 {totalTokens} 個可轉換為設計 token 的硬編碼值",
                AffectedClasses = new List<string>(),
                Details = new Dictionary<string, object>
                {
                    { "colorTokens", tokenAnalysis.Colors.Count },
                    { "spacingTokens", tokenAnalysis.Spacings.Count }
                },
                Priority = 7
            });
        }
    }

    private static void FindSimilarClasses(List<CssClass> classes, RefactoringAnalysis analysis)
    {
        for (int i = 0; i < classes.Count - 1; i++)
        {
            for (int j = i + 1; j < classes.Count; j++)
            {
                var similarity = CalculateSimilarity(classes[i].Content, classes[j].Content);
                if (similarity >= 0.8)
                {
                    analysis.Suggestions.Add(new RefactoringSuggestion
                    {
                        Type = "merge-similar-classes",
                        Description = $"Class '{classes[i].ClassName}' 和 '{classes[j].ClassName}' 相似度 {(similarity * 100):F0}%",
                        AffectedClasses = new List<string> { classes[i].ClassName, classes[j].ClassName },
                        Details = new Dictionary<string, object> { { "similarity", similarity } },
                        Priority = (int)(similarity * 10)
                    });
                }
            }
        }
    }

    private static void FindComplexClasses(List<CssClass> classes, RefactoringAnalysis analysis)
    {
        foreach (var cssClass in classes)
        {
            var props = CssParser.ContentToPropertiesPublic(cssClass.Content);
            if (props.Count > 15)
            {
                analysis.Suggestions.Add(new RefactoringSuggestion
                {
                    Type = "split-complex-class",
                    Description = $"Class '{cssClass.ClassName}' 包含 {props.Count} 個屬性，建議拆分",
                    AffectedClasses = new List<string> { cssClass.ClassName },
                    Details = new Dictionary<string, object> { { "propertyCount", props.Count } },
                    Priority = Math.Min(10, props.Count / 5)
                });
            }
        }
    }

    private static void FindDuplicatePropertiesInClass(List<CssClass> classes, RefactoringAnalysis analysis)
    {
        foreach (var cssClass in classes)
        {
            var props = CssParser.ContentToPropertiesPublic(cssClass.Content);
            var duplicates = props.GroupBy(x => x.Key).Where(g => g.Count() > 1).ToList();
            
            if (duplicates.Any())
            {
                analysis.Suggestions.Add(new RefactoringSuggestion
                {
                    Type = "remove-duplicate-properties",
                    Description = $"Class '{cssClass.ClassName}' 包含重複定義的屬性",
                    AffectedClasses = new List<string> { cssClass.ClassName },
                    Details = new Dictionary<string, object>
                    {
                        { "duplicateProperties", duplicates.Select(g => g.Key).ToList() }
                    },
                    Priority = 6
                });
            }
        }
    }

    private static double CalculateSimilarity(string content1, string content2)
    {
        var props1 = CssParser.ContentToPropertiesPublic(content1);
        var props2 = CssParser.ContentToPropertiesPublic(content2);

        if (props1.Count == 0 && props2.Count == 0) return 1.0;
        if (props1.Count == 0 || props2.Count == 0) return 0.0;

        var common = props1.Keys.Intersect(props2.Keys).Count(key => props1[key] == props2[key]);
        var total = Math.Max(props1.Count, props2.Count);

        return (double)common / total;
    }
}
