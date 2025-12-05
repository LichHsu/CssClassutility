using System.Text.Json.Serialization;

namespace CssClassutility.Models;

public class CssClass
{
    [JsonPropertyName("className")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("selector")]
    public string Selector { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("context")]
    public string? Context { get; set; }

    [JsonPropertyName("file")]
    public string File { get; set; } = string.Empty;

    [JsonPropertyName("startIndex")]
    public int StartIndex { get; set; }

    [JsonPropertyName("blockEnd")]
    public int BlockEnd { get; set; }
}

/// <summary>
/// CSS 實體結構 (JSON 格式)
/// </summary>
public class CssEntity
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("selector")]
    public string Selector { get; set; } = string.Empty;

    [JsonPropertyName("properties")]
    public SortedDictionary<string, string> Properties { get; set; } = [];

    [JsonPropertyName("metadata")]
    public CssEntityMetadata Metadata { get; set; } = new();
}

public class CssEntityMetadata
{
    [JsonPropertyName("sourceFile")]
    public string? SourceFile { get; set; }

    [JsonPropertyName("context")]
    public string? Context { get; set; }
}

/// <summary>
/// CSS 樣式比較結果
/// </summary>
public class CssCompareResult
{
    [JsonPropertyName("isIdentical")]
    public bool IsIdentical { get; set; }

    [JsonPropertyName("normalizedA")]
    public string NormalizedA { get; set; } = string.Empty;

    [JsonPropertyName("normalizedB")]
    public string NormalizedB { get; set; } = string.Empty;
}

/// <summary>
/// 合併策略列舉
/// </summary>
public enum MergeStrategy
{
    Overwrite,
    FillMissing,
    PruneDuplicate
}
