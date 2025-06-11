namespace SubMerge.Storage.Buffer;

public class BufferFrame
{
    public BufferFrame(Models.Page page)
    {
        Page = page;
        IsDirty = false;
        PinCount = 0;
    }

    public Models.Page Page { get; set; }
    public bool IsDirty { get; set; }
    public int PinCount { get; set; }
}
