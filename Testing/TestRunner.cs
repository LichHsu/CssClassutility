using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CssClassutility.AI;
using CssClassutility.Models;
using CssClassutility.Core;
using CssClassutility.Operations;
using CssClassutility.Diagnostics;

namespace CssClassutility.Testing;

/// <summary>
/// 測試執行器
/// </summary>
public static class TestRunner
{
    // 偵錯用：設定 Log 檔案路徑 (會產生在 exe 同層目錄)
    private static readonly string _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mcp_debug_log.txt");
    private static readonly string _testCssPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test.css");
    private static bool _isTestingMode = false;

    /// <summary>
    /// 執行所有功能測試
    /// </summary>
    public static void RunAllTests()
    {
        _isTestingMode = true;
        Log("\n========== 開始執行全功能測試 ==========");

        int totalTests = 0;
        int passedTests = 0;
        int failedTests = 0;

        // 確保測試檔案存在
        if (!File.Exists(_testCssPath))
        {
            Console.WriteLine($"[錯誤] 找不到測試檔案: {_testCssPath}");
            return;
        }

        Console.WriteLine($"使用測試檔案: {_testCssPath}\n");

        // === 測試 1: get_css_classes ===
        totalTests++;
        try
        {
            Log("[測試 1] get_css_classes");
            var classes = CssParser.GetClasses(_testCssPath);
            Console.WriteLine($"✓ 測試 1: get_css_classes - 找到 {classes.Count} 個 classes");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 1 失敗: {ex.Message}");
            Log($"[錯誤] 測試 1: {ex}");
            failedTests++;
        }

        // === 測試 2: update_css_class ===
        totalTests++;
        try
        {
            Log("[測試 2] update_css_class");
            string result = CssParser.UpdateClassProperty(_testCssPath, "update-test", "border", "2px solid red", "Set");
            Console.WriteLine($"✓ 測試 2: update_css_class - {result}");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 2 失敗: {ex.Message}");
            Log($"[錯誤] 測試 2: {ex}");
            failedTests++;
        }

        // === 測試 3: compare_css_style ===
        totalTests++;
        try
        {
            Log("[測試 3] compare_css_style");
            var result = CssParser.CompareCssStyle("color: red; padding: 10px;", "padding: 10px; color: red;");
            if (result.IsIdentical)
            {
                Console.WriteLine("✓ 測試 3: compare_css_style - 成功比較樣式");
                passedTests++;
            }
            else
            {
                Console.WriteLine("✗ 測試 3: 樣式比較失敗（應該相同）");
                failedTests++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 3 失敗: {ex.Message}");
            Log($"[錯誤] 測試 3: {ex}");
            failedTests++;
        }

        // === 測試 4: convert_to_css_json ===
        totalTests++;
        try
        {
            Log("[測試 4] convert_to_css_json");
            var entity = CssParser.ConvertToCssJson(_testCssPath, "test-single-prop");
            Console.WriteLine($"✓ 測試 4: convert_to_css_json - 轉換為 JSON，屬性數: {entity.Properties.Count}");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 4 失敗: {ex.Message}");
            Log($"[錯誤] 測試 4: {ex}");
            failedTests++;
        }

        // === 測試 5: convert_from_css_json ===
        totalTests++;
        try
        {
            Log("[測試 5] convert_from_css_json");
            var entity = new CssEntity
            {
                Name = "test",
                Selector = ".test",
                Properties = new SortedDictionary<string, string> { { "color", "blue" } }
            };
            string css = CssParser.ConvertFromCssJson(entity);
            Console.WriteLine($"✓ 測試 5: convert_from_css_json - 成功轉換回 CSS");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 5 失敗: {ex.Message}");
            Log($"[錯誤] 測試 5: {ex}");
            failedTests++;
        }

        // === 測試 6: diagnosis_css_struct ===
        totalTests++;
        try
        {
            Log("[測試 6] diagnosis_css_struct");
            var diagnosis = CssParser.DiagnosisCssStruct(_testCssPath);
            Console.WriteLine($"✓ 測試 6: diagnosis_css_struct - 有效性: {diagnosis.IsValid}, 重複 classes: {diagnosis.DuplicateClasses.Count}");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 6 失敗: {ex.Message}");
            Log($"[錯誤] 測試 6: {ex}");
            failedTests++;
        }

        // === 測試 7: get_duplicate_classes ===
        totalTests++;
        try
        {
            Log("[測試 7] get_duplicate_classes");
            var duplicates = CssParser.GetDuplicateClasses(_testCssPath);
            Console.WriteLine($"✓ 測試 7: get_duplicate_classes - 找到 {duplicates.Count} 個重複 classes");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 7 失敗: {ex.Message}");
            Log($"[錯誤] 測試 7: {ex}");
            failedTests++;
        }

        // === 測試 8: take_css_class ===
        totalTests++;
        try
        {
            Log("[測試 8] take_css_class");
            string cssText = CssParser.TakeCssClass(_testCssPath, "test-single-prop", 0);
            Console.WriteLine($"✓ 測試 8: take_css_class - 成功取得 CSS 文字（長度: {cssText.Length}）");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 8 失敗: {ex.Message}");
            Log($"[錯誤] 測試 8: {ex}");
            failedTests++;
        }

        // === 測試 9: restructure_css (創建副本測試) ===
        totalTests++;
        try
        {
            Log("[測試 9] restructure_css");
            string testCopy = _testCssPath.Replace(".css", "_copy.css");
            File.Copy(_testCssPath, testCopy, true);
            string result = CssParser.RestructureCss(testCopy);
            Console.WriteLine($"✓ 測試 9: restructure_css - {result}");
            File.Delete(testCopy);
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 9 失敗: {ex.Message}");
            Log($"[錯誤] 測試 9: {ex}");
            failedTests++;
        }

        // === 測試 10: merge_css_class ===
        totalTests++;
        try
        {
            Log("[測試 10] merge_css_class");
            string testCopy = _testCssPath.Replace(".css", "_merge.css");
            File.Copy(_testCssPath, testCopy, true);
            // 將路徑中的反斜線轉為正斜線以符合格式要求
            string sourceObj = $"{testCopy.Replace("\\", "/")}:.merge-source-overwrite";
            string result = CssParser.MergeCssClass(testCopy, "merge-target", sourceObj, MergeStrategy.Overwrite);
            Console.WriteLine($"✓ 測試 10: merge_css_class - {result}");
            File.Delete(testCopy);
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 10 失敗: {ex.Message}");
            Log($"[錯誤] 測試 10: {ex}");
            failedTests++;
        }

        // === 測試 11: export_css_to_entities ===
        totalTests++;
        try
        {
            Log("[測試 11] export_css_to_entities");
            string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CssEntities_Test");
            string result = CssParser.ExportCssToEntities(_testCssPath, outputDir, "DeleteAll");
            Console.WriteLine($"✓ 測試 11: export_css_to_entities - {result}");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 11 失敗: {ex.Message}");
            Log($"[錯誤] 測試 11: {ex}");
            failedTests++;
        }

        // === 測試 12: import_css_from_entities ===
        totalTests++;
        try
        {
            Log("[測試 12] import_css_from_entities");
            string sourceDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CssEntities_Test", "test");
            string outputFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_imported.css");
            string result = CssParser.ImportCssFromEntities(sourceDir, outputFile, false);
            Console.WriteLine($"✓ 測試 12: import_css_from_entities - {result}");
            if (File.Exists(outputFile)) File.Delete(outputFile);
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 12 失敗: {ex.Message}");
            Log($"[錯誤] 測試 12: {ex}");
            failedTests++;
        }

        // === 測試 13: get_css_entity ===
        totalTests++;
        try
        {
            Log("[測試 13] get_css_entity");
            string entityPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CssEntities_Test", "test", "test-single-prop.json");
            var entity = CssParser.GetCssEntity(entityPath);
            Console.WriteLine($"✓ 測試 13: get_css_entity - 成功讀取實體 {entity.Name}");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 13 失敗: {ex.Message}");
            Log($"[錯誤] 測試 13: {ex}");
            failedTests++;
        }

        // === 測試 14: update_css_entity_property ===
        totalTests++;
        try
        {
            Log("[測試 14] update_css_entity_property");
            string entityPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CssEntities_Test", "test", "test-single-prop.json");
            string result = CssParser.UpdateCssEntityProperty(entityPath, "color", "purple", "Set");
            Console.WriteLine($"✓ 測試 14: update_css_entity_property - {result}");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 14 失敗: {ex.Message}");
            Log($"[錯誤] 測試 14: {ex}");
            failedTests++;
        }

        // === 測試 15: merge_css_entity ===
        totalTests++;
        try
        {
            Log("[測試 15] merge_css_entity");
            string targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CssEntities_Test", "test", "merge-target.json");
            string sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CssEntities_Test", "test", "merge-source-overwrite.json");
            string result = CssParser.MergeCssEntity(targetPath, sourcePath, MergeStrategy.Overwrite);
            Console.WriteLine($"✓ 測試 15: merge_css_entity - {result}");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 15 失敗: {ex.Message}");
            Log($"[錯誤] 測試 15: {ex}");
            failedTests++;
        }

        // === 測試 16: merge_css_class_from_file ===
        totalTests++;
        try
        {
            Log("[測試 16] merge_css_class_from_file");
            string testCopy = _testCssPath.Replace(".css", "_merge_file.css");
            File.Copy(_testCssPath, testCopy, true);
            string result = CssParser.MergeCssClassFromFile(testCopy, "merge-target", _testCssPath, "merge-source-overwrite", MergeStrategy.Overwrite);
            Console.WriteLine($"✓ 測試 16: merge_css_class_from_file - {result}");
            File.Delete(testCopy);
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 16 失敗: {ex.Message}");
            Log($"[錯誤] 測試 16: {ex}");
            failedTests++;
        }

        // === 測試 17: identify_design_tokens ===
        totalTests++;
        try
        {
            Log("[測試 17] identify_design_tokens");
            var result = DesignTokenAnalyzer.IdentifyDesignTokens(_testCssPath, 1);
            Console.WriteLine($"✓ 測試 17: identify_design_tokens - 找到 {result.Colors.Count} 個顏色 tokens");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 17 失敗: {ex.Message}");
            Log($"[錯誤] 測試 17: {ex}");
            failedTests++;
        }

        // === 測試 18: trace_css_usage ===
        totalTests++;
        try
        {
            Log("[測試 18] trace_css_usage");
            // 建立一個臨時的 razor 檔案來測試
            string tempRazor = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test.razor");
            File.WriteAllText(tempRazor, "<div class=\"test-single-prop\"></div>");
            
            var result = UsageTracer.TraceCssUsage("test-single-prop", AppDomain.CurrentDomain.BaseDirectory, new[] { ".razor" });
            Console.WriteLine($"✓ 測試 18: trace_css_usage - 找到 {result.TotalOccurrences} 次使用");
            
            File.Delete(tempRazor);
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 18 失敗: {ex.Message}");
            Log($"[錯誤] 測試 18: {ex}");
            failedTests++;
        }

        // === 測試 19: suggest_css_refactoring ===
        totalTests++;
        try
        {
            Log("[測試 19] suggest_css_refactoring");
            var result = RefactoringAdvisor.SuggestRefactoring(_testCssPath, 1);
            Console.WriteLine($"✓ 測試 19: suggest_css_refactoring - 提出 {result.Suggestions.Count} 個建議");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 19 失敗: {ex.Message}");
            Log($"[錯誤] 測試 19: {ex}");
            failedTests++;
        }

        // === 測試 20: batch_replace_property_values ===
        totalTests++;
        try
        {
            Log("[測試 20] batch_replace_property_values");
            string testCopy = _testCssPath.Replace(".css", "_batch.css");
            File.Copy(_testCssPath, testCopy, true);
            
            var result = BatchReplacer.BatchReplacePropertyValues(testCopy, "red", "blue", "color", false);
            Console.WriteLine($"✓ 測試 20: batch_replace_property_values - 替換了 {result.AffectedClasses.Count} 個 classes");
            
            File.Delete(testCopy);
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 20 失敗: {ex.Message}");
            Log($"[錯誤] 測試 20: {ex}");
            failedTests++;
        }

        // === 測試 21: analyze_variable_impact ===
        totalTests++;
        try
        {
            Log("[測試 21] analyze_variable_impact");
            // 假設測試檔案中有變數，若無則此測試可能回傳 0 影響，但也算通過執行
            var result = VariableAnalyzer.AnalyzeVariableImpact(_testCssPath, "--test-var");
            Console.WriteLine($"✓ 測試 21: analyze_variable_impact - 分析完成，影響 {result.TotalImpact} 個 classes");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 21 失敗: {ex.Message}");
            Log($"[錯誤] 測試 21: {ex}");
            failedTests++;
        }

        // === 測試 22: remove_css_class ===
        totalTests++;
        try
        {
            Log("[測試 22] remove_css_class");
            string testCopy = _testCssPath.Replace(".css", "_remove.css");
            File.Copy(_testCssPath, testCopy, true);
            
            string result = CssParser.RemoveCssClass(testCopy, "test-single-prop");
            Console.WriteLine($"✓ 測試 22: remove_css_class - {result}");
            
            File.Delete(testCopy);
            // 清理備份
            foreach (var f in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*_remove.css.safe_backup_*"))
            {
                File.Delete(f);
            }
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 22 失敗: {ex.Message}");
            Log($"[錯誤] 測試 22: {ex}");
            failedTests++;
        }

        // === 測試 23: CssSessionManager ===
        totalTests++;
        try
        {
            Log("[測試 23] CssSessionManager");
            var session = CssSessionManager.CreateSession(_testCssPath);
            if (session.Content.Length == 0) throw new Exception("Session 內容為空");
            
            string newContent = session.Content + "\n.new-session-class { color: pink; }";
            CssSessionManager.UpdateSessionContent(session.Id, newContent);
            
            var updatedSession = CssSessionManager.GetSession(session.Id);
            if (updatedSession.Content != newContent) throw new Exception("Session 內容更新失敗");
            if (!updatedSession.IsDirty) throw new Exception("Session IsDirty 狀態錯誤");
            
            string tempSavePath = _testCssPath.Replace(".css", "_session_save.css");
            CssSessionManager.SaveSession(session.Id, tempSavePath);
            
            if (!File.Exists(tempSavePath)) throw new Exception("Session 儲存失敗");
            
            CssSessionManager.CloseSession(session.Id);
            if (CssSessionManager.GetSession(session.Id) != null) throw new Exception("Session 關閉失敗");
            
            Console.WriteLine($"✓ 測試 23: CssSessionManager - 成功建立、更新、儲存與關閉 Session");
            File.Delete(tempSavePath);
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ 測試 23 失敗: {ex.Message}");
            Log($"[錯誤] 測試 23: {ex}");
            failedTests++;
        }

        // === 測試 24: consolidate_css_files ===
        totalTests++;
        try
        {
            Log("[測試 24] consolidate_css_files");
            string file1 = _testCssPath.Replace(".css", "_1.css");
            string file2 = _testCssPath.Replace(".css", "_2.css");
            string merged = _testCssPath.Replace(".css", "_merged.css");
            
            File.WriteAllText(file1, ".class1 { color: red; }");
            File.WriteAllText(file2, ".class2 { color: blue; }");
            
            CssMerger.BatchMerge(new[] { file1, file2 }, merged, MergeStrategy.Overwrite);
            
            if (!File.Exists(merged)) throw new Exception("合併檔案未建立");
            string mergedContent = File.ReadAllText(merged);
            if (!mergedContent.Contains(".class1") || !mergedContent.Contains(".class2")) 
                throw new Exception("合併內容不完整");
            
            Console.WriteLine($"✓ 測試 24: consolidate_css_files - 成功合併 2 個檔案");
            
            File.Delete(file1);
            File.Delete(file2);
            File.Delete(merged);
            passedTests++;
        }
        catch (Exception ex)
        {
             Console.WriteLine($"✗ 測試 24 失敗: {ex.Message}");
             Log($"[錯誤] 測試 24: {ex}");
             failedTests++;
        }

        // === 測試 25: analyze_css_usage ===
        totalTests++;
        try
        {
            Log("[測試 25] analyze_css_usage");
            string tempRazor = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestUsage.razor");
            string tempCss = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestUsage.css");
            
            File.WriteAllText(tempRazor, "<div class=\"used-class\"></div>");
            File.WriteAllText(tempCss, ".used-class { color: red; } .unused-class { color: blue; }");
            
            var result = CssUsageAnalyzer.AnalyzeUsage(tempCss, AppDomain.CurrentDomain.BaseDirectory, new[] { ".razor" });
            
            // 驗證 .unused-class 被標記為 Unused
            if (!result.UnusedClasses.Contains("unused-class")) 
                 throw new Exception("未能偵測到未使用 Class");
            
            Console.WriteLine($"✓ 測試 25: analyze_css_usage - 成功偵測到 {result.UnusedClasses.Count} 個未使用 Class");
            
            File.Delete(tempRazor);
            File.Delete(tempCss);
            passedTests++;
        }
        catch (Exception ex)
        {
             Console.WriteLine($"✗ 測試 25 失敗: {ex.Message}");
             Log($"[錯誤] 測試 25: {ex}");
             failedTests++;
        }

        // === 測試 26: list_css_sessions ===
        totalTests++;
        try
        {
            Log("[測試 26] list_css_sessions");
            var session = CssSessionManager.CreateSession(_testCssPath);
            var sessions = CssSessionManager.ListSessions();
            
            if (sessions.Count == 0) throw new Exception("未能列出 Session");
            if (!sessions.Any(s => s.Id == session.Id)) throw new Exception("列表未包含剛建立的 Session");
            
            CssSessionManager.CloseSession(session.Id);
            Console.WriteLine($"✓ 測試 26: list_css_sessions - 成功列出活躍 Sessions");
            passedTests++;
        }
        catch (Exception ex)
        {
             Console.WriteLine($"✗ 測試 26 失敗: {ex.Message}");
             Log($"[錯誤] 測試 26: {ex}");
             failedTests++;
        }

        // 輸出測試摘要
        Console.WriteLine("\n========== 測試摘要 ==========");
        Console.WriteLine($"總測試數: {totalTests}");
        Console.WriteLine($"通過: {passedTests}");
        Console.WriteLine($"失敗: {failedTests}");
        Console.WriteLine($"成功率: {(passedTests * 100.0 / totalTests):F1}%");

        Log($"\n測試完成 - 通過: {passedTests}/{totalTests}");

        Console.WriteLine($"\n詳細日誌已寫入: {_logPath}");
        Console.WriteLine("============================\n");
        
        // 清理測試產生的目錄
        try 
        {
            string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CssEntities_Test");
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
        }
        catch {}
    }

    // 簡單的檔案寫入 Log
    private static void Log(string message)
    {
        if (!_isTestingMode) return;
        try { File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}"); }
        catch { }
    }
}
