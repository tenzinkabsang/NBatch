using NBatch.Core.Interfaces;

namespace NBatch.Writers.FileWriter;

internal sealed class FlatFileItemWriter<TItem> : IWriter<TItem> where TItem : class
{
    private readonly IPropertyValueSerializer _serializer;
    private readonly IFileWriterService _fileService;

    public FlatFileItemWriter(string destinationPath)
        : this(new PropertyValueSerializer(), new FileWriterService(destinationPath)) { }

    internal FlatFileItemWriter(IPropertyValueSerializer serializer, IFileWriterService fileService)
    {
        _serializer = serializer;
        _fileService = fileService;
    }

    /// <summary>
    /// Provide the separator token. If no token is provided, uses a comma as the default token.
    /// </summary>
    public FlatFileItemWriter<TItem> WithToken(char token)
    {
        _serializer.Token = token;
        return this;
    }

    /// <summary>
    /// Writes the content to the designated file.
    /// </summary>
    /// <param name="items">Items to serialize and save.</param>
    /// <returns>True if operation is successful.</returns>
    public async Task<bool> WriteAsync(IEnumerable<TItem> items)
    {
        var contents = _serializer.Serialize(items);

        if (!contents.Any()) 
            return false;

        await _fileService.WriteFileAsync(contents);
        return true;
    }
}
