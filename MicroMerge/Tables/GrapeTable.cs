using System.Globalization;
using CsvHelper;
using MicroMerge.Models.Record;

namespace MicroMerge.Tables;

public class GrapeTable : Table
{
    public GrapeTable(string csvFilePath)
    {
        using var reader = new StreamReader(csvFilePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        var records = csv.GetRecords<GrapeRecord>();

        var recordOnPage = 0;
        Name = "grapes";
        var page = new Page(new PageId(Name, PageAmount), []);

        foreach (var grapeRecord in records)
            if (recordOnPage != 10)
            {
                recordOnPage++;
                page.Records.Add(grapeRecord.ToRecord());
            }
            else
            {
                Pages.Add(page);
                page = new Page(new PageId(Name, PageAmount), []);
                recordOnPage = 0;
            }

        if (page.Records.Count > 0) Pages.Add(page);

        Columns = new List<string>(
            new[] { "uva_id", "nome", "tipo", "ano_colheita", "pais_origem_id" }
        );
    }
}
