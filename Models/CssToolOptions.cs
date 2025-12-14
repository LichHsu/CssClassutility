using Lichs.MCP.Core.Attributes;

namespace CssClassUtility.Models;

public class CssAnalysisOptions
{
    public int? Threshold { get; set; } // Min occurrences
    public List<string>? ClassesToCheck { get; set; }
    public string? ClassName { get; set; }
    public string? ProjectRoot { get; set; }
    public List<string>? KnownUsedClasses { get; set; }
}

public class CssEditOperation
{
    public string Op { get; set; } = ""; // Set, Remove, Merge
    public string ClassName { get; set; } = "";

    // For Set
    public string? Key { get; set; }
    public string? Value { get; set; }

    // For Merge
    [McpParameter("來源格式: path/to/file.css:.className")]
    public string? Source { get; set; }
    public string? Strategy { get; set; } // Overwrite, FillMissing
}
