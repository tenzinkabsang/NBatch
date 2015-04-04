using System;
using System.IO;
using NBatch.Main.Core;
using NBatch.Main.Readers.SqlReader;
using NBatch.Main.Writers.FileWriter;
using NBatch.Main.Writers.SqlWriter;

namespace NBatch.ConsoleDemo.Tests
{
    /// <summary>
    /// Read from database, lowercase all values and save it to a csv file.
    /// </summary>
    public class SqlReaderFileWriterTest
    {
        public static void Run()
        {
            IStep processDb = new Step<Product, Product>("dbProcessor")
                .SetReader(SqlReader())
                .SetProcessor(new ProductLowercaseProcessor())
                .SetWriter(FlatFileWriter())
                .WithChunkSize(3);

            new Job("Job5", "JobDB")
                .AddStep(processDb)
                .Start();
        }

        private static IReader<Product> SqlReader()
        {
            return new SqlDbItemReader<Product>("ApplicationDB")
                .Query("Select * from Product")
                .OrderBy("ProductId");
        }

        private static IWriter<Product> FlatFileWriter()
        {
            string destPath = GetRelativeFilePath();

            return new FlatFileItemWriter<Product>(destPath)
                        .WithToken(',');
        }

        private static string GetRelativeFilePath()
        {
            string target = PathUtil.GetPath(@"Files\Processed");
            return Path.Combine(target, "sqlReaderFileWriter.txt");
        }
    }
}