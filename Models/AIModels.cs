using System.Text.Json.Serialization;

namespace CssClassUtility.Models;

/// <summary>
/// 設計 Token 識別分析結果
/// </summary>
public class DesignTokenAnalysis
{
    [JsonPropertyName("colors")]
    public Dictionary<string, TokenSuggestion> Colors { get; set; } = new();

    [JsonPropertyName("spacings")]
    public Dictionary<string, TokenSuggestion> Spacings { get; set; } = new();

    [JsonPropertyName("fontSizes")]
    public Dictionary<string, TokenSuggestion> FontSizes { get; set; } = new();

    [JsonPropertyName("lineHeights")]
    public Dictionary<string, TokenSuggestion> LineHeights { get; set; } = new();

    [JsonPropertyName("borderRadius")]
    public Dictionary<string, TokenSuggestion> BorderRadius { get; set; } = new();

    [JsonPropertyName("shadows")]
    public Dictionary<string, TokenSuggestion> Shadows { get; set; } = new();
}

public class TokenSuggestion
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("occurrences")]
    public int Occurrences { get; set; }

    [JsonPropertyName("suggestedTokenName")]
    public string SuggestedTokenName { get; set; } = string.Empty;

    [JsonPropertyName("usedInClasses")]
    public List<string> UsedInClasses { get; set; } = [];
}

/// <summary>
/// CSS 使用追蹤結果
/// </summary>
public class CssUsageTrace
{
    [JsonPropertyName("className")]
    public string ClassName { get; set; } = string.Empty;

    [JsonPropertyName("totalOccurrences")]
    public int TotalOccurrences { get; set; }

    [JsonPropertyName("locations")]
    public List<UsageLocation> Locations { get; set; } = [];
}

public class UsageLocation
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("lineNumber")]
    public int LineNumber { get; set; }

    [JsonPropertyName("context")]
    public string Context { get; set; } = string.Empty;
}

/// <summary>
/// CSS 重構建議
/// </summary>
public class RefactoringSuggestion
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("affectedClasses")]
    public List<string> AffectedClasses { get; set; } = [];

    [JsonPropertyName("details")]
    public Dictionary<string, object> Details { get; set; } = new();

    [JsonPropertyName("priority")]
    public int Priority { get; set; }
}

public class RefactoringAnalysis
{
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("suggestions")]
    public List<RefactoringSuggestion> Suggestions { get; set; } = [];

    [JsonPropertyName("statistics")]
    public Dictionary<string, int> Statistics { get; set; } = new();
}
