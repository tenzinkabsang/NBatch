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
        private IReader<TInput> _reader;
        private IWriter<TOutput> _writer;
        private IProcessor<TInput, TOutput> _processor;

        public Step(string name)
        {
            Name = name;
            _processor = new DefaultProcessor<TInput, TOutput>();
            _skipPolicy = new SkipPolicy();
        }

        public bool Process(StepContext stepContext, IStepRepository stepRepository)
        {
            bool success = true;
            StepContext ctx = StepContext.InitialRun(stepContext, _chunkSize);
            while (ctx.HasNext)
            {
                long newStepId = stepRepository.InsertStep(ctx.StepName, ctx.NextStepIndex);

                bool exceptionThrown = false;
                bool skip = false;
                bool error = false;
                List<TInput> items = Enumerable.Empty<TInput>().ToList();
                TOutput[] processed = Enumerable.Empty<TOutput>().ToArray();

                try
                {
                    items = _reader.Read(ctx.StepIndex, _chunkSize).ToList();

                    processed = items.Select(item => _processor.Process(item)).ToArray();

                    if (processed.Any())
                        success &= _writer.Write(processed);
                }
                catch (Exception ex)
                {
                    error = true;
                    skip = _skipPolicy.IsSatisfiedBy(stepRepository, new SkipContext(ctx.StepName, ctx.NextStepIndex, ex));
                    if (!skip)
                    {
                        exceptionThrown = true;
                        throw;
                    }
                }
                finally
                {
                    if (!exceptionThrown)
                        ctx = StepContext.Increment(ctx, items.Count, processed.Length, skip);

                    stepRepository.UpdateStep(newStepId, processed.Length, error, skip);
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
            _reader = reader;
            return this;
        }

        public Step<TInput, TOutput> SetProcessor(IProcessor<TInput, TOutput> processor)
        {
            _processor = processor;
            return this;
        }

        public Step<TInput, TOutput> SetWriter(IWriter<TOutput> writer)
        {
            _writer = writer;
            return this;
        }
    }
}