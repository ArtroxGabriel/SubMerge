using System.Collections;
using CsvHelper;
using System.Globalization;

namespace MicroMerge;

public class SortedTable: IDisposable
{
    private readonly string _filePath;

    public SortedTable(Table originalTable, string sortColumn)
    {
        var tableName = originalTable.Name ?? throw new ArgumentNullException(nameof(originalTable));
        var sortColumnName = sortColumn ?? throw new ArgumentNullException(nameof(sortColumn));
        Name = $"{tableName}_sorted_{sortColumnName}";
        _filePath = Path.Combine(Path.GetTempPath(), $"{Name}.dat");
        Columns = new List<string>(originalTable.Columns);
    }

    public SortedTable(string originalTableName, List<string> columns, string sortColumn)
    {
        var sortColumnName = sortColumn ?? throw new ArgumentNullException(nameof(sortColumn));
        Name = $"{originalTableName}_sorted_{sortColumnName}";
        _filePath = Path.Combine(Path.GetTempPath(), $"{Name}.dat");
        Columns = new List<string>(columns);
    }

    public string Name { get; }
    public List<string> Columns { get; }
    public int PageAmount { get; private set; }
    public int AmountOfColumns => Columns.Count;

    public IEnumerable<Page> GetPagesIterable()
    {
        if (!File.Exists(_filePath))
            yield break;

        using var fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fileStream);

        int pageIndex = 0;

        try
        {
            while (fileStream.Position < fileStream.Length)
            {
                var records = new List<Record>();

                // Read up to 10 records for this page
                for (int i = 0; i < 10 && fileStream.Position < fileStream.Length; i++)
                {
                    var record = ReadRecord(reader);
                    record.ColumnNames = Columns;
                    records.Add(record);
                }

                if (records.Count > 0)
                {
                    var pageId = new PageId(Name, pageIndex++);
                    yield return new Page(pageId, records.ToArray());
                }
            }
        }
        finally
        {
            reader?.Close();
            fileStream?.Close();
        }
    }

    internal void WriteToFile(IEnumerable<Record> sortedRecords)
    {
        using var fileStream = new FileStream(_filePath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fileStream);

        int recordCount = 0;
        foreach (var record in sortedRecords)
        {
            WriteRecord(writer, record);
            recordCount++;
        }

        PageAmount = (int)Math.Ceiling(recordCount / 10.0);
    }

    internal void WriteToFile(Page page)
    {
        using var fileStream = new FileStream(_filePath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fileStream);

        int recordCount = 0;
        foreach (var record in page.Records)
        {
            WriteRecord(writer, record);
            recordCount++;
        }

        PageAmount = (int)Math.Ceiling(recordCount / 10.0);
    }

    internal void DeleteFile()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }

    private Record ReadRecord(BinaryReader reader)
    {
        var record = new Record();
        var columnCount = reader.ReadInt32();

        for (int i = 0; i < columnCount; i++)
        {
            var value = reader.ReadString();
            record.Columns.Add(value);
        }

        return record;
    }

    private void WriteRecord(BinaryWriter writer, Record record)
    {
        writer.Write(record.Columns.Count);
        foreach (var value in record.Columns)
        {
            writer.Write(value ?? string.Empty);
        }
    }

    public void WriteToCsv(string csvFilePath)
    {
        using var writer = new StringWriter();
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        // Write the header (column names)
        foreach (var column in Columns)
        {
            csv.WriteField(column);
        }
        csv.NextRecord();

        // Read all pages and write their records to CSV
        foreach (var page in GetPagesIterable())
        {
            foreach (var record in page.Records)
            {
                // Write each column value for the record
                foreach (var columnValue in record.Columns)
                {
                    csv.WriteField(columnValue);
                }
                csv.NextRecord();
            }
        }

        // Write the CSV content to file

        File.WriteAllText(csvFilePath, writer.ToString());
    }

    public void Cleanup()
    {
        // Delete the temporary file if it exists
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }
}
