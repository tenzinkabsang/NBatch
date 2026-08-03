using System.Globalization;

namespace NBatch.Readers.FileReader;

/// <summary>
/// Represents a single parsed row from a delimited file.
/// Provides typed accessors to retrieve column values by header name or index.
/// Numeric and date values are parsed with <see cref="CultureInfo.InvariantCulture"/>.
/// The <c>*OrNull</c> accessors return null for a missing column or an empty value.
/// </summary>
public sealed class CsvRow
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private readonly Dictionary<string, string> _values;

    private CsvRow(Dictionary<string, string> values) => _values = values;

    /// <summary>The row's raw values keyed by header name. Used by the auto-mapper.</summary>
    internal IReadOnlyDictionary<string, string> Fields => _values;

    internal static CsvRow Create(IList<string> headers, IList<string> columns)
    {
        var keys = headers.Count > 0 ? headers : DefaultKeys(columns);

        var result = keys
            .Zip(columns, (k, v) => (Key: k, Value: v.Trim()))
            .ToDictionary(x => x.Key, x => x.Value);

        return new CsvRow(result);
    }

    private static IEnumerable<string> DefaultKeys(IList<string> columns)
        => Enumerable.Range(0, columns.Count).Select(i => i.ToString());

    /// <summary>Gets the column value as a <see cref="string"/>.</summary>
    /// <param name="column">The header name of the column.</param>
    public string GetString(string column) => GetValue(column);
    /// <summary>Gets the column value as a <see cref="string"/>.</summary>
    /// <param name="index">The zero-based column index.</param>
    public string GetString(int index) => GetValue(index.ToString());

    /// <summary>Gets the column value as an <see cref="int"/>.</summary>
    /// <param name="column">The header name of the column.</param>
    public int GetInt(string column) => int.Parse(GetValue(column), Culture);
    /// <summary>Gets the column value as an <see cref="int"/>.</summary>
    /// <param name="index">The zero-based column index.</param>
    public int GetInt(int index) => int.Parse(GetValue(index.ToString()), Culture);

    /// <summary>Gets the column value as a <see cref="decimal"/>.</summary>
    /// <param name="column">The header name of the column.</param>
    public decimal GetDecimal(string column) => decimal.Parse(GetValue(column), Culture);
    /// <summary>Gets the column value as a <see cref="decimal"/>.</summary>
    /// <param name="index">The zero-based column index.</param>
    public decimal GetDecimal(int index) => decimal.Parse(GetValue(index.ToString()), Culture);

    /// <summary>Gets the column value as a <see cref="long"/>.</summary>
    /// <param name="column">The header name of the column.</param>
    public long GetLong(string column) => long.Parse(GetValue(column), Culture);
    /// <summary>Gets the column value as a <see cref="long"/>.</summary>
    /// <param name="index">The zero-based column index.</param>
    public long GetLong(int index) => long.Parse(GetValue(index.ToString()), Culture);

    /// <summary>Gets the column value as a <see cref="double"/>.</summary>
    /// <param name="column">The header name of the column.</param>
    public double GetDouble(string column) => double.Parse(GetValue(column), Culture);
    /// <summary>Gets the column value as a <see cref="double"/>.</summary>
    /// <param name="index">The zero-based column index.</param>
    public double GetDouble(int index) => double.Parse(GetValue(index.ToString()), Culture);

    /// <summary>Gets the column value as a <see cref="bool"/>.</summary>
    /// <param name="column">The header name of the column.</param>
    public bool GetBool(string column) => bool.Parse(GetValue(column));
    /// <summary>Gets the column value as a <see cref="bool"/>.</summary>
    /// <param name="index">The zero-based column index.</param>
    public bool GetBool(int index) => bool.Parse(GetValue(index.ToString()));

    /// <summary>Gets the column value as a <see cref="DateTime"/>.</summary>
    /// <param name="column">The header name of the column.</param>
    public DateTime GetDateTime(string column) => DateTime.Parse(GetValue(column), Culture);
    /// <summary>Gets the column value as a <see cref="DateTime"/>.</summary>
    /// <param name="index">The zero-based column index.</param>
    public DateTime GetDateTime(int index) => DateTime.Parse(GetValue(index.ToString()), Culture);

    /// <summary>Gets the column value as a <see cref="Guid"/>.</summary>
    /// <param name="column">The header name of the column.</param>
    public Guid GetGuid(string column) => Guid.Parse(GetValue(column));
    /// <summary>Gets the column value as a <see cref="Guid"/>.</summary>
    /// <param name="index">The zero-based column index.</param>
    public Guid GetGuid(int index) => Guid.Parse(GetValue(index.ToString()));

    /// <summary>Gets the column value, or null when the column is missing or empty.</summary>
    /// <param name="column">The header name of the column.</param>
    public string? GetStringOrNull(string column) => GetValueOrNull(column);
    /// <summary>Gets the column value, or null when the column is missing or empty.</summary>
    /// <param name="index">The zero-based column index.</param>
    public string? GetStringOrNull(int index) => GetValueOrNull(index.ToString());

    /// <inheritdoc cref="GetStringOrNull(string)" />
    public int? GetIntOrNull(string column) => GetValueOrNull(column) is { } v ? int.Parse(v, Culture) : null;
    /// <inheritdoc cref="GetStringOrNull(int)" />
    public int? GetIntOrNull(int index) => GetIntOrNull(index.ToString());

    /// <inheritdoc cref="GetStringOrNull(string)" />
    public long? GetLongOrNull(string column) => GetValueOrNull(column) is { } v ? long.Parse(v, Culture) : null;
    /// <inheritdoc cref="GetStringOrNull(int)" />
    public long? GetLongOrNull(int index) => GetLongOrNull(index.ToString());

    /// <inheritdoc cref="GetStringOrNull(string)" />
    public decimal? GetDecimalOrNull(string column) => GetValueOrNull(column) is { } v ? decimal.Parse(v, Culture) : null;
    /// <inheritdoc cref="GetStringOrNull(int)" />
    public decimal? GetDecimalOrNull(int index) => GetDecimalOrNull(index.ToString());

    /// <inheritdoc cref="GetStringOrNull(string)" />
    public double? GetDoubleOrNull(string column) => GetValueOrNull(column) is { } v ? double.Parse(v, Culture) : null;
    /// <inheritdoc cref="GetStringOrNull(int)" />
    public double? GetDoubleOrNull(int index) => GetDoubleOrNull(index.ToString());

    /// <inheritdoc cref="GetStringOrNull(string)" />
    public bool? GetBoolOrNull(string column) => GetValueOrNull(column) is { } v ? bool.Parse(v) : null;
    /// <inheritdoc cref="GetStringOrNull(int)" />
    public bool? GetBoolOrNull(int index) => GetBoolOrNull(index.ToString());

    /// <inheritdoc cref="GetStringOrNull(string)" />
    public DateTime? GetDateTimeOrNull(string column) => GetValueOrNull(column) is { } v ? DateTime.Parse(v, Culture) : null;
    /// <inheritdoc cref="GetStringOrNull(int)" />
    public DateTime? GetDateTimeOrNull(int index) => GetDateTimeOrNull(index.ToString());

    /// <inheritdoc cref="GetStringOrNull(string)" />
    public Guid? GetGuidOrNull(string column) => GetValueOrNull(column) is { } v ? Guid.Parse(v) : null;
    /// <inheritdoc cref="GetStringOrNull(int)" />
    public Guid? GetGuidOrNull(int index) => GetGuidOrNull(index.ToString());

    private string GetValue(string key)
    {
        if (!_values.TryGetValue(key, out var value))
            throw new KeyNotFoundException($"No column with the key '{key}' exists.");
        return value;
    }

    private string? GetValueOrNull(string key)
        => _values.TryGetValue(key, out var value) && value.Length > 0 ? value : null;
}
