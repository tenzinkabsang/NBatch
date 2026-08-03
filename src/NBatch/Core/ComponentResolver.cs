using Microsoft.Extensions.DependencyInjection;

namespace NBatch.Core;

/// <summary>
/// Resolves step components (readers, processors, writers, tasklets) from the
/// per-run service provider: a registered service wins; otherwise the type is
/// constructed via <see cref="ActivatorUtilities"/> with its constructor
/// dependencies supplied from the container.
/// </summary>
internal static class ComponentResolver
{
    public static T Resolve<T>(IServiceProvider serviceProvider) where T : class
        => serviceProvider.GetService<T>() ?? ActivatorUtilities.CreateInstance<T>(serviceProvider);
}
