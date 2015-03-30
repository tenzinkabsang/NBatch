using NBatch.Main.Core;
using System;
using System.Collections.Generic;

namespace NBatch.ConsoleDemo
{
    public class ConsoleWriter<TInput> : IWriter<TInput>
    {
        public bool Write(IEnumerable<TInput> items)
        {
            foreach (var item in items)
                Console.WriteLine(item);

            return true;
        }
    }
}