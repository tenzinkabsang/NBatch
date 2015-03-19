using System;
using System.Collections.Generic;
using System.Linq;
using NBatch.Core.ItemProcessor;
using NBatch.Core.ItemReader;
using NBatch.Core.ItemWriter;
using NBatch.Core.Repositories;

namespace NBatch.Core
{
    public class Step<TInput, TOutput>: IStep
    {
        private int _chunkSize = 10;
        private readonly SkipPolicy _skipPolicy;
        public string Name { get; private set; }
        public IReader<TInput> Reader { get; private set; }
        public IWriter<TOutput> Writer { get; private set; }
        public IProcessor<TInput, TOutput> Processor { get; private set; }

        public Step(string name)
        {
            Name = name;
            Processor = new DefaultProcessor<TInput, TOutput>();
            _skipPolicy = new SkipPolicy();
        }

        public bool Process(int startIndex, IStepRepository stepRepository)
        {
            int headerIndexValue = startIndex + _chunkSize;
            bool success = true;
            bool skip;
            IList<TInput> items = Enumerable.Empty<TInput>().ToList();
            do
            {
                try
                {
                    skip = false;

                    // IReader
                    items = Reader.Read(startIndex, _chunkSize).ToList();

                    // IProcessor
                    TOutput[] processed = items.Select(item => Processor.Process(item))
                        .Where(result => result != null)
                        .ToArray();

                    // IWriter
                    if(processed.Any())
                        success &= Writer.Write(processed);
                }
                catch (Exception ex)
                {
                    skip = _skipPolicy.IsSatisfiedBy(stepRepository, new SkipContext(startIndex, ex));
                    if (!skip) throw;
                }
                finally
                {
                    startIndex += _chunkSize;
                    stepRepository.SaveIndex(startIndex);
                }

            } while (items.Any() || skip || FirstIterationWithLinesToSkipAndChunkSizeOfEqualValue(startIndex, headerIndexValue));

            return success;
        }

        private static bool FirstIterationWithLinesToSkipAndChunkSizeOfEqualValue(int startIndex, int headerIndexValue)
        {
            return startIndex == headerIndexValue;
        }

        public Step<TInput, TOutput> SkippableExceptions(params Type[] exceptions)
        {
            _skipPolicy.AddSkippableExceptions(exceptions);
            return this;
        }

        public Step<TInput, TOutput> SkipLimit(int skipLimit)
        {
            _skipPolicy.SkipLimit = skipLimit;
            return this;
        }

        public Step<TInput, TOutput> WithChunkSize(int size)
        {
            _chunkSize = size;
            return this;
        }

        public Step<TInput, TOutput> SetReader(IReader<TInput> reader)
        {
            Reader = reader;
            return this;
        }

        public Step<TInput, TOutput> SetProcessor(IProcessor<TInput, TOutput> processor)
        {
            Processor = processor;
            return this;
        }

        public Step<TInput, TOutput> SetWriter(IWriter<TOutput> writer)
        {
            Writer = writer;
            return this;
        }
    }
}