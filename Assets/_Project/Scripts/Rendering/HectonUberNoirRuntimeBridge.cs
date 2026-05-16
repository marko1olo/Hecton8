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
        private const uint DumpMagic = 0x55424E52u; // UBNR
        private const string IntegratorDumpFileName = "Dump_UBER_NOIR_INTEGRATOR.bin";

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
        /// Fixed-size Pack=1 shader feature telemetry entry for ARM64/Quest-safe DataVault storage.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = TelemetryEntrySizeBytes)]
        public struct UberNoirShaderTelemetryEntry
        {
            public uint Frame;
            public uint FeatureMask;
            public float SystemStress01;
            public float HighCostAllowed01;
            public float VisualOverkill01;
            public uint QualityTier;
            public uint Flags;
            public uint StateHash;
            public float PomEnabled01;
            public float SecondaryCaustics01;
            public float Refraction01;
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
            float highCostAllowed01 = lowTier || stressShed ? 0f : 1f;
            float visualOverkill01 = !lowTier && !stressShed && IsHighTier(GlobalRegistry.ScalabilityTier) ? 1f : 0f;
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
            if (vault == null || !vault.TryLockBuffer(BufferID.ShaderFeatureTelemetryRing))
                return;

            try
            {
                var ring = _telemetryHandle.Resolve(vault);
                if (!ring.IsCreated || ring.Length < TelemetryCapacity)
                    return;

                uint tier = (uint)GlobalRegistry.ScalabilityTier;
                ring[_telemetryCursor] = new UberNoirShaderTelemetryEntry
                {
                    Frame = (uint)math.max(0, Time.frameCount),
                    FeatureMask = featureMask,
                    SystemStress01 = stress01,
                    HighCostAllowed01 = highCostAllowed01,
                    VisualOverkill01 = visualOverkill01,
                    QualityTier = tier,
                    Flags = 0u,
                    StateHash = Mix(featureMask ^ ((uint)math.round(stress01 * 1000f) << 12) ^ (tier << 24)),
                    PomEnabled01 = (featureMask & FeaturePom) != 0u ? 1f : 0f,
                    SecondaryCaustics01 = (featureMask & FeatureSecondaryCaustics) != 0u ? 1f : 0f,
                    Refraction01 = (featureMask & FeatureScreenRefraction) != 0u ? 1f : 0f,
                    Reserved0 = 0f
                };

                _telemetryCursor++;
                if (_telemetryCursor >= TelemetryCapacity)
                    _telemetryCursor = 0;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.ShaderFeatureTelemetryRing);
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
            if (vault == null || !vault.TryLockBuffer(BufferID.ShaderFeatureTelemetryRing))
            {
                WriteEmptyBlackBox(reasonFlags | TelemetryFlagVaultUnavailable);
                return;
            }

            try
            {
                var ring = _telemetryHandle.Resolve(vault);
                if (!ring.IsCreated || ring.Length < TelemetryCapacity)
                {
                    WriteEmptyBlackBox(reasonFlags | TelemetryFlagVaultUnavailable);
                    return;
                }

                _dumpedFault = true;
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                string logDirectory = Path.Combine(projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(logDirectory);
                WriteBlackBoxFile(Path.Combine(logDirectory, IntegratorDumpFileName), reasonFlags, _telemetryCursor, ring);
            }
            catch (Exception)
            {
                _dumpedFault = false;
            }
            finally
            {
                vault.TryUnlockBuffer(BufferID.ShaderFeatureTelemetryRing);
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
            NativeArray<UberNoirShaderTelemetryEntry> ring)
        {
            using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            using BinaryWriter writer = new BinaryWriter(stream);
            int entryCount = math.min(TelemetryCapacity, ring.Length);
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

            if (highCostAllowed01 > 0.5f)
            {
                mask |= FeaturePom | FeatureSecondaryCaustics | FeatureScreenRefraction;
                if (!lowTier)
                    mask |= FeatureBlueNoiseDither;
            }

            if (visualOverkill01 > 0.5f)
                mask |= FeatureVisualOverkill;

            return mask;
        }

        private static float ResolveSystemStress01()
        {
            float stress01 = HomeostasisBrain.SystemHealthIndex01;
            return math.isfinite(stress01) ? math.saturate(stress01) : 0f;
        }

        private static bool IsLowTier(HectonQualityTier tier, byte profileByte)
        {
            return profileByte == ScalabilityTierProfiles.LowMx350 ||
                   tier == HectonQualityTier.Unknown ||
                   tier == HectonQualityTier.Low ||
                   tier == HectonQualityTier.Mx350;
        }

        private static bool IsHighTier(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.High || tier == HectonQualityTier.Ultra;
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
