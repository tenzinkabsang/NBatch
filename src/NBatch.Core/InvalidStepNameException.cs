using System;

namespace NBatch.Core
{
    public class InvalidStepNameException : Exception
    {
        public InvalidStepNameException(string stepName)
            :base(string.Format("Step with name {0} already exists. Names must be unique.", stepName))
        {
        }
    }
}