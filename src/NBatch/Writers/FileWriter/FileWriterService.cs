namespace NBatch.Writers.FileWriter;

internal sealed class FileWriterService(string destinationPath) : IFileWriterService
{
    public async Task WriteFileAsync(IEnumerable<string> contents)
        => await File.AppendAllLinesAsync(destinationPath, contents);
}
