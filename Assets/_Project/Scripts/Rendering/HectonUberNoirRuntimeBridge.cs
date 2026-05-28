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
        private const int TelemetryEntrySizeBytes = 64;
        private const int RecoveryFramesRequired = 120;
        private const float StressShedThreshold = 0.8f;
        private const float StressRecoveryThreshold = 0.72f;
        private const float FeatureMaskEpsilon = 0.001f;
        private const uint DumpMagic = 0x55424E52u; // UBNR
        private const string DumpFileName = "Dump_1335_UberNoirRuntimeBridge.bin";

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
        private const int DumpHeaderSizeBytes = 16;

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
            [FieldOffset(48)]
            private byte _pad0;
            [FieldOffset(49)]
            private byte _pad1;
            [FieldOffset(50)]
            private byte _pad2;
            [FieldOffset(51)]
            private byte _pad3;
            [FieldOffset(52)]
            private byte _pad4;
            [FieldOffset(53)]
            private byte _pad5;
            [FieldOffset(54)]
            private byte _pad6;
            [FieldOffset(55)]
            private byte _pad7;
            [FieldOffset(56)]
            private byte _pad8;
            [FieldOffset(57)]
            private byte _pad9;
            [FieldOffset(58)]
            private byte _pad10;
            [FieldOffset(59)]
            private byte _pad11;
            [FieldOffset(60)]
            private byte _pad12;
            [FieldOffset(61)]
            private byte _pad13;
            [FieldOffset(62)]
            private byte _pad14;
            [FieldOffset(63)]
            private byte _pad15;
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
            EnsureTelemetryBuffer(allowAllocation: true);
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
            EnsureTelemetryBuffer(allowAllocation: true);
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

            RebindDataVaultForLifecycle(currentService as IDataVault, previousService as IDataVault);
            EnsureTelemetryBuffer(allowAllocation: true);
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
            RebindDataVaultForLifecycle(vault);
        }

        private void RebindDataVaultForLifecycle(IDataVault vault, IDataVault releaseVaultOverride = null)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            ReleaseVaultBuffer(_dataVault ?? releaseVaultOverride, ref _telemetryHandle);
            _dataVault = vault;
            _telemetryCursor = 0;
        }

        private bool EnsureTelemetryBuffer(bool allowAllocation)
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                _telemetryHandle = default;
                return false;
            }

            if (TryReadTelemetryRing(vault, in _telemetryHandle, out NativeArray<UberNoirShaderTelemetryEntry>.ReadOnly currentRing) &&
                currentRing.Length >= TelemetryCapacity)
            {
                return true;
            }

            _telemetryHandle = default;
            if (vault.TryGetGenerationHandle(
                    BufferID.ShaderFeatureTelemetryRing,
                    out VaultGenerationHandle<UberNoirShaderTelemetryEntry> existing) &&
                IsTelemetryHandle(in existing) &&
                TryReadTelemetryRing(vault, in existing, out NativeArray<UberNoirShaderTelemetryEntry>.ReadOnly existingRing) &&
                existingRing.Length >= TelemetryCapacity)
            {
                _telemetryHandle = existing;
                return true;
            }

            if (!allowAllocation || vault.IsAllocationLocked)
                return false;

            VaultGenerationHandle<UberNoirShaderTelemetryEntry> acquired = vault.EnsureGenerationHandle<UberNoirShaderTelemetryEntry>(
                BufferID.ShaderFeatureTelemetryRing,
                TelemetryCapacity,
                SystemID.GraphicsScalability,
                NativeArrayOptions.ClearMemory);
            if (!IsVaultHandleCreated(in acquired) ||
                !TryReadTelemetryRing(vault, in acquired, out NativeArray<UberNoirShaderTelemetryEntry>.ReadOnly acquiredRing) ||
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
            if (!EnsureTelemetryBuffer(allowAllocation: false))
                return;

            IDataVault vault = _dataVault;
            if (!TryAcquireTelemetryWriteBuffer(vault, in _telemetryHandle, out NativeArray<UberNoirShaderTelemetryEntry> ring))
                return;

            try
            {
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
                vault.ReleaseWriteLock(in _telemetryHandle, SystemID.GraphicsScalability);
            }
        }

        private void DumpBlackBox(uint reasonFlags)
        {
            if (_dumpedFault)
                return;

            if (!EnsureTelemetryBuffer(allowAllocation: false))
            {
                WriteEmptyBlackBox(reasonFlags | TelemetryFlagVaultUnavailable);
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string logDirectory = Path.Combine(projectRoot, "Docs", "AgentLogs");
            Span<UberNoirShaderTelemetryEntry> snapshot = stackalloc UberNoirShaderTelemetryEntry[TelemetryCapacity];
            int telemetryCursor = _telemetryCursor;
            int entryCount = 0;
            if (!TryCopyTelemetrySnapshot(snapshot, out telemetryCursor, out entryCount))
            {
                WriteEmptyBlackBox(reasonFlags | TelemetryFlagVaultUnavailable);
                return;
            }

            try
            {
                _dumpedFault = true;
                Directory.CreateDirectory(logDirectory);
                WriteBlackBoxFile(Path.Combine(logDirectory, DumpFileName), reasonFlags, telemetryCursor, snapshot, entryCount);
            }
            catch (IOException)
            {
                _dumpedFault = false;
            }
            catch (UnauthorizedAccessException)
            {
                _dumpedFault = false;
            }
            catch (ObjectDisposedException)
            {
                _dumpedFault = false;
            }
            catch (InvalidOperationException)
            {
                _dumpedFault = false;
            }
            catch (ArgumentException)
            {
                _dumpedFault = false;
            }
            catch (NotSupportedException)
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
                WriteEmptyBlackBoxFile(Path.Combine(logDirectory, DumpFileName), reasonFlags, _telemetryCursor);
            }
            catch (IOException)
            {
                _dumpedFault = false;
            }
            catch (UnauthorizedAccessException)
            {
                _dumpedFault = false;
            }
            catch (ObjectDisposedException)
            {
                _dumpedFault = false;
            }
            catch (InvalidOperationException)
            {
                _dumpedFault = false;
            }
            catch (ArgumentException)
            {
                _dumpedFault = false;
            }
            catch (NotSupportedException)
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
            Span<byte> header = stackalloc byte[DumpHeaderSizeBytes];
            Span<byte> rowBytes = stackalloc byte[TelemetryEntrySizeBytes];
            int wrappedCursor = telemetryCursor % math.max(entryCount, 1);
            WriteDumpHeader(header, reasonFlags, wrappedCursor, entryCount);
            stream.Write(header);
            for (int i = 0; i < entryCount; i++)
            {
                UberNoirShaderTelemetryEntry entry = ring[(wrappedCursor + i) % entryCount];
                WriteTelemetryEntry(rowBytes, in entry);
                stream.Write(rowBytes);
            }
        }

        private static void WriteEmptyBlackBoxFile(string dumpPath, uint reasonFlags, int telemetryCursor)
        {
            using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            Span<byte> header = stackalloc byte[DumpHeaderSizeBytes];
            WriteDumpHeader(header, reasonFlags, telemetryCursor, 0);
            stream.Write(header);
        }

        private bool TryCopyTelemetrySnapshot(
            Span<UberNoirShaderTelemetryEntry> snapshot,
            out int telemetryCursor,
            out int entryCount)
        {
            telemetryCursor = _telemetryCursor;
            entryCount = 0;
            IDataVault vault = _dataVault;
            if (!TryReadTelemetryRing(vault, in _telemetryHandle, out NativeArray<UberNoirShaderTelemetryEntry>.ReadOnly ring) ||
                ring.Length < TelemetryCapacity)
            {
                return false;
            }

            entryCount = math.min(TelemetryCapacity, ring.Length);
            for (int i = 0; i < entryCount; i++)
                snapshot[i] = ring[i];

            telemetryCursor = _telemetryCursor;
            return true;
        }

        private static bool TryAcquireTelemetryWriteBuffer(
            IDataVault vault,
            in VaultGenerationHandle<UberNoirShaderTelemetryEntry> handle,
            out NativeArray<UberNoirShaderTelemetryEntry> buffer)
        {
            buffer = default;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsTelemetryHandle(in handle) ||
                !vault.TryAcquireWriteLock(in handle, SystemID.GraphicsScalability, out buffer))
            {
                return false;
            }

            if (vault.IsCompactionFenceActive ||
                !buffer.IsCreated ||
                buffer.Length < TelemetryCapacity)
            {
                vault.ReleaseWriteLock(in handle, SystemID.GraphicsScalability);
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool TryReadTelemetryRing(
            IDataVault vault,
            in VaultGenerationHandle<UberNoirShaderTelemetryEntry> handle,
            out NativeArray<UberNoirShaderTelemetryEntry>.ReadOnly ring)
        {
            ring = default;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsTelemetryHandle(in handle) ||
                !vault.TryReadOnlyHandle(in handle, out ring))
            {
                return false;
            }

            if (vault.IsCompactionFenceActive || ring.Length <= 0)
            {
                ring = default;
                return false;
            }

            return true;
        }

        private static bool IsTelemetryHandle(in VaultGenerationHandle<UberNoirShaderTelemetryEntry> handle)
        {
            return handle.BufferID == (uint)BufferID.ShaderFeatureTelemetryRing &&
                   handle.SystemID == (uint)SystemID.GraphicsScalability &&
                   handle.Generation != 0u;
        }

        private static void WriteDumpHeader(Span<byte> destination, uint reasonFlags, int telemetryCursor, int entryCount)
        {
            WriteUInt32LittleEndian(destination, 0, DumpMagic);
            WriteUInt32LittleEndian(destination, 4, reasonFlags);
            WriteInt32LittleEndian(destination, 8, telemetryCursor);
            WriteInt32LittleEndian(destination, 12, entryCount);
        }

        private static void WriteTelemetryEntry(Span<byte> destination, in UberNoirShaderTelemetryEntry entry)
        {
            destination.Clear();
            WriteUInt32LittleEndian(destination, 0, entry.Frame);
            WriteUInt32LittleEndian(destination, 4, entry.FeatureMask);
            WriteFloatLittleEndian(destination, 8, entry.SystemStress01);
            WriteFloatLittleEndian(destination, 12, entry.HighCostAllowed01);
            WriteFloatLittleEndian(destination, 16, entry.VisualOverkill01);
            WriteUInt32LittleEndian(destination, 20, entry.QualityWeightByte);
            WriteUInt32LittleEndian(destination, 24, entry.Flags);
            WriteUInt32LittleEndian(destination, 28, entry.StateHash);
            WriteFloatLittleEndian(destination, 32, entry.PomEnabled01);
            WriteFloatLittleEndian(destination, 36, entry.ReservedVisualDetail01);
            WriteFloatLittleEndian(destination, 40, entry.Refraction01);
            WriteFloatLittleEndian(destination, 44, entry.Reserved0);
        }

        private static void WriteFloatLittleEndian(Span<byte> destination, int offset, float value)
        {
            WriteUInt32LittleEndian(destination, offset, math.asuint(value));
        }

        private static void WriteInt32LittleEndian(Span<byte> destination, int offset, int value)
        {
            WriteUInt32LittleEndian(destination, offset, (uint)value);
        }

        private static void WriteUInt32LittleEndian(Span<byte> destination, int offset, uint value)
        {
            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
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
            if (vault != null &&
                handle.BufferID == (uint)BufferID.ShaderFeatureTelemetryRing &&
                handle.SystemID == (uint)SystemID.GraphicsScalability &&
                handle.Generation != 0u)
            {
                vault.ReleaseBuffer(in handle);
            }

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
