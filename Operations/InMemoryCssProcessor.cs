using CssClassUtility.Models;
using System.Text;

namespace CssClassUtility.Operations;

/// <summary>
/// 處理 CSS 的記憶體內操作，避免頻繁磁碟 I/O
/// </summary>
public class InMemoryCssProcessor
{
    private readonly string _cssPath;
    private readonly Dictionary<string, CssEntity> _entities = new();
    private readonly List<string> _processingLog = new();

    public InMemoryCssProcessor(string cssPath)
    {
        _cssPath = cssPath;
        Load();
    }

    /// <summary>
    /// 載入並解析 CSS 至記憶體
    /// </summary>
    private void Load()
    {
        var classes = CssParser.GetClasses(_cssPath);
        _entities.Clear();

        // 將 CSS Class 轉換為實體，並處理重複名稱 (保留最後一個定義)
        foreach (var cls in classes)
        {
            var entity = CssParser.ConvertToCssJson(cls);
            // Key 使用 "Context|ClassName" 以區分不同 Media Query 下的同名 Class
            string key = GetEntityKey(entity);
            _entities[key] = entity;
        }
    }

    private string GetEntityKey(CssEntity entity)
    {
        string context = entity.Metadata?.Context ?? "";
        return $"{context}|{entity.Name}";
    }

    /// <summary>
    /// 更新屬性
    /// </summary>
    public void UpdateProperty(string className, string key, string value)
    {
        ProcessClass(className, "Set", key, value);
    }

    /// <summary>
    /// 移除屬性
    /// </summary>
    public void RemoveProperty(string className, string key)
    {
        ProcessClass(className, "Remove", key, "");
    }

    /// <summary>
    /// 移除整個類別
    /// </summary>
    public void RemoveClass(string className)
    {
        // 簡易搜尋移除所有 Context 下的同名 Class
        var keysToRemove = _entities.Keys.Where(k => k.EndsWith($"|{className}")).ToList();
        foreach (var key in keysToRemove)
        {
            _entities.Remove(key);
        }
    }

    /// <summary>
    /// 對指定 Class 執行操作 (內部核心)
    /// </summary>
    private string ProcessClass(string className, string operation, string key, string value)
    {
        // ... (existing logic)
        string entityKey = $"|{className}";

        if (!_entities.TryGetValue(entityKey, out var entity))
        {
            // 嘗試搜尋其他 Context
            var match = _entities.Keys.FirstOrDefault(k => k.EndsWith($"|{className}"));
            if (match == null) return $"找不到 Class: {className}";
            entity = _entities[match];
        }

        bool modified = false;

        if (operation.Equals("Set", StringComparison.OrdinalIgnoreCase))
        {
            if (!entity.Properties.TryGetValue(key, out string? oldVal) || oldVal != value)
            {
                entity.Properties[key] = value;
                modified = true;
            }
        }
        else if (operation.Equals("Remove", StringComparison.OrdinalIgnoreCase))
        {
            if (entity.Properties.Remove(key)) modified = true;
        }

        return modified ? "Modified" : "No Change";
    }

    /// <summary>
    /// 合併其他 CSS 來源 (去重與合併)
    /// </summary>
    public string Merge(string sourcePath, string strategy)
    {
        if (!File.Exists(sourcePath)) return "找不到來源檔案";

        var sourceClasses = CssParser.GetClasses(sourcePath);
        int mergedCount = 0;

        foreach (var cls in sourceClasses)
        {
            var sourceEntity = CssParser.ConvertToCssJson(cls);
            string key = GetEntityKey(sourceEntity);

            if (_entities.TryGetValue(key, out var targetEntity))
            {
                // 合併屬性
                if (CssParser.MergePropertiesPublic(targetEntity.Properties, sourceEntity.Properties, Enum.Parse<MergeStrategy>(strategy)))
                {
                    mergedCount++;
                }
            }
            else
            {
                // 新增不存在的 Class
                _entities[key] = sourceEntity;
                mergedCount++;
            }
        }

        return $"已合併 {mergedCount} 個變更來自 {sourcePath}";
    }

    /// <summary>
    /// 將記憶體狀態寫回磁碟
    /// </summary>
    public void Save(string? outputPath = null)
    {
        string targetPath = outputPath ?? _cssPath;

        // 分組處理 Context
        var grouped = _entities.Values.GroupBy(e => e.Metadata?.Context ?? "");
        var cssOutput = new List<string>();

        // Root
        var rootGroup = grouped.FirstOrDefault(g => string.IsNullOrEmpty(g.Key));
        if (rootGroup != null)
        {
            foreach (var entity in rootGroup.OrderBy(e => e.Name))
            {
                cssOutput.Add(CssParser.ConvertFromCssJson(entity));
            }
        }

        // Media Queries etc.
        foreach (var group in grouped.Where(g => !string.IsNullOrEmpty(g.Key)))
        {
            cssOutput.Add($"\n{group.Key} {{");
            foreach (var entity in group.OrderBy(e => e.Name))
            {
                string rule = CssParser.ConvertFromCssJson(entity);
                var indentedRule = string.Join("\n", rule.Split('\n').Select(l => "    " + l));
                cssOutput.Add(indentedRule);
            }
            cssOutput.Add("}");
        }

        File.WriteAllText(targetPath, string.Join("\n\n", cssOutput), Encoding.UTF8);
    }
}
