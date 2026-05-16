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
    /// Read-only diagnostic bridge from <see cref="IDataVault"/> buffers into byte spans.
    /// </summary>
    [Preserve]
    public static unsafe class VaultProbeUtility
    {
        /// <summary>
        /// Attempts to expose an existing vault buffer as raw bytes without allocating or resizing it.
        /// </summary>
        /// <typeparam name="T">Known element type for the requested vault buffer.</typeparam>
        /// <param name="vault">Vault instance resolved from <see cref="GlobalRegistry.DataVault"/> during cold setup.</param>
        /// <param name="bufferId">Stable vault buffer identifier.</param>
        /// <param name="bytes">Raw byte span over the existing native buffer.</param>
        /// <returns>True when the span points at a live vault buffer.</returns>
        public static bool TryReadBufferBytes<T>(IDataVault vault, BufferID bufferId, out Span<byte> bytes)
            where T : unmanaged
        {
            bytes = Span<byte>.Empty;
            if (vault == null || bufferId == BufferID.Unknown)
                return false;

            if (!vault.TryGetBuffer(bufferId, out NativeArray<T> buffer) || !buffer.IsCreated || buffer.Length <= 0)
                return false;

            void* pointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(buffer);
            int byteLength = buffer.Length * UnsafeUtility.SizeOf<T>();
            bytes = new Span<byte>(pointer, byteLength);
            return true;
        }

        /// <summary>
        /// Attempts to expose an existing vault buffer as raw read-only bytes.
        /// </summary>
        public static bool TryReadOnlyBufferBytes<T>(IDataVault vault, BufferID bufferId, out ReadOnlySpan<byte> bytes)
            where T : unmanaged
        {
            bytes = ReadOnlySpan<byte>.Empty;
            if (!TryReadBufferBytes<T>(vault, bufferId, out Span<byte> mutableBytes))
                return false;

            bytes = mutableBytes;
            return true;
        }

        /// <summary>
        /// Attempts to expose an existing global vault buffer as raw bytes.
        /// </summary>
        public static bool TryReadGlobalBufferBytes<T>(BufferID bufferId, out Span<byte> bytes)
            where T : unmanaged
        {
            return TryReadBufferBytes<T>(GlobalRegistry.DataVault, bufferId, out bytes);
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
    }
}
