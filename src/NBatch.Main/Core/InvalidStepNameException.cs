using System;

namespace NBatch.Main.Core;

public class InvalidStepNameException(string stepName) 
    : Exception(string.Format("Step with name {0} already exists. Names must be unique.", stepName)) { }