using System;

namespace Hecton8.Modding
{
    /// <summary>
    /// Thrown when a mod attempts to bind to a forbidden managed or non-blittable runtime contract.
    /// </summary>
    public sealed class IllegalContractException : Exception
    {
        /// <summary>
        /// Creates a contract violation exception with the supplied diagnostic text.
        /// </summary>
        /// <param name="message">Reason the mod contract was rejected.</param>
        public IllegalContractException(string message)
            : base(message)
        {
        }
    }
}
