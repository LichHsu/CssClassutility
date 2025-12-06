using CssClassUtility.Models;
using System.Text.Json;

namespace CssClassUtility.Core;

/// <summary>
/// CSS JSON 轉換器
/// </summary>
public static class CssJsonConverter
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// 將 CSS Class 轉換為 JSON 實體
    /// </summary>
    public static CssEntity ConvertToCssJson(string path, string className, int index = 0)
    {
        var classes = CssParser.GetClasses(path)
            .Where(c => c.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (classes.Count == 0)
            throw new Exception($"找不到 Class .{className}");

        if (index >= classes.Count)
            throw new Exception($"索引 {index} 超出範圍");

        var target = classes[index];
        var props = CssParser.ContentToPropertiesPublic(target.Content);

        return new CssEntity
        {
            Name = className,
            Selector = target.Selector,
            Properties = props.ToSortedDictionary()
        };
    }

    /// <summary>
    /// 將 JSON 實體轉換回 CSS
    /// </summary>
    public static string ConvertFromCssJson(CssEntity entity)
    {
        return CssParser.PropertiesToContentPublic(
            entity.Properties.ToDictionary(k => k.Key, v => v.Value),
            entity.Selector);
    }

    /// <summary>
    /// 匯出 CSS 檔案為 JSON 實體集合
    /// </summary>
    public static string ExportCssToEntities(string cssPath, string outputDir)
    {
        if (!File.Exists(cssPath))
            throw new FileNotFoundException("找不到 CSS 檔案", cssPath);

        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        var classes = CssParser.GetClasses(cssPath);
        int exported = 0;

        foreach (var cssClass in classes)
        {
            var entity = new CssEntity
            {
                Name = cssClass.ClassName,
                Selector = cssClass.Selector,
                Properties = CssParser.ContentToPropertiesPublic(cssClass.Content).ToSortedDictionary()
            };

            string jsonPath = Path.Combine(outputDir, $"{cssClass.ClassName}.json");
            string json = JsonSerializer.Serialize(entity, _jsonOptions);
            File.WriteAllText(jsonPath, json);
            exported++;
        }

        return $"已匯出 {exported} 個 CSS 實體到 {outputDir}";
    }

    /// <summary>
    /// 從 JSON 實體集合重建 CSS 檔案
    /// </summary>
    public static string ImportCssFromEntities(string inputDir, string outputPath)
    {
        if (!Directory.Exists(inputDir))
            throw new DirectoryNotFoundException("找不到實體目錄");

        var jsonFiles = Directory.GetFiles(inputDir, "*.json");
        var sb = new System.Text.StringBuilder();
        int imported = 0;

        foreach (var jsonFile in jsonFiles)
        {
            string json = File.ReadAllText(jsonFile);
            var entity = JsonSerializer.Deserialize<CssEntity>(json);

            if (entity != null)
            {
                string css = ConvertFromCssJson(entity);
                sb.AppendLine(css);
                sb.AppendLine();
                imported++;
            }
        }

        File.WriteAllText(outputPath, sb.ToString());
        return $"已從 {imported} 個實體重建 CSS 到 {outputPath}";
    }
}

public static class DictionaryExtensions
{
    public static SortedDictionary<TKey, TValue> ToSortedDictionary<TKey, TValue>(
        this IDictionary<TKey, TValue> source) where TKey : notnull
    {
        return new SortedDictionary<TKey, TValue>(source);
    }
}
