using NBatch.Core.Interfaces;

namespace NBatch.Core;

internal sealed class DelegateTasklet(Func<Task> action) : ITasklet
{
    public Task ExecuteAsync() => action();
}
