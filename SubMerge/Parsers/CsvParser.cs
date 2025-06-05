namespace SubMerge.Parsers;

public static class CsvParser
{
    /// <summary>
    ///     Splits a comma-separated string and validates that it contains exactly N parts.
    /// </summary>
    /// <param name="s">The input string to parse.</param>
    /// <param name="n">The total N of expected parts of csv</param>
    /// <returns>An array of strings representing the parsed parts.</returns>
    /// <exception cref="FormatException">Thrown if the input string does not contain exactly N comma-separated values.</exception>
    public static string[] SplitAndValidateCsv(string s, int n)
    {
        var parts = s.Split(',');
        if (parts.Length != n)
            throw new FormatException($"Input string must contain exactly {n} comma-separated values.");

        return parts;
    }
}
