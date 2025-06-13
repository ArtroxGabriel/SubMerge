using CsvHelper.Configuration.Attributes;

namespace MicroMerge.Models.Record;

public class WineRecord
{
    [Name("vinho_id")] public int VinhoId { get; set; }

    [Name("rotulo")] public string Rotulo { get; set; }

    [Name("ano_producao")] public int AnoProducao { get; set; }

    [Name("uva_id")] public int UvaId { get; set; }

    [Name("pais_producao_id")] public int PaisProducaoId { get; set; }

    public Record ToRecord()
    {
        return new Record
        {
            Columns =
            [
                VinhoId.ToString(),
                Rotulo,
                AnoProducao.ToString(),
                UvaId.ToString(),
                PaisProducaoId.ToString()
            ]
        };
    }
}
