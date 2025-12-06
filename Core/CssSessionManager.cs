using System.Collections.Concurrent;
using CssClassUtility.Models;

namespace CssClassUtility.Core;

/// <summary>
/// 管理 CSS 編輯工作階段
/// </summary>
public static class CssSessionManager
{
    private static readonly ConcurrentDictionary<string, CssSession> _sessions = new();

    /// <summary>
    /// 建立新的工作階段
    /// </summary>
    public static CssSession CreateSession(string? filePath = null)
    {
        string content = "";
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            content = File.ReadAllText(filePath);
        }

        var session = new CssSession
        {
            OriginalFilePath = filePath,
            Content = content,
            IsDirty = false
        };

        _sessions[session.Id] = session;
        return session;
    }

    /// <summary>
    /// 取得工作階段
    /// </summary>
    public static CssSession? GetSession(string id)
    {
        if (_sessions.TryGetValue(id, out var session))
        {
            return session;
        }
        return null;
    }

    /// <summary>
    /// 更新工作階段內容
    /// </summary>
    public static void UpdateSessionContent(string id, string newContent)
    {
        if (_sessions.TryGetValue(id, out var session))
        {
            session.Content = newContent;
            session.LastModified = DateTime.Now;
            session.IsDirty = true;
        }
        else
        {
            throw new KeyNotFoundException($"找不到 Session ID: {id}");
        }
    }

    /// <summary>
    /// 儲存工作階段到檔案
    /// </summary>
    public static void SaveSession(string id, string? targetPath = null)
    {
        if (_sessions.TryGetValue(id, out var session))
        {
            string path = targetPath ?? session.OriginalFilePath 
                ?? throw new ArgumentException("未指定儲存路徑，且 Session 無原始路徑");

            File.WriteAllText(path, session.Content);
            
            // 如果是儲存到原始路徑，重置 Dirty 狀態
            if (path == session.OriginalFilePath)
            {
                session.IsDirty = false;
            }
            // 如果是另存新檔，更新原始路徑? 視需求而定，這裡暫時更新
            if (session.OriginalFilePath == null)
            {
                session.OriginalFilePath = path;
                session.IsDirty = false;
            }
        }
        else
        {
            throw new KeyNotFoundException($"找不到 Session ID: {id}");
        }
    }

    /// <summary>
    /// 關閉工作階段
    /// </summary>
    public static bool CloseSession(string id)
    {
        return _sessions.TryRemove(id, out _);
    }

    /// <summary>
    /// 列出所有工作階段
    /// </summary>
    public static List<CssSession> ListSessions()
    {
        return _sessions.Values.ToList();
    }
}
