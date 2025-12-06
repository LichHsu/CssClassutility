using System.Text.Json.Serialization;

namespace CssClassUtility.Models;

/// <summary>
/// CSS 結構診斷結果
/// </summary>
public class CssDiagnosisResult
{
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }

    [JsonPropertyName("openBraceCount")]
    public int OpenBraceCount { get; set; }

    [JsonPropertyName("closeBraceCount")]
    public int CloseBraceCount { get; set; }

    [JsonPropertyName("totalClasses")]
    public int TotalClasses { get; set; }

    [JsonPropertyName("duplicateClasses")]
    public List<DuplicateClassInfo> DuplicateClasses { get; set; } = [];

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = [];
}

public class DuplicateClassInfo
{
    [JsonPropertyName("className")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("selectors")]
    public List<string> Selectors { get; set; } = [];
}
