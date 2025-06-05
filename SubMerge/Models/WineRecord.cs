using System.Diagnostics.CodeAnalysis;

namespace SubMerge.Models;

public record struct WineRecord : IParsable<WineRecord>
{
    public WineRecord(int wineId, string label, int productionYear, int grapeId, int countryProductionId)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label cannot be empty", nameof(label));
        if (productionYear <= 0)
            throw new ArgumentException("Harvest year must be positive", nameof(productionYear));

        WineId = wineId;
        Label = label;
        ProductionYear = productionYear;
        GrapeId = grapeId;
        CountryProductionId = countryProductionId;
    }

    public int WineId { get; set; }
    public string Label { get; set; }
    public int ProductionYear { get; set; }
    public int GrapeId { get; set; }
    public int CountryProductionId { get; set; }

    public static WineRecord Parse(string s, IFormatProvider? provider)
    {
        if (string.IsNullOrWhiteSpace(s)) throw new ArgumentException("Input cannot be null or empty", nameof(s));

        var parts = s.Split(',');
        if (parts.Length != 5)
            throw new FormatException("Input string must contain exactly 5 comma-separated values.");

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
