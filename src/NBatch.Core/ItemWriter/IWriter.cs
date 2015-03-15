using System.Collections.Generic;

namespace NBatch.Core.ItemWriter
{
    public interface IWriter<in TInput>
    {
        bool Write(IEnumerable<TInput> items);
    }
}