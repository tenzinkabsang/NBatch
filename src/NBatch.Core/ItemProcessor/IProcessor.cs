
namespace NBatch.Core.ItemProcessor
{
    public interface IProcessor<in TInput, out TOutput>
    {
        TOutput Process(TInput input);
    }
}