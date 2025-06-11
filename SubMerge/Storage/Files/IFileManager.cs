using SubMerge.Models;

namespace SubMerge.Storage.Files;

/// <summary>
///     Manages file operations for heap files.
/// </summary>
public interface IFileManager
{
    /// <summary>
    ///     Creates a new file with the specified name.
    /// </summary>
    /// <param name="fileName">The name of the file to create.</param>
    /// <returns>A result containing a tuple with the file metadata and the open file stream, or a file error.</returns>
    Task<Result<Tuple<HeapFileMetadata, FileStream>, FileError>> CreateFileAsync(string fileName);

    /// <summary>
    ///     Deletes a file with the specified name.
    /// </summary>
    /// <param name="fileName">The name of the file to delete.</param>
    /// <returns>A result indicating success or a file error.</returns>
    Task<Result<Unit, FileError>> DeleteFileAsync(string fileName);

    /// <summary>
    ///     Opens an existing file with the specified name.
    /// </summary>
    /// <param name="fileName">The name of the file to open.</param>
    /// <returns>A result containing a tuple with the file metadata and the open file stream, or a file error.</returns>
    Task<Result<Tuple<HeapFileMetadata, FileStream>, FileError>> OpenFileAsync(string fileName);

    /// <summary>
    ///     Closes an open file with the specified name.
    /// </summary>
    /// <param name="fileName">The name of the file to close.</param>
    /// <returns>A result indicating success or a file error.</returns>
    Task<Result<Unit, FileError>> CloseFileAsync(string fileName);

    Task<Result<bool, FileError>> FileExistsAsync(string fileName);
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
    public required string FilePath { get; set; }
    public ulong LastPageId { get; set; }
    public ulong PageCount { get; set; }
    public long HeapSizeInBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastModifiedAt { get; set; }
}
