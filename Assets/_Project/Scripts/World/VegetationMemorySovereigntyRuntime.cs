using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class HectonMapMagicVegetationBridge
    {
        private VaultGenerationHandle<VegetationMemoryTelemetryEntry> _vegetationMemoryTelemetryHandle;
        private VaultGenerationHandle<int> _vegetationMemoryTelemetryCursorHandle;
        private IDataVault _vegetationMemoryVault;
        private bool _vegetationMemoryTelemetryDumped;

        private IDataVault CacheVegetationMemoryVaultCold()
        {
            if (_vegetationMemoryVault == null)
                _vegetationMemoryVault = GlobalRegistry.DataVault;

            return _vegetationMemoryVault;
        }

        private void RebindVegetationMemoryVault(IDataVault currentVault)
        {
            IDataVault previousVault = _vegetationMemoryVault;
            if (previousVault != null && !ReferenceEquals(previousVault, currentVault))
                ReleaseVegetationMemoryTelemetryResources(previousVault);

            _vegetationMemoryVault = currentVault;
            if (currentVault != null)
                EnsureVegetationMemoryTelemetryCold();
        }

        private bool EnsureVegetationMemoryTelemetryCold()
        {
            IDataVault vault = CacheVegetationMemoryVaultCold();
            if (vault == null)
                return false;

            bool hadRing = IsExactVegetationMemoryHandle(
                in _vegetationMemoryTelemetryHandle,
                VegetationMemorySovereigntyConstants.TelemetryRingBufferId);
            bool hadCursor = IsExactVegetationMemoryHandle(
                in _vegetationMemoryTelemetryCursorHandle,
                VegetationMemorySovereigntyConstants.TelemetryCursorBufferId);

            if (!hadRing)
            {
                _vegetationMemoryTelemetryHandle = vault.EnsureGenerationHandle<VegetationMemoryTelemetryEntry>(
                    VegetationMemorySovereigntyConstants.TelemetryRingBufferId,
                    VegetationMemorySovereigntyConstants.TelemetryFrameCount,
                    VegetationMemorySovereigntyConstants.OwnerSystemId,
                    NativeArrayOptions.ClearMemory);
            }

            if (!hadCursor)
            {
                _vegetationMemoryTelemetryCursorHandle = vault.EnsureGenerationHandle<int>(
                    VegetationMemorySovereigntyConstants.TelemetryCursorBufferId,
                    1,
                    VegetationMemorySovereigntyConstants.OwnerSystemId,
                    NativeArrayOptions.ClearMemory);
            }

            bool ready = TryReadVegetationMemoryTelemetry(out _) &&
                         TryReadVegetationMemoryTelemetryCursor(out NativeArray<int>.ReadOnly cursor) &&
                         cursor.Length > 0;
            if (ready && (!hadRing || !hadCursor))
            {
                RecordVegetationMemoryTelemetry(
                    VegetationMemorySovereigntyConstants.TelemetryRingBufferId,
                    _vegetationMemoryTelemetryHandle.Generation,
                    VegetationMemorySovereigntyConstants.TelemetryFrameCount,
                    VegetationMemorySovereigntyConstants.TelemetryFrameCount,
                    0,
                    0f,
                    VegetationMemoryTelemetryCode.ColdBootRegistered,
                    VegetationMemoryTelemetryPhase.ColdBoot,
                    VegetationMemorySovereigntyConstants.FlagColdBoot,
                    default);
            }

            return ready;
        }

        private void ReleaseVegetationMemoryTelemetryResources()
        {
            ReleaseVegetationMemoryTelemetryResources(_vegetationMemoryVault);
        }

        private void ReleaseVegetationMemoryTelemetryResources(IDataVault vault)
        {
            if (vault != null)
            {
                if (IsExactVegetationMemoryHandle(
                        in _vegetationMemoryTelemetryHandle,
                        VegetationMemorySovereigntyConstants.TelemetryRingBufferId))
                {
                    vault.ReleaseBuffer(in _vegetationMemoryTelemetryHandle);
                }

                if (IsExactVegetationMemoryHandle(
                        in _vegetationMemoryTelemetryCursorHandle,
                        VegetationMemorySovereigntyConstants.TelemetryCursorBufferId))
                {
                    vault.ReleaseBuffer(in _vegetationMemoryTelemetryCursorHandle);
                }
            }

            _vegetationMemoryTelemetryHandle = default;
            _vegetationMemoryTelemetryCursorHandle = default;
            _vegetationMemoryTelemetryDumped = false;
        }

        private void RecordVegetationMemoryTelemetry(
            BufferID bufferId,
            uint generation,
            int expectedLength,
            int actualLength,
            int culledInstances,
            float jobMicroseconds,
            VegetationMemoryTelemetryCode code,
            VegetationMemoryTelemetryPhase phase,
            uint flags,
            float3 position)
        {
            IDataVault vault = _vegetationMemoryVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsExactVegetationMemoryHandle(
                    in _vegetationMemoryTelemetryHandle,
                    VegetationMemorySovereigntyConstants.TelemetryRingBufferId) ||
                !IsExactVegetationMemoryHandle(
                    in _vegetationMemoryTelemetryCursorHandle,
                    VegetationMemorySovereigntyConstants.TelemetryCursorBufferId))
            {
                return;
            }

            if (vault.IsCompactionFenceActive)
                return;

            if (!vault.TryAcquireWriteLock(
                    in _vegetationMemoryTelemetryCursorHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId,
                    out NativeArray<int> cursorBuffer))
            {
                return;
            }

            int cursor = 0;
            try
            {
                if (vault.IsCompactionFenceActive)
                    return;

                if (!cursorBuffer.IsCreated ||
                    cursorBuffer.Length == 0)
                {
                    return;
                }

                cursor = cursorBuffer[0];
                if ((uint)cursor >= (uint)VegetationMemorySovereigntyConstants.TelemetryFrameCount)
                    cursor = 0;

                int nextCursor = cursor + 1;
                if (nextCursor >= VegetationMemorySovereigntyConstants.TelemetryFrameCount)
                    nextCursor = 0;

                cursorBuffer[0] = nextCursor;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in _vegetationMemoryTelemetryCursorHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }

            if (vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(
                    in _vegetationMemoryTelemetryHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId,
                    out NativeArray<VegetationMemoryTelemetryEntry> telemetry))
            {
                return;
            }

            try
            {
                if (!telemetry.IsCreated ||
                    telemetry.Length < VegetationMemorySovereigntyConstants.TelemetryFrameCount)
                {
                    return;
                }

                uint frame = unchecked((uint)SystemDispatcher.CurrentFrameIndex);
                float rawQuality = HomeostasisBrain.GlobalQualityWeight;
                float quality = math.saturate(math.select(1f, rawQuality, math.isfinite(rawQuality)));
                VegetationMemoryTelemetryEntry entry = default;
                entry.BufferId = unchecked((uint)(int)bufferId);
                entry.Generation = generation;
                entry.Frame = frame;
                entry.ExpectedLength = expectedLength;
                entry.ActualLength = actualLength;
                entry.CulledInstances = culledInstances;
                entry.JobMicroseconds = math.select(0f, jobMicroseconds, math.isfinite(jobMicroseconds));
                entry.QualityWeight = quality;
                entry.FailureCode = (ushort)code;
                entry.Phase = (ushort)phase;
                entry.Flags = flags;
                entry.Position = position;
                entry.StateHash = HashVegetationMemoryTelemetry(entry);
                telemetry[cursor] = entry;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in _vegetationMemoryTelemetryHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }

            if ((flags & VegetationMemorySovereigntyConstants.FlagNan) != 0u ||
                code == VegetationMemoryTelemetryCode.NaNDetected)
            {
                DumpVegetationMemoryBlackBox();
            }
        }

        private bool TryReadVegetationMemoryTelemetry(out NativeArray<VegetationMemoryTelemetryEntry>.ReadOnly telemetry)
        {
            telemetry = default;
            IDataVault vault = _vegetationMemoryVault;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   IsExactVegetationMemoryHandle(
                       in _vegetationMemoryTelemetryHandle,
                       VegetationMemorySovereigntyConstants.TelemetryRingBufferId) &&
                   vault.TryReadOnlyHandle(in _vegetationMemoryTelemetryHandle, out telemetry) &&
                   telemetry.IsCreated &&
                   telemetry.Length >= VegetationMemorySovereigntyConstants.TelemetryFrameCount &&
                   !vault.IsCompactionFenceActive;
        }

        private bool TryReadVegetationMemoryTelemetryCursor(out NativeArray<int>.ReadOnly cursor)
        {
            cursor = default;
            IDataVault vault = _vegetationMemoryVault;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   IsExactVegetationMemoryHandle(
                       in _vegetationMemoryTelemetryCursorHandle,
                       VegetationMemorySovereigntyConstants.TelemetryCursorBufferId) &&
                   vault.TryReadOnlyHandle(in _vegetationMemoryTelemetryCursorHandle, out cursor) &&
                   cursor.IsCreated &&
                   cursor.Length > 0 &&
                   !vault.IsCompactionFenceActive;
        }

        private bool TryAcquireVegetationMemoryBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out IDataVault vault,
            out NativeArray<T> buffer)
            where T : struct
        {
            vault = _vegetationMemoryVault;
            buffer = default;
            if (vault == null)
                return false;

            if (vault.IsCompactionFenceActive)
            {
                RecordVegetationMemoryTelemetry(
                    bufferId,
                    handle.Generation,
                    requiredLength,
                    0,
                    0,
                    0f,
                    VegetationMemoryTelemetryCode.CompactionFenceActive,
                    VegetationMemoryTelemetryPhase.Defrag,
                    VegetationMemorySovereigntyConstants.FlagCompactionFence,
                    default);
                return false;
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                math.max(1, requiredLength),
                VegetationMemorySovereigntyConstants.OwnerSystemId,
                options);

            if (vault.IsCompactionFenceActive ||
                !IsExactVegetationMemoryHandle(in handle, bufferId) ||
                !vault.TryAcquireWriteLock(in handle, VegetationMemorySovereigntyConstants.OwnerSystemId, out buffer))
            {
                bool compactionActive = vault.IsCompactionFenceActive;
                RecordVegetationMemoryTelemetry(
                    bufferId,
                    handle.Generation,
                    requiredLength,
                    0,
                    0,
                    0f,
                    compactionActive
                        ? VegetationMemoryTelemetryCode.CompactionFenceActive
                        : VegetationMemoryTelemetryCode.WriteLockContention,
                    VegetationMemoryTelemetryPhase.Defrag,
                    compactionActive
                        ? VegetationMemorySovereigntyConstants.FlagCompactionFence
                        : VegetationMemorySovereigntyConstants.FlagLockContention,
                    default);
                buffer = default;
                return false;
            }

            if (!vault.IsCompactionFenceActive &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            bool fenceActiveAfterLock = vault.IsCompactionFenceActive;
            vault.ReleaseWriteLock(in handle, VegetationMemorySovereigntyConstants.OwnerSystemId);
            RecordVegetationMemoryTelemetry(
                bufferId,
                handle.Generation,
                requiredLength,
                buffer.IsCreated ? buffer.Length : 0,
                0,
                0f,
                fenceActiveAfterLock
                    ? VegetationMemoryTelemetryCode.CompactionFenceActive
                    : VegetationMemoryTelemetryCode.VaultResolveFailed,
                VegetationMemoryTelemetryPhase.Defrag,
                fenceActiveAfterLock
                    ? VegetationMemorySovereigntyConstants.FlagCompactionFence
                    : VegetationMemorySovereigntyConstants.FlagStaleHandle,
                default);
            buffer = default;
            return false;
        }

        private bool TryReadVegetationMemoryBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _vegetationMemoryVault;
            if (vault != null &&
                !vault.IsCompactionFenceActive &&
                IsExactVegetationMemoryHandle(in handle, bufferId) &&
                vault.TryReadHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength &&
                !vault.IsCompactionFenceActive)
            {
                return true;
            }

            buffer = default;
            return false;
        }

        private bool TryReadOnlyVegetationMemoryBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _vegetationMemoryVault;
            if (vault != null &&
                !vault.IsCompactionFenceActive &&
                IsExactVegetationMemoryHandle(in handle, bufferId) &&
                vault.TryReadOnlyHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength &&
                !vault.IsCompactionFenceActive)
            {
                return true;
            }

            buffer = default;
            return false;
        }

        private bool TryPublishVegetationMemorySnapshot<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            NativeArray<T> source,
            int count,
            NativeArrayOptions options)
            where T : struct
        {
            if (!source.IsCreated ||
                count <= 0 ||
                source.Length < count)
            {
                RecordVegetationMemoryTelemetry(
                    bufferId,
                    handle.Generation,
                    count,
                    source.IsCreated ? source.Length : 0,
                    0,
                    0f,
                    VegetationMemoryTelemetryCode.VaultResolveFailed,
                    VegetationMemoryTelemetryPhase.SlowTick,
                    VegetationMemorySovereigntyConstants.FlagCapacity,
                    default);
                return false;
            }

            if (!TryAcquireVegetationMemoryBuffer(
                    ref handle,
                    bufferId,
                    count,
                    options,
                    out IDataVault vault,
                    out NativeArray<T> destination))
            {
                return false;
            }

            try
            {
                NativeArray<T>.Copy(source, 0, destination, 0, count);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in handle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private void ReleaseVegetationMemoryBuffer<T>(ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            IDataVault vault = _vegetationMemoryVault;
            if (vault != null && handle.BufferID != 0u && handle.Generation != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void DumpVegetationMemoryBlackBox()
        {
            if (_vegetationMemoryTelemetryDumped ||
                !TryReadVegetationMemoryTelemetry(out NativeArray<VegetationMemoryTelemetryEntry>.ReadOnly telemetry) ||
                !TryReadVegetationMemoryTelemetryCursor(out NativeArray<int>.ReadOnly cursorBuffer))
            {
                return;
            }

            _vegetationMemoryTelemetryDumped = true;
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string dumpPath = Path.Combine(projectRoot, VegetationMemorySovereigntyConstants.DumpRelativePath);
                string dumpDirectory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(dumpDirectory))
                    Directory.CreateDirectory(dumpDirectory);

                using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(VegetationMemorySovereigntyConstants.DumpMagic);
                writer.Write(VegetationMemorySovereigntyConstants.DumpVersion);
                writer.Write(VegetationMemorySovereigntyConstants.TelemetryFrameCount);
                writer.Write(VegetationMemorySovereigntyConstants.TelemetryEntryStrideBytes);
                writer.Write(cursorBuffer[0]);
                writer.Flush();
                for (int i = 0; i < telemetry.Length; i++)
                {
                    VegetationMemoryTelemetryEntry entry = telemetry[i];
                    unsafe
                    {
                        stream.Write(new ReadOnlySpan<byte>(
                            UnsafeUtility.AddressOf(ref entry),
                            VegetationMemorySovereigntyConstants.TelemetryEntryStrideBytes));
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static bool IsExactVegetationMemoryHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) && handle.Generation != 0u;
        }

        private static ulong HashVegetationMemoryTelemetry(VegetationMemoryTelemetryEntry entry)
        {
            ulong hash = 1469598103934665603UL;
            hash = MixVegetationMemoryHash(hash, entry.BufferId);
            hash = MixVegetationMemoryHash(hash, entry.Generation);
            hash = MixVegetationMemoryHash(hash, entry.Frame);
            hash = MixVegetationMemoryHash(hash, unchecked((uint)entry.ExpectedLength));
            hash = MixVegetationMemoryHash(hash, unchecked((uint)entry.ActualLength));
            hash = MixVegetationMemoryHash(hash, unchecked((uint)entry.CulledInstances));
            hash = MixVegetationMemoryHash(hash, math.asuint(entry.JobMicroseconds));
            hash = MixVegetationMemoryHash(hash, math.asuint(entry.QualityWeight));
            hash = MixVegetationMemoryHash(hash, entry.FailureCode);
            hash = MixVegetationMemoryHash(hash, entry.Phase);
            hash = MixVegetationMemoryHash(hash, entry.Flags);
            hash = MixVegetationMemoryHash(hash, math.asuint(entry.Position.x));
            hash = MixVegetationMemoryHash(hash, math.asuint(entry.Position.y));
            hash = MixVegetationMemoryHash(hash, math.asuint(entry.Position.z));
            return hash;
        }

        private static ulong MixVegetationMemoryHash(ulong hash, uint value)
        {
            hash ^= value;
            hash *= 1099511628211UL;
            return hash;
        }
    }
}
