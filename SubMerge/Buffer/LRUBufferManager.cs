using System.Diagnostics;
using Serilog;
using SubMerge.Models;
using SubMerge.Storage;

namespace SubMerge.Buffer;

public sealed class LruBufferManager
    : IBufferManager, IDisposable
{
    private readonly ulong _amountOfIndexFrames;

    private readonly ulong _amountOfPageFrames;

    private readonly IFileManager _fileManager;
    private readonly ILogger _logger = Log.ForContext<LruBufferManager>();
    private readonly Dictionary<ulong, Page> _pageFrames = new();
    private readonly LinkedList<ulong> _pageLruList = [];

    private int _readCount;
    private int _writeCount;

    public LruBufferManager(
        IFileManager fileManager,
        ulong amountOfPageFrames = 2,
        ulong amountOfIndexFrames = 1
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amountOfPageFrames);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amountOfIndexFrames);
        _amountOfPageFrames = amountOfPageFrames;
        _amountOfIndexFrames = amountOfIndexFrames;
        _fileManager = fileManager;
    }

    public async Task<Result<Unit, BufferError>> InitializeAsync()
    {
        _logger.Debug("Initializing LRU buffer manager");
        Debug.Assert(_amountOfIndexFrames > 0);
        Debug.Assert(_amountOfPageFrames > 0);
        Debug.Assert(_fileManager != null);


        _logger.Information(
            "LRU buffer manager initialized with {PageFrames} page frames and {IndexFrames} index frames",
            _amountOfPageFrames, _amountOfIndexFrames);
        return await Task.FromResult(Result<Unit, BufferError>.Success(Unit.Value));
    }

    public async Task<Result<BufferMetrics, BufferError>> BufferMetricsAsync()
    {
        return await Task.FromResult(
            Result<BufferMetrics, BufferError>.Success(
                new BufferMetrics(_readCount, _writeCount)
            ));
    }

    public async Task<Result<Page, BufferError>> GetRandomPageAsync()
    {
        _logger.Debug("Getting random page from buffer");

        // Check if is there a page in the buffer
        if (_pageLruList.Count == 0)
        {
            _logger.Warning("No pages in buffer");

            // Load First page, this is tided to the file manager using sequential
            var firstPageResult = await LoadPageAsync(1L);
            if (firstPageResult.IsSuccess)
                return await Task.FromResult(Result<Page, BufferError>.Success(firstPageResult.GetValueOrThrow()));

            var error = firstPageResult.GetErrorOrThrow();
            _logger.Error("Failed to load first page: {Error}", error);
            return await Task.FromResult(Result<Page, BufferError>.Error(
                new BufferError($"Failed to load first page {error}")));
        }

        // Get the first page in the LRU list
        Debug.Assert(_pageLruList.First != null);

        var pageId = _pageLruList.First.Value;
        var page = _pageFrames[pageId];

        _logger.Debug("Random page found: {PageId}", pageId);

        return await Task.FromResult(Result<Page, BufferError>.Success(page));
    }

    public async Task<Result<Page, BufferError>> LoadPageAsync(ulong pageId)
    {
        _logger.Debug("Loading page {PageId}", pageId);

        if (_pageFrames.TryGetValue(pageId, out var page))
        {
            _logger.Debug("Page {PageId} found in buffer", pageId);
            _pageLruList.Remove(pageId);
            _pageLruList.AddFirst(pageId);
            return await Task.FromResult(Result<Page, BufferError>.Success(page));
        }

        _logger.Debug("Page {PageId} not found in buffer, loading from file manager", pageId);

        var result = await _fileManager.PageExistsAsync(pageId);
        if (result.IsError)
        {
            var error = result.GetErrorOrThrow();
            _logger.Error("Failed to load page {PageId} from file manager: {Error}", pageId, error);
            return await Task.FromResult(Result<Page, BufferError>.Error(
                new BufferError($"Failed to check if page exists on file manager {result.GetErrorOrThrow()}")));
        }

        if (!result.GetValueOrThrow())
        {
            _logger.Error("Page {PageId} does not exist in file manager", pageId);
            return await Task.FromResult(Result<Page, BufferError>.Error(
                new BufferError($"Page {pageId} does not exist in file manager")));
        }

        var pageResult = await _fileManager.ReadPageAsync(pageId);
        if (pageResult.IsError)
        {
            var error = pageResult.GetErrorOrThrow();
            _logger.Error("Failed to read page {PageId} from file manager: {Error}", pageId, error);
            return await Task.FromResult(Result<Page, BufferError>.Error(
                new BufferError($"Failed to read page from file manager {error}")));
        }

        _readCount++;

        var pageData = pageResult.GetValueOrThrow();

        if (_pageFrames.Count < (int)_amountOfPageFrames)
        {
            _logger.Debug("Adding page {PageId} to buffer", pageData.PageId);
            _pageFrames.Add(pageData.PageId, pageData);
            _pageLruList.AddFirst(pageData.PageId);

            return await Task.FromResult(Result<Page, BufferError>.Success(pageData));
        }

        _logger.Debug("Buffer is full, evicting least recently used page");

        // Should never be null, but just in case
        Debug.Assert(_pageLruList.Last != null);

        var evictResult = await EvictLruPage();
        if (evictResult.IsError)
        {
            var error = evictResult.GetErrorOrThrow();
            _logger.Error("Failed to evict page from buffer: {Error}", error);
            return await Task.FromResult(Result<Page, BufferError>.Error(
                new BufferError($"Failed to evict page from buffer {result.GetErrorOrThrow()}")));
        }

        _pageFrames.Add(pageData.PageId, pageData);
        _pageLruList.AddFirst(pageData.PageId);

        return await Task.FromResult(Result<Page, BufferError>.Success(pageData));
    }

    public async Task<Result<Unit, BufferError>> PutPageAsync(Page page)
    {
        _logger.Debug("Putting page {PageId} to buffer", page.PageId);
        if (_pageFrames.ContainsKey(page.PageId))
        {
            _logger.Debug("Page {PageId} already exists in buffer", page.PageId);
            return await Task.FromResult(Result<Unit, BufferError>.Success(Unit.Value));
        }

        if (_pageFrames.Count >= (int)_amountOfPageFrames)
        {
            _logger.Debug("Buffer is full, evicting least recently used page");
            var evictResult = await EvictLruPage();
            if (evictResult.IsError)
            {
                var error = evictResult.GetErrorOrThrow();
                _logger.Error("Failed to evict page from buffer: {Error}", error);
                return await Task.FromResult(Result<Unit, BufferError>.Error(
                    new BufferError($"Failed to evict page from buffer {error}")));
            }
        }

        _logger.Debug("Adding page {PageId} to buffer", page.PageId);

        _pageFrames.Add(page.PageId, page);
        _pageLruList.AddFirst(page.PageId);

        return await Task.FromResult(Result<Unit, BufferError>.Success(Unit.Value));
    }

    public async Task<Result<Unit, BufferError>> FlushPageAsync(ulong pageId)
    {
        _logger.Debug("Flushing page {PageId}", pageId);

        if (!_pageFrames.TryGetValue(pageId, out var page))
        {
            _logger.Warning("Page {PageId} not found in buffer", pageId);
            return await Task.FromResult(Result<Unit, BufferError>.Success(Unit.Value));
        }

        var result = await _fileManager.WritePageAsync(page);
        if (result.IsError)
        {
            var error = result.GetErrorOrThrow();
            return await Task.FromResult(Result<Unit, BufferError>.Error(
                new BufferError($"Failed to write page {pageId} to file manager {error}")));
        }

        _writeCount++;

        _logger.Information("Flushed page {PageId}", pageId);

        return await Task.FromResult(Result<Unit, BufferError>.Success(Unit.Value));
    }

    public Task<Result<Unit, BufferError>> SetPageDirty(ulong pageId)
    {
        _logger.Debug("Setting page {PageId} as dirty", pageId);
        if (_pageFrames.TryGetValue(pageId, out var page))
        {
            page.IsDirty = true;
            _logger.Information("Page {PageId} set as dirty", pageId);
        }
        else
        {
            _logger.Warning("Page {PageId} not found in buffer", pageId);
        }

        return Task.FromResult(Result<Unit, BufferError>.Success(Unit.Value));
    }

    public async Task<Result<Unit, BufferError>> FlushAllFramesAsync()
    {
        _logger.Debug("Flushing all frames");

        _logger.Debug("Flushing all page frames");
        foreach (var frame in _pageFrames.Values.ToList())
        {
            var result = await _fileManager.WritePageAsync(frame);
            if (result.IsError)
            {
                var error = result.GetErrorOrThrow();
                _logger.Error("Failed to flush page {PageId}: {Error}", frame.PageId, error);
                return await Task.FromResult(Result<Unit, BufferError>.Error(
                    new BufferError($"Failed to flush page {frame.PageId} to file manager {error}")));
            }

            _writeCount++;
        }

        _logger.Information("Flushed all page frames");

        _logger.Information("Flushing all frames complete");

        return await Task.FromResult(Result<Unit, BufferError>.Success(Unit.Value));
    }

    public void Dispose()
    {
    }

    private async Task<Result<Unit, BufferError>> EvictLruPage()
    {
        if (_pageLruList.Last == null)
            return await Task.FromResult(Result<Unit, BufferError>.Error(
                new BufferError("No pages to evict")));

        var lruPageId = _pageLruList.Last.Value;
        var evictedPage = _pageFrames[lruPageId];
        var pageWriteResult = await FlushPageAsync(evictedPage.PageId);
        if (pageWriteResult.IsError)
        {
            var error = pageWriteResult.GetErrorOrThrow();
            _logger.Error("Failed to write evicted page {PageId} to file manager: {Error}", evictedPage.PageId, error);
            return await Task.FromResult(Result<Unit, BufferError>.Error(
                new BufferError($"Failed to write evicted page to file manager {error}")));
        }

        _pageLruList.RemoveLast();
        _pageFrames.Remove(lruPageId);

        _logger.Debug("Evicted page {PageId} from buffer", lruPageId);

        return await Task.FromResult(Result<Unit, BufferError>.Success(Unit.Value));
    }
}
