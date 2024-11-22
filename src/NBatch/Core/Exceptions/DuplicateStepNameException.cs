namespace NBatch.Core.Exceptions;

public class DuplicateStepNameException(string stepName) 
    : Exception($"Step with name {stepName} already exists. Step name must be unique.") { }
