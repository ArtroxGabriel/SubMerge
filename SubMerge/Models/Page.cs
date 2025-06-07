namespace SubMerge.Models;

public class Page
{
    public Page(PageId pageId, Tuple[] content)
    {
        PageId = pageId;
        Content = content;
        LastAccessed = DateTime.UtcNow;
        LastUpdated = DateTime.UtcNow;
        CreatedAt = DateTime.UtcNow;
    }

    public int NumberOfTuples => Content.Length;
    public PageId PageId { get; private set; }
    public Tuple[] Content { get; private set; }
    public DateTime LastAccessed { get; set; }
    public DateTime LastUpdated { get; set; }
    public DateTime CreatedAt { get; set; }

    public void AddTuple(Tuple tuple)
    {
        if (Content.Length > 10)
        {
            throw new InvalidOperationException("Page is full, cannot add more tuples.");
        }

        Content = Content.Append(tuple).ToArray();
        LastUpdated = DateTime.UtcNow;
        LastAccessed = DateTime.UtcNow;
    }
}
