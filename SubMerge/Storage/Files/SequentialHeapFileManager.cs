using Serilog;
using SubMerge.Models;
using System.Text.Json;

namespace SubMerge.Storage.Files;

public class SequentialHeapFileManager(
    string storagePath = "./heaps",
    ulong heapSizeInBytes = 100000000
) : IFileManager
{
    private readonly Dictionary<string, FileStream> _files = new ();
    private readonly Dictionary<string, HeapFileMetadata> _metadataFiles = new ();
    private readonly ILogger _logger = Log.ForContext<SequentialHeapFileManager>();

    public async Task<Result<Tuple<HeapFileMetadata, FileStream>, FileError>> CreateFileAsync(string fileName)
    {
        _logger.Debug("Creating file storage manager with fileName: {FileName}", fileName);

        var filePath = Path.Combine(storagePath, fileName);

        if (!Directory.Exists(storagePath))
        {
            var result = CreateDirectory(storagePath);
            if (result.IsError) return Result<Tuple<HeapFileMetadata, FileStream>, FileError>.Error(result.GetErrorOrThrow());
        }
        else
        {
            _logger.Information("File storage was already created skipping heap and metadata creation");
            return Result<Tuple<HeapFileMetadata, FileStream>, FileError>.Success(new Tuple<HeapFileMetadata, FileStream>(_metadataFiles[filePath], _files[filePath]));
        }

        var heapResult = CreateHeapMetadataFile(filePath);
        if (heapResult.IsError) return Result<Tuple<HeapFileMetadata, FileStream>, FileError>.Error(heapResult.GetErrorOrThrow());

        heapResult = await CreateHeapFile(filePath);
        if (heapResult.IsError) return Result<Tuple<HeapFileMetadata, FileStream>, FileError>.Error(heapResult.GetErrorOrThrow());
        _logger.Information("File storage manager created new file with success");

        return Result<Tuple<HeapFileMetadata, FileStream>, FileError>.Success(new Tuple<HeapFileMetadata, FileStream>(_metadataFiles[filePath], _files[filePath]));
    }

    public async Task<Result<Unit, FileError>> DeleteFileAsync(string fileName)
    {
        var hasFile = _files.TryGetValue(fileName, out var file);
        if (!hasFile)
        {
            _logger.Information("No file found to delete in buffer, searching in storage")
        }
        else {
            _files.Remove(fileName);
            _metadataFiles.Remove(fileName);
        }
        var filePath = Path.Combine(storagePath, fileName);
        try
        {
            File.Delete(filePath + ".heap");
            File.Delete(filePath + ".metadata");
        } catch (Exception ex) {
            _logger.Error("Failed to delete file at path {FilePath} with error: {Exception}", filePath, ex.Message);
            return Result<Unit, FileError>.Error(
                new FileError($"Failed to delete file at path {filePath} with error: {ex.Message}")
                )
        }
        return Result<Unit, FileError>.Success(Unit.Value);
    }

    public Task<Result<Tuple<HeapFileMetadata, FileStream>, FileError>> OpenFileAsync(string fileName)
    {
        throw new NotImplementedException();
    }

    public async Task<Result<Unit, FileError>> CloseFileAsync(string fileName)
    {
        var hasFile = _files.TryGetValue(fileName, out var file);
        if (!hasFile)
        {
            _logger.Information("No file found to close, skipping...");
            return await Task.FromResult(Result<Unit, FileError>.Success(Unit.Value));
        }

        _files.Remove(fileName);
        _metadataFiles.Remove(fileName);

        file?.Dispose();

        return await Task.FromResult(Result<Unit, FileError>.Success(Unit.Value));
    }

    private Result<Unit, FileError> CreateDirectory(string directory)
    {
        _logger.Debug("Creating storage directory: {StoragePath}", directory);

        try
        {
            Directory.CreateDirectory(storagePath);
            _logger.Information("Created storage directory: {StoragePath}", directory);
        }
        catch (Exception e)
        {
            _logger.Error("Failed to create storage directory: {StoragePath}. Error: {Error}", directory,
                e.Message);
            return Result<Unit, FileError>.Error(
                new FileError($"Failed to create storage directory: {e.Message}"));
        }

        return Result<Unit, FileError>.Success(Unit.Value);
    }

    private Result<Unit, FileError> CreateHeapMetadataFile(string fileName)
    {
        var heapFilePath = fileName + ".metadata";
        _logger.Debug("Creating heap file in path: {HeapFilePath}", heapFilePath);

        try
        {
            var metadata = new HeapFileMetadata
            {
                FilePath = fileName + ".heap",
                LastPageId = 0,
                PageCount = 0,
                HeapSizeInBytes = (long)heapSizeInBytes,
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            };
            _metadataFiles.Add(fileName, metadata);

            var jsonMetadata =
                JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(heapFilePath, jsonMetadata);

            _logger.Information("Heap Metadata file created successfully: {HeapFilePath}", heapFilePath);
        }
        catch (Exception e)
        {
            _logger.Error("Failed to create heap file: {HeapFilePath}. Error: {Error}", heapFilePath, e.Message);
            return Result<Unit, FileError>.Error(
                new FileError($"Failed to create heap file: {e.Message}"));
        }

        _logger.Debug("Heap file created successfully: {HeapFilePath}", heapFilePath);
        return Result<Unit, FileError>.Success(Unit.Value);
    }

    private Result<Unit, FileError> UnmarshalHeapMetadataFile(string fileName)
    {
        var heapFilePath = fileName + ".metadata";
        _logger.Debug("Unmarshalling heap file metadata at {HeapFilePath}", heapFilePath);

        try
        {
            var jsonMetadata = File.ReadAllText(heapFilePath);
            var metadata = JsonSerializer.Deserialize<HeapFileMetadata>(jsonMetadata);

            if (metadata == null)
                return Result<Unit, FileError>.Error(
                    new FileError("Failed to deserialize metadata: result was null"));

            _logger.Debug("Heap metadata loaded: {FilePath}, {LastPageId}, {PageCount}, " +
                          "{HeapSize}MB, {Created}, {Modified}",
                metadata.FilePath,
                metadata.LastPageId,
                metadata.PageCount,
                metadata.HeapSizeInBytes / (1024 * 1024),
                metadata.CreatedAt,
                metadata.LastModifiedAt);
            _metadataFiles.Add(fileName, metadata);

            return Result<Unit, FileError>.Success(Unit.Value);
        }
        catch (Exception e)
        {
            _logger.Error("Failed to unmarshal heap file: {HeapFilePath}. Error: {Error}", heapFilePath, e.Message);
            return Result<Unit, FileError>.Error(
                new FileError($"Failed to unmarshal heap file: {e.Message}"));
        }
    }

    private async Task<Result<Unit, FileError>> CreateHeapFile(string heapFilePath)
    {
        _logger.Debug("Creating heap file in path: {HeapFilePath}", heapFilePath);

        try
        {
            await using var fs = new FileStream(heapFilePath, FileMode.Create, FileAccess.Write);
            fs.SetLength((long)heapSizeInBytes);
            _files.Add(heapFilePath, fs);
        }
        catch (Exception e)
        {
            _logger.Error("Failed to create heap file: {HeapFilePath}. Error: {Error}", heapFilePath, e.Message);
            return Result<Unit, FileError>.Error(
                new FileError($"Failed to create heap file: {e.Message}"));
        }

        _logger.Information("Heap file created successfully: {HeapFilePath}", heapFilePath);
        return Result<Unit, FileError>.Success(Unit.Value);
    }

    private Result<Unit, FileError> OpenHeapFile(string heapFilePath)
    {
        _logger.Debug("Opening heap file j enin path: {HeapFilePath}", heapFilePath);

        try
        {
            var fs = new FileStream(heapFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            _files.Add(heapFilePath, fs);
        }
        catch (Exception e)
        {
            _logger.Error("Failed to open heap file: {HeapFilePath}. Error: {Error}", heapFilePath, e.Message);
            return Result<Unit, FileError>.Error(
                new FileError($"Failed to open heap file: {e.Message}"));
        }

        _logger.Debug("Heap file opened successfully: {HeapFilePath}", heapFilePath);
        return Result<Unit, FileError>.Success(Unit.Value);
    }

    private Result<Unit, FileError> UpdateHeapMetadataFile(string metadataFilePath, HeapFileMetadata metadata)
    {
        try
        {
            metadata.LastModifiedAt = DateTime.UtcNow;
            var jsonMetadata =
                JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(metadataFilePath, jsonMetadata);

            return Result<Unit, FileError>.Success(Unit.Value);
        }
        catch (Exception e)
        {
            _logger.Error("Failed to update metadata file: {MetadataFilePath}. Error: {Error}",
                metadataFilePath, e.Message);
            return Result<Unit, FileError>.Error(
                new FileError($"Failed to update metadata file: {e.Message}"));
        }
    }

}

