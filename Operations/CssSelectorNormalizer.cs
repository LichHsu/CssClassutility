using System.Text.RegularExpressions;

namespace CssClassUtility.Operations;

public static class CssSelectorNormalizer
{
    private static readonly Regex _selectorSplitter = new(@"(?=[ >+~,])|(?<=[ >+~,])"); // Split keeping delimiters

    /// <summary>
    /// 正規化 CSS 選擇器 (例如將 .foo.bar 轉換為 .bar.foo 以進行去重)
    /// </summary>
    public static string Normalize(string selector)
    {
        // 1. 移除多餘空白
        selector = NormalizeSpaces(selector);

        // 2. 分割為選擇器群組 (comma separated)
        var groups = selector.Split(',');
        var normalizedGroups = new List<string>();

        foreach (var group in groups)
        {
            normalizedGroups.Add(NormalizeGroup(group.Trim()));
        }

        // 3. 排序群組本身 (.a, .b 與 .b, .a 視為相同)
        normalizedGroups.Sort();

        return string.Join(", ", normalizedGroups);
    }

    private static string NormalizeSpaces(string s)
    {
        return Regex.Replace(s.Trim(), @"\s+", " ");
    }

    private static string NormalizeGroup(string groupSelector)
    {
        // 暫時簡單實作：依據空白與運算子拆分
        // e.g. ".a.b > .c" -> [".a.b", " > ", ".c"]
        // 這邊用簡單的正則拆分可能不夠完美，但對於 Class 去重已足夠

        // Split by operators/spaces, but keeping them.
        // Logic: Search for sequences of class names (starting with .)
        // For a segment like ".btn.btn-primary", we want to sort it to ".btn.btn-primary" (or .btn-primary.btn? alphabetical)

        // 為了避免過度破壞結構，我們只針對 "連續的 Class 選擇器" 進行排序
        // Find pattern: (\.[a-zA-Z0-9_-]+)+

        return Regex.Replace(groupSelector, @"(\.[a-zA-Z0-9_-]+){2,}", MatchEvaluator);
    }

    private static string MatchEvaluator(Match match)
    {
        // Split into individual classes
        var classes = match.Value.Split('.', StringSplitOptions.RemoveEmptyEntries);
        Array.Sort(classes);
        return "." + string.Join(".", classes);
    }
}
