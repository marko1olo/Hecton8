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
    public sealed class HectonUberNoirRuntimeBridge : MonoBehaviour, ILateFrameTickable
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
        private const uint FeatureAnalyticalCaustics = 1u << 1;
        private const uint FeatureSecondaryCaustics = 1u << 2;
        private const uint FeatureScreenRefraction = 1u << 3;
        private const uint FeatureLowTier = 1u << 4;
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
        private VaultBufferHandle<UberNoirShaderTelemetryEntry> _telemetryHandle;
        private Vector4 _lastRuntimeParams = new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);
        private float _lastFeatureMask = float.NaN;
        private int _telemetryCursor;
        private int _recoveryFrames;
        private bool _stressShedLatched;
        private bool _registeredLateFrame;
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
            public uint QualityTier;
            [FieldOffset(24)]
            public uint Flags;
            [FieldOffset(28)]
            public uint StateHash;
            [FieldOffset(32)]
            public float PomEnabled01;
            [FieldOffset(36)]
            public float SecondaryCaustics01;
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

            UploadShaderGlobals(0f, 1f, 0u, 0f, force: true);
            _dataVault = null;
            _telemetryHandle = default;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(s_runtimeInstance, this))
                s_runtimeInstance = null;
        }

        /// <summary>
        /// Publishes end-of-frame shader feature state and pushes the 300-frame telemetry ring.
        /// </summary>
        public void LateFrameTick()
        {
            if (!_registeredLateFrame)
                TryRegisterLateFrameTickable();

            if (!ValidateTelemetryLayout())
            {
                DumpBlackBox(TelemetryFlagLayoutFault);
                return;
            }

            float stress01 = ResolveSystemStress01();
            bool lowTier = IsLowTier(GlobalRegistry.ScalabilityTier, GlobalRegistry.ScalabilityTierProfileByte);
            bool stressShed = ResolveStressShed(stress01);
            float quality01 = ResolveGlobalQualityWeight01();
            float hardwareCeiling01 = ResolveHardwareVisualCeiling01(GlobalRegistry.ScalabilityTier, GlobalRegistry.ScalabilityTierProfileByte);
            float stressAllowance01 = 1f - Smooth01(math.saturate((stress01 - StressRecoveryThreshold) * math.rcp(math.max(0.0001f, StressShedThreshold - StressRecoveryThreshold))));
            float highCostAllowed01 = quality01 * hardwareCeiling01 * stressAllowance01;
            if (stressShed)
                highCostAllowed01 = math.min(highCostAllowed01, 0.05f);
            float visualOverkill01 = Smooth01(math.saturate((quality01 - 0.78f) * math.rcp(0.22f))) *
                                     Smooth01(hardwareCeiling01) *
                                     stressAllowance01;
            uint featureMask = BuildFeatureMask(lowTier, stressShed, highCostAllowed01, visualOverkill01);

            if (!math.isfinite(stress01) || !math.isfinite(highCostAllowed01) || !math.isfinite(visualOverkill01))
            {
                DumpBlackBox(TelemetryFlagNonFinite);
                stress01 = 0f;
                highCostAllowed01 = 0f;
                visualOverkill01 = 0f;
                featureMask |= FeatureHomeostasisShed;
            }

            UploadShaderGlobals(stress01, highCostAllowed01, featureMask, visualOverkill01, force: false);
            PushBlackBox(stress01, highCostAllowed01, visualOverkill01, featureMask);
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private bool EnsureTelemetryBuffer()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                _dataVault = null;
                _telemetryHandle = default;
                return false;
            }

            if (!ReferenceEquals(_dataVault, vault))
            {
                _dataVault = vault;
                _telemetryHandle = default;
            }

            if (!_telemetryHandle.IsCreated ||
                _telemetryHandle.BufferId != BufferID.ShaderFeatureTelemetryRing ||
                _telemetryHandle.Length < TelemetryCapacity)
            {
                if (vault.TryGetBufferHandle(
                    BufferID.ShaderFeatureTelemetryRing,
                    out VaultBufferHandle<UberNoirShaderTelemetryEntry> existing) &&
                    existing.IsCreated &&
                    existing.Length >= TelemetryCapacity)
                {
                    _telemetryHandle = existing;
                    return true;
                }

                if (vault.IsAllocationLocked)
                    return false;

                _telemetryHandle = vault.GetBufferHandle<UberNoirShaderTelemetryEntry>(
                    BufferID.ShaderFeatureTelemetryRing,
                    TelemetryCapacity,
                    SystemID.GraphicsScalability,
                    NativeArrayOptions.ClearMemory);
            }

            return _telemetryHandle.IsCreated && _telemetryHandle.Length >= TelemetryCapacity;
        }

        private void PushBlackBox(float stress01, float highCostAllowed01, float visualOverkill01, uint featureMask)
        {
            if (!EnsureTelemetryBuffer())
                return;

            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryLockBuffer(BufferID.ShaderFeatureTelemetryRing, SystemID.GraphicsScalability))
                return;

            try
            {
                var ring = _telemetryHandle.Resolve(vault);
                if (!ring.IsCreated || ring.Length < TelemetryCapacity)
                    return;

                uint tier = (uint)GlobalRegistry.ScalabilityTier;
                UberNoirShaderTelemetryEntry entry = default;
                entry.Frame = (uint)math.max(0, Time.frameCount);
                entry.FeatureMask = featureMask;
                entry.SystemStress01 = stress01;
                entry.HighCostAllowed01 = highCostAllowed01;
                entry.VisualOverkill01 = visualOverkill01;
                entry.QualityTier = tier;
                entry.Flags = 0u;
                uint stressBucket = (uint)math.round(math.saturate(stress01) * 1000f);
                uint highCostBucket = (uint)math.round(math.saturate(highCostAllowed01) * 1000f);
                uint overkillBucket = (uint)math.round(math.saturate(visualOverkill01) * 1000f);
                entry.StateHash = Mix(featureMask ^ (stressBucket << 12) ^ (tier << 24) ^ (highCostBucket << 2) ^ (overkillBucket << 14));
                entry.PomEnabled01 = math.saturate(highCostAllowed01);
                entry.SecondaryCaustics01 = math.saturate(highCostAllowed01);
                entry.Refraction01 = math.saturate(highCostAllowed01);
                entry.Reserved0 = 0f;
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
                NativeArray<UberNoirShaderTelemetryEntry> ring = _telemetryHandle.Resolve(vault);
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
                writer.Write(entry.QualityTier);
                writer.Write(entry.Flags);
                writer.Write(entry.StateHash);
                writer.Write(entry.PomEnabled01);
                writer.Write(entry.SecondaryCaustics01);
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

        private static uint BuildFeatureMask(bool lowTier, bool stressShed, float highCostAllowed01, float visualOverkill01)
        {
            uint mask = FeatureAnalyticalCaustics | FeatureHullDents | FeatureWakeSilt;
            if (lowTier)
                mask |= FeatureLowTier;

            if (stressShed)
                mask |= FeatureHomeostasisShed;

            if (highCostAllowed01 > FeatureMaskEpsilon)
            {
                mask |= FeaturePom | FeatureSecondaryCaustics | FeatureScreenRefraction;
                if (!lowTier)
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

        private static float ResolveHardwareVisualCeiling01(HectonQualityTier tier, byte profileByte)
        {
            if (profileByte == ScalabilityTierProfiles.LowMx350 || tier == HectonQualityTier.Unknown || tier == HectonQualityTier.Low)
                return 0.24f;

            if (tier == HectonQualityTier.Mx350)
                return 0.34f;

            if (tier == HectonQualityTier.Mid)
                return 0.58f;

            if (tier == HectonQualityTier.High)
                return 0.82f;

            return 1f;
        }

        private static float Smooth01(float value)
        {
            value = math.saturate(value);
            return value * value * (3f - 2f * value);
        }

        private static bool IsLowTier(HectonQualityTier tier, byte profileByte)
        {
            return profileByte == ScalabilityTierProfiles.LowMx350 ||
                   tier == HectonQualityTier.Unknown ||
                   tier == HectonQualityTier.Low ||
                   tier == HectonQualityTier.Mx350;
        }

        private static bool ValidateTelemetryLayout()
        {
            return UnsafeUtility.SizeOf<UberNoirShaderTelemetryEntry>() == TelemetryEntrySizeBytes;
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
