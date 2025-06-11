using System.Diagnostics;
using Serilog;
using SubMerge.Models;
using SubMerge.Storage.Page;

namespace SubMerge.Storage.Buffer;

public sealed class LruBufferManager
    : IBufferManager, IDisposable
{
    private readonly ulong _amountOfPageFrames;
    private readonly ILogger _logger = Log.ForContext<LruBufferManager>();
    private readonly Dictionary<PageId, BufferFrame> _pageFrames = new();
    private readonly LinkedList<PageId> _pageLruList = [];
    private readonly IPageManager _pageManager;

    public LruBufferManager(
        IPageManager pageManager,
        ulong amountOfPageFrames = 4
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amountOfPageFrames);
        _amountOfPageFrames = amountOfPageFrames;
        _pageManager = pageManager;
    }

    public async Task<Result<Unit, BufferError>> InitializeAsync()
    {
        _logger.Debug("Initializing LRU buffer manager");
        Debug.Assert(_amountOfPageFrames > 0);
        Debug.Assert(_pageManager != null);


        _logger.Information(
            "LRU buffer manager initialized with {PageFrames} page frames",
            _amountOfPageFrames);
        return await Task.FromResult(Result<Unit, BufferError>.Success(Unit.Value));
    }

    public async Task<Result<Models.Page, BufferError>> PinPageAsync(PageId pageId)
    {
        _logger.Debug("Loading page {PageId}", pageId);

        if (_pageFrames.TryGetValue(pageId, out var frame))
        {
            _logger.Debug("Page {PageId} found in buffer", pageId);
            _pageLruList.Remove(pageId);
            _pageLruList.AddFirst(pageId);
            frame.PinCount++;
            return await Task.FromResult(Result<Models.Page, BufferError>.Success(frame.Page));
        }

        _logger.Debug("Page {PageId} not found in buffer, loading from file manager", pageId);

        var result = await _pageManager.PageExistsAsync(pageId);
        if (result.IsError)
        {
            var error = result.GetErrorOrThrow();
            _logger.Error("Failed to load page {PageId} from file manager: {Error}", pageId, error);
            return await Task.FromResult(Result<Models.Page, BufferError>.Error(
                new BufferError($"Failed to check if page exists on file manager {result.GetErrorOrThrow()}")));
        }

        if (!result.GetValueOrThrow())
        {
            _logger.Error("Page {PageId} does not exist in file manager", pageId);
            return await Task.FromResult(Result<Models.Page, BufferError>.Error(
                new BufferError($"Page {pageId} does not exist in file manager")));
        }

        var pageResult = await _pageManager.ReadPageAsync(pageId);
        if (pageResult.IsError)
        {
            var error = pageResult.GetErrorOrThrow();
            _logger.Error("Failed to read page {PageId} from file manager: {Error}", pageId, error);
            return await Task.FromResult(Result<Models.Page, BufferError>.Error(
                new BufferError($"Failed to read page from file manager {result.GetErrorOrThrow()}")));
        }

        var pageData = pageResult.GetValueOrThrow();

        if (_pageFrames.Count < (int)_amountOfPageFrames)
        {
            _logger.Debug("Adding page {PageId} to buffer", pageData.PageId);
            _pageFrames.Add(pageData.PageId, pageData);
            _pageLruList.AddFirst(pageData.PageId);

            return await Task.FromResult(Result<Models.Page, BufferError>.Success(pageData));
        }

        _logger.Debug("Buffer is full, evicting least recently used page");

        // Should never be null, but just in case
        Debug.Assert(_pageLruList.Last != null);

        var evictResult = await EvictLruPage();
        if (evictResult.IsError)
        {
            var error = evictResult.GetErrorOrThrow();
            _logger.Error("Failed to evict page from buffer: {Error}", error);
            return await Task.FromResult(Result<Models.Page, BufferError>.Error(
                new BufferError($"Failed to evict page from buffer {result.GetErrorOrThrow()}")));
        }

        _pageFrames.Add(pageData.PageId, pageData);
        _pageLruList.AddFirst(pageData.PageId);

        return await Task.FromResult(Result<Models.Page, BufferError>.Success(pageData));
    }

    public async Task<Result<Unit, BufferError>> UnpinPageAsync(PageId pageId)
    {
        _logger.Debug("Putting page {PageId} to buffer", pageId);
        if (_pageFrames.ContainsKey(pageId))
        {
            _logger.Debug("Page {PageId} already exists in buffer", pageId);
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

        _logger.Debug("Adding page {PageId} to buffer", pageId);

        _pageFrames.Add(pageId, pageId);
        _pageLruList.AddFirst(pageId);

        return await Task.FromResult(Result<Unit, BufferError>.Success(Unit.Value));
    }

    public async Task<Result<Unit, BufferError>> FlushPageAsync(PageId pageId)
    {
        _logger.Debug("Flushing page {PageId}", pageId);

        if (!_pageFrames.TryGetValue(pageId, out var page))
        {
            _logger.Warning("Page {PageId} not found in buffer", pageId);
            return await Task.FromResult(Result<Unit, BufferError>.Success(Unit.Value));
        }

        var result = await _pageManager.WritePageAsync(pageId, page);
        if (result.IsError)
        {
            var error = result.GetErrorOrThrow();
            return await Task.FromResult(Result<Unit, BufferError>.Error(
                new BufferError($"Failed to write page {pageId} to file manager {error}")));
        }

        _logger.Information("Flushed page {PageId}", pageId);

        return await Task.FromResult(Result<Unit, BufferError>.Success(Unit.Value));
    }

    public async Task<Result<Unit, BufferError>> FlushAllFramesAsync()
    {
        _logger.Debug("Flushing all frames");

        _logger.Debug("Flushing all page frames");
        foreach (var frame in _pageFrames.Values.ToList())
        {
            var result = await _pageManager.WritePageAsync(pageId, frame.Page);
            if (result.IsError)
            {
                var error = result.GetErrorOrThrow();
                _logger.Error("Failed to flush page {PageId}: {Error}", frame.PageId, error);
                return await Task.FromResult(Result<Unit, BufferError>.Error(
                    new BufferError($"Failed to flush page {frame.PageId} to file manager {error}")));
            }
        }

        _logger.Information("Flushed all page frames");

        _logger.Information("Flushing all frames complete");

        return await Task.FromResult(Result<Unit, BufferError>.Success(Unit.Value));
    }

    public void Dispose()
    {
    }

    public Task<Result<Unit, BufferError>> SetPageDirty(PageId pageId)
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
