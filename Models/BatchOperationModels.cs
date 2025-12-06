using System.Text.Json.Serialization;

namespace CssClassUtility.Models;

/// <summary>
/// 批次替換結果
/// </summary>
public class BatchReplaceResult
{
    [JsonPropertyName("totalMatches")]
    public int TotalMatches { get; set; }

    [JsonPropertyName("affectedClasses")]
    public List<string> AffectedClasses { get; set; } = new();

    [JsonPropertyName("replacements")]
    public List<ReplacementDetail> Replacements { get; set; } = new();

    [JsonPropertyName("backupPath")]
    public string BackupPath { get; set; } = string.Empty;
}

public class ReplacementDetail
{
    [JsonPropertyName("className")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("property")]
    public string Property { get; set; } = string.Empty;

    [JsonPropertyName("oldValue")]
    public string OldValue { get; set; } = string.Empty;

    [JsonPropertyName("newValue")]
    public string NewValue { get; set; } = string.Empty;
}

/// <summary>
/// CSS 變數影響分析結果
/// </summary>
public class VariableImpactAnalysis
{
    [JsonPropertyName("variableName")]
    public string VariableName { get; set; } = string.Empty;

    [JsonPropertyName("isDefined")]
    public bool IsDefined { get; set; }

    [JsonPropertyName("definedValue")]
    public string? DefinedValue { get; set; }

    [JsonPropertyName("directUsages")]
    public List<VariableUsage> DirectUsages { get; set; } = new();

    [JsonPropertyName("indirectUsages")]
    public List<VariableUsage> IndirectUsages { get; set; } = new();

    [JsonPropertyName("totalImpact")]
    public int TotalImpact { get; set; }
}

public class VariableUsage
{
    [JsonPropertyName("className")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("property")]
    public string Property { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; } // 0=直接, 1+=間接層級
}
