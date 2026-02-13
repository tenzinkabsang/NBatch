namespace NBatch.ConsoleApp;

public static class PathUtil
{
    public static string GetPath(string file)
        => Path.Combine(AppContext.BaseDirectory, file);
}