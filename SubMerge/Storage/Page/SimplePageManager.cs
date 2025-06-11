using System.Text;
using System.Text.Json;
using Serilog;
using SubMerge.Models;
using SubMerge.Storage.Files;
using Tuple = SubMerge.Models.Tuple;

namespace SubMerge.Storage.Page;

public class SimplePageManager(
    IFileManager fileManager,
    int pageSizeInBytes = 4096
) : IPageManager
{
    private readonly ILogger _logger = Log.ForContext<SimplePageManager>();

    public async Task<Result<Models.Page, PageManagerError>> ReadPageAsync(PageId pageId)
    {
        _logger.Debug("Reading page {PageId}", pageId);

        var openFileResult = await fileManager.OpenFileAsync(pageId.FileName);
        if (openFileResult.IsError)
        {
            _logger.Error("Failed to open file for page {PageId}: {Error}", pageId, openFileResult.GetErrorOrThrow());
            return Result<Models.Page, PageManagerError>.Error(new PageManagerError("Failed to open file for page"));
        }

        var (_, openFile) = openFileResult.GetValueOrThrow();

        openFile.Seek(pageId.PageNumInFile * pageSizeInBytes, SeekOrigin.Begin);

        var buffer = new byte[pageSizeInBytes];

        var bytesRead = await openFile.ReadAsync(buffer, 0, pageSizeInBytes);

        if (bytesRead == 0)
        {
            _logger.Warning("No data was read for page {PageId}", pageId);
            return Result<Models.Page, PageManagerError>.Error(
                new PageManagerError($"No data found for page {pageId}"));
        }

        // Deserialize the buffer into a Page object
        var jsonData = Encoding.UTF8.GetString(buffer).TrimEnd('\0');

        // Skip empty pages
        if (string.IsNullOrWhiteSpace(jsonData))
        {
            _logger.Warning("Page {PageId} is empty", pageId);
            return Result<Models.Page, PageManagerError>.Error(new PageManagerError($"Page {pageId} is empty"));
        }

        var page = JsonSerializer.Deserialize<Models.Page>(jsonData);
        if (page == null)
        {
            _logger.Warning("Failed to deserialize page {PageId}", pageId);
            return Result<Models.Page, PageManagerError>.Error(
                new PageManagerError($"Failed to deserialize page {pageId}"));
        }

        page.LastAccessed = DateTime.Now;

        return await Task.FromResult(Result<Models.Page, PageManagerError>.Success(page));
    }

    public async Task<Result<Unit, PageManagerError>> WritePageAsync(PageId pageId, Models.Page page)
    {
        _logger.Debug("Writing page with ID: {PageId}", page.PageId);

        var openFileResult = await fileManager.OpenFileAsync(pageId.FileName);
        if (openFileResult.IsError)
        {
            _logger.Error("Failed to open file for page {PageId}: {Error}", pageId, openFileResult.GetErrorOrThrow());
            return Result<Unit, PageManagerError>.Error(new PageManagerError("Failed to open file for page"));
        }

        var (heapFileMetadata, openFile) = openFileResult.GetValueOrThrow();

        if ((ulong)page.PageId.PageNumInFile > heapFileMetadata.LastPageId)
        {
            _logger.Error("Page ID {PageId} is greater than the last page ID {LastPageId}",
                page.PageId, heapFileMetadata.LastPageId);
            return await Task.FromResult(Result<Unit, PageManagerError>.Error(
                new PageManagerError(
                    $"Page ID {page.PageId} is greater than the last page ID {heapFileMetadata.LastPageId}")));
        }

        try
        {
            var jsonData = JsonSerializer.Serialize(page);
            var buffer = Encoding.UTF8.GetBytes(jsonData);

            if (buffer.Length > pageSizeInBytes)
            {
                _logger.Error(
                    "Page size exceeds the maximum allowed size of {PageSizeInBytes} bytes, creating a new one",
                    pageSizeInBytes);
                return await Task.FromResult(Result<Unit, PageManagerError>.Error(
                    new PageManagerError(
                        $"Page size exceeds the maximum allowed size of {pageSizeInBytes} bytes, creating a new one")));
            }

            var paddedBuffer = new byte[pageSizeInBytes];
            Array.Copy(buffer, paddedBuffer, buffer.Length);

            openFile.Seek(page.PageId.PageNumInFile * pageSizeInBytes, SeekOrigin.Begin);
            await openFile.WriteAsync(paddedBuffer, 0, paddedBuffer.Length);
        }
        catch (Exception e)
        {
            _logger.Error("Failed to write page: {Error}", e.Message);
            return await Task.FromResult(Result<Unit, PageManagerError>.Error(
                new PageManagerError($"Failed to write page: {e.Message}")));
        }

        var updateResult = UpdateHeapMetadataFile(heapFileMetadata.FilePath, heapFileMetadata);
        if (updateResult.IsError)
            return await Task.FromResult(Result<Unit, PageManagerError>.Error(
                new PageManagerError($"Failed to update heap metadata file: {updateResult}")));

        _logger.Information("Page with ID: {PageId} written successfully", page.PageId);

        return await Task.FromResult(Result<Unit, PageManagerError>.Success(Unit.Value));
    }

    public async Task<Result<Models.Page, PageManagerError>> AllocateNewPageAsync(string fileName)
    {
        _logger.Debug("Allocating new page");

        var openFileResult = await fileManager.OpenFileAsync(fileName);
        if (openFileResult.IsError)
        {
            _logger.Error("Failed to open file for page {FileName}: {Error}", fileName,
                openFileResult.GetErrorOrThrow());
            return Result<Models.Page, PageManagerError>.Error(new PageManagerError("Failed to open file for page"));
        }

        var (heapFileMetadata, _) = openFileResult.GetValueOrThrow();

        var pageId = new PageId((int)heapFileMetadata.LastPageId + 1, fileName);
        var newPage = new Models.Page(pageId, []);

        heapFileMetadata.LastPageId++;
        heapFileMetadata.PageCount++;

        var result = await WritePageAsync(pageId, newPage);
        if (result.IsError)
        {
            _logger.Error("Failed to allocate new page: {Error}", result);
            return Result<Models.Page, PageManagerError>.Error(result.GetErrorOrThrow());
        }

        _logger.Information("Allocated new page with ID: {PageId}", newPage.PageId);

        return await Task.FromResult(Result<Models.Page, PageManagerError>.Success(newPage));
    }

    public async Task<Result<bool, PageManagerError>> HasEnoughSpaceToInsertTuple(Models.Page page, Tuple tuple)
    {
        _logger.Debug("Checking if page {PageId} has enough space to insert record", page.PageId);

        try
        {
            // Create a copy of the page and add the new record to estimate its size
            var tempContent = page.Content.ToList();
            tempContent.Add(tuple);

            var tempPage = new Models.Page(page.PageId, tempContent.ToArray());

            // Serialize to calculate size
            var jsonData = JsonSerializer.Serialize(tempPage);
            var serializedSize = Encoding.UTF8.GetByteCount(jsonData);

            var hasEnoughSpace = serializedSize <= pageSizeInBytes;

            _logger.Debug("Page {PageId} serialized size with new record would be {Size} bytes " +
                          "({HasSpace} for page size limit of {PageSize} bytes)",
                page.PageId, serializedSize,
                hasEnoughSpace ? "enough space" : "not enough space",
                pageSizeInBytes);

            return await Task.FromResult(Result<bool, PageManagerError>.Success(hasEnoughSpace));
        }
        catch (Exception e)
        {
            _logger.Error("Error checking page space capacity: {Error}", e.Message);
            return await Task.FromResult(Result<bool, PageManagerError>
                .Error(new PageManagerError($"Failed to check page space: {e.Message}")));
        }
    }

    private Result<Unit, PageManagerError> UpdateHeapMetadataFile(string metadataFilePath, HeapFileMetadata metadata)
    {
        try
        {
            metadata.LastModifiedAt = DateTime.UtcNow;
            var jsonMetadata =
                JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(metadataFilePath, jsonMetadata);

            return Result<Unit, PageManagerError>.Success(Unit.Value);
        }
        catch (Exception e)
        {
            _logger.Error("Failed to update metadata file: {MetadataFilePath}. Error: {Error}",
                metadataFilePath, e.Message);
            return Result<Unit, PageManagerError>.Error(
                new PageManagerError($"Failed to update metadata file: {e.Message}"));
        }
    }
}
