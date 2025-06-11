using System.Reflection;
using SubMerge.Storage.Files;
using Assert = Xunit.Assert;

namespace SubMerge.Tests.Unit.Storage;

public class SequentialHeapFileManagerTests
{
    [Fact]
    public async Task CreateFileAsync_CreatesNewFileAndMetadata_Success()
    {
        var manager = new SequentialHeapFileManager("./testheaps", 1024 * 1024);
        var result = await manager.CreateFileAsync("file1");
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.GetValueOrThrow().Item1);
        Assert.NotNull(result.GetValueOrThrow().Item2);
        await manager.DeleteFileAsync("file1");
    }

    [Fact]
    public async Task CreateFileAsync_WhenFileAlreadyExists_ReturnsExistingFile()
    {
        var manager = new SequentialHeapFileManager("./testheaps", 1024 * 1024);
        await manager.CreateFileAsync("file2");
        var result = await manager.CreateFileAsync("file2");
        Assert.True(result.IsSuccess);
        await manager.DeleteFileAsync("file2");
    }

    [Fact]
    public async Task DeleteFileAsync_RemovesFileAndMetadata_Success()
    {
        var manager = new SequentialHeapFileManager("./testheaps", 1024 * 1024);
        await manager.CreateFileAsync("file3");
        var result = await manager.DeleteFileAsync("file3");
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DeleteFileAsync_WhenFileDoesNotExist_ReturnsSuccess()
    {
        var manager = new SequentialHeapFileManager("./testheaps", 1024 * 1024);
        var result = await manager.DeleteFileAsync("nonexistent");
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task OpenFileAsync_OpensExistingFile_Success()
    {
        var manager = new SequentialHeapFileManager("./testheaps", 1024 * 1024);
        await manager.CreateFileAsync("file4");
        var result = await manager.OpenFileAsync("file4");
        Assert.True(result.IsSuccess);
        await manager.DeleteFileAsync("file4");
    }

    [Fact]
    public async Task OpenFileAsync_WhenFileDoesNotExist_ReturnsError()
    {
        var manager = new SequentialHeapFileManager("./testheaps", 1024 * 1024);
        var result = await manager.OpenFileAsync("missingfile");
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task CloseFileAsync_ClosesOpenFile_Success()
    {
        var manager = new SequentialHeapFileManager("./testheaps", 1024 * 1024);
        await manager.CreateFileAsync("file5");
        var result = await manager.CloseFileAsync("file5");
        Assert.True(result.IsSuccess);
        await manager.DeleteFileAsync("file5");
    }

    [Fact]
    public async Task CloseFileAsync_WhenFileNotOpen_ReturnsSuccess()
    {
        var manager = new SequentialHeapFileManager("./testheaps", 1024 * 1024);
        var result = await manager.CloseFileAsync("notopen");
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task FileExistsAsync_WhenFileExists_ReturnsTrue()
    {
        var manager = new SequentialHeapFileManager("./testheaps", 1024 * 1024);
        await manager.CreateFileAsync("file6");
        var result = await manager.FileExistsAsync("file6");
        Assert.True(result.IsSuccess);
        Assert.True(result.GetValueOrThrow());
        await manager.DeleteFileAsync("file6");
    }

    [Fact]
    public async Task FileExistsAsync_WhenFileDoesNotExist_ReturnsFalse()
    {
        var manager = new SequentialHeapFileManager("./testheaps", 1024 * 1024);
        var result = await manager.FileExistsAsync("doesnotexist");
        Assert.True(result.IsSuccess);
        Assert.False(result.GetValueOrThrow());
    }

    [Fact]
    public void CreateDirectory_WhenDirectoryDoesNotExist_CreatesSuccessfully()
    {
        var testDir = "./testheaps_createdir";
        if (Directory.Exists(testDir))
            Directory.Delete(testDir, true);

        var manager = new SequentialHeapFileManager(testDir, 1024 * 1024);
        var result = typeof(SequentialHeapFileManager)
            .GetMethod("CreateDirectory", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(manager, [testDir]);

        Assert.NotNull(result);
        Assert.True((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!);
        Assert.True(Directory.Exists(testDir));
        Directory.Delete(testDir, true);
    }

    [Fact]
    public void CreateDirectory_WhenDirectoryAlreadyExists_ReturnsSuccess()
    {
        var testDir = "./testheaps_createdir2";
        Directory.CreateDirectory(testDir);

        var manager = new SequentialHeapFileManager(testDir, 1024 * 1024);
        var result = typeof(SequentialHeapFileManager)
            .GetMethod("CreateDirectory", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(manager, new object[] { testDir });

        Assert.NotNull(result);
        Assert.True((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!);
        Directory.Delete(testDir, true);
    }

    [Fact]
    public void UpdateHeapMetadataFile_WithValidMetadata_UpdatesFile()
    {
        var testDir = "./testheaps_updatemeta";
        Directory.CreateDirectory(testDir);
        var manager = new SequentialHeapFileManager(testDir, 1024 * 1024);

        var fileName = "meta1";
        var metadata = new HeapFileMetadata
        {
            FilePath = Path.Combine(testDir, fileName) + ".heap",
            LastPageId = 1,
            PageCount = 1,
            HeapSizeInBytes = 1024 * 1024,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };

        var metadataFilePath = Path.Combine(testDir, fileName) + ".metadata";
        File.WriteAllText(metadataFilePath, "{}"); // create file

        var result = typeof(SequentialHeapFileManager)
            .GetMethod("UpdateHeapMetadataFile", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(manager, new object[] { fileName, metadata });

        Assert.NotNull(result);
        Assert.True((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!);
        Directory.Delete(testDir, true);
    }

    [Fact]
    public void OpenHeapFile_WhenFileExists_OpensSuccessfully()
    {
        var testDir = "./testheaps_openheap";
        Directory.CreateDirectory(testDir);
        var manager = new SequentialHeapFileManager(testDir, 1024 * 1024);

        var fileName = "heap1";
        var heapFilePath = Path.Combine(testDir, fileName) + ".heap";
        using (var fs = new FileStream(heapFilePath, FileMode.Create, FileAccess.ReadWrite)) { }

        var result = typeof(SequentialHeapFileManager)
            .GetMethod("OpenHeapFile", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(manager, new object[] { fileName });

        Assert.NotNull(result);
        Assert.True((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!);
        Directory.Delete(testDir, true);
    }

    [Fact]
    public void OpenHeapFile_WhenFileDoesNotExist_ReturnsError()
    {
        var testDir = "./testheaps_openheap2";
        Directory.CreateDirectory(testDir);
        var manager = new SequentialHeapFileManager(testDir, 1024 * 1024);

        var fileName = "heap2";
        var result = typeof(SequentialHeapFileManager)
            .GetMethod("OpenHeapFile", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(manager, new object[] { fileName });

        Assert.NotNull(result);
        Assert.True((bool)result.GetType().GetProperty("IsError")!.GetValue(result)!);
        Directory.Delete(testDir, true);
    }
}
