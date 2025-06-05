using System.Diagnostics.CodeAnalysis;
using SubMerge.Parsers;

namespace SubMerge.Models;

public record struct WineRecord : IParsable<WineRecord>
{
    public WineRecord(int wineId, string label, int productionYear, int grapeId, int countryProductionId)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label cannot be empty", nameof(label));
        if (productionYear <= 0)
            throw new ArgumentException("Production year must be positive", nameof(productionYear));

        WineId = wineId;
        Label = label;
        ProductionYear = productionYear;
        GrapeId = grapeId;
        CountryProductionId = countryProductionId;
    }

    public int WineId { get; init; }
    public string Label { get; init; }
    public int ProductionYear { get; init; }
    public int GrapeId { get; init; }
    public int CountryProductionId { get; init; }

    public static WineRecord Parse(string s, IFormatProvider? provider)
    {
        if (string.IsNullOrWhiteSpace(s)) throw new ArgumentException("Input cannot be null or empty", nameof(s));

        var parts = CsvParser.SplitAndValidateCsv(s, 5).Select(p => p.Trim()).ToArray();

        return new WineRecord(
            int.Parse(parts[0]),
            parts[1],
            int.Parse(parts[2]),
            int.Parse(parts[3]),
            int.Parse(parts[4])
        );
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out WineRecord result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(s)) return false;

        try
        {
            result = Parse(s, provider);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
