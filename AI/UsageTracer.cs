using System.Text.RegularExpressions;
using CssClassutility.Models;

namespace CssClassutility.AI;

/// <summary>
/// CSS 使用追蹤器
/// </summary>
public static class UsageTracer
{
    /// <summary>
    /// 追蹤 CSS class 在專案中的使用位置
    /// </summary>
    public static CssUsageTrace TraceCssUsage(string className, string projectRoot, string[]? fileExtensions = null)
    {
        var trace = new CssUsageTrace
        {
            ClassName = className,
            TotalOccurrences = 0,
            Locations = new List<UsageLocation>()
        };

        // 預設搜尋的副檔名
        fileExtensions ??= new[] { ".html", ".razor", ".jsx", ".tsx", ".vue", ".cshtml", ".aspx" };

        // 排除的目錄
        var excludeDirs = new[] { "node_modules", "bin", "obj", ".git", ".vs", "wwwroot\\lib" };

        // 正則表達式模式
        var patterns = new[]
        {
            new Regex($@"\bclass\s*=\s*[""']([^""']*\b{Regex.Escape(className)}\b[^""']*)[""']", RegexOptions.Compiled),
            new Regex($@"\bclassName\s*=\s*[""']([^""']*\b{Regex.Escape(className)}\b[^""']*)[""']", RegexOptions.Compiled),
            new Regex($@"classList\.(?:add|remove|toggle)\([""']{Regex.Escape(className)}[""']\)", RegexOptions.Compiled),
            new Regex($@"@class\s*=\s*[""']([^""']*\b{Regex.Escape(className)}\b[^""']*)[""']", RegexOptions.Compiled) // Blazor
        };

        if (!Directory.Exists(projectRoot))
        {
            return trace;
        }

        // 遞迴搜尋檔案
        SearchDirectory(projectRoot, fileExtensions, excludeDirs, patterns, className, trace);

        trace.TotalOccurrences = trace.Locations.Count;
        return trace;
    }

    private static void SearchDirectory(
        string directory,
        string[] extensions,
        string[] excludeDirs,
        Regex[] patterns,
        string className,
        CssUsageTrace trace)
    {
        try
        {
            // 檢查是否為排除目錄
            if (excludeDirs.Any(ex => directory.Contains(ex, StringComparison.OrdinalIgnoreCase)))
                return;

            // 搜尋檔案
            foreach (var ext in extensions)
            {
                var files = Directory.GetFiles(directory, $"*{ext}", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    SearchFile(file, patterns, className, trace);
                }
            }

            // 遞迴搜尋子目錄
            foreach (var subDir in Directory.GetDirectories(directory))
            {
                SearchDirectory(subDir, extensions, excludeDirs, patterns, className, trace);
            }
        }
        catch
        {
            // 忽略無法存取的目錄
        }
    }

    private static void SearchFile(string filePath, Regex[] patterns, string className, CssUsageTrace trace)
    {
        try
        {
            var lines = File.ReadAllLines(filePath);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                foreach (var pattern in patterns)
                {
                    if (pattern.IsMatch(line))
                    {
                        trace.Locations.Add(new UsageLocation
                        {
                            FilePath = filePath,
                            LineNumber = i + 1,
                            Context = line.Trim()
                        });
                        break; // 每行只記錄一次
                    }
                }
            }
        }
        catch
        {
            // 忽略無法讀取的檔案
        }
    }
}
