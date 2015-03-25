using System;
using System.Collections.Generic;
using System.Linq;
using NBatch.Core.ItemProcessor;
using NBatch.Core.ItemReader;
using NBatch.Core.ItemWriter;
using NBatch.Core.Repositories;

namespace NBatch.Core
{
    public class Step<TInput, TOutput> : IStep
    {
        private int _chunkSize = 10;
        private readonly SkipPolicy _skipPolicy;
        public string Name { get; private set; }
        public IReader<TInput> Reader { get; private set; }
        public IWriter<TOutput> Writer { get; private set; }
        public IProcessor<TInput, TOutput> Processor { get; private set; }

        private readonly StepContext _ctx;
        public Step(string name)
        {
            Name = name;
            Processor = new DefaultProcessor<TInput, TOutput>();
            _skipPolicy = new SkipPolicy();
            _ctx = new StepContext(Name);
        }

        public bool Process(long startIndex, IStepRepository stepRepository)
        {
            _ctx.HeaderIndexValue = startIndex + _chunkSize;
            _ctx.StepIndex = startIndex;

            bool success = true;
            IList<TInput> items = Enumerable.Empty<TInput>().ToList();
            TOutput[] processed = Enumerable.Empty<TOutput>().ToArray();
            do
            {
                try
                {
                    _ctx.Skip = false;

                    // IReader
                    items = Reader.Read(_ctx.StepIndex, _chunkSize).ToList();

                    // IProcessor
                    processed = items.Select(item => Processor.Process(item))
                        .Where(result => result != null)
                        .ToArray();

                    // IWriter
                    if (processed.Any())
                        success &= Writer.Write(processed);
                }
                catch (Exception ex)
                {
                    _ctx.Skip = _skipPolicy.IsSatisfiedBy(stepRepository, new SkipContext(_ctx.StepName, _ctx.StepIndex, ex));
                    if (!_ctx.Skip)
                    {
                        _ctx.ExceptionThrown = true;
                        throw;
                    }
                }
                finally
                {
                    if (!_ctx.ExceptionThrown)
                    {
                        _ctx.StepIndex += _chunkSize;
                        _ctx.NumberOfItemsProcessed = processed.Length;
                        stepRepository.SaveStepContext(_ctx);
                    }
                }

            } while (items.Any() || _ctx.ShouldSkip);

            return success;
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