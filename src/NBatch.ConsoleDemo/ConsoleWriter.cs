using System;
using System.Collections.Generic;
using System.Linq;
using NBatch.Main.Core;

namespace NBatch.ConsoleDemo
{
    public class ConsoleWriter<TInput> : IWriter<TInput>
    {
        public bool Write(IEnumerable<TInput> items)
        {
            items.ToList()
                .ForEach(item => Console.WriteLine(item));

            return true;
        }
    }
}