using MicroMerge.Models.Record;

namespace MicroMerge;

public class Page
{
    public Page(PageId id, Record[] records)
    {
        if (records == null || records.Length > 10)
            throw new ArgumentException("Page can only contain up to 10 records.");

        Records = new List<Record>(records);
        Id = id;
    }

    public PageId Id { get; set; }
    public List<Record> Records { get; set; }
    public int RecordAmount => Records.Count;
}
