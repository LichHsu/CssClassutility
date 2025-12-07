using CssClassUtility.Models;

namespace CssClassUtility.Core;

public static class CliHandler
{
    public static void Handle(string[] args)
    {
        if (args.Length < 2) return;

        string command = args[0].ToLower();
        string subCommand = args[1].ToLower();

        if (command == "audit" && subCommand == "css")
        {
            string? path = null;
            // Simple arg parsing
            for (int i = 2; i < args.Length; i++)
            {
                if (args[i] == "--path" && i + 1 < args.Length)
                {
                    path = args[i + 1];
                    break;
                }
            }

            if (path == null)
            {
                Console.WriteLine("錯誤: 未指定路徑 (--path)");
                return;
            }

            RunInternalCssAudit(path);
        }
        else
        {
             Console.WriteLine($"未知指令: {command} {subCommand}");
        }
    }

    private static void RunInternalCssAudit(string path)
    {
        Console.WriteLine($"[Internal Audit] Scanning directory: {path}");

        if (!Directory.Exists(path))
        {
             Console.WriteLine($"錯誤: 目錄不存在 {path}");
             return;
        }

        var cssFiles = Directory.GetFiles(path, "*.css", SearchOption.AllDirectories);
        Console.WriteLine($"Found {cssFiles.Length} CSS files.");
        Console.WriteLine("---------------------------------------------------");

        int totalErrors = 0;
        int totalWarnings = 0;
        int totalClasses = 0;

        foreach (var file in cssFiles)
        {
            try
            {
                // 調用內部函式 DiagnosisCssStruct
                var diagnosis = CssParser.DiagnosisCssStruct(file);
                
                totalClasses += diagnosis.TotalClasses;

                bool hasIssues = !diagnosis.IsValid || diagnosis.DuplicateClasses.Count > 0;
                
                if (hasIssues)
                {
                    Console.WriteLine($"FILE: {Path.GetRelativePath(path, file)}");
                    
                    if (!diagnosis.IsValid)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        foreach(var err in diagnosis.Errors)
                        {
                            Console.WriteLine($"  [Error] {err}");
                            totalErrors++;
                        }
                        Console.ResetColor();
                    }

                    if (diagnosis.DuplicateClasses.Count > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"  [Warning] Found {diagnosis.DuplicateClasses.Count} duplicate classes:");
                        foreach(var dup in diagnosis.DuplicateClasses.Take(5))
                        {
                             Console.WriteLine($"    - {dup.ClassName} (Defined {dup.Count} times)");
                        }
                        if (diagnosis.DuplicateClasses.Count > 5) Console.WriteLine($"    ... and {diagnosis.DuplicateClasses.Count - 5} more.");
                        Console.ResetColor();
                        totalWarnings++;
                    }
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"FILE: {Path.GetRelativePath(path, file)}");
                Console.WriteLine($"  [Critical] Failed to parse: {ex.Message}");
                Console.ResetColor();
                totalErrors++;
            }
        }

        Console.WriteLine("---------------------------------------------------");
        Console.WriteLine("Audit Complete.");
        Console.WriteLine($"Files Scanned: {cssFiles.Length}");
        Console.WriteLine($"Total Classes: {totalClasses}");
        
        if (totalErrors > 0) Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Errors Found: {totalErrors}");
        Console.ResetColor();

        if (totalWarnings > 0) Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Warnings (Duplicates): {totalWarnings}");
        Console.ResetColor();
        
        if (totalErrors == 0 && totalWarnings == 0)
        {
             Console.ForegroundColor = ConsoleColor.Green;
             Console.WriteLine("\nResult: CLEAN. No structural issues found.");
             Console.ResetColor();
        }
    }
}
