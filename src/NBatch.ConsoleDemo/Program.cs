using NBatch.ConsoleDemo.Tests;
using System;

namespace NBatch.ConsoleDemo
{
    class Program
    {
        public static readonly string SourceUrl = PathUtil.GetPath(@"Files\NewItems\sample.txt");

        // UNCOMMENT each lines below for testing.
        // PLEASE ensure that the BatchJob and BatchStep database tables are reset after each run, because one of the features
        // of NBatch is that it will not reprocess items that has already been processed :)
        static void Main(string[] args)
        {
            //FileReaderConsoleWriterTest.Run();

            //FileReaderFileWriterTest.Run();

            FileReaderSqlWriterTest.Run();

            //SqlReaderConsoleWriterTest.Run();

            //SqlReaderFileWriterTest.Run();

            //SqlReaderSqlWriterTest.Run();

            Console.WriteLine("Done!!");
        }
    }
}