namespace CssClassutility.Operations;

/// <summary>
/// CSS Class 刪除工具
/// </summary>
public static class CssRemover
{
    /// <summary>
    /// 移除指定的 CSS Class
    /// </summary>
    public static string RemoveCssClass(string path, string className, int index = 0)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("找不到檔案", path);

        string content = File.ReadAllText(path);
        var classes = CssParser.GetClasses(path)
            .Where(c => c.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (classes.Count == 0)
            throw new Exception($"找不到 Class .{className}");

        if (index >= classes.Count)
            throw new Exception($"索引 {index} 超出範圍，共有 {classes.Count} 個同名 Class");

        var target = classes[index];

        // 移除 Class 區塊
        string newContent = content.Remove(target.StartIndex, target.BlockEnd - target.StartIndex + 1);
        File.WriteAllText(path, newContent);
        return $"已移除 Class .{className} (索引 {index})";
    }
}
