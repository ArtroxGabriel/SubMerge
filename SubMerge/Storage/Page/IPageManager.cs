using SubMerge.Models;
using Tuple = SubMerge.Models.Tuple;

namespace SubMerge.Storage.Page;

public interface IPageManager
{
    // Page operations
    Task<Result<Models.Page, PageManagerError>> ReadPageAsync(PageId pageId);
    Task<Result<Unit, PageManagerError>> WritePageAsync(PageId pageId, Models.Page page);
    Task<Result<Models.Page, PageManagerError>> AllocateNewPageAsync(string fileName);
    Task<Result<bool, PageManagerError>> HasEnoughSpaceToInsertTuple(Models.Page page, Tuple tuple);
}

public readonly struct PageManagerError
{
    public string Message { get; }

    public PageManagerError(string message)
    {
        Message = message;
    }
}
