namespace SubMerge.Models;

public class Tuple
{
    public Tuple(string[] cols)
    {
        Columns = cols;
    }

    public required string[] Columns { get; set; }

    public override string ToString()
    {
        return string.Join("$", Columns);
    }
}
