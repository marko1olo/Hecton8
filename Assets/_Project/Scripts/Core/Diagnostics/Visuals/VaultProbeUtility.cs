using System;
using System.Runtime.CompilerServices;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Scripting;

namespace Hecton8.Core.Diagnostics.Visuals
{
    /// <summary>
    /// Diagnostic bridge from <see cref="IDataVault"/> buffers into bounded read-only byte spans.
    /// </summary>
    [Preserve]
    public static unsafe class VaultProbeUtility
    {
        /// <summary>
        /// Attempts to expose an existing vault buffer as raw read-only bytes.
        /// </summary>
        public static bool TryReadOnlyBufferBytes<T>(IDataVault vault, BufferID bufferId, out ReadOnlySpan<byte> bytes)
            where T : unmanaged
        {
            bytes = ReadOnlySpan<byte>.Empty;
            if (!TryResolveBuffer<T>(vault, bufferId, out NativeArray<T> buffer, out int byteLength))
                return false;

            void* pointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(buffer);
            bytes = new ReadOnlySpan<byte>(pointer, byteLength);
            return true;
        }

        private static bool TryResolveBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            out NativeArray<T> buffer,
            out int byteLength)
            where T : unmanaged
        {
            buffer = default;
            byteLength = 0;
            if (vault == null || bufferId == BufferID.Unknown)
                return false;

            if (!vault.TryGetBuffer(bufferId, out buffer) || !buffer.IsCreated || buffer.Length <= 0)
                return false;

            int elementSize = UnsafeUtility.SizeOf<T>();
            long bytes = (long)buffer.Length * elementSize;
            if (bytes <= 0L || bytes > int.MaxValue)
            {
                buffer = default;
                return false;
            }

            byteLength = (int)bytes;
            return true;
        }

        /// <summary>
        /// Attempts to copy a typed vault handle without creating the buffer.
        /// </summary>
        public static bool TryGetHandle<T>(IDataVault vault, BufferID bufferId, out VaultBufferHandle<T> handle)
            where T : unmanaged
        {
            handle = default;
            return vault != null &&
                   bufferId != BufferID.Unknown &&
                   vault.TryGetBufferHandle(bufferId, out handle) &&
                   handle.IsCreated;
        }

        /// <summary>
        /// Scans a float buffer for the first non-finite value.
        /// </summary>
        public static bool TryFindFirstNonFinite(NativeArray<float> buffer, out int index)
        {
            index = -1;
            if (!buffer.IsCreated)
                return false;

            for (int i = 0; i < buffer.Length; i++)
            {
                if (math.isfinite(buffer[i]))
                    continue;

                index = i;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Scans a float3 buffer for the first non-finite vector.
        /// </summary>
        public static bool TryFindFirstNonFinite(NativeArray<float3> buffer, out int index)
        {
            index = -1;
            if (!buffer.IsCreated)
                return false;

            for (int i = 0; i < buffer.Length; i++)
            {
                if (math.all(math.isfinite(buffer[i])))
                    continue;

                index = i;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Scans an AUP buffer for invalid local offsets.
        /// </summary>
        public static bool TryFindFirstNonFinite(NativeArray<AbsoluteUniversePosition> buffer, out int index)
        {
            index = -1;
            if (!buffer.IsCreated)
                return false;

            for (int i = 0; i < buffer.Length; i++)
            {
                AbsoluteUniversePosition aup = buffer[i];
                if (math.all(math.isfinite(new float3(aup.LocalX, aup.LocalY, aup.LocalZ))))
                    continue;

                index = i;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Converts one AUP to a presentation position relative to the local sector.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ToLocalMeters(in AbsoluteUniversePosition aup)
        {
            return new float3(aup.LocalX, aup.LocalY, aup.LocalZ);
        }

        /// <summary>
        /// Fast scalar finite guard for AUP local fields. Grid longs cannot carry NaN.
        /// </summary>
        public static bool IsFinite(in AbsoluteUniversePosition aup)
        {
            return math.all(math.isfinite(new float3(aup.LocalX, aup.LocalY, aup.LocalZ)));
        }
    }
}
