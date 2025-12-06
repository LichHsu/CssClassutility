using System.Collections.Generic;

namespace CssClassUtility.Models;

public class CssUsageAnalysisResult
{
    public int DefinedCount { get; set; }
    public int UsedCount { get; set; }
    public List<string> UnusedClasses { get; set; } = new();
    public List<string> UndefinedClasses { get; set; } = new();
}
