using NBatch.Core;
using NBatch.Core.Repositories;
using System;
using System.IO;

namespace NBatch.ConsoleDemo
{
    /// <summary>
    /// Copies the file from the source location into the destination path.
    /// Not deleting the source file for this demo.
    /// </summary>
    class CleanupStep : IStep
    {
        private readonly string _source;
        private readonly string _targetPath;
        private readonly string _fileName;

        public CleanupStep(string sourcePath, string targetPath)
        {
            _source = sourcePath;
            _fileName = sourcePath.Substring(sourcePath.LastIndexOf("\\", StringComparison.Ordinal) + 1);
            _targetPath = PathUtil.GetPath(targetPath);
        }

        public string Name
        {
            get { return "Cleanup"; }
        }

        public bool Process(long startIndex, IStepRepository stepRepository)
        {
            // Create a new target folder, if necessary.
            if (!Directory.Exists(_targetPath))
                Directory.CreateDirectory(_targetPath);

            string destinationFile = Path.Combine(_targetPath, _fileName);

            // Copy the file to dest location and overwriter the destination file if it already exists.
            File.Copy(_source, destinationFile, true);

            return true;
        }
    }
}
