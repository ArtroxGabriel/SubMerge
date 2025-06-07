using FluentAssertions;
using Serilog;
using SubMerge.Models;
using SubMerge.Storage;

namespace SubMerge.Tests.Integration.Storage;

public sealed class SequentialHeapFileManagerIntegrationTests : IDisposable
{
    private readonly string _tempBasePath;

    public SequentialHeapFileManagerIntegrationTests()
    {
        // Setup logging for tests
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        // Create base temporary directory for all tests
        _tempBasePath = Path.Combine(Path.GetTempPath(), "SubMergeTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempBasePath);
    }

    public void Dispose()
    {
        // Clean up temp directories after tests
        if (!Directory.Exists(_tempBasePath)) return;
        try
        {
            Directory.Delete(_tempBasePath, true);
        }
        catch (Exception ex)
        {
            Log.Error("Warning: Failed to clean up temp directory: {ExMessage}", ex.Message);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private string CreateTempDirectory()
    {
        var tempDir = Path.Combine(_tempBasePath, Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    [Fact]
    public async Task InitializeAsync_CreatesAllRequiredFiles()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var fileManager = new SequentialHeapFileManager(tempDir, 1024 * 10, 512);

        // Act
        var result = await fileManager.InitializeAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(File.Exists(Path.Combine(tempDir, "heap_metadata.ygg")));
        Assert.True(File.Exists(Path.Combine(tempDir, "heap.ygg")));

        var firstPageResult = await fileManager.ReadPageAsync(1L);
        Assert.True(firstPageResult.IsSuccess);
        firstPageResult.GetValueOrThrow().PageId.Should().Be(1);
    }

    [Theory]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(4096)]
    public async Task AllocateNewPage_CreatesNewPageWithCorrectId(ulong pageSize)
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var fileManager = new SequentialHeapFileManager(tempDir, 1024 * 10, pageSize);
        await fileManager.InitializeAsync();

        // Act
        var result1 = await fileManager.AllocateNewPageAsync();
        var result2 = await fileManager.AllocateNewPageAsync();
        var result3 = await fileManager.AllocateNewPageAsync();

        // Assert
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.True(result3.IsSuccess);
        Assert.Equal(2UL, result1.GetValueOrThrow().PageId);
        Assert.Equal(3UL, result2.GetValueOrThrow().PageId);
        Assert.Equal(4UL, result3.GetValueOrThrow().PageId);
    }

    [Theory]
    [InlineData(512)]
    [InlineData(2048)]
    public async Task WritePageAsync_WritesPageCorrectly(ulong pageSize)
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var fileManager = new SequentialHeapFileManager(tempDir, 1024 * 10, pageSize);
        await fileManager.InitializeAsync();

        var allocateResult = await fileManager.AllocateNewPageAsync();
        var page = allocateResult.GetValueOrThrow();

        // Add content to the page
        var wineRecords = new[]
        {
            new WineRecord(1, "Cabernet Sauvignon", 2018, 1, 1), new WineRecord(2, "Merlot", 2019, 2, 2)
        };
        page.Content = wineRecords;

        // Act
        var writeResult = await fileManager.WritePageAsync(page);
        await fileManager.FlushAsync();

        // Read the page back
        var readResult = await fileManager.ReadPageAsync(page.PageId);

        // Assert
        Assert.True(writeResult.IsSuccess);
        Assert.True(readResult.IsSuccess);

        var readPage = readResult.GetValueOrThrow();
        Assert.Equal(page.PageId, readPage.PageId);
        Assert.Equal(2, readPage.Content.Length);
        Assert.Equal("Cabernet Sauvignon", readPage.Content[0].Label);
        Assert.Equal("Merlot", readPage.Content[1].Label);
    }

    [Fact]
    public async Task PageExistsAsync_ReturnsCorrectResult()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var fileManager = new SequentialHeapFileManager(tempDir, 1024 * 10, 1024);
        await fileManager.InitializeAsync();

        // Allocate a new page
        var allocateResult = await fileManager.AllocateNewPageAsync();
        var page = allocateResult.GetValueOrThrow();

        // Act
        var existsResult1 = await fileManager.PageExistsAsync(page.PageId);
        var existsResult2 = await fileManager.PageExistsAsync(9999); // Non-existent page

        // Assert
        Assert.True(existsResult1.IsSuccess);
        Assert.True(existsResult1.GetValueOrThrow());

        Assert.True(existsResult2.IsSuccess);
        Assert.False(existsResult2.GetValueOrThrow());
    }

    [Fact]
    public async Task ReadPageAsync_ReturnsErrorForNonExistentPage()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var fileManager = new SequentialHeapFileManager(tempDir, 1024 * 10, 1024);
        await fileManager.InitializeAsync();

        // Act
        var result = await fileManager.ReadPageAsync(9999);

        // Assert
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task WritePageAsync_ReturnsErrorForNonAllocatedPage()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var fileManager = new SequentialHeapFileManager(tempDir, 1024 * 10, 1024);
        await fileManager.InitializeAsync();

        var page = new Page(9999, []);

        // Act
        var result = await fileManager.WritePageAsync(page);

        // Assert
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task FlushAsync_EnsuresDataIsPersisted()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var fileManager = new SequentialHeapFileManager(tempDir, 1024 * 10, 1024);
        await fileManager.InitializeAsync();

        // Create a page with content
        var allocateResult = await fileManager.AllocateNewPageAsync();
        var page = allocateResult.GetValueOrThrow();
        page.Content = [new WineRecord(1, "Test Wine", 2020, 1, 1)];

