using System;

namespace NBatch.Core.Reader.FileReader
{
    public sealed class FlatFileParseException : Exception
    {
        public FlatFileParseException()
            :base("Unable to parse file")
        {
        }

        public FlatFileParseException(Exception innerException)
            :base("Unable to parse file", innerException)
        {   
        }
    }
}