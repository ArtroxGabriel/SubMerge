namespace SubMerge.Models;

public class Page(ulong pageId, WineRecord[] content)
{
    public ulong PageId { get; set; } = pageId;
    public WineRecord[] Content { get; set; } = content;
    public bool IsDirty { get; set; } = false;
    public DateTime LastAccessed { get; set; } = DateTime.UtcNow;
}
