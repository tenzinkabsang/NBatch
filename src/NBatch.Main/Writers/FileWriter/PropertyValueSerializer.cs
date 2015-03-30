using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NBatch.Main.Writers.FileWriter
{
    sealed class PropertyValueSerializer : IPropertyValueSerializer
    {
        private const char DEFAULT_TOKEN = ',';
        public char Token { get; set; }

        public PropertyValueSerializer()
        {
            Token = DEFAULT_TOKEN;
        }

        public IEnumerable<string> Serialize<T>(IEnumerable<T> items) where T : class
        {
            if (items == null)
                return Enumerable.Empty<string>();

            return items.Select(item =>
                                {
                                    // Get all properties for each item.
                                    PropertyInfo[] props = item.GetType().GetProperties();

                                    // Get values for each item, adding tokens in between, then remove the initial token from the front.
                                    return props.Aggregate("", (s, propInfo) => s + Token + propInfo.GetValue(item)).Substring(1);
                                });
        }

    }
}