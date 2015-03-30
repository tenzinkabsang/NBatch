using System.Collections.Generic;

namespace NBatch.Main.Writers.FileWriter
{
    interface IPropertyValueSerializer
    {
        IEnumerable<string> Serialize<T>(IEnumerable<T> items) where T : class;
        char Token { get; set; }
    }
}