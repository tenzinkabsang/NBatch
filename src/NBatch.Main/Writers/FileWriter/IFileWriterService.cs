namespace NBatch.Main.Writers.FileWriter
{
    internal interface IFileWriterService
    {
        bool Write(string values);
    }

    class FileWriterService : IFileWriterService
    {
        private readonly string _destinationPath;

        public FileWriterService(string destinationPath)
        {
            _destinationPath = destinationPath;
        }

        public bool Write(string values)
        {
            throw new System.NotImplementedException();
        }
    }
}