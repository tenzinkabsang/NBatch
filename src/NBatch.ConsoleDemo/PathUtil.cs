using System;
using System.IO;

namespace NBatch.ConsoleDemo
{
    static class PathUtil
    {
        public static string GetPath(string file)
        {
            string value = Environment.CurrentDirectory;

            value = value.Substring(0, value.IndexOf("bin", StringComparison.Ordinal));

            string path = Path.Combine(value, @file);

            Console.WriteLine(path);

            return path;
        }
    }
}