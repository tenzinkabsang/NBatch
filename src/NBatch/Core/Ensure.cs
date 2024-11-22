using NBatch.Core.Exceptions;
using NBatch.Core.Interfaces;

namespace NBatch.Core;

internal static class Ensure
{
    public static void UniqueStepNames(IList<IStep> steps)
    {
        var uniqueNames = new HashSet<string>(steps.Select(s => s.Name));

        if (uniqueNames.Count == steps.Count) return;

        throw new DuplicateStepNameException();
    }
}
