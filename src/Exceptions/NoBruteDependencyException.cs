using System;

namespace NoBrute.Exceptions
{
    public class NoBruteDependencyException : Exception
    {
        public NoBruteDependencyException(string message) : base(message: message)
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