namespace MicroMerge.Tables;

public class Table
{
    public string Name { get; set; } = string.Empty;
    public List<Page> Pages { get; set; } = new();
    public int PageAmount => Pages.Count;
    public int AmountOfColumns => Columns.Count;
    public List<string> Columns { get; set; } = new();
}
