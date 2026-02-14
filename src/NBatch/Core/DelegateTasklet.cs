using NBatch.Core.Interfaces;

namespace NBatch.Core;

internal sealed class DelegateTasklet(Func<CancellationToken, Task> action) : ITasklet
{
    public Task ExecuteAsync(CancellationToken cancellationToken = default) => action(cancellationToken);
}
