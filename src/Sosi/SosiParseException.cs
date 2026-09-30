using System;

namespace Sosi
{
    /// <summary>Thrown when SOSI data cannot be parsed or a geometry cannot be built.</summary>
    [Serializable]
    public class SosiParseException : Exception
    {
        public SosiParseException(string message)
            : base(message)
        {
        }

        public SosiParseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected SosiParseException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }
}
