using System.Collections.Generic;
using System.Linq;

namespace NBatch.Main.Readers.FileReaders
{
    public sealed class FieldSet
    {
        private readonly IDictionary<string, string> _header;

        private FieldSet(IDictionary<string, string> valueWithHeader)
        {
            _header = valueWithHeader;
        }

        public static FieldSet Create(IList<string> headers, IList<string> line)
        {
            var keys = headers.Any() ? headers : UseIndexAsKeys(line);

            var result = keys
                .Zip(line, (key, value) => new { key, value })
                .ToDictionary(x => x.key, x => x.value);

            return new FieldSet(result);
        }

        private static IEnumerable<string> UseIndexAsKeys(IList<string> line)
        {
            return Enumerable.Range(0, line.Count).Select(index => index.ToString());
        }

        public string GetString(string key)
        {
            return GetValue(key);
        }

        public string GetString(int index)
        {
            return GetValue(index.ToString());
        }

        public decimal GetDecimal(int index)
        {
            return GetDecimal(index.ToString());
        }

        public decimal GetDecimal(string key)
        {
            string value = GetValue(key);
            return decimal.Parse(value);
        }

        public int GetInt(int index)
        {
            return GetInt(index.ToString());
        }

        public int GetInt(string key)
        {
            string value = GetValue(key);
            return int.Parse(value);
        }

        private string GetValue(string key)
        {
            ValidateKeyExists(key);
            return _header[key];
        }

        private void ValidateKeyExists(string key)
        {
            if (!_header.ContainsKey(key))
                throw new KeyNotFoundException("No value with the given name exists");
        }
    }
}