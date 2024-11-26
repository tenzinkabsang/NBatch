namespace NBatch.Core.Exceptions;

public sealed class DuplicateStepNameException() 
    : Exception($"Step names must be unique.") { }
