using System.Collections.Generic;

namespace NBatch.Core.ItemWriter
{
    public interface IWriter<in TItem>
    {
        bool Write(IEnumerable<TItem> items);
    }
}