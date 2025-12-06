namespace CssClassUtility.Operations;

/// <summary>
/// CSS Class 更新工具
/// </summary>
public static class CssUpdater
{
    /// <summary>
    /// 更新 CSS Class 的屬性
    /// </summary>
    public static string UpdateClassProperty(
        string path,
        string className,
        string key,
        string value,
        string action = "Set",
        int index = 0)
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
        var props = CssParser.ContentToPropertiesPublic(target.Content);

        // 根據 action 處理
        switch (action.ToLower())
        {
            case "set":
                props[key] = value;
                break;
            case "remove":
                props.Remove(key);
                break;
            default:
                throw new ArgumentException($"不支援的操作：{action}");
        }

        // 重建 CSS 內容
        string newCssContent = CssParser.PropertiesToContentPublic(props, target.Selector);
        CssParser.ReplaceBlockPublic(path, target.StartIndex, target.BlockEnd, newCssContent);

        return $"已更新 Class .{className} (索引 {index})";
    }
}
