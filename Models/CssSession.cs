namespace CssClassUtility.Models;

/// <summary>
/// 代表一個記憶體內的 CSS 編輯工作階段
/// </summary>
public class CssSession
{
    /// <summary>
    /// 工作階段 ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 原始檔案路徑 (若有的話)
    /// </summary>
    public string? OriginalFilePath { get; set; }

    /// <summary>
    /// 目前的 CSS 內容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 最後修改時間
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.Now;

    /// <summary>
    /// 是否有未儲存的變更
    /// </summary>
    public bool IsDirty { get; set; } = false;

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
