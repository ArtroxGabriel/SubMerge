using System.Reflection;
using Moq;
using SubMerge.Models;
using SubMerge.Storage.Files;
using SubMerge.Storage.Page;
using Assert = Xunit.Assert;
using Tuple = SubMerge.Models.Tuple;

namespace SubMerge.Tests.Unit.Storage;

public class SimplePageManagerTests
{

[Fact]
public async Task ReadPageAsync_ReturnsPage_WhenFileAndStreamAreValid()
{
    var fileManager = new Mock<IFileManager>();
    var fileName = "valid_file";
    var pageId = new PageId(0, fileName);
    var page = new Page(pageId, []);
    var stream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite) ;
    fileManager.Setup(fm => fm.OpenFileAsync(fileName))
        .ReturnsAsync(Result<Tuple<HeapFileMetadata, FileStream>, FileError>.Success(
            new Tuple<HeapFileMetadata, FileStream>(new HeapFileMetadata { FilePath = "valid_file_write" }, stream))
        );

    var manager = new SimplePageManager(fileManager.Object);

    // Simulate writing a page to the stream for reading
    stream.Position = 0;

    var result = await manager.ReadPageAsync(pageId);

    Assert.True(result.IsSuccess);
    Assert.NotNull(result.GetValueOrThrow());
}

[Fact]
public async Task WritePageAsync_WritesSuccessfully_WhenFileAndStreamAreValid()
{
    var fileManager = new Mock<IFileManager>();
    var pageId = new PageId(0, "valid_file_write");
    var page = new Page(pageId, []);
    var stream = new FileStream("valid_file", FileMode.OpenOrCreate, FileAccess.ReadWrite) ;
    fileManager.Setup(fm => fm.OpenFileAsync("valid_file_write"))
        .ReturnsAsync(Result<Tuple<HeapFileMetadata, FileStream>, FileError>.Success(
            new Tuple<HeapFileMetadata, FileStream>(new HeapFileMetadata { FilePath = "valid_file_write" }, stream))
        );

    var manager = new SimplePageManager(fileManager.Object);

    var result = await manager.WritePageAsync(pageId, page);

    Assert.True(result.IsSuccess);
}

[Fact]
public async Task AllocateNewPageAsync_AllocatesPage_WhenFileAndStreamAreValid()
{
    var fileManager = new Mock<IFileManager>();
    var fileName = "valid_file_alloc";
    var stream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite) ;
    fileManager.Setup(fm => fm.OpenFileAsync(fileName))
        .ReturnsAsync(Result<Tuple<HeapFileMetadata, FileStream>, FileError>.Success(
            new Tuple<HeapFileMetadata, FileStream>(new HeapFileMetadata { FilePath = "valid_file_alloc_write" }, stream))
        );

    var manager = new SimplePageManager(fileManager.Object);

    var result = await manager.AllocateNewPageAsync(fileName);

    Assert.True(result.IsSuccess);
    Assert.NotNull(result.GetValueOrThrow());
}

[Fact]
public async Task HasEnoughSpaceToInsertTuple_ReturnsTrue_WhenEnoughSpace()
{
    var fileManager = new Mock<IFileManager>();
    var pageId = new PageId(0, "enough_space_file");
    var page = new Page(pageId, []);
    var tuple = new Tuple([]);
    var manager = new SimplePageManager(fileManager.Object);

    var result = await manager.HasEnoughSpaceToInsertTuple(page, tuple);

    Assert.True(result.IsSuccess);
    Assert.True(result.GetValueOrThrow());
}

[Fact]
public async Task PageExistsAsync_ReturnsTrue_WhenFileExists()
{
    var fileManager = new Mock<IFileManager>();
    var pageId = new PageId(0, "existing_file");
    fileManager.Setup(fm => fm.FileExistsAsync("existing_file"))
        .ReturnsAsync(Result<bool, FileError>.Success(true));

    var manager = new SimplePageManager(fileManager.Object);

    var result = await manager.PageExistsAsync(pageId);

    Assert.True(result.IsSuccess);
    Assert.True(result.GetValueOrThrow());
}

[Fact]
public async Task PageExistsAsync_ReturnsFalse_WhenFileDoesNotExist()
{
    var fileManager = new Mock<IFileManager>();
    var pageId = new PageId(0, "nonexistent_file");
    fileManager.Setup(fm => fm.FileExistsAsync("nonexistent_file"))
        .ReturnsAsync(Result<bool, FileError>.Success(false));

    var manager = new SimplePageManager(fileManager.Object);

    var result = await manager.PageExistsAsync(pageId);

    Assert.True(result.IsSuccess);
    Assert.False(result.GetValueOrThrow());
}}
