
namespace NBatch.Main.Core
{
    public interface IProcessor<in TInput, out TOutput>
    {
        TOutput Process(TInput input);
    }
}