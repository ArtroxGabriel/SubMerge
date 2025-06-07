using System.Diagnostics.CodeAnalysis;
using SubMerge.Parsers;

namespace SubMerge.Models;

public record struct CountryRecord : IParsable<CountryRecord>
{
    public CountryRecord(int countryId, string name, string code)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty", nameof(name));
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code cannot be empty", nameof(code));

        CountryId = countryId;
        Name = name;
        Code = code;
    }

    public int CountryId { get; init; }
    public string Name { get; init; }
    public string Code { get; init; }


    public static CountryRecord Parse(string s, IFormatProvider? provider)
    {
        if (string.IsNullOrWhiteSpace(s)) throw new ArgumentException("Input cannot be null or empty", nameof(s));


        var parts = CsvParser.SplitAndValidateCsv(s, 3).Select(p => p.Trim()).ToArray();

        return new CountryRecord(
            int.Parse(parts[0]),
            parts[1],
            parts[2]
        );
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out CountryRecord result)
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
