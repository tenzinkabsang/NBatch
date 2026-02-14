using NBatch.Core.Interfaces;

namespace NBatch.Writers.FileWriter;

/// <summary>
/// Writes items to a delimited text file.
/// </summary>
/// <typeparam name="TItem">The type of items to serialize and write.</typeparam>
public sealed class FlatFileItemWriter<TItem> : IWriter<TItem> where TItem : class
{
    private readonly IPropertyValueSerializer _serializer;
    private readonly IFileWriterService _fileService;

    /// <summary>
    /// Creates a writer that serializes items to the specified file path.
    /// </summary>
    /// <param name="destinationPath">The file path to write to.</param>
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
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task WriteAsync(IEnumerable<TItem> items, CancellationToken cancellationToken = default)
    {
        var contents = _serializer.Serialize(items);

        if (!contents.Any()) 
            return;

        await _fileService.WriteFileAsync(contents, cancellationToken);
    }
}
