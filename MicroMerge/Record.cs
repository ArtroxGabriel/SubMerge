namespace MicroMerge;

public class Record
{
    public List<string> Columns { get; set; } = [];
    public List<string> ColumnNames { get; set; } = [];

    public string this[int index]
    {
        get => Columns[index];
        set
        {
            if (index < 0 || index >= Columns.Count)
                throw new IndexOutOfRangeException($"Index {index} is out of range for record with {Columns.Count} columns.");
            Columns[index] = value;
        }
    }

    public string this[string columnName]
    {
        get
        {
            int index = ColumnNames.IndexOf(columnName);
            if (index == -1)
                throw new KeyNotFoundException($"Column '{columnName}' not found in record.");
            return Columns[index];
        }
        set
        {
            int index = ColumnNames.IndexOf(columnName);
            if (index == -1)
                throw new KeyNotFoundException($"Column '{columnName}' not found in record.");
            Columns[index] = value;
        }
    }
}
