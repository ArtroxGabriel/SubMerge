using SubMerge.Models;

namespace SubMerge.Storage.Buffer;

public interface IBufferManager
{
    Task<Result<Unit, BufferError>> InitializeAsync();

    // Frame operations
    Task<Result<Models.Page, BufferError>> PinPageAsync(PageId pageId);
    Task<Result<Unit, BufferError>> UnpinPageAsync(PageId pageId);
    Task<Result<Unit, BufferError>> FlushPageAsync(PageId pageId);

    Task<Result<Unit, BufferError>> FlushAllFramesAsync();
}

public readonly struct BufferError
{
    public string Message { get; }

    public BufferError(string message)
    {
        Message = message;
    }
}
