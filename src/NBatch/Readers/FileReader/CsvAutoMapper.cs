using System.Globalization;
using System.Reflection;

namespace NBatch.Readers.FileReader;

/// <summary>
/// Reflection-based row binder for <see cref="CsvReader{T}"/>'s auto-mapping
/// constructor: matches header names to public settable properties of
/// <typeparamref name="T"/> case-insensitively and converts values with
/// <see cref="CultureInfo.InvariantCulture"/>.
/// </summary>
internal static class CsvAutoMapper<T>
{
    // Lazy caches the validation exception too, so a bad T fails consistently
    // (and without TypeInitializationException wrapping).
    private static readonly Lazy<Dictionary<string, Action<object, string>>> Setters = new(BuildSetters);

    /// <summary>
    /// Creates the row-mapping function.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="T"/> cannot be auto-mapped (no public parameterless
    /// constructor, abstract, or ambiguous property names).
    /// </exception>
    public static Func<CsvRow, T> CreateMap()
    {
        var setters = Setters.Value;

        return row =>
        {
            object instance = Activator.CreateInstance(typeof(T))!;
            foreach (var (header, value) in row.Fields)
            {
                if (setters.TryGetValue(header, out var setter))
                    setter(instance, value);
            }
            return (T)instance;
        };
    }

    private static Dictionary<string, Action<object, string>> BuildSetters()
    {
        var type = typeof(T);

        if (type.IsAbstract || (!type.IsValueType && type.GetConstructor(Type.EmptyTypes) is null))
            throw new InvalidOperationException(
                $"Type '{type.Name}' has no public parameterless constructor, so it cannot be auto-mapped. " +
                "Use the CsvReader(string, Func<CsvRow, T>) overload to map it explicitly (e.g. for positional records).");

        var setters = new Dictionary<string, Action<object, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite || property.SetMethod?.IsPublic != true)
                continue;
            if (!IsSupported(property.PropertyType))
                continue; // unsupported property types are simply left unbound

            var converter = BuildConverter(property);
            if (!setters.TryAdd(property.Name, converter))
                throw new InvalidOperationException(
                    $"Type '{type.Name}' has multiple settable properties named '{property.Name}' " +
                    "(differing only by case), so headers cannot be matched unambiguously. " +
                    "Use the CsvReader(string, Func<CsvRow, T>) overload instead.");
        }

        return setters;
    }

    private static bool IsSupported(Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        return type == typeof(string)
            || type == typeof(int) || type == typeof(long)
            || type == typeof(decimal) || type == typeof(double) || type == typeof(float)
            || type == typeof(bool)
            || type == typeof(DateTime) || type == typeof(DateTimeOffset)
            || type == typeof(Guid)
            || type.IsEnum;
    }

    private static Action<object, string> BuildConverter(PropertyInfo property)
    {
        var propertyType = property.PropertyType;
        var underlying = Nullable.GetUnderlyingType(propertyType);
        var targetType = underlying ?? propertyType;

        return (instance, raw) =>
        {
            if (raw.Length == 0)
            {
                if (targetType == typeof(string))
                {
                    property.SetValue(instance, raw);
                    return;
                }

                if (underlying is not null)
                    return; // nullable value type: leave as null

                throw new FormatException(
                    $"Empty value cannot be converted to {targetType.Name} for property '{property.Name}'.");
            }

            property.SetValue(instance, ConvertValue(raw, targetType));
        };
    }

    private static object ConvertValue(string raw, Type targetType)
    {
        var culture = CultureInfo.InvariantCulture;

        if (targetType == typeof(string)) return raw;
        if (targetType == typeof(int)) return int.Parse(raw, culture);
        if (targetType == typeof(long)) return long.Parse(raw, culture);
        if (targetType == typeof(decimal)) return decimal.Parse(raw, culture);
        if (targetType == typeof(double)) return double.Parse(raw, culture);
        if (targetType == typeof(float)) return float.Parse(raw, culture);
        if (targetType == typeof(bool)) return bool.Parse(raw);
        if (targetType == typeof(DateTime)) return DateTime.Parse(raw, culture);
        if (targetType == typeof(DateTimeOffset)) return DateTimeOffset.Parse(raw, culture);
        if (targetType == typeof(Guid)) return Guid.Parse(raw);
        if (targetType.IsEnum) return Enum.Parse(targetType, raw, ignoreCase: true);

        throw new NotSupportedException($"Unsupported conversion target '{targetType.Name}'.");
    }
}
