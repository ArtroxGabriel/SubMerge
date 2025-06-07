using SubMerge.Models;

namespace SubMerge.Storage;

public interface IFileManager
{
    Task<Result<Unit, StoreError>> InitializeAsync();
    Task<Result<Page, StoreError>> ReadPageAsync(ulong pageId);
    Task<Result<Unit, StoreError>> WritePageAsync(Page page);
    Task<Result<bool, StoreError>> PageExistsAsync(ulong pageId);
    Task<Result<Page, StoreError>> AllocateNewPageAsync();
    Task<Result<Unit, StoreError>> FlushAsync();

    // FIXME: replace this WineRecord with a more generic type
    Task<Result<bool, StoreError>> PageHasEnoughSpaceToInsertRecord(Page page, WineRecord newRecord);
}

public readonly struct StoreError(string message)
{
    public string Message { get; init; } = message;
}
