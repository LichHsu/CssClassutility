namespace CssClassUtility.Diagnostics;

/// <summary>
/// CSS 重構工具
/// </summary>
public static class CssRestructurer
{
    /// <summary>
    /// 重構 CSS 檔案（去除多餘空行、排序）
    /// </summary>
    public static string RestructureCss(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("找不到檔案", path);

        var classes = CssParser.GetClasses(path);

        // 按 class 名稱排序
        var sortedClasses = classes.OrderBy(c => c.ClassName).ToList();

        var sb = new System.Text.StringBuilder();

        foreach (var cssClass in sortedClasses)
        {
            // 重建 CSS
            sb.AppendLine($"{cssClass.Selector} {{");
            sb.AppendLine($"    {cssClass.Content.Trim()}");
            sb.AppendLine("}");
            sb.AppendLine(); // 空行
        }

        // 寫回檔案
        File.WriteAllText(path, sb.ToString());

        return $"已重構 {sortedClasses.Count} 個 classes";
    }
}
