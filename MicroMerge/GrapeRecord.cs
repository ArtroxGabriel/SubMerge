using CsvHelper.Configuration.Attributes;

namespace MicroMerge;

public class GrapeRecord
{
    [Name("uva_id")]
    public int UvaId { get; set; }
    [Name("nome")]
    public string Nome { get; set; } = string.Empty;
    [Name("tipo")]
    public string Tipo { get; set; } = string.Empty;
    [Name("ano_colheita")]
    public int AnoColheita { get; set; }
    [Name("pais_origem_id")]
    public int PaisOrigemId { get; set; }

    public Record ToRecord()
    {
        return new Record
        {
            Columns = new List<string>
            {
                UvaId.ToString(),
                Nome,
                Tipo,
                AnoColheita.ToString(),
                PaisOrigemId.ToString()
            }
        };
    }
}
