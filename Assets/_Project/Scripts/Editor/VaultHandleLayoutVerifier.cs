#if UNITY_EDITOR
using System;
using System.Reflection;
using Hecton8.Core.Memory;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;

namespace Hecton8.Core.Memory.Editor
{
    internal static class VaultHandleLayoutVerifier
    {
        [InitializeOnLoadMethod]
        private static void Verify()
        {
            bool valid =
                UnsafeUtility.SizeOf<VaultGenerationHandle<byte>>() == 16 &&
                OffsetOf(nameof(VaultGenerationHandle<byte>.BufferID)) == 0 &&
                OffsetOf(nameof(VaultGenerationHandle<byte>.SystemID)) == 4 &&
                OffsetOf(nameof(VaultGenerationHandle<byte>.Generation)) == 8 &&
                OffsetOf(nameof(VaultGenerationHandle<byte>.Flags)) == 12;

            if (!valid)
                throw new InvalidOperationException("VaultGenerationHandle<T> ABI violation. Required: explicit 16 bytes, offsets 0/4/8/12.");
        }

        private static int OffsetOf(string fieldName)
        {
            FieldInfo field = typeof(VaultGenerationHandle<byte>).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
    }
}
#endif
