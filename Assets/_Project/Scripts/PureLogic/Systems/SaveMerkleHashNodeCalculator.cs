using System;
using System.Security.Cryptography;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for SaveMerkleHashNodeCalculator.
    /// Extracted from SaveStateMerkleTree.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class SaveMerkleHashNodeCalculator
    {
        private static readonly byte[] EmptyByteArray = new byte[0];

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="leftChildHash">Parameter representing the leftChildHash (byte[]).</param>
        /// <param name="rightChildHash">Parameter representing the rightChildHash (byte[]).</param>
        /// <returns>Returns parentHash of type byte[].</returns>
        public static byte[] Compute(byte[] leftChildHash, byte[] rightChildHash)
        {
            var left = leftChildHash ?? EmptyByteArray;
            var right = rightChildHash ?? EmptyByteArray;

            var combined = new byte[left.Length + right.Length];
            if (left.Length > 0)
            {
                Array.Copy(left, 0, combined, 0, left.Length);
            }
            if (right.Length > 0)
            {
                Array.Copy(right, 0, combined, left.Length, right.Length);
            }

            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(combined);
            }
        }
    }
}
