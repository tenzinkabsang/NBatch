using System.Collections.Generic;

namespace NBatch.Core.Reader.FileReader.Services
{
    public interface IFileService
    {
        IEnumerable<string> ReadLines(int startIndex, int chunkSize);
    }

    sealed class FileService : IFileService
    {
        private readonly string _resourceUrl;

        public FileService(string resourceUrl)
        {
            _resourceUrl = resourceUrl;
        }

        public IEnumerable<string> ReadLines(int startIndex, int chunkSize)
        {
            throw new System.NotImplementedException();
        }
    }
}