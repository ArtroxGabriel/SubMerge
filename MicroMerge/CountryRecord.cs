using CsvHelper.Configuration.Attributes;

namespace MicroMerge;

public class CountryRecord
{
    [Name("pais_id")]
    public int PaisId { get; set; }
    [Name("nome")]
    public string Name { get; set; }
    [Name("sigla")]
    public string Sigla { get; set; }

    public Record ToRecord()
    {
        return new Record
        {
            Columns = new List<string>
            {
                PaisId.ToString(),
                Name,
                Sigla
            }
        };
    }

}
