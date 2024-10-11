using System;

namespace NoBrute.Exceptions
{
    public class NoBruteConfigurationException : Exception
    {
        public NoBruteConfigurationException(string message) : base(message: message)
        {
        }

        public override string Message
        {
            get
            {
                return $"[NoBrute::DependencyException] " + base.Message;
            }
        }
    }
}