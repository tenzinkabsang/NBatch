namespace NBatch.Writers.FileWriter;

internal interface IFileWriterService
{
    Task WriteFileAsync(IEnumerable<string> contents);
}
