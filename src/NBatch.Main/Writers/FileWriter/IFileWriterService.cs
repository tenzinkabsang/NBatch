using System.Collections.Generic;

namespace NBatch.Main.Writers.FileWriter
{
    internal interface IFileWriterService
    {
        void WriteFile(IEnumerable<string> values);
    }
}