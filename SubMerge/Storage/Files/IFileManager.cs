using SubMerge.Models;
using Tuple = System.Tuple;

namespace SubMerge.Storage.Files;

public interface IFileManager
{
    Task<Result<Tuple<HeapFileMetadata, FileStream>, FileError>> CreateFileAsync(string fileName);
    Task<Result<Unit, FileError>> DeleteFileAsync(string fileName);
    Task<Result<Tuple<HeapFileMetadata, FileStream>, FileError>> OpenFileAsync(string fileName);
    Task<Result<Unit, FileError>> CloseFileAsync(string fileName);
}

public readonly struct FileError
{
    public string Message { get; }

    public FileError(string message)
    {
        Message = message;
    }
}

public class HeapFileMetadata
{
    public string FilePath { get; set; }
    public ulong LastPageId { get; set; }
    public ulong PageCount { get; set; }
    public long HeapSizeInBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastModifiedAt { get; set; }
}

