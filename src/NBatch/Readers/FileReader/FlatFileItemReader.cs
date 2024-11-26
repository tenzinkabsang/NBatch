using NBatch.Core.Interfaces;
using NBatch.Readers.FileReader.Services;

namespace NBatch.Readers.FileReader;

internal sealed class FlatFileItemReader<TInput> : IReader<TInput>
{
    private readonly ILineMapper<TInput> _lineMapper;
    private readonly IFileService _fileService;

    public int LinesToSkip { get; set; } = 0;

    public FlatFileItemReader(ILineMapper<TInput> lineMapper, IFileService fileService)
    {
        _lineMapper = lineMapper;
        _fileService = fileService;
    }

    public async Task<IEnumerable<TInput>> ReadAsync(long startIndex, int chunkSize)
    {
        try
        {
            var itemsToProcess = await _fileService.ReadLinesAsync(startIndex, chunkSize).ToListAsync();

            return itemsToProcess
                .Skip(IsFirstLine(startIndex) ? LinesToSkip : 0)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(_lineMapper.MapToModel);

        }
        catch(Exception ex)
        {
            throw new FlatFileParseException(ex);
        }
    }

    private static bool IsFirstLine(long index) => index == 0;
}
