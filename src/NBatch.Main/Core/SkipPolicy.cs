using System;
using System.Linq;
using NBatch.Main.Core.Repositories;

namespace NBatch.Main.Core;

public sealed class SkipPolicy
{
    public int SkipLimit { get; set; }
    private Type[] _skippableExceptions;

    public bool IsSatisfiedBy(IStepRepository stepRepository, SkipContext skipContext)
    {
        if (SkipLimit == 0)
            return false;

        int exceptionCount = stepRepository.GetExceptionCountAsync(skipContext);
        if (exceptionCount >= SkipLimit || !_skippableExceptions.Contains(skipContext.GetExceptionType()))
            return false;

        stepRepository.SaveExceptionInfo(skipContext, exceptionCount);
        return true;
    }

    public void AddSkippableExceptions(Type[] exceptions)
    {
        if(!exceptions.All(ex => ex.IsSubclassOf(typeof(Exception)) || ex == typeof(Exception)))
            throw new Exception("Invalid Skippable Exception Type");

        _skippableExceptions = exceptions;
    }
}