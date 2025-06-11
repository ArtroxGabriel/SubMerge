using System.Text.Json;
using Serilog;
using SubMerge.Models;

namespace SubMerge.Storage.Files;

public class SequentialHeapFileManager(
    string storagePath = "./heaps",
    ulong heapSizeInBytes = 100000000
) : IFileManager
{
    private readonly Dictionary<string, FileStream> _files = new();
    private readonly ILogger _logger = Log.ForContext<SequentialHeapFileManager>();
    private readonly Dictionary<string, HeapFileMetadata> _metadataFiles = new();

    public async Task<Result<Tuple<HeapFileMetadata, FileStream>, FileError>> CreateFileAsync(string fileName)
    {
        _logger.Debug("Creating file storage manager with fileName: {FileName}", fileName);

        if (_files.TryGetValue(fileName, out var file) && _metadataFiles.TryGetValue(fileName, out var metadata))
        {
            _logger.Information("File already exists in memory, returning existing file");
            return Result<Tuple<HeapFileMetadata, FileStream>, FileError>.Success(
                new Tuple<HeapFileMetadata, FileStream>(metadata, file));
        }

        if (!Directory.Exists(storagePath))
        {
            var result = CreateDirectory(storagePath);
            if (result.IsError)
                return Result<Tuple<HeapFileMetadata, FileStream>, FileError>.Error(result.GetErrorOrThrow());
        }

        var filePath = Path.Combine(storagePath, fileName);
        if (File.Exists(filePath + ".heap") && File.Exists(filePath + ".metadata"))
        {
            _logger.Warning("File already exists on disk, trying to open it");
            return await OpenFileAsync(fileName);
        }

        var heapResult = CreateHeapMetadataFile(fileName);
        if (heapResult.IsError)
            return Result<Tuple<HeapFileMetadata, FileStream>, FileError>.Error(heapResult.GetErrorOrThrow());

        heapResult = await CreateHeapFile(fileName);
        if (heapResult.IsError)
            return Result<Tuple<HeapFileMetadata, FileStream>, FileError>.Error(heapResult.GetErrorOrThrow());
        _logger.Information("File storage manager created new file with success");

        return Result<Tuple<HeapFileMetadata, FileStream>, FileError>.Success(
            new Tuple<HeapFileMetadata, FileStream>(_metadataFiles[fileName], _files[fileName]));
    }

    public async Task<Result<Unit, FileError>> DeleteFileAsync(string fileName)
    {
        _logger.Debug("Deleting file with name: {FileName}", fileName);

        var filePath = Path.Combine(storagePath, fileName);

        if (!_files.Remove(fileName) && !_metadataFiles.Remove(fileName))
            _logger.Information("No file found to delete in buffer");

        try
        {
            File.Delete(filePath + ".heap");
            File.Delete(filePath + ".metadata");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to delete file at path {FilePath} with error: {Exception}", filePath, ex.Message);
            return await Task.FromResult(
                Result<Unit, FileError>.Error(
                    new FileError($"Failed to delete file at path {filePath} with error: {ex.Message}")
                ));
        }

        return await Task.FromResult(Result<Unit, FileError>.Success(Unit.Value));
    }

    public async Task<Result<Tuple<HeapFileMetadata, FileStream>, FileError>> OpenFileAsync(string fileName)
    {
        if (_metadataFiles.TryGetValue(fileName, out var value))
            return Result<Tuple<HeapFileMetadata, FileStream>, FileError>.Success(
                new Tuple<HeapFileMetadata, FileStream>(value, _files[fileName]));

        _logger.Debug("File {FileName} not found in buffer, trying to open from disk", fileName);

        var metadataResult = await UnmarshalHeapMetadataFile(fileName);
        if (metadataResult.IsError)
            return Result<Tuple<HeapFileMetadata, FileStream>, FileError>.Error(
                metadataResult.GetErrorOrThrow());

        var heapResult = OpenHeapFile(fileName);
        if (heapResult.IsError)
            return Result<Tuple<HeapFileMetadata, FileStream>, FileError>.Error(
                heapResult.GetErrorOrThrow());


        return Result<Tuple<HeapFileMetadata, FileStream>, FileError>.Success(
            new Tuple<HeapFileMetadata, FileStream>(_metadataFiles[fileName], _files[fileName]));
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

        file?.DisposeAsync();

        return await Task.FromResult(Result<Unit, FileError>.Success(Unit.Value));
    }

    public Task<Result<bool, FileError>> FileExistsAsync(string fileName)
    {
        _logger.Debug("Checking if file exists: {FileName}", fileName);

        var filePath = Path.Combine(storagePath, fileName);
        var heapFileExists = File.Exists(filePath + ".heap");
        var metadataFileExists = File.Exists(filePath + ".metadata");

        if (heapFileExists && metadataFileExists)
        {
            _logger.Information("File exists on disk: {FileName}", fileName);
            return Task.FromResult(Result<bool, FileError>.Success(true));
        }

        _logger.Information("File does not exist on disk: {FileName}", fileName);
        return Task.FromResult(Result<bool, FileError>.Success(false));
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
        var filePath = Path.Combine(storagePath, fileName);
        var metadataFilePath = filePath + ".metadata";

        _logger.Debug("Creating heap metadata file in path: {MetadataFilePath}", metadataFilePath);


        try
        {
            var metadata = new HeapFileMetadata
            {
                FilePath = filePath + ".heap",
                LastPageId = 0,
                PageCount = 0,
                HeapSizeInBytes = (long)heapSizeInBytes,
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            };
            _metadataFiles.Add(fileName, metadata);

            var jsonMetadata =
                JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(metadataFilePath, jsonMetadata);

            _logger.Information("Heap Metadata file created successfully: {HeapFilePath}", metadataFilePath);
        }
        catch (Exception e)
        {
            _logger.Error("Failed to create heap file: {HeapFilePath}. Error: {Error}", metadataFilePath, e.Message);
            return Result<Unit, FileError>.Error(
                new FileError($"Failed to create heap file: {e.Message}"));
        }

        _logger.Debug("Heap file created successfully: {HeapFilePath}", metadataFilePath);
        return Result<Unit, FileError>.Success(Unit.Value);
    }

    private async Task<Result<Unit, FileError>> UnmarshalHeapMetadataFile(string fileName)
    {
        var heapFilePath = Path.Combine(storagePath, fileName) + ".metadata";
        _logger.Debug("Unmarshalling heap file metadata at {HeapFilePath}", heapFilePath);

        try
        {
            var jsonMetadata = await File.ReadAllTextAsync(heapFilePath);
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

    private Task<Result<Unit, FileError>> CreateHeapFile(string fileName)
    {
        var heapFilePath = Path.Combine(storagePath, fileName) + ".heap";
        _logger.Debug("Creating heap file in path: {HeapFilePath}", heapFilePath);

        try
        {
            var fs = new FileStream(
                heapFilePath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.ReadWrite);
            fs.SetLength((long)heapSizeInBytes);
            _files.Add(fileName, fs);
        }
        catch (Exception e)
        {
            _logger.Error("Failed to create heap file: {HeapFilePath}. Error: {Error}", heapFilePath, e.Message);
            return Task.FromResult(
                Result<Unit, FileError>.Error(
                    new FileError($"Failed to create heap file: {e.Message}")));
        }

        _logger.Information("Heap file created successfully: {HeapFilePath}", heapFilePath);
        return Task.FromResult(Result<Unit, FileError>.Success(Unit.Value));
    }

    private Result<Unit, FileError> OpenHeapFile(string fileName)
    {
        var heapFilePath = Path.Combine(storagePath, fileName) + ".heap";
        _logger.Debug("Opening heap file in path: {HeapFilePath}", heapFilePath);

        try
        {
            var fs = new FileStream(heapFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            _files.Add(fileName, fs);
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

    private Result<Unit, FileError> UpdateHeapMetadataFile(string fileName, HeapFileMetadata metadata)
    {
        var metadataFilePath = Path.Combine(storagePath, fileName) + ".metadata";
        try
        {
            metadata.LastModifiedAt = DateTime.UtcNow;
            var jsonMetadata =
                JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(metadataFilePath, jsonMetadata);

            // update the in-memory metadata
            _metadataFiles[fileName] = metadata;

            return Result<Unit, FileError>.Success(Unit.Value);
        }
        catch (Exception e)
        {
            _logger.Error("Failed to update metadata file: {MetadataFilePath}. Error: {Error}",
                fileName, e.Message);
            return Result<Unit, FileError>.Error(
                new FileError($"Failed to update metadata file: {e.Message}"));
        }
    }
}
