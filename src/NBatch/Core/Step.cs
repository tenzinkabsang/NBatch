using NBatch.Core.Interfaces;
using NBatch.Core.Repositories;

namespace NBatch.Core;

/// <summary>
/// Step consists of three parts: Reader, (optional) Processor and a Writer.
/// It supports skipping certain types of `ERRORS` as well as the `NUMBER` of times errors should be skipped. 
/// </summary>
/// <typeparam name="TInput">Input data for the Reader.</typeparam>
/// <typeparam name="TOutput">Output data for the Processor & the Writer.</typeparam>
/// <param name="stepName">A unique name for the Step.</param>
/// <param name="reader">Used to read data from various sources (file, databases, etc,.) and feeds into the processor.</param>
/// <param name="processor">Handles any intermediary processes that needs to be performed on the data before sending it to a writer</param>
/// <param name="writer">Used to write/save the processed items.</param>
/// <param name="chunkSize">Ability to perform operation in chunks.</param>
public class Step<TInput, TOutput>(string stepName,
    IReader<TInput> reader,
    IProcessor<TInput, TOutput> processor,
    IWriter<TOutput> writer,
    SkipPolicy? skipPolicy = null,
    int chunkSize = int.MaxValue) : IStep
{
    private readonly IProcessor<TInput, TOutput> _processor = processor ?? new DefaultProcessor<TInput, TOutput>();
    private readonly SkipPolicy _skipPolicy = skipPolicy ?? SkipPolicy.None();
    public string Name { get; init; } = stepName;
    public int ChunkSize { get; init; } = chunkSize;

    /// <summary>
    /// Attempts to process the step based on the provided Reader, Processor and Writer.
    /// 
    /// </summary>
    /// <param name="stepContext"></param>
    /// <param name="stepRepository"></param>
    /// <returns></returns>
    public async Task<bool> ProcessAsync(StepContext stepContext, IStepRepository stepRepository)
    {
        bool success = true;
        var ctx = StepContext.InitialRun(stepContext, ChunkSize);
        while (ctx.HasNext)
        {
            long newStepId = await stepRepository.InsertStepAsync(ctx.StepName, ctx.NextStepIndex);
            bool exceptionThrown = false;
            List<TInput> items = [];
            List<TOutput> processed = [];
            try
            {
                items = (await reader.ReadAsync(ctx.StepIndex, ChunkSize)).ToList();

                foreach (var item in items)
                {
                    var processedItem = await _processor.ProcessAsync(item);
                    processed.Add(processedItem);
                }

                success &= await writer.WriteAsync(processed);
            }
            catch (Exception ex)
            {
                ctx.Error = true;
                ctx.Skip = await _skipPolicy.IsSatisfiedByAsync(stepRepository, new SkipContext(ctx.StepName, ctx.NextStepIndex, ex));
                if(!ctx.Skip)
                {
                    exceptionThrown = true;
                    throw;
                }
            }
            finally
            {
                if (!exceptionThrown)
                    ctx = StepContext.Increment(ctx, items.Count, processed.Count);

                await stepRepository.UpdateStepAsync(newStepId, processed.Count, ctx.Error, ctx.Skip);
            }
        }
        return success;
    }
}
