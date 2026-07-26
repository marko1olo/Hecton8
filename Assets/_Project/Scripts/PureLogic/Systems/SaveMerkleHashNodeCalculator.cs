using System;
using System.Security.Cryptography;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for SaveMerkleHashNodeCalculator.
    /// Extracted from SaveStateMerkleTree.cs. Stateless; allocates only the combined
    /// buffer and the returned hash (cold save path, not a per-frame route).
    /// </summary>
    public static class SaveMerkleHashNodeCalculator
    {
        private static readonly byte[] EmptyByteArray = new byte[0];

        /// <summary>
        /// Computes the parent hash of two child hashes.
        /// </summary>
        /// <remarks>
        /// Each child is prefixed with its 4-byte little-endian length before hashing.
        /// Plain concatenation is ambiguous at the child boundary — ({1,2},{3}) and
        /// ({1},{2,3}) concatenate to the same bytes, and (null,X)/(X,null) collide —
        /// which lets bytes shift between children without changing the parent hash.
        /// The length prefix makes the encoding injective, so the hash is order- and
        /// boundary-dependent as an integrity tree requires.
        /// </remarks>
        /// <param name="leftChildHash">Parameter representing the leftChildHash (byte[]).</param>
        /// <param name="rightChildHash">Parameter representing the rightChildHash (byte[]).</param>
        /// <returns>Returns parentHash of type byte[].</returns>
        public static byte[] Compute(byte[] leftChildHash, byte[] rightChildHash)
        {
            var left = leftChildHash ?? EmptyByteArray;
            var right = rightChildHash ?? EmptyByteArray;

            var combined = new byte[8 + left.Length + right.Length];
            combined[0] = (byte)left.Length;
            combined[1] = (byte)(left.Length >> 8);
            combined[2] = (byte)(left.Length >> 16);
            combined[3] = (byte)(left.Length >> 24);
            if (left.Length > 0)
            {
                Array.Copy(left, 0, combined, 4, left.Length);
            }
            int rightLengthOffset = 4 + left.Length;
            combined[rightLengthOffset] = (byte)right.Length;
            combined[rightLengthOffset + 1] = (byte)(right.Length >> 8);
            combined[rightLengthOffset + 2] = (byte)(right.Length >> 16);
            combined[rightLengthOffset + 3] = (byte)(right.Length >> 24);
            if (right.Length > 0)
            {
                Array.Copy(right, 0, combined, rightLengthOffset + 4, right.Length);
            }

            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(combined);
            }
        }
    }
}