        // Write the page
        await fileManager.WritePageAsync(page);

        // Act
        var flushResult = await fileManager.FlushAsync();

        // Create a new file manager instance to verify data was persisted
        var newFileManager = new SequentialHeapFileManager(tempDir, 1024 * 10, 1024);
        await newFileManager.InitializeAsync();
        var readResult = await newFileManager.ReadPageAsync(page.PageId);

        // Assert
        Assert.True(flushResult.IsSuccess);
        Assert.True(readResult.IsSuccess);
        Assert.Equal("Test Wine", readResult.GetValueOrThrow().Content[0].Label);
    }

    [Fact]
    public async Task WritePageAsync_HandlesLargeContent()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var fileManager = new SequentialHeapFileManager(tempDir, 1024 * 10, 2048);
        await fileManager.InitializeAsync();

        var allocateResult = await fileManager.AllocateNewPageAsync();
        var page = allocateResult.GetValueOrThrow();

        // Create a large collection of wine records
        var wineRecords = new List<WineRecord>();
        for (var i = 0; i < 20; i++)
            wineRecords.Add(new WineRecord(i, $"Wine {i}", 2000 + i, i, i));

        page.Content = wineRecords.ToArray();

        // Act
        var writeResult = await fileManager.WritePageAsync(page);
        await fileManager.FlushAsync();
        var readResult = await fileManager.ReadPageAsync(page.PageId);

        // Assert
        Assert.True(writeResult.IsSuccess);
        Assert.True(readResult.IsSuccess);
        Assert.Equal(20, readResult.GetValueOrThrow().Content.Length);
    }

    [Fact]
    public async Task InitializeAsync_LoadsExistingData()
    {
        // Arrange
        var tempDir = CreateTempDirectory();

        // First file manager creates and populates the heap
        var fileManager1 = new SequentialHeapFileManager(tempDir, 1024 * 10, 1024);
        await fileManager1.InitializeAsync();

        // Allocate some pages
        await fileManager1.AllocateNewPageAsync();
        await fileManager1.AllocateNewPageAsync();
        await fileManager1.AllocateNewPageAsync();
        await fileManager1.FlushAsync();

        // Act
        // Create a second file manager that should load existing data
        var fileManager2 = new SequentialHeapFileManager(tempDir, 1024 * 10, 1024);
        await fileManager2.InitializeAsync();

        // Assert - we should be able to access the previously created pages
        var existsResult = await fileManager2.PageExistsAsync(3);
        Assert.True(existsResult.IsSuccess);
        Assert.True(existsResult.GetValueOrThrow());
    }

    [Fact]
    public async Task PageHasEnoughSpaceToInsertRecord_ReturnsTrueWhenEnoughSpace()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var pageSize = 1024UL;
        var fileManager = new SequentialHeapFileManager(tempDir, 1024 * 10, pageSize);
        await fileManager.InitializeAsync();

        var allocateResult = await fileManager.AllocateNewPageAsync();
        var page = allocateResult.GetValueOrThrow();

        // Add a few records to the page, but not too many
        page.Content =
        [
            new WineRecord(1, "Cabernet Sauvignon", 2018, 1, 1), new WineRecord(2, "Merlot", 2019, 1, 1)
        ];

        // Create a new record to insert
        var newRecord = new WineRecord(3, "Chardonnay", 2020, 1, 1);

        // Act
        var result = await fileManager.PageHasEnoughSpaceToInsertRecord(page, newRecord);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.GetValueOrThrow(), "Page should have enough space for a new record");
    }

    [Fact]
    public async Task PageHasEnoughSpaceToInsertRecord_ReturnsFalseWhenNotEnoughSpace()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var pageSize = 512UL; // Using a small page size to ensure we run out of space quickly
        var fileManager = new SequentialHeapFileManager(tempDir, 1024 * 10, pageSize);
        await fileManager.InitializeAsync();

        var allocateResult = await fileManager.AllocateNewPageAsync();
        var page = allocateResult.GetValueOrThrow();

        // Fill the page with many records to consume space
        var wineRecords = new List<WineRecord>();
        for (var i = 0; i < 20; i++)
            wineRecords.Add(
                new WineRecord(
                    i,
                    $"Wine with a very long name to consume space {i}",
                    2000 + i,
                    i, i
                )
            );
        page.Content = wineRecords.ToArray();

        // Create a new record to insert (also with a long name)
        var newRecord = new WineRecord(
            100,
            "This is a very long wine name that will help exceed the page size limit when added to existing records",
            2021,
            100, 100
        );

        // Act
        var result = await fileManager.PageHasEnoughSpaceToInsertRecord(page, newRecord);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(
            result.GetValueOrThrow(),
            "Page should not have enough space for a new record"
        );
    }

    [Fact]
    public async Task PageHasEnoughSpaceToInsertRecord_WorksWithEmptyPage()
    {
        // Arrange
        var tempDir = CreateTempDirectory();
        var pageSize = 512UL;
        var fileManager = new SequentialHeapFileManager(tempDir, 1024 * 10, pageSize);
        await fileManager.InitializeAsync();

        var allocateResult = await fileManager.AllocateNewPageAsync();
        var page = allocateResult.GetValueOrThrow();

        // Page is empty (has no records)
        page.Content = [];

        // Create a new record to insert
        var newRecord = new WineRecord(1, "First Wine", 2020, 1, 1);

        // Act
        var result = await fileManager.PageHasEnoughSpaceToInsertRecord(page, newRecord);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(
            result.GetValueOrThrow(),
            "Empty page should have enough space for a new record"
        );
    }
}
