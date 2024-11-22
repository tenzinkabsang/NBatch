using NBatch.Core.Exceptions;
using NBatch.Core.Interfaces;

namespace NBatch.Core;

internal static class Ensure
{
    public static void UniqueStepName(ICollection<string> steps, IStep newStep)
    {
        if (!steps.Contains(newStep.Name)) return;

        throw new DuplicateStepNameException(newStep.Name);
    }
}
