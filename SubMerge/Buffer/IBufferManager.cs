using SubMerge.Models;

namespace SubMerge.Buffer;

/// <summary>
/// Defines the contract for a buffer manager that handles page and index frames in memory.
/// Provides methods for initialization, metrics, and page/frame operations.
/// </summary>
public interface IBufferManager
{
    /// <summary>
    /// Initializes the buffer manager and prepares it for use.
    /// </summary>
    /// <returns>A result indicating success or a buffer error.</returns>
    Task<Result<Unit, BufferError>> InitializeAsync();

    /// <summary>
    /// Retrieves current buffer metrics, such as read and write counts.
    /// </summary>
    /// <returns>A result containing buffer metrics or a buffer error.</returns>
    Task<Result<BufferMetrics, BufferError>> BufferMetricsAsync();

    /// <summary>
    /// Gets a random page from the buffer, loading it if not present.
    /// </summary>
    /// <returns>A result containing the page or a buffer error.</returns>
    Task<Result<Page, BufferError>> GetRandomPageAsync();

    /// <summary>
    /// Loads a page into the buffer from the file manager, or retrieves it if already loaded.
    /// </summary>
    /// <param name="pageId">The ID of the page to load.</param>
    /// <returns>A result containing the loaded page or a buffer error.</returns>
    Task<Result<Page, BufferError>> LoadPageAsync(ulong pageId);

    /// <summary>
    /// Puts a page into the buffer, evicting a page if the buffer is full.
    /// </summary>
    /// <param name="page">The page to put in the buffer.</param>
    /// <returns>A result indicating success or a buffer error.</returns>
    Task<Result<Unit, BufferError>> PutPageAsync(Page page);

    /// <summary>
    /// Flushes a page from the buffer to the file manager, writing changes if necessary.
    /// </summary>
    /// <param name="pageId">The ID of the page to flush.</param>
    /// <returns>A result indicating success or a buffer error.</returns>
    Task<Result<Unit, BufferError>> FlushPageAsync(ulong pageId);

    /// <summary>
    /// Marks a page as dirty in the buffer, indicating it has unsaved changes.
    /// </summary>
    /// <param name="pageId">The ID of the page to mark as dirty.</param>
    /// <returns>A result indicating success or a buffer error.</returns>
    Task<Result<Unit, BufferError>> SetPageDirty(ulong pageId);

    /// <summary>
    /// Flushes all frames in the buffer to the file manager.
    /// </summary>
    /// <returns>A result indicating success or a buffer error.</returns>
    Task<Result<Unit, BufferError>> FlushAllFramesAsync();
}

public readonly struct BufferError(string message)
{
    public string Message { get; } = message;
}

public readonly struct BufferMetrics(
    int totalReads,
    int totalWrites
    // int totalTuples
)
{
    public int TotalReads { get; } = totalReads;

    public int TotalWrites { get; } = totalWrites;
    // public int TotalTuples { get; } = totalTuples;
}
