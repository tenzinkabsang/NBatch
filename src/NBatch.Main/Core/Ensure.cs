using System;
using System.Collections.Generic;
using System.Linq;

namespace NBatch.Main.Core;

internal static class Ensure
{
    public static void UniqueStepName(ICollection<string> steps, IStep newStep)
    {
        bool isUnique = steps.All(s => !s.Equals(newStep.Name, StringComparison.OrdinalIgnoreCase));
        if (!isUnique)
            throw new InvalidStepNameException(newStep.Name);
    }
}