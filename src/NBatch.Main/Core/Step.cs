using System;
using System.Collections.Generic;
using System.Linq;
using NBatch.Main.Core.Repositories;

namespace NBatch.Main.Core
{
    public class Step<TInput, TOutput> : IStep
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

        public bool Process(long startIndex, IStepRepository stepRepository)
        {
            bool success = true;
            StepContext ctx = StepContext.InitialRun(Name, startIndex, _chunkSize);
            while (ctx.HasNext)
            {
                bool exceptionThrown = false;
                bool skip = false;
                List<TInput> items = Enumerable.Empty<TInput>().ToList();
                TOutput[] processed = Enumerable.Empty<TOutput>().ToArray();

                try
                {
                    items = Reader.Read(ctx.StepIndex, _chunkSize).ToList();

                    processed = items.Select(item => Processor.Process(item)).ToArray();

                    if (processed.Any())
                        success &= Writer.Write(processed);
                }
                catch (Exception ex)
                {
                    skip = _skipPolicy.IsSatisfiedBy(stepRepository, new SkipContext(ctx.StepName, ctx.StepIndex, ex));
                    if (!skip)
                    {
                        exceptionThrown = true;
                        throw;
                    }
                }
                finally
                {
                    if (!exceptionThrown)
                    {
                        ctx = StepContext.Increment(ctx, items.Count, processed.Length, skip);
                        if(ctx.NumberOfItemsReceived > 0)
                            stepRepository.SaveStepContext(ctx);
                    }
                }
            }
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