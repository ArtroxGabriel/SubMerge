using System.Diagnostics.CodeAnalysis;
using SubMerge.Parsers;

namespace SubMerge.Models;

public record struct GrapeRecord : IParsable<GrapeRecord>
{
    public GrapeRecord(int grapeId, string name, GrapeType type, int harvestYear, int countrySourceId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty", nameof(name));
        if (harvestYear <= 0)
            throw new ArgumentException("Harvest year must be positive", nameof(harvestYear));

        GrapeID = grapeId;
        Name = name;
        Type = type;
        HarvestYear = harvestYear;
        CountrySourceId = countrySourceId;
    }

    public int GrapeID { get; init; }
    public string Name { get; init; }
    public GrapeType Type { get; init; }
    public int HarvestYear { get; init; }
    public int CountrySourceId { get; init; }

    public static GrapeRecord Parse(string s, IFormatProvider? provider)
    {
        if (string.IsNullOrWhiteSpace(s)) throw new ArgumentException("Input cannot be null or empty", nameof(s));

        var parts = CsvParser.SplitAndValidateCsv(s, 5).Select(p => p.Trim()).ToArray();

        return new GrapeRecord(
            int.Parse(parts[0]),
            parts[1],
            Enum.Parse<GrapeType>(parts[2], true),
            int.Parse(parts[3]),
            int.Parse(parts[4])
        );
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider,
        [MaybeNullWhen(false)] out GrapeRecord result)
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

public enum GrapeType
{
    Red,
    White,
    Rose
}
