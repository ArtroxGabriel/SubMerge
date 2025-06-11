namespace MicroMerge;

public class PageId
{
    public PageId(string tableName, int number)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));
        }

        TableName = tableName;
        Number = number;
    }
    public string TableName { get; set; }
    public int Number { get; set; }
}
