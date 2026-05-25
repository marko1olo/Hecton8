using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// DataVault-backed runtime authority for UberNoir shader feature gates and blackbox telemetry.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Rendering/Uber Noir Runtime Bridge")]
    public sealed class HectonUberNoirRuntimeBridge : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int TelemetryCapacity = 300;
        private const int TelemetryEntrySizeBytes = 48;
        private const int RecoveryFramesRequired = 120;
        private const float StressShedThreshold = 0.8f;
        private const float StressRecoveryThreshold = 0.72f;
        private const float FeatureMaskEpsilon = 0.001f;
        private const uint DumpMagic = 0x55424E52u; // UBNR
        private const string IntegratorDumpFileName = "Dump_UBER_NOIR_INTEGRATOR.bin";
        private const string IntegratorH8DumpFileName = "Dump_UBER_NOIR_INTEGRATOR.h8dump";
        private const string ExtinctionDumpFileName = "Dump_EXTINCTION_LUT_SAMPLER.bin";
        private const string ExtinctionH8DumpFileName = "Dump_EXTINCTION_LUT_SAMPLER.h8dump";

        private const uint FeaturePom = 1u << 0;
        private const uint FeatureScreenRefraction = 1u << 3;
        private const uint FeatureSurvivalPressure = 1u << 4;
        private const uint FeatureHomeostasisShed = 1u << 5;
        private const uint FeatureHullDents = 1u << 6;
        private const uint FeatureBlueNoiseDither = 1u << 7;
        private const uint FeatureWakeSilt = 1u << 8;
        private const uint FeatureVisualOverkill = 1u << 9;

        private const uint TelemetryFlagLayoutFault = 1u << 0;
        private const uint TelemetryFlagNonFinite = 1u << 1;
        private const uint TelemetryFlagVaultUnavailable = 1u << 2;

        private static HectonUberNoirRuntimeBridge s_runtimeInstance;

        private IDataVault _dataVault;
        private VaultGenerationHandle<UberNoirShaderTelemetryEntry> _telemetryHandle;
        private Vector4 _lastRuntimeParams = new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);
        private float _lastFeatureMask = float.NaN;
        private int _telemetryCursor;
        private int _recoveryFrames;
        private bool _stressShedLatched;
        private bool _registeredLateFrame;
        private bool _hotSwapListenerRegistered;
        private bool _dumpedFault;

        /// <summary>
        /// Fixed-size shader feature telemetry entry for ARM64/Quest-safe DataVault storage.
        /// </summary>
        [StructLayout(LayoutKind.Explicit, Size = TelemetryEntrySizeBytes)]
        public struct UberNoirShaderTelemetryEntry
        {
            public const int SizeBytes = TelemetryEntrySizeBytes;

            [FieldOffset(0)]
            public uint Frame;
            [FieldOffset(4)]
            public uint FeatureMask;
            [FieldOffset(8)]
            public float SystemStress01;
            [FieldOffset(12)]
            public float HighCostAllowed01;
            [FieldOffset(16)]
            public float VisualOverkill01;
            [FieldOffset(20)]
            public uint QualityWeightByte;
            [FieldOffset(24)]
            public uint Flags;
            [FieldOffset(28)]
            public uint StateHash;
            [FieldOffset(32)]
            public float PomEnabled01;
            [FieldOffset(36)]
            public float ReservedVisualDetail01;
            [FieldOffset(40)]
            public float Refraction01;
            [FieldOffset(44)]
            public float Reserved0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_runtimeInstance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureSceneRuntime()
        {
            if (!Application.isPlaying || s_runtimeInstance != null)
                return;

            // COLD ALLOC: GameObject[1] - fallback scene runtime bridge - owner: HectonUberNoirRuntimeBridge
            GameObject host = new GameObject("H8_UberNoirRuntimeBridge");
            host.hideFlags = HideFlags.DontSave;
            host.AddComponent<HectonUberNoirRuntimeBridge>();
        }

        private void Awake()
        {
            if (s_runtimeInstance != null && !ReferenceEquals(s_runtimeInstance, this))
            {
                enabled = false;
                return;
            }

            s_runtimeInstance = this;
            CacheDataVaultCold(forceRefresh: true);
            TryRegisterHotSwapListener();
            EnsureTelemetryBuffer();
            UploadShaderGlobals(0f, 1f, 0u, 0f, force: true);
        }

        private void OnEnable()
        {
            if (s_runtimeInstance != null && !ReferenceEquals(s_runtimeInstance, this))
            {
                enabled = false;
                return;
            }

            s_runtimeInstance = this;
            CacheDataVaultCold(forceRefresh: false);
            TryRegisterHotSwapListener();
            EnsureTelemetryBuffer();
            TryRegisterLateFrameTickable();
        }

        private void OnDisable()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            TryUnregisterHotSwapListener();
            UploadShaderGlobals(0f, 1f, 0u, 0f, force: true);
            ReleaseTelemetryBuffer();
            _dataVault = null;
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            ReleaseTelemetryBuffer();
            if (ReferenceEquals(s_runtimeInstance, this))
                s_runtimeInstance = null;
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                _registeredLateFrame = false;
                if (currentService != null && isActiveAndEnabled)
                    TryRegisterLateFrameTickable();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            ReleaseVaultBuffer(previousService as IDataVault ?? _dataVault, ref _telemetryHandle);
            _dataVault = currentService as IDataVault;
        }

        /// <summary>
        /// Publishes end-of-frame shader feature state and pushes the 300-frame telemetry ring.
        /// </summary>
        public void LateFrameTick()
        {
            if (!ValidateTelemetryLayout())
            {
                DumpBlackBox(TelemetryFlagLayoutFault);
                return;
            }

            float stress01 = ResolveSystemStress01();
            bool stressShed = ResolveStressShed(stress01);
            float quality01 = ResolveGlobalQualityWeight01();
            float lowTierWeight01 = ResolveLowTierWeight01(quality01);
            float hardwareCeiling01 = ResolveHardwareVisualCeiling01(quality01);
            float stressAllowance01 = 1f - Smooth01(math.saturate((stress01 - StressRecoveryThreshold) * math.rcp(math.max(0.0001f, StressShedThreshold - StressRecoveryThreshold))));
            float highCostAllowed01 = quality01 * hardwareCeiling01 * stressAllowance01;
            if (stressShed)
                highCostAllowed01 = math.min(highCostAllowed01, 0.05f);
            float visualOverkill01 = Smooth01(math.saturate((quality01 - 0.78f) * math.rcp(0.22f))) *
                                     Smooth01(hardwareCeiling01) *
                                     stressAllowance01;
            uint featureMask = BuildFeatureMask(lowTierWeight01, stressShed, highCostAllowed01, visualOverkill01);

            if (!math.isfinite(stress01) || !math.isfinite(highCostAllowed01) || !math.isfinite(visualOverkill01))
            {
                DumpBlackBox(TelemetryFlagNonFinite);
                stress01 = 0f;
                highCostAllowed01 = 0f;
                visualOverkill01 = 0f;
                featureMask |= FeatureHomeostasisShed;
            }

            UploadShaderGlobals(stress01, highCostAllowed01, featureMask, visualOverkill01, force: false);
            PushBlackBox(stress01, highCostAllowed01, visualOverkill01, lowTierWeight01, quality01, featureMask);
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void CacheDataVaultCold(bool forceRefresh)
        {
            if (!forceRefresh && _dataVault != null)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(_dataVault, vault))
            {
                ReleaseTelemetryBuffer();
                _dataVault = vault;
            }
        }

        private bool EnsureTelemetryBuffer()
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                _telemetryHandle = default;
                return false;
            }

            if (IsVaultHandleCreated(in _telemetryHandle) &&
                vault.TryResolveHandle(in _telemetryHandle, out NativeArray<UberNoirShaderTelemetryEntry> currentRing) &&
                currentRing.IsCreated &&
                currentRing.Length >= TelemetryCapacity)
            {
                return true;
            }

            _telemetryHandle = default;
            if (vault.TryGetGenerationHandle(
                    BufferID.ShaderFeatureTelemetryRing,
                    out VaultGenerationHandle<UberNoirShaderTelemetryEntry> existing) &&
                vault.TryResolveHandle(in existing, out NativeArray<UberNoirShaderTelemetryEntry> existingRing) &&
                existingRing.IsCreated &&
                existingRing.Length >= TelemetryCapacity)
            {
                _telemetryHandle = existing;
                return true;
            }

            if (vault.IsAllocationLocked)
                return false;

            VaultGenerationHandle<UberNoirShaderTelemetryEntry> acquired = vault.EnsureGenerationHandle<UberNoirShaderTelemetryEntry>(
                BufferID.ShaderFeatureTelemetryRing,
                TelemetryCapacity,
                SystemID.GraphicsScalability,
                NativeArrayOptions.ClearMemory);
            if (!IsVaultHandleCreated(in acquired) ||
                !vault.TryResolveHandle(in acquired, out NativeArray<UberNoirShaderTelemetryEntry> acquiredRing) ||
                !acquiredRing.IsCreated ||
                acquiredRing.Length < TelemetryCapacity)
            {
                _telemetryHandle = default;
                return false;
            }

            _telemetryHandle = acquired;
            return true;
        }

        private void PushBlackBox(
            float stress01,
            float highCostAllowed01,
            float visualOverkill01,
            float lowTierWeight01,
            float quality01,
            uint featureMask)
        {
            if (!EnsureTelemetryBuffer())
                return;

            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryLockBuffer(BufferID.ShaderFeatureTelemetryRing, SystemID.GraphicsScalability))
                return;

            try
            {
                if (!vault.TryResolveHandle(in _telemetryHandle, out NativeArray<UberNoirShaderTelemetryEntry> ring))
                    return;

                if (!ring.IsCreated || ring.Length < TelemetryCapacity)
                    return;

                uint qualityByte = EncodeQualityWeightByte(quality01);
                UberNoirShaderTelemetryEntry entry = default;
                entry.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
                entry.FeatureMask = featureMask;
                entry.SystemStress01 = stress01;
                entry.HighCostAllowed01 = highCostAllowed01;
                entry.VisualOverkill01 = visualOverkill01;
                entry.QualityWeightByte = qualityByte;
                entry.Flags = 0u;
                uint stressBucket = (uint)math.round(math.saturate(stress01) * 1000f);
                uint highCostBucket = (uint)math.round(math.saturate(highCostAllowed01) * 1000f);
                uint overkillBucket = (uint)math.round(math.saturate(visualOverkill01) * 1000f);
                uint lowTierBucket = (uint)math.round(math.saturate(lowTierWeight01) * 1000f);
                entry.StateHash = Mix(featureMask ^ (stressBucket << 12) ^ (qualityByte << 24) ^ (highCostBucket << 2) ^ (overkillBucket << 14) ^ (lowTierBucket << 4));
                entry.PomEnabled01 = math.saturate(highCostAllowed01);
                entry.ReservedVisualDetail01 = math.saturate(highCostAllowed01);
                entry.Refraction01 = math.saturate(highCostAllowed01);
                entry.Reserved0 = math.saturate(lowTierWeight01);
                ring[_telemetryCursor] = entry;

                _telemetryCursor++;
                if (_telemetryCursor >= TelemetryCapacity)
                    _telemetryCursor = 0;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.ShaderFeatureTelemetryRing, SystemID.GraphicsScalability);
            }
        }

        private void DumpBlackBox(uint reasonFlags)
        {
            if (_dumpedFault)
                return;

            if (!EnsureTelemetryBuffer())
            {
                WriteEmptyBlackBox(reasonFlags | TelemetryFlagVaultUnavailable);
                return;
            }

            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryLockBuffer(BufferID.ShaderFeatureTelemetryRing, SystemID.GraphicsScalability))
            {
                WriteEmptyBlackBox(reasonFlags | TelemetryFlagVaultUnavailable);
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string logDirectory = Path.Combine(projectRoot, "Docs", "AgentLogs");
            Span<UberNoirShaderTelemetryEntry> snapshot = stackalloc UberNoirShaderTelemetryEntry[TelemetryCapacity];
            int telemetryCursor = _telemetryCursor;
            int entryCount = 0;
            try
            {
                if (!vault.TryResolveHandle(in _telemetryHandle, out NativeArray<UberNoirShaderTelemetryEntry> ring))
                {
                    WriteEmptyBlackBox(reasonFlags | TelemetryFlagVaultUnavailable);
                    return;
                }

                if (!ring.IsCreated || ring.Length < TelemetryCapacity)
                {
                    WriteEmptyBlackBox(reasonFlags | TelemetryFlagVaultUnavailable);
                    return;
                }

                entryCount = math.min(TelemetryCapacity, ring.Length);
                telemetryCursor = _telemetryCursor;
                for (int i = 0; i < entryCount; i++)
                    snapshot[i] = ring[i];
            }
            catch (Exception)
            {
                _dumpedFault = false;
                return;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.ShaderFeatureTelemetryRing, SystemID.GraphicsScalability);
            }

            try
            {
                _dumpedFault = true;
                Directory.CreateDirectory(logDirectory);
                WriteBlackBoxFile(Path.Combine(logDirectory, IntegratorDumpFileName), reasonFlags, telemetryCursor, snapshot, entryCount);
                WriteBlackBoxFile(Path.Combine(logDirectory, IntegratorH8DumpFileName), reasonFlags, telemetryCursor, snapshot, entryCount);
                WriteBlackBoxFile(Path.Combine(logDirectory, ExtinctionDumpFileName), reasonFlags, telemetryCursor, snapshot, entryCount);
                WriteBlackBoxFile(Path.Combine(logDirectory, ExtinctionH8DumpFileName), reasonFlags, telemetryCursor, snapshot, entryCount);
            }
            catch (Exception)
            {
                _dumpedFault = false;
            }
        }

        private void WriteEmptyBlackBox(uint reasonFlags)
        {
            if (_dumpedFault)
                return;

            _dumpedFault = true;
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                string logDirectory = Path.Combine(projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(logDirectory);
                WriteEmptyBlackBoxFile(Path.Combine(logDirectory, IntegratorDumpFileName), reasonFlags, _telemetryCursor);
                WriteEmptyBlackBoxFile(Path.Combine(logDirectory, IntegratorH8DumpFileName), reasonFlags, _telemetryCursor);
                WriteEmptyBlackBoxFile(Path.Combine(logDirectory, ExtinctionDumpFileName), reasonFlags, _telemetryCursor);
                WriteEmptyBlackBoxFile(Path.Combine(logDirectory, ExtinctionH8DumpFileName), reasonFlags, _telemetryCursor);
            }
            catch (Exception)
            {
                _dumpedFault = false;
            }
        }

        private static void WriteBlackBoxFile(
            string dumpPath,
            uint reasonFlags,
            int telemetryCursor,
            ReadOnlySpan<UberNoirShaderTelemetryEntry> ring,
            int entryCount)
        {
            using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            using BinaryWriter writer = new BinaryWriter(stream);
            int wrappedCursor = telemetryCursor % math.max(entryCount, 1);
            writer.Write(DumpMagic);
            writer.Write(reasonFlags);
            writer.Write(wrappedCursor);
            writer.Write(entryCount);
            for (int i = 0; i < entryCount; i++)
            {
                UberNoirShaderTelemetryEntry entry = ring[(wrappedCursor + i) % entryCount];
                writer.Write(entry.Frame);
                writer.Write(entry.FeatureMask);
                writer.Write(entry.SystemStress01);
                writer.Write(entry.HighCostAllowed01);
                writer.Write(entry.VisualOverkill01);
                writer.Write(entry.QualityWeightByte);
                writer.Write(entry.Flags);
                writer.Write(entry.StateHash);
                writer.Write(entry.PomEnabled01);
                writer.Write(entry.ReservedVisualDetail01);
                writer.Write(entry.Refraction01);
                writer.Write(entry.Reserved0);
            }
        }

        private static void WriteEmptyBlackBoxFile(string dumpPath, uint reasonFlags, int telemetryCursor)
        {
            using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(DumpMagic);
            writer.Write(reasonFlags);
            writer.Write(telemetryCursor);
            writer.Write(0);
        }

        private void UploadShaderGlobals(float stress01, float highCostAllowed01, uint featureMask, float visualOverkill01, bool force)
        {
            float featureMaskFloat = featureMask & 0x00FFFFFFu;
            Vector4 runtimeParams = new Vector4(stress01, highCostAllowed01, featureMaskFloat, visualOverkill01);
            bool runtimeChanged = force || HasVectorChanged(runtimeParams, _lastRuntimeParams);
            bool featureMaskChanged = force || math.abs(_lastFeatureMask - featureMaskFloat) > 0.5f;

            if (!runtimeChanged && !featureMaskChanged)
                return;

            HectonShaderGlobalDataVaultBridge.PublishUberNoirRuntime(runtimeParams, featureMaskFloat);
            _lastRuntimeParams = runtimeParams;
            _lastFeatureMask = featureMaskFloat;
        }

        private bool ResolveStressShed(float stress01)
        {
            if (stress01 > StressShedThreshold)
            {
                _stressShedLatched = true;
                _recoveryFrames = 0;
                return true;
            }

            if (!_stressShedLatched)
                return false;

            if (stress01 < StressRecoveryThreshold)
            {
                _recoveryFrames++;
                if (_recoveryFrames >= RecoveryFramesRequired)
                    _stressShedLatched = false;
            }
            else
            {
                _recoveryFrames = 0;
            }

            return _stressShedLatched;
        }

        private static uint BuildFeatureMask(float lowTierWeight01, bool stressShed, float highCostAllowed01, float visualOverkill01)
        {
            lowTierWeight01 = math.saturate(lowTierWeight01);
            uint mask = FeatureHullDents | FeatureWakeSilt;
            if (lowTierWeight01 > FeatureMaskEpsilon)
                mask |= FeatureSurvivalPressure;

            if (stressShed)
                mask |= FeatureHomeostasisShed;

            if (highCostAllowed01 > FeatureMaskEpsilon)
            {
                mask |= FeaturePom | FeatureScreenRefraction;
                float ditherAllowance01 = highCostAllowed01 * Smooth01(1f - lowTierWeight01);
                if (ditherAllowance01 > FeatureMaskEpsilon)
                    mask |= FeatureBlueNoiseDither;
            }

            if (visualOverkill01 > FeatureMaskEpsilon)
                mask |= FeatureVisualOverkill;

            return mask;
        }

        private static float ResolveSystemStress01()
        {
            float stress01 = HomeostasisBrain.SystemHealthIndex01;
            return math.isfinite(stress01) ? math.saturate(stress01) : 0f;
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float quality01 = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(quality01) ? math.saturate(quality01) : 0f;
        }

        private static float ResolveHardwareVisualCeiling01(float quality01)
        {
            float quality = math.saturate(math.isfinite(quality01) ? quality01 : 0f);
            float visualCurve01 = Smooth01(math.saturate((quality - 0.18f) * 1.2195122f));
            return math.lerp(0.24f, 1f, visualCurve01);
        }

        private static float Smooth01(float value)
        {
            value = math.saturate(value);
            return value * value * (3f - 2f * value);
        }

        private static float ResolveLowTierWeight01(float quality01)
        {
            float quality = math.saturate(math.isfinite(quality01) ? quality01 : 1f);
            float qualityDrivenWeight01 = 1f - Smooth01(math.saturate((quality - 0.18f) * 1.2195122f));
            float hardwareFloor01 = ResolveLowTierFloor01(quality);
            return math.max(qualityDrivenWeight01, hardwareFloor01);
        }

        private static float ResolveLowTierFloor01(float quality01)
        {
            float quality = math.saturate(math.isfinite(quality01) ? quality01 : 1f);
            float survivalPressure01 = 1f - Smooth01(math.saturate((quality - 0.12f) * 1.1363636f));
            return 0.25f * survivalPressure01;
        }

        private static uint EncodeQualityWeightByte(float quality01)
        {
            float quality = math.saturate(math.isfinite(quality01) ? quality01 : 0f);
            return (uint)math.round(quality * 255f);
        }

        private static bool ValidateTelemetryLayout()
        {
            return UnsafeUtility.SizeOf<UberNoirShaderTelemetryEntry>() == TelemetryEntrySizeBytes;
        }

        private void ReleaseTelemetryBuffer()
        {
            ReleaseVaultBuffer(_dataVault, ref _telemetryHandle);
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static void ReleaseVaultBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && IsVaultHandleCreated(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool HasVectorChanged(Vector4 a, Vector4 b)
        {
            return math.abs(a.x - b.x) > 0.0001f ||
                   math.abs(a.y - b.y) > 0.0001f ||
                   math.abs(a.z - b.z) > 0.5f ||
                   math.abs(a.w - b.w) > 0.0001f;
        }

        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }
}
