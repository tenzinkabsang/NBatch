using NBatch.Core.Exceptions;
using NBatch.Core.Interfaces;

namespace NBatch.Core;

internal static class Ensure
{
    public static void UniqueStepNames(ICollection<string> steps, IStep newStep)
    {
        bool isUnique = steps.All(s => !s.Equals(newStep.Name, StringComparison.OrdinalIgnoreCase));
        
        if (isUnique) return;

        throw new DuplicateStepNameException();
    }
}
