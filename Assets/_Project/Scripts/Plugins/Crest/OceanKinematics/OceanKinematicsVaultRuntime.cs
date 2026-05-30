using System;
using System.IO;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    /// <summary>
    /// Cold-phase DataVault owner for ocean kinematics native buffers and black-box telemetry.
    /// </summary>
    public static unsafe class OceanKinematicsVaultRuntime
    {
        private const SystemID OwnerSystemId = SystemID.Fluid;
        private const uint DumpFaultFlag = 1u << 30;
        private static IDataVault _dataVault;
        private static VaultGenerationHandle<OceanKinematicsSampleRequestDTO> _requestsHandle;
        private static VaultGenerationHandle<FluidSampleResultDTO> _resultsHandle;
        private static VaultGenerationHandle<GerstnerWaveDTO> _wavesHandle;
        private static VaultGenerationHandle<int> _queueCountersHandle;
        private static VaultGenerationHandle<OceanKinematicsTuningDTO> _tuningHandle;
        private static VaultGenerationHandle<OceanMacroStateDTO> _macroStateHandle;
        private static VaultGenerationHandle<OceanKinematicsRollbackFenceDTO> _rollbackFenceHandle;
        private static VaultGenerationHandle<OceanKinematicsTelemetryEntry> _telemetryRingHandle;
        private static VaultGenerationHandle<int> _telemetryCursorHandle;
        private static VaultGenerationHandle<OceanCachedFluidSampleDTO> _cachedResultsHandle;
        private static VaultGenerationHandle<byte> _csvScratchHandle;

        public ref struct Views
        {
            public NativeArray<OceanKinematicsSampleRequestDTO> Requests;
            public NativeArray<FluidSampleResultDTO> Results;
            public NativeArray<GerstnerWaveDTO> Waves;
            public NativeArray<int> QueueCounters;
            public NativeArray<OceanKinematicsTuningDTO> Tuning;
            public NativeArray<OceanMacroStateDTO> MacroState;
            public NativeArray<OceanKinematicsRollbackFenceDTO> RollbackFence;
            public NativeArray<OceanKinematicsTelemetryEntry> TelemetryRing;
            public NativeArray<int> TelemetryCursor;
            public NativeArray<OceanCachedFluidSampleDTO> CachedResults;
            public NativeArray<byte> CsvScratch;
        }

        public static bool EnsureBuffers(IDataVault vault, out Views views)
        {
            views = default;
            if (vault == null)
                return false;

            if (!ReferenceEquals(_dataVault, vault))
            {
                ClearHandles();
                _dataVault = vault;
            }

            if (!EnsureVaultBuffer(vault, ref _requestsHandle, OceanKinematicsBufferIds.Requests, OceanKinematicsConstants.RequestCapacity, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBuffer(vault, ref _resultsHandle, OceanKinematicsBufferIds.Results, OceanKinematicsConstants.RequestCapacity, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBuffer(vault, ref _wavesHandle, OceanKinematicsBufferIds.GerstnerWaves, OceanKinematicsConstants.WaveCapacity, NativeArrayOptions.UninitializedMemory) ||
                !EnsureVaultBuffer(vault, ref _queueCountersHandle, OceanKinematicsBufferIds.QueueCounters, OceanKinematicsConstants.QueueCounterCapacity, NativeArrayOptions.ClearMemory) ||
                !EnsureVaultBuffer(vault, ref _tuningHandle, OceanKinematicsBufferIds.Tuning, 1, NativeArrayOptions.ClearMemory) ||
                !EnsureVaultBuffer(vault, ref _macroStateHandle, OceanKinematicsBufferIds.MacroState, 1, NativeArrayOptions.ClearMemory) ||
                !EnsureVaultBuffer(vault, ref _rollbackFenceHandle, OceanKinematicsBufferIds.RollbackFence, 1, NativeArrayOptions.ClearMemory) ||
                !EnsureVaultBuffer(vault, ref _telemetryRingHandle, OceanKinematicsBufferIds.TelemetryRing, OceanKinematicsConstants.TelemetryCapacity, NativeArrayOptions.ClearMemory) ||
                !EnsureVaultBuffer(vault, ref _telemetryCursorHandle, OceanKinematicsBufferIds.TelemetryCursor, 1, NativeArrayOptions.ClearMemory) ||
                !EnsureVaultBuffer(vault, ref _cachedResultsHandle, OceanKinematicsBufferIds.GpuCachedResults, OceanKinematicsConstants.RequestCapacity, NativeArrayOptions.ClearMemory) ||
                !EnsureVaultBuffer(vault, ref _csvScratchHandle, OceanKinematicsBufferIds.CsvScratch, OceanKinematicsConstants.CsvScratchBytes, NativeArrayOptions.UninitializedMemory))
            {
                return false;
            }

            return TryResolveViews(vault, out views);
        }

        public static bool TryResolveViews(IDataVault vault, out Views views)
        {
            views = default;
            if (vault == null || !ReferenceEquals(_dataVault, vault))
                return false;

            views.Requests = ResolveVaultBuffer(vault, in _requestsHandle);
            views.Results = ResolveVaultBuffer(vault, in _resultsHandle);
            views.Waves = ResolveVaultBuffer(vault, in _wavesHandle);
            views.QueueCounters = ResolveVaultBuffer(vault, in _queueCountersHandle);
            views.Tuning = ResolveVaultBuffer(vault, in _tuningHandle);
            views.MacroState = ResolveVaultBuffer(vault, in _macroStateHandle);
            views.RollbackFence = ResolveVaultBuffer(vault, in _rollbackFenceHandle);
            views.TelemetryRing = ResolveVaultBuffer(vault, in _telemetryRingHandle);
            views.TelemetryCursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
            views.CachedResults = ResolveVaultBuffer(vault, in _cachedResultsHandle);
            views.CsvScratch = ResolveVaultBuffer(vault, in _csvScratchHandle);
            return views.Requests.IsCreated &&
                   views.Results.IsCreated &&
                   views.Waves.IsCreated &&
                   views.QueueCounters.IsCreated &&
                   views.Tuning.IsCreated &&
                   views.MacroState.IsCreated &&
                   views.RollbackFence.IsCreated &&
                   views.TelemetryRing.IsCreated &&
                   views.TelemetryCursor.IsCreated &&
                   views.CachedResults.IsCreated &&
                   views.CsvScratch.IsCreated;
        }

        public static bool TryPublishMacroState(
            IDataVault vault,
            in OceanKinematicsTuningDTO tuning,
            NativeArray<GerstnerWaveDTO> waves,
            int waveCount,
            out OceanMacroStateDTO macroState)
        {
            macroState = default;
            if (!TryResolveViews(vault, out Views views) ||
                views.MacroState.Length == 0 ||
                views.Tuning.Length == 0 ||
                views.RollbackFence.Length == 0)
            {
                return false;
            }

            macroState = BuildMacroState(in tuning, waves, waveCount);
            views.Tuning[0] = tuning;
            views.MacroState[0] = macroState;
            int activeOctaves = ResolveActiveOctaves(tuning.MaxOctaveLimit, waves.IsCreated ? math.min(waveCount, waves.Length) : 0);
            views.RollbackFence[0] = BuildRollbackFence(in macroState, resultStateHash: 0u, queryCount: 0, activeOctaves);
            return true;
        }

        public static bool TryRecordTelemetry(
            IDataVault vault,
            in OceanKinematicsTuningDTO tuning,
            NativeArray<int> queueCounters,
            NativeArray<FluidSampleResultDTO> results,
            int resultCount,
            float burstExecutionMicros,
            uint lastRequestHash)
        {
            if (!TryResolveViews(vault, out Views views) ||
                views.TelemetryRing.Length < OceanKinematicsConstants.TelemetryCapacity ||
                views.TelemetryCursor.Length == 0)
            {
                return false;
            }

            int queryCount = ResolveCounter(queueCounters, OceanKinematicsConstants.QueueCounterPacked, resultCount);
            int depthCulled = ResolveCounter(queueCounters, OceanKinematicsConstants.QueueCounterDepthCulled, 0);
            int fallbackActiveOctaves = ResolveActiveOctaves(tuning.MaxOctaveLimit, OceanKinematicsConstants.WaveCapacity);
            int activeOctaves = ResolveCounter(queueCounters, OceanKinematicsConstants.QueueCounterActiveOctaves, fallbackActiveOctaves);
            int nonFinite = ResolveCounter(queueCounters, OceanKinematicsConstants.QueueCounterNonFinite, 0);
            uint resultHash = unchecked((uint)ResolveCounter(queueCounters, OceanKinematicsConstants.QueueCounterResultHash, unchecked((int)2166136261u)));
            int resultNonFinite = ResolveCounter(queueCounters, OceanKinematicsConstants.QueueCounterResultNonFinite, 0);
            nonFinite += resultNonFinite;
            float micros = math.select(0f, burstExecutionMicros, math.isfinite(burstExecutionMicros));
            uint flags = tuning.Flags;
            if (!math.isfinite(burstExecutionMicros) || micros > 1000f || nonFinite > 0)
                flags |= DumpFaultFlag;

            FluidSampleResultDTO lastResult = default;
            if (results.IsCreated && resultCount > 0)
            {
                int lastIndex = math.min(resultCount, results.Length) - 1;
                if (lastIndex >= 0)
                    lastResult = results[lastIndex];
            }

            OceanKinematicsTelemetryEntry entry = default;
            entry.FrameIndex = tuning.FrameIndex;
            entry.QueryCount = math.max(0, queryCount);
            entry.DepthCulledCount = math.max(0, depthCulled);
            entry.ActiveOctaves = activeOctaves;
            entry.BurstExecutionMicros = math.max(0f, micros);
            entry.GlobalQualityWeight = math.saturate(math.select(1f, tuning.GlobalQualityWeight, math.isfinite(tuning.GlobalQualityWeight)));
            entry.OceanSurfaceY = math.select(0f, tuning.OceanSurfaceY, math.isfinite(tuning.OceanSurfaceY));
            entry.Flags = flags;
            entry.LastRequestHash = lastRequestHash;
            entry.MaxWavePeakHeight = math.max(0f, math.select(0f, tuning.MaxPeakHeight, math.isfinite(tuning.MaxPeakHeight)));
            entry.LastSurfaceVelocity = math.select(float3.zero, lastResult.SurfaceVelocity, math.isfinite(lastResult.SurfaceVelocity));
            entry.NonFiniteCount = unchecked((uint)math.max(0, nonFinite));

            int cursor = views.TelemetryCursor[0];
            if ((uint)cursor >= (uint)views.TelemetryRing.Length)
                cursor = 0;

            views.TelemetryRing[cursor] = entry;
            views.TelemetryCursor[0] = cursor + 1 >= views.TelemetryRing.Length ? 0 : cursor + 1;

            if (views.RollbackFence.IsCreated && views.RollbackFence.Length > 0)
            {
                OceanMacroStateDTO macro = views.MacroState.IsCreated && views.MacroState.Length > 0 ? views.MacroState[0] : BuildMacroState(in tuning, default, 0);
                views.RollbackFence[0] = BuildRollbackFence(in macro, resultHash, queryCount, activeOctaves);
            }

            if ((flags & DumpFaultFlag) != 0u)
                return DumpTelemetry(views.TelemetryRing);

            return true;
        }

        public static bool DumpTelemetry(NativeArray<OceanKinematicsTelemetryEntry> telemetryRing)
        {
            if (!telemetryRing.IsCreated || telemetryRing.Length == 0)
                return false;

            string path = Path.Combine(Directory.GetCurrentDirectory(), OceanKinematicsConstants.DumpRelativePath);
            int count = math.min(telemetryRing.Length, OceanKinematicsConstants.TelemetryCapacity);
            int bytes = count * UnsafeUtility.SizeOf<OceanKinematicsTelemetryEntry>();
            void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetryRing);
            return NativeFaultDumpWriter.TryWriteAll(path, new ReadOnlySpan<byte>(ptr, bytes), bytes);
        }

        private static OceanMacroStateDTO BuildMacroState(
            in OceanKinematicsTuningDTO tuning,
            NativeArray<GerstnerWaveDTO> waves,
            int waveCount)
        {
            OceanMacroStateDTO state = default;
            float quality = math.saturate(math.select(1f, tuning.GlobalQualityWeight, math.isfinite(tuning.GlobalQualityWeight)));
            int activeOctaves = ResolveActiveOctaves(tuning.MaxOctaveLimit, waves.IsCreated ? math.min(waveCount, waves.Length) : 0);
            float peak = math.max(0f, math.select(0f, tuning.MaxPeakHeight, math.isfinite(tuning.MaxPeakHeight)));
            if (waves.IsCreated && activeOctaves > 0)
            {
                peak = 0f;
                float amplitudeMultiplier = math.max(0f, math.select(OceanKinematicsConstants.DefaultAmplitudeMultiplier, tuning.WaveAmplitudeMultiplier, math.isfinite(tuning.WaveAmplitudeMultiplier)));
                for (int i = 0; i < activeOctaves; i++)
                {
                    GerstnerWaveDTO wave = waves[i];
                    if ((wave.Flags & OceanKinematicsConstants.FlagActive) == 0u)
                        continue;

                    float amplitude = math.abs(math.select(0f, wave.Amplitude, math.isfinite(wave.Amplitude))) * amplitudeMultiplier;
                    float steepness = math.saturate(math.select(0f, wave.Steepness, math.isfinite(wave.Steepness)));
                    peak += amplitude * math.max(0.001f, steepness);
                }
            }

            state.RestingWaterHeight = math.select(0f, tuning.OceanSurfaceY, math.isfinite(tuning.OceanSurfaceY));
            state.MaxWavePeakHeight = peak;
            state.OceanSurfaceY = state.RestingWaterHeight;
            state.GlobalQualityWeight = quality;
            state.FrameIndex = tuning.FrameIndex;
            state.Flags = tuning.Flags | OceanKinematicsConstants.FlagActive;
            return state;
        }

        private static OceanKinematicsRollbackFenceDTO BuildRollbackFence(
            in OceanMacroStateDTO macroState,
            uint resultStateHash,
            int queryCount,
            int activeOctaves)
        {
            OceanKinematicsRollbackFenceDTO fence = default;
            fence.FrameIndex = macroState.FrameIndex;
            fence.MacroStateHash = ComputeMacroHash(in macroState);
            fence.ResultStateHash = resultStateHash;
            fence.QueryCount = math.max(0, queryCount);
            fence.OceanSurfaceY = macroState.OceanSurfaceY;
            fence.GlobalQualityWeight = macroState.GlobalQualityWeight;
            fence.Flags = macroState.Flags;
            fence.ActiveOctaves = unchecked((uint)math.max(0, activeOctaves));
            return fence;
        }

        private static bool EnsureVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            if (vault == null || requiredLength <= 0)
                return false;

            if (HasHandle(in handle) &&
                vault.TryResolveHandle(in handle, out NativeArray<T> existing) &&
                existing.IsCreated &&
                existing.Length >= requiredLength)
            {
                return true;
            }

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existingHandle) &&
                HasHandle(in existingHandle) &&
                vault.TryResolveHandle(in existingHandle, out NativeArray<T> existingBuffer) &&
                existingBuffer.IsCreated &&
                existingBuffer.Length >= requiredLength)
            {
                handle = existingHandle;
                return true;
            }

            if (vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                return false;

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, OwnerSystemId, options);
            return HasHandle(in handle) &&
                   vault.TryResolveHandle(in handle, out NativeArray<T> resolved) &&
                   resolved.IsCreated &&
                   resolved.Length >= requiredLength;
        }

        private static NativeArray<T> ResolveVaultBuffer<T>(IDataVault vault, in VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null &&
                HasHandle(in handle) &&
                vault.TryResolveHandle(in handle, out NativeArray<T> buffer) &&
                buffer.IsCreated)
            {
                return buffer;
            }

            return default;
        }

        private static void ClearHandles()
        {
            _requestsHandle = default;
            _resultsHandle = default;
            _wavesHandle = default;
            _queueCountersHandle = default;
            _tuningHandle = default;
            _macroStateHandle = default;
            _rollbackFenceHandle = default;
            _telemetryRingHandle = default;
            _telemetryCursorHandle = default;
            _cachedResultsHandle = default;
            _csvScratchHandle = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasHandle<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveActiveOctaves(int maxOctaveLimit, int availableWaves)
        {
            if (availableWaves <= 0)
                return 0;

            int maxOctaves = math.clamp(maxOctaveLimit, 1, math.max(1, availableWaves));
            return maxOctaves;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveCounter(NativeArray<int> counters, int index, int fallback)
        {
            if (counters.IsCreated && (uint)index < (uint)counters.Length)
                return counters[index];

            return fallback;
        }

        private static uint ComputeMacroHash(in OceanMacroStateDTO state)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, AsUInt32(state.RestingWaterHeight));
            hash = Mix(hash, AsUInt32(state.MaxWavePeakHeight));
            hash = Mix(hash, AsUInt32(state.OceanSurfaceY));
            hash = Mix(hash, state.FrameIndex);
            hash = Mix(hash, state.Flags);
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            hash ^= value & 0xFFu;
            hash *= 16777619u;
            hash ^= (value >> 8) & 0xFFu;
            hash *= 16777619u;
            hash ^= (value >> 16) & 0xFFu;
            hash *= 16777619u;
            hash ^= (value >> 24) & 0xFFu;
            hash *= 16777619u;
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint AsUInt32(float value)
        {
            return *(uint*)&value;
        }
    }
}
