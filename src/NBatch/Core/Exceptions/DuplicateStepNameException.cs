namespace NBatch.Core.Exceptions;

/// <summary>Thrown when a step is registered with a name that already exists in the job.</summary>
public sealed class DuplicateStepNameException() 
    : Exception($"Step names must be unique.") { }
