using System.Collections.Generic;
using System.Text;

namespace NBatch.Main.Writers.FileWriter
{
    interface IPropertyValueSerializer
    {
        StringBuilder Serialize<T>(IEnumerable<T> items);
        char Token { get; set; }
    }
}