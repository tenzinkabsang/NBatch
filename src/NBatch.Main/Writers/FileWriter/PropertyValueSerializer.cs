using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

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

        public StringBuilder Serialize<T>(IEnumerable<T> items)
        {
            return items.Select(item =>
                                {
                                    // Get all properties for each item.
                                    PropertyInfo[] props = item.GetType().GetProperties();

                                    // Get values for each item, adding tokens in between, then remove the initial token from the front.
                                    return props.Aggregate("", (s, propInfo) => s + Token + propInfo.GetValue(item)).Substring(1);
                                })
                        .Aggregate(new StringBuilder(), (builder, s) => builder.Append(s).AppendLine());
        }

    }
}