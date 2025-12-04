using CssClassutility.Models;

namespace CssClassutility.Operations;

/// <summary>
/// CSS Class 合併工具
/// </summary>
public static class CssMerger
{
    /// <summary>
    /// 合併 CSS Class（從 JSON 實體或另一個檔案）
    /// </summary>
    public static string MergeCssClass(
        string targetPath,
        string targetClassName,
        string sourceObject,
        MergeStrategy strategy = MergeStrategy.Overwrite,
        int targetIndex = 0)
    {
        // sourceObject 格式：
        // 1. JSON 字串 (以 { 開頭)
        // 2. 檔案路徑:.className 格式

        CssEntity sourceEntity;

        if (sourceObject.TrimStart().StartsWith("{"))
        {
            // JSON 格式
            sourceEntity = System.Text.Json.JsonSerializer.Deserialize<CssEntity>(sourceObject)
                ?? throw new Exception("無法解析來源 JSON");
        }
        else
        {
            // 檔案格式：path:.className
            var match = System.Text.RegularExpressions.Regex.Match(sourceObject, @"(.+\\.css):\\.?(.+)");
            if (!match.Success)
                throw new ArgumentException("來源格式錯誤，應為 'path:.className' 或 JSON");

            string sourcePath = match.Groups[1].Value;
            string sourceClassName = match.Groups[2].Value;

            var sourceClasses = CssParser.GetClasses(sourcePath)
                .Where(c => c.ClassName.Equals(sourceClassName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (sourceClasses.Count == 0)
                throw new Exception($"來源檔案中找不到 Class .{sourceClassName}");

            var sourceClass = sourceClasses[0];
            sourceEntity = new CssEntity
            {
                Name = sourceClassName,
                Selector = sourceClass.Selector,
                Properties = new System.Collections.Generic.SortedDictionary<string, string>(
                    CssParser.ContentToPropertiesPublic(sourceClass.Content))
            };
        }

        // 取得目標 Class
        var targetClasses = CssParser.GetClasses(targetPath)
            .Where(c => c.ClassName.Equals(targetClassName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetClasses.Count == 0)
            throw new Exception($"目標檔案中找不到 Class .{targetClassName}");

        if (targetIndex >= targetClasses.Count)
            throw new Exception($"索引 {targetIndex} 超出範圍");

        var target = targetClasses[targetIndex];
        var targetProps = CssParser.ContentToPropertiesPublic(target.Content);

        // 根據策略合併
        switch (strategy)
        {
            case MergeStrategy.Overwrite:
                foreach (var kvp in sourceEntity.Properties)
                    targetProps[kvp.Key] = kvp.Value;
                break;

            case MergeStrategy.FillMissing:
                foreach (var kvp in sourceEntity.Properties)
                    if (!targetProps.ContainsKey(kvp.Key))
                        targetProps[kvp.Key] = kvp.Value;
                break;

            case MergeStrategy.PruneDuplicate:
                foreach (var kvp in sourceEntity.Properties.ToList())
                    if (targetProps.ContainsKey(kvp.Key) && targetProps[kvp.Key] == kvp.Value)
                        targetProps.Remove(kvp.Key);
                break;
        }

        // 寫回
        string newContent = CssParser.PropertiesToContentPublic(targetProps, target.Selector);
        CssParser.ReplaceBlockPublic(targetPath, target.StartIndex, target.BlockEnd, newContent);

        return $"已合併到 Class .{targetClassName} (策略: {strategy})";
    }
}

public enum MergeStrategy
{
    Overwrite,
    FillMissing,
    PruneDuplicate
}
