using System;
using System.Collections.Generic;
using System.IO;

namespace NBatch.Main.Writers.FileWriter
{
    class FileWriterService : IFileWriterService
    {
        private readonly string _destinationPath;

        public FileWriterService(string destinationPath)
        {
            _destinationPath = destinationPath;
        }

        public void WriteFile(IEnumerable<string> values)
        {
            try
            {
                File.AppendAllLines(_destinationPath, values);
            }
            catch (Exception ex)
            {
                // Add logging
                throw;
            }
        }
    }
}