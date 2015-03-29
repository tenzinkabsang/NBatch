using NBatch.Main.Core;
using System.Collections.Generic;
using System.Text;

namespace NBatch.Main.Writers.FileWriter
{
    public sealed class FlatFileItemWriter<TItem> : IWriter<TItem>
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

        public FlatFileItemWriter<TItem> Token(char token)
        {
            _serializer.Token = token;
            return this;
        }

        public bool Write(IEnumerable<TItem> items)
        {
            StringBuilder values = _serializer.Serialize(items);

            return _fileService.Write(values.ToString());
        }
    }
}
