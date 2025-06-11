using System.Globalization;
using CsvHelper;

namespace MicroMerge;

public class WineTable : Table
{
    public WineTable(string csvFilePath)
    {
        using var reader = new StreamReader(csvFilePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        var records = csv.GetRecords<WineRecord>();

        var recordOnPage = 0;
        Name = "wines";
        var page = new Page(new PageId(Name, PageAmount ),[]);

        foreach (var wineRecord in records)
        {
            if (recordOnPage != 10)
            {
                recordOnPage++;
                page.Records.Add(wineRecord.ToRecord());
            }
            else
            {
                Pages.Add(page);
                page = new Page(new PageId(Name, PageAmount ),[]);
                recordOnPage = 0;
            }
        }

        if (page.Records.Count > 0)
        {
            Pages.Add(page);
        }

        Columns = new List<string>(
            new[] { "vinho_id", "rotulo", "ano_producao", "uva_id", "pais_producao_id" }
        );
    }
}
