namespace NBatch.Main.Core
{
    sealed class DefaultProcessor<TInput, TOutput>:IProcessor<TInput, TOutput>
    {
        public TOutput Process(TInput input)
        {
            return (dynamic)input;
        }
    }
}