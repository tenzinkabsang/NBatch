using System.Collections.Generic;
using System.IO;

namespace NBatch.Core.Reader.FileReader.Services
{
    sealed class FileService : IFileService
    {
        private readonly string _resourceUrl;

        public FileService(string resourceUrl)
        {
            _resourceUrl = resourceUrl;
        }

        public IEnumerable<string> ReadLines(long startIndex, int chunkSize)
        {
            int rowCounter = -1;
            int chunkCounter = 0;
            using (var reader = File.OpenText(_resourceUrl))
            {
                string input;
                while ((input = reader.ReadLine()) != null)
                {
                    if (LineAlreadyProcessed(startIndex, ref rowCounter))
                        continue;

                    if (HasReachedChunkSize(chunkSize, ref chunkCounter))
                        break;

                    yield return input;
                }
            }
        }

        private static bool HasReachedChunkSize(int chunkSize, ref int chunkCounter)
        {
            return ++chunkCounter > chunkSize;
        }

        private static bool LineAlreadyProcessed(long startIndex, ref int rowCounter)
        {
            return ++rowCounter < startIndex;
        }
    }
}