using System;
using KeepKeySharp.Contracts;

namespace KeepKeySharp
{
    public class KeepKeyException : Exception
    {
        public KeepKeyException(Failure failure) : base(failure.Message)
        {
            Code = failure.Code;
        }

        public FailureType? Code { get; }
    }
}