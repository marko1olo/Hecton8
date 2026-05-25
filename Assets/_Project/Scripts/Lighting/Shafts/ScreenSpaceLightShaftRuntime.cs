using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Hecton8.Lighting.Shafts
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct LightShaftTelemetryEntry
    {
        [FieldOffset(0)]
        public uint Frame;
        [FieldOffset(4)]
        public uint PrimarySourceId;
        [FieldOffset(8)]
        public float2 PrimaryUv;
        [FieldOffset(16)]
        public float ActiveLightShafts;
        [FieldOffset(20)]
        public float PrimaryIntensity;
        [FieldOffset(24)]
        public float Soot01;
        [FieldOffset(28)]
        public float Brownout01;
        [FieldOffset(32)]
        public byte Flags;
        [FieldOffset(33)]
        public byte _pad0;
        [FieldOffset(34)]
        public ushort _pad1;
        [FieldOffset(36)]
        public uint _pad2;
        [FieldOffset(40)]
        public ulong _pad3;
        [FieldOffset(48)]
        public ulong _pad4;
        [FieldOffset(56)]
        public ulong _pad5;
    }

    /// <summary>
    /// Zero-GC bridge from registered light sources and global signals to the visor post shader.
    /// </summary>
    [DefaultExecutionOrder(-2550)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Lighting/Screen Space Light Shaft Runtime")]
    public sealed class ScreenSpaceLightShaftRuntime : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener, IDisposable
    {
        private static int s_x001ScreenSpaceLightShaftRuntimeSignalPushDropCount;
        private const int MaxTrackedSources = 3;
        private const int TelemetryCapacity = 300;
        private const float FpsDisableThreshold = 40f;
        private const float LoadShedSeconds = 2.5f;
        private const float CameraRetrySeconds = 0.75f;
        private const uint NaNFallbackWarningHash = 0x4C534E41u;
        private const uint RuntimeContextHash = 0x4C534654u;
        private const byte TelemetryFlagQualityPressure = 1 << 0;
        private const byte TelemetryFlagLoadShed = 1 << 1;
        private const byte TelemetryFlagNoCamera = 1 << 2;
        private const byte TelemetryFlagNaN = 1 << 3;

        private static readonly int _LightShaftParamsId = Shader.PropertyToID("_HectonLightShaftParams");
        private static readonly int _LightShaftQualityId = Shader.PropertyToID("_HectonLightShaftQuality");
        private static readonly int _LightShaftSource0Id = Shader.PropertyToID("_HectonLightShaftSource0");
        private static readonly int _LightShaftSource1Id = Shader.PropertyToID("_HectonLightShaftSource1");
        private static readonly int _LightShaftSource2Id = Shader.PropertyToID("_HectonLightShaftSource2");
        private static readonly int _LightShaftColor0Id = Shader.PropertyToID("_HectonLightShaftColor0");
        private static readonly int _LightShaftColor1Id = Shader.PropertyToID("_HectonLightShaftColor1");
        private static readonly int _LightShaftColor2Id = Shader.PropertyToID("_HectonLightShaftColor2");
        private static readonly int _AtmosphereSootId = Shader.PropertyToID("_HectonAtmosphereSoot");

        [Header("Quality")]
        [Tooltip("High emission luminance threshold before a pixel can scatter into the shaft pass.")]
        [SerializeField, Range(0.4f, 8f)] private float emissionThreshold = 1.18f;
        [Tooltip("Depth separation in eye-space meters before geometry suppresses scattered light.")]
        [SerializeField, Range(0.001f, 2f)] private float depthBiasMeters = 0.12f;
        [Tooltip("Minimum-quality tap count. Shader clamps to 8.")]
        [FormerlySerializedAs("lowTierSampleCount")]
        [SerializeField, Range(4f, 8f)] private float minimumQualitySampleCount = 8f;
        [Tooltip("Maximum-quality tap count. Shader clamps to 16.")]
        [FormerlySerializedAs("highTierSampleCount")]
        [SerializeField, Range(8f, 16f)] private float maximumQualitySampleCount = 16f;
        [Tooltip("History blend factor. Lower values smooth harder but lag more.")]
        [SerializeField, Range(0.35f, 1f)] private float historyBlendFactor = 0.68f;

        [Header("Coupling")]
        [Tooltip("Base soot contribution when light-level data is valid.")]
        [SerializeField, Range(0f, 1f)] private float baseSoot01 = 0.22f;
        [Tooltip("Darkness-to-soot multiplier. Dirty and dark water buys stronger fake shafts.")]
        [SerializeField, Range(0f, 2f)] private float darknessToSoot = 0.9f;
        [Tooltip("Global shaft intensity scalar before brownout stutter.")]
        [SerializeField, Range(0f, 4f)] private float shaftIntensityScale = 0.82f;
        [Tooltip("Brownout recovery speed in units per second.")]
        [SerializeField, Min(0.1f)] private float brownoutRecoveryPerSecond = 2.4f;

        private IDataVault _dataVault;
        private VaultGenerationHandle<LightShaftContribution> _topContributionsHandle;
        private VaultGenerationHandle<LightShaftContribution> _historyContributionsHandle;
        private VaultGenerationHandle<LightShaftTelemetryEntry> _telemetryHandle;
        private IPlayerRuntimeContext _playerContext;
        private Camera _renderCamera;
        private float _nextCameraResolveTime;
        private float _loadShedTimer;
        private float _soot01;
        private float _brownout01;
        private int _telemetryWriteIndex;
        private int _lastLightLevelSequence;
        private float _qualityWeight01 = 1f;
        private float _qualityPressure01;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _ownsTopContributions;
        private bool _ownsHistoryContributions;
        private bool _ownsTelemetry;
        private bool _disposed;

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (!isActiveAndEnabled || !Application.isPlaying)
                return;

            if (!EnsureBuffers() ||
                !TryLockFrameBuffers(out NativeArray<LightShaftContribution> topContributions,
                    out NativeArray<LightShaftContribution> historyContributions,
                    out NativeArray<LightShaftTelemetryEntry> telemetry))
            {
                ClearShaderGlobals();
                return;
            }

            try
            {
                UpdateLoadShedState();
                UpdateSignalCoupling();
                RefreshQualityPolicy();

                if (_renderCamera == null)
                    ResolveRenderCamera();

                byte flags = _qualityPressure01 > 0.001f ? TelemetryFlagQualityPressure : (byte)0;
                if (_loadShedTimer > 0f)
                    flags |= TelemetryFlagLoadShed;
                if (_renderCamera == null)
                    flags |= TelemetryFlagNoCamera;

                if (_loadShedTimer > 0f || _renderCamera == null)
                {
                    ClearShaderGlobals();
                    RecordTelemetry(0, flags, topContributions, telemetry);
                    return;
                }

                int activeCount = SelectTopContributions(_renderCamera, topContributions);
                if (!ApplyHistoryAndValidate(ref activeCount, topContributions, historyContributions))
                {
                    flags |= TelemetryFlagNaN;
                    ClearShaderGlobals();
                    RecordTelemetry(0, flags, topContributions, telemetry);
                    DumpBlackbox(telemetry);
                    GlobalTelemetryBus.PublishPerformanceWarning(NaNFallbackWarningHash, RuntimeContextHash, 1f);
                    return;
                }

                PushShaderGlobals(activeCount, topContributions);
                EmitVisualFlareSignals(activeCount, topContributions);
                RecordTelemetry(activeCount, flags, topContributions, telemetry);
            }
            finally
            {
                UnlockFrameBuffers();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            ReleaseBuffers();
            _disposed = true;
        }

        private void OnEnable()
        {
            _disposed = false;
            RefreshQualityPolicy();
            TryRegisterHotSwapListener();
            CacheDataVaultCold(GlobalRegistry.DataVault);
            CachePlayerCold(Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext);
            SignalCorridorRuntime.EnsureInitialized();
            EnsureBuffers();
            ResolveRenderCamera();
            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            ClearShaderGlobals();
        }

        private void OnDisable()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            TryUnregisterHotSwapListener();
            CachePlayerCold(null);
            ClearShaderGlobals();
            ReleaseBuffers();
        }

        private void OnDestroy()
        {
            if (!_disposed)
                Dispose();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                ReleaseOwnedVaultHandles();
                CacheDataVaultCold(currentService as IDataVault);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                CachePlayerCold(currentService as IPlayerRuntimeContext);
            }
        }

        private bool EnsureBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                ClearVaultDescriptors();
                return false;
            }

            return EnsureVaultHandle(
                    ref _topContributionsHandle,
                    ref _ownsTopContributions,
                    BufferID.LightShaftTopContributions,
                    MaxTrackedSources) &&
                EnsureVaultHandle(
                    ref _historyContributionsHandle,
                    ref _ownsHistoryContributions,
                    BufferID.LightShaftHistoryContributions,
                    MaxTrackedSources) &&
                EnsureVaultHandle(
                    ref _telemetryHandle,
                    ref _ownsTelemetry,
                    BufferID.LightShaftTelemetryRing,
                    TelemetryCapacity);
        }

        private bool EnsureVaultHandle<T>(
            ref VaultGenerationHandle<T> handle,
            ref bool ownsHandle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (IsVaultHandleCreated(in handle) &&
                handle.BufferID == unchecked((uint)(int)bufferId) &&
                vault.TryResolveHandle(in handle, out NativeArray<T> current) &&
                current.IsCreated &&
                current.Length >= requiredLength)
            {
                return true;
            }

            handle = default;
            ownsHandle = false;

            if (vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> existing) &&
                vault.TryResolveHandle(in existing, out NativeArray<T> existingBuffer) &&
                existingBuffer.IsCreated &&
                existingBuffer.Length >= requiredLength)
            {
                handle = existing;
                return true;
            }

            if (vault.IsAllocationLocked)
                return false;

            VaultGenerationHandle<T> acquired = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.Vfx,
                NativeArrayOptions.ClearMemory);
            if (!IsVaultHandleCreated(in acquired) ||
                !vault.TryResolveHandle(in acquired, out NativeArray<T> acquiredBuffer) ||
                !acquiredBuffer.IsCreated ||
                acquiredBuffer.Length < requiredLength)
            {
                return false;
            }

            handle = acquired;
            ownsHandle = true;
            return true;
        }

        private bool TryLockFrameBuffers(
            out NativeArray<LightShaftContribution> topContributions,
            out NativeArray<LightShaftContribution> historyContributions,
            out NativeArray<LightShaftTelemetryEntry> telemetry)
        {
            topContributions = default;
            historyContributions = default;
            telemetry = default;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            bool topLocked = vault.TryLockBuffer(BufferID.LightShaftTopContributions, SystemID.Vfx);
            if (!topLocked)
                return false;

            bool historyLocked = vault.TryLockBuffer(BufferID.LightShaftHistoryContributions, SystemID.Vfx);
            if (!historyLocked)
            {
                vault.TryUnlockBuffer(BufferID.LightShaftTopContributions, SystemID.Vfx);
                return false;
            }

            bool telemetryLocked = vault.TryLockBuffer(BufferID.LightShaftTelemetryRing, SystemID.Vfx);
            if (!telemetryLocked)
            {
                vault.TryUnlockBuffer(BufferID.LightShaftHistoryContributions, SystemID.Vfx);
                vault.TryUnlockBuffer(BufferID.LightShaftTopContributions, SystemID.Vfx);
                return false;
            }

            vault.TryResolveHandle(in _topContributionsHandle, out topContributions);
            vault.TryResolveHandle(in _historyContributionsHandle, out historyContributions);
            vault.TryResolveHandle(in _telemetryHandle, out telemetry);
            if (topContributions.IsCreated &&
                topContributions.Length >= MaxTrackedSources &&
                historyContributions.IsCreated &&
                historyContributions.Length >= MaxTrackedSources &&
                telemetry.IsCreated &&
                telemetry.Length >= TelemetryCapacity)
            {
                return true;
            }

            UnlockFrameBuffers();
            ClearVaultDescriptors();
            return false;
        }

        private void UnlockFrameBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            vault.TryUnlockBuffer(BufferID.LightShaftTelemetryRing, SystemID.Vfx);
            vault.TryUnlockBuffer(BufferID.LightShaftHistoryContributions, SystemID.Vfx);
            vault.TryUnlockBuffer(BufferID.LightShaftTopContributions, SystemID.Vfx);
        }

        private void ReleaseBuffers()
        {
            ReleaseOwnedVaultHandles();
            _dataVault = null;
            _telemetryWriteIndex = 0;
        }

        private void ClearVaultDescriptors()
        {
            _topContributionsHandle = default;
            _historyContributionsHandle = default;
            _telemetryHandle = default;
            _ownsTopContributions = false;
            _ownsHistoryContributions = false;
            _ownsTelemetry = false;
        }

        private void ReleaseOwnedVaultHandles()
        {
            IDataVault vault = _dataVault;
            if (vault != null)
            {
                ReleaseOwnedVaultHandle(vault, ref _topContributionsHandle, ref _ownsTopContributions, BufferID.LightShaftTopContributions);
                ReleaseOwnedVaultHandle(vault, ref _historyContributionsHandle, ref _ownsHistoryContributions, BufferID.LightShaftHistoryContributions);
                ReleaseOwnedVaultHandle(vault, ref _telemetryHandle, ref _ownsTelemetry, BufferID.LightShaftTelemetryRing);
            }

            ClearVaultDescriptors();
        }

        private static void ReleaseOwnedVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            ref bool ownsHandle,
            BufferID bufferId) where T : struct
        {
            if (vault == null ||
                !ownsHandle ||
                !IsVaultHandleCreated(in handle) ||
                vault.IsCompactionFenceActive ||
                !vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> current) ||
                current.Generation != handle.Generation)
            {
                handle = default;
                ownsHandle = false;
                return;
            }

            vault.ReleaseBuffer(in handle);
            handle = default;
            ownsHandle = false;
        }

        private void CacheDataVaultCold(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            ReleaseOwnedVaultHandles();
            _dataVault = vault;
        }

        private void CachePlayerCold(IPlayerRuntimeContext playerContext)
        {
            _playerContext = playerContext;
            _renderCamera = playerContext != null ? playerContext.PlayerCamera : null;
            _nextCameraResolveTime = 0f;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private void ResolveRenderCamera()
        {
            float now = Time.unscaledTime;
            if (_renderCamera != null || now < _nextCameraResolveTime)
                return;

            _nextCameraResolveTime = now + CameraRetrySeconds;
            IPlayerRuntimeContext playerContext = _playerContext;
            _renderCamera = playerContext != null ? playerContext.PlayerCamera : null;
        }

        private void UpdateLoadShedState()
        {
            float dt = SanitizeDelta(Time.unscaledDeltaTime);
            if (dt > math.rcp(FpsDisableThreshold))
            {
                _loadShedTimer = LoadShedSeconds;
                return;
            }

            if (_loadShedTimer > 0f)
                _loadShedTimer = math.max(0f, _loadShedTimer - dt);
        }

        private void UpdateSignalCoupling()
        {
            if (SignalBus<LightLevelSignal>.TryGetLatest(out LightLevelSignal lightLevel, out int sequence) &&
                sequence != _lastLightLevelSequence &&
                (lightLevel.Flags & LightLevelSignalFlags.ValidSample) != 0)
            {
                _lastLightLevelSequence = sequence;
                _soot01 = math.saturate(baseSoot01 + math.saturate(lightLevel.Darkness01) * darknessToSoot);
            }
            else
            {
                _soot01 = math.saturate(math.lerp(_soot01, baseSoot01, 0.025f));
            }

            float maxBrownout = 0f;
            NativeArray<BrownoutSignal>.ReadOnly brownoutSignals = SignalBus<BrownoutSignal>.GetFrameSnapshotArray();
            if (brownoutSignals.IsCreated)
            {
                int count = brownoutSignals.Length;
                for (int i = 0; i < count; i++)
                    maxBrownout = math.max(maxBrownout, math.saturate(brownoutSignals[i].Severity01));
            }

            if (maxBrownout > _brownout01)
            {
                _brownout01 = maxBrownout;
            }
            else
            {
                float dt = SanitizeDelta(Time.unscaledDeltaTime);
                _brownout01 = math.max(0f, _brownout01 - brownoutRecoveryPerSecond * dt);
            }
        }

        private int SelectTopContributions(Camera renderCamera, NativeArray<LightShaftContribution> topContributions)
        {
            ClearTopContributions(topContributions);

            double3 cameraAup = ResolveCameraAup();
            int sourceCount = ScreenSpaceLightShaftSource.RegisteredCount;
            for (int i = 0; i < sourceCount; i++)
            {
                ScreenSpaceLightShaftSource source = ScreenSpaceLightShaftSource.GetRegisteredAt(i);
                if (source == null || !source.TryGetContribution(renderCamera, in cameraAup, out LightShaftContribution contribution))
                    continue;

                InsertContribution(in contribution, topContributions);
            }

            int activeCount = 0;
            for (int i = 0; i < MaxTrackedSources; i++)
            {
                if (topContributions[i].Score > 0.0001f)
                    activeCount++;
            }

            return activeCount;
        }

        private double3 ResolveCameraAup()
        {
            IPlayerRuntimeContext playerContext = _playerContext;
            if (playerContext != null)
            {
                if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                    snapshot.Aup.IsFinite())
                {
                    return snapshot.Aup.ToAbsoluteDouble3();
                }

                var playerMovement = playerContext.PlayerMovement;
                if (playerMovement != null)
                {
                    AbsoluteUniversePosition currentAup = playerMovement.CurrentAup;
                    if (currentAup.IsFinite())
                        return currentAup.ToAbsoluteDouble3();
                }
            }

            return HectonFloatingOrigin.CurrentTotalOffsetDouble;
        }

        private void ClearTopContributions(NativeArray<LightShaftContribution> topContributions)
        {
            for (int i = 0; i < MaxTrackedSources; i++)
                topContributions[i] = default;
        }

        private void InsertContribution(in LightShaftContribution contribution, NativeArray<LightShaftContribution> topContributions)
        {
            for (int slot = 0; slot < MaxTrackedSources; slot++)
            {
                if (contribution.Score <= topContributions[slot].Score)
                    continue;

                for (int shift = MaxTrackedSources - 1; shift > slot; shift--)
                    topContributions[shift] = topContributions[shift - 1];

                topContributions[slot] = contribution;
                return;
            }
        }

        private bool ApplyHistoryAndValidate(
            ref int activeCount,
            NativeArray<LightShaftContribution> topContributions,
            NativeArray<LightShaftContribution> historyContributions)
        {
            float blend = math.saturate(historyBlendFactor);
            for (int i = 0; i < MaxTrackedSources; i++)
            {
                LightShaftContribution contribution = topContributions[i];
                if (i >= activeCount)
                {
                    historyContributions[i] = default;
                    continue;
                }

                for (int h = 0; h < MaxTrackedSources; h++)
                {
                    LightShaftContribution previous = historyContributions[h];
                    if (previous.SourceId != 0u && previous.SourceId == contribution.SourceId)
                    {
                        contribution.ScreenUv = math.lerp(previous.ScreenUv, contribution.ScreenUv, blend);
                        contribution.Intensity = math.lerp(previous.Intensity, contribution.Intensity, blend);
                        break;
                    }
                }

                if (!IsContributionFinite(in contribution))
                {
                    activeCount = 0;
                    return false;
                }

                topContributions[i] = contribution;
            }

            for (int i = 0; i < MaxTrackedSources; i++)
                historyContributions[i] = i < activeCount ? topContributions[i] : default;

            return true;
        }

        private void PushShaderGlobals(int activeCount, NativeArray<LightShaftContribution> topContributions)
        {
            activeCount = math.clamp(activeCount, 0, MaxTrackedSources);
            float qualityCurve = math.smoothstep(0f, 1f, _qualityWeight01);
            float sampleBudget = math.lerp(math.min(8f, minimumQualitySampleCount), math.min(16f, maximumQualitySampleCount), qualityCurve);
            float brownoutStutter = ResolveBrownoutStutter();
            float intensity = shaftIntensityScale * brownoutStutter;

            Shader.SetGlobalVector(_LightShaftParamsId, new Vector4(activeCount, intensity, _soot01, _brownout01));
            Shader.SetGlobalVector(_LightShaftQualityId, new Vector4(math.max(0.01f, emissionThreshold), sampleBudget, math.max(0.001f, depthBiasMeters), _qualityPressure01));
            Shader.SetGlobalFloat(_AtmosphereSootId, _soot01);

            PushContributionGlobals(0, activeCount > 0 ? topContributions[0] : default);
            PushContributionGlobals(1, activeCount > 1 ? topContributions[1] : default);
            PushContributionGlobals(2, activeCount > 2 ? topContributions[2] : default);
        }

        private void PushContributionGlobals(int index, in LightShaftContribution contribution)
        {
            Vector4 source = new Vector4(contribution.ScreenUv.x, contribution.ScreenUv.y, contribution.Intensity, contribution.RadialFalloff);
            Vector4 color = new Vector4(contribution.ColorRgb.x, contribution.ColorRgb.y, contribution.ColorRgb.z, contribution.MaxDistanceMeters);

            switch (index)
            {
                case 0:
                    Shader.SetGlobalVector(_LightShaftSource0Id, source);
                    Shader.SetGlobalVector(_LightShaftColor0Id, color);
                    break;
                case 1:
                    Shader.SetGlobalVector(_LightShaftSource1Id, source);
                    Shader.SetGlobalVector(_LightShaftColor1Id, color);
                    break;
                default:
                    Shader.SetGlobalVector(_LightShaftSource2Id, source);
                    Shader.SetGlobalVector(_LightShaftColor2Id, color);
                    break;
            }
        }

        private void ClearShaderGlobals()
        {
            Shader.SetGlobalVector(_LightShaftParamsId, Vector4.zero);
            Shader.SetGlobalVector(_LightShaftQualityId, Vector4.zero);
            Shader.SetGlobalFloat(_AtmosphereSootId, 0f);
            PushContributionGlobals(0, default);
            PushContributionGlobals(1, default);
            PushContributionGlobals(2, default);
        }

        private void EmitVisualFlareSignals(int activeCount, NativeArray<LightShaftContribution> topContributions)
        {
            int frame = Time.frameCount;
            for (int i = 0; i < activeCount; i++)
            {
                LightShaftContribution contribution = topContributions[i];
                if ((contribution.Flags & 1) == 0)
                    continue;

                ScreenSpaceLightShaftSource source = FindSourceById(contribution.SourceId);
                if (source == null || !source.ShouldEmitBurst(contribution.Intensity, frame))
                    continue;

                VisualFlareSignal signal = new VisualFlareSignal
                {
                    SourceId = contribution.SourceId,
                    Intensity01 = math.saturate(contribution.Intensity),
                    ScreenUv = contribution.ScreenUv,
                    Frame = unchecked((uint)frame),
                    Flags = 1
                };
                SignalBus<VisualFlareSignal>.TryPushTracked(in signal, ref s_x001ScreenSpaceLightShaftRuntimeSignalPushDropCount);
            }
        }

        private ScreenSpaceLightShaftSource FindSourceById(uint sourceId)
        {
            int sourceCount = ScreenSpaceLightShaftSource.RegisteredCount;
            for (int i = 0; i < sourceCount; i++)
            {
                ScreenSpaceLightShaftSource source = ScreenSpaceLightShaftSource.GetRegisteredAt(i);
                if (source == null)
                    continue;

                if (source.ResolvedSourceId == sourceId)
                    return source;
            }

            return null;
        }

        private void RecordTelemetry(
            int activeCount,
            byte flags,
            NativeArray<LightShaftContribution> topContributions,
            NativeArray<LightShaftTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated)
                return;

            LightShaftContribution primary = activeCount > 0 ? topContributions[0] : default;
            telemetry[_telemetryWriteIndex] = new LightShaftTelemetryEntry
            {
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                PrimarySourceId = primary.SourceId,
                PrimaryUv = primary.ScreenUv,
                ActiveLightShafts = activeCount,
                PrimaryIntensity = primary.Intensity,
                Soot01 = _soot01,
                Brownout01 = _brownout01,
                Flags = flags
            };

            _telemetryWriteIndex++;
            if (_telemetryWriteIndex >= TelemetryCapacity)
                _telemetryWriteIndex = 0;
        }

        private void DumpBlackbox(NativeArray<LightShaftTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated)
                return;

            string path = Path.Combine(Application.dataPath, "../Docs/AgentLogs/Dump_ABYSSAL_LIGHTING_TECH.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(TelemetryCapacity);
                writer.Write(_telemetryWriteIndex);
                for (int i = 0; i < TelemetryCapacity; i++)
                {
                    LightShaftTelemetryEntry entry = telemetry[i];
                    writer.Write(entry.Frame);
                    writer.Write(entry.PrimarySourceId);
                    writer.Write(entry.PrimaryUv.x);
                    writer.Write(entry.PrimaryUv.y);
                    writer.Write(entry.ActiveLightShafts);
                    writer.Write(entry.PrimaryIntensity);
                    writer.Write(entry.Soot01);
                    writer.Write(entry.Brownout01);
                    writer.Write(entry.Flags);
                }
            }
        }

        private float ResolveBrownoutStutter()
        {
            if (_brownout01 <= 0.0001f)
                return 1f;

            float phase = math.frac(Time.frameCount * 0.381966f);
            float triangle = 1f - math.abs(phase * 2f - 1f);
            return math.saturate(1f - _brownout01 * (0.35f + triangle * 0.45f));
        }

        private static bool IsContributionFinite(in LightShaftContribution contribution)
        {
            return math.isfinite(contribution.ScreenUv.x) &&
                   math.isfinite(contribution.ScreenUv.y) &&
                   math.isfinite(contribution.ColorRgb.x) &&
                   math.isfinite(contribution.ColorRgb.y) &&
                   math.isfinite(contribution.ColorRgb.z) &&
                   math.isfinite(contribution.Intensity) &&
                   math.isfinite(contribution.RadialFalloff) &&
                   math.isfinite(contribution.Score);
        }

        private void RefreshQualityPolicy()
        {
            _qualityWeight01 = ResolveQualityWeight01();
            _qualityPressure01 = ResolveQualityPressureFromWeight(_qualityWeight01);
        }

        private static float ResolveQualityPressureFromWeight(float globalQualityWeight)
        {
            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            return 1f - math.smoothstep(0.12f, 0.44f, quality);
        }

        private static float ResolveQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private static float SanitizeDelta(float dt)
        {
            return math.isfinite(dt) ? math.clamp(dt, 0.0001f, 0.25f) : 0.0166667f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            emissionThreshold = math.max(0.01f, emissionThreshold);
            depthBiasMeters = math.max(0.001f, depthBiasMeters);
            minimumQualitySampleCount = math.clamp(minimumQualitySampleCount, 4f, 8f);
            maximumQualitySampleCount = math.clamp(maximumQualitySampleCount, 8f, 16f);
            historyBlendFactor = math.clamp(historyBlendFactor, 0.35f, 1f);
            baseSoot01 = math.saturate(baseSoot01);
            darknessToSoot = math.max(0f, darknessToSoot);
            shaftIntensityScale = math.max(0f, shaftIntensityScale);
            brownoutRecoveryPerSecond = math.max(0.1f, brownoutRecoveryPerSecond);
        }
#endif
    }
}
