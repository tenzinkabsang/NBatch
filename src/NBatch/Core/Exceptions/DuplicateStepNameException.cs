namespace NBatch.Core.Exceptions;

public class DuplicateStepNameException() 
    : Exception($"Step names must be unique.") { }
