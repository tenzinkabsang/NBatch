using System.Text;

namespace NBatch.Readers.FileReader;

/// <summary>
/// RFC 4180-style field splitting for a single line: fields may be enclosed in
/// double quotes, quoted fields may contain the delimiter, and a doubled quote
/// (<c>""</c>) inside a quoted field is an escaped literal quote.
/// Embedded newlines inside quoted fields are not supported — the reader is
/// line-based.
/// </summary>
internal static class CsvLineParser
{
    /// <exception cref="FormatException">A quoted field is not terminated before the end of the line.</exception>
    public static string[] Parse(string line, char delimiter)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"'); // escaped quote
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == delimiter)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else if (c == '"' && current.Length == 0)
            {
                inQuotes = true; // opening quote at field start
            }
            else
            {
                current.Append(c);
            }
        }

        if (inQuotes)
            throw new FormatException("Unterminated quoted field.");

        fields.Add(current.ToString());
        return [.. fields];
    }
}
