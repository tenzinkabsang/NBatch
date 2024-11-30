namespace NBatch.ConsoleApp;

public static class PathUtil
{
    public static string GetPath(string file)
    {
        var currentDir = Environment.CurrentDirectory;
        var value = currentDir[..currentDir.IndexOf("bin", StringComparison.Ordinal)];
        return Path.Combine(value, @file);
    }
}