using NBatch.Main.Core;
using System.Collections.Generic;
using System.Linq;

namespace NBatch.Main.Writers.FileWriter
{
    public sealed class FlatFileItemWriter<TItem> : IWriter<TItem> where TItem : class
    {
        private readonly IPropertyValueSerializer _serializer;
        private readonly IFileWriterService _fileService;

        public FlatFileItemWriter(string destinationPath)
            :this(new PropertyValueSerializer(), new FileWriterService(destinationPath))
        {
        }

        internal FlatFileItemWriter(IPropertyValueSerializer serializer, IFileWriterService fileService)
        {
            _serializer = serializer;
            _fileService = fileService;
        }

        public FlatFileItemWriter<TItem> WithToken(char token)
        {
            _serializer.Token = token;
            return this;
        }

        public bool Write(IEnumerable<TItem> items)
        {
            IEnumerable<string> values = _serializer.Serialize(items).ToList();

            if (!values.Any()) 
                return false;

            _fileService.WriteFile(values);
            return true;
        }
    }
}
