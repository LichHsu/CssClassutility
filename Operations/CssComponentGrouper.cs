using System.Text.RegularExpressions;
using CssClassUtility.Models;

namespace CssClassUtility.Operations;

/// <summary>
/// 識別並分組 CSS 元件 (例如 .btn, .btn:hover => Component 'btn')
/// </summary>
public static class CssComponentGrouper
{
    public class ComponentGroup
    {
        public string Name { get; set; } = "";
        public List<CssClass> Classes { get; set; } = new();
        public List<string> Selectors { get; set; } = new();
    }

    public static List<ComponentGroup> GroupComponents(string cssPath)
    {
        var classes = CssParser.GetClasses(cssPath);
        var groups = new Dictionary<string, ComponentGroup>();

        foreach (var cls in classes)
        {
            // 嘗試從 Selector 提取 "Base Name"
            // 規則: 取第一個 Class Name，移除偽類別 (:hover) 和偽元素 (::before)，以及 BEM 修飾符 (--modifier)
            string baseName = ExtractBaseName(cls.Selector);

            if (!groups.ContainsKey(baseName))
            {
                groups[baseName] = new ComponentGroup { Name = baseName };
            }

            groups[baseName].Classes.Add(cls);
            groups[baseName].Selectors.Add(cls.Selector);
        }

        return groups.Values.OrderBy(g => g.Name).ToList();
    }

    private static string ExtractBaseName(string selector)
    {
        // 簡單實作：取第一個出現的 Class Name (.foo)
        var match = Regex.Match(selector, @"\.([a-zA-Z0-9_-]+)");
        if (!match.Success) return "unknown";

        string name = match.Groups[1].Value;

        // 移除 BEM Modifier (--active, __element) - 簡易版
        // 如果是 .card--active，Base 是 .card
        // 如果是 .card__title，Base 是 .card (視為 Component 的一部分)
        int doubleDash = name.IndexOf("--");
        if (doubleDash > 0) name = name.Substring(0, doubleDash);

        int doubleUnderscore = name.IndexOf("__");
        if (doubleUnderscore > 0) name = name.Substring(0, doubleUnderscore);
        
        // 處理偽類別 (通常 Regex 只抓到 classname，不含 :hover，所以只需處理 class 名稱內的字串)
        // 但如果 selector 是 .btn:hover，Regex classname 會抓到 "btn"，這已經是我們要的了。

        return name;
    }
}
