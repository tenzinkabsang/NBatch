using System.Collections.Generic;

namespace NBatch.Main.Core
{
    public interface IWriter<in TItem>
    {
        bool Write(IEnumerable<TItem> items);
    }
}