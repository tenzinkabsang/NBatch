using System;
using System.Collections.Generic;
using System.Linq;
using NBatch.Core.ItemReader;
using NBatch.Core.Reader.FileReader.Services;

namespace NBatch.Core.Reader.FileReader
{
    sealed class FlatFileItemReader<TInput>:IReader<TInput>
    {
        private readonly ILineMapper<TInput> _lineMapper;
        private readonly IFileService _fileService;

        public FlatFileItemReader(ILineMapper<TInput> lineMapper, IFileService fileService )
        {
            _lineMapper = lineMapper;
            _fileService = fileService;
        }

        public int LinesToSkip { get; set; }

        public IEnumerable<TInput> Read(int startIndex, int chunkSize)
        {
            try
            {
                // Read based on chunk size
                var itemsToProcess = _fileService.ReadLines(startIndex, chunkSize);

                if (IsFirstLine(startIndex))
                    itemsToProcess = itemsToProcess.Skip(LinesToSkip).ToList();

                return itemsToProcess
                    .Where(item => !string.IsNullOrEmpty(item))
                    .Select(item => _lineMapper.MapToModel(item))
                    .ToList();
            }
            catch (Exception ex)
            {
                throw new FlatFileParseException(ex);
            }
        }

        private static bool IsFirstLine(int startIndex)
        {
            return startIndex == 0;
        }
    }
}