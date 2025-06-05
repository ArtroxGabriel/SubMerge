using System.Diagnostics.CodeAnalysis;

namespace SubMerge.Models;

public struct CountryRecord : IParsable<CountryRecord>
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

    public int CountryId { get; set; }
    public string Name { get; set; }
    public string Code { get; set; }


    public static CountryRecord Parse(string s, IFormatProvider? provider)
    {
        if (string.IsNullOrWhiteSpace(s)) throw new ArgumentException("Input cannot be null or empty", nameof(s));

        var parts = s.Split(',');
        if (parts.Length != 3)
            throw new FormatException("Input string must contain exactly 3 comma-separated values.");

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
