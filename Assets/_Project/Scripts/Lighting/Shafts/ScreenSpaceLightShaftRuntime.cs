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

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>
    /// Broadcast signal emitted when a shaft source resolves as a burst-grade bioluminescent flare.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    public struct VisualFlareSignal : ISignal
    {
        /// <summary>Stable source ID or component instance fallback.</summary>
        public uint SourceId;
        /// <summary>Resolved burst intensity after LOD and distance gates.</summary>
        public float Intensity01;
        /// <summary>Viewport-space source position.</summary>
        public float2 ScreenUv;
        /// <summary>Unity frame index at emission.</summary>
        public uint Frame;
        /// <summary>Bitfield reserved for source kind and debug state.</summary>
        public byte Flags;
    }
}

namespace Hecton8.Lighting.Shafts
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct LightShaftTelemetryEntry
    {
        public uint Frame;
        public uint PrimarySourceId;
        public float2 PrimaryUv;
        public float ActiveLightShafts;
        public float PrimaryIntensity;
        public float Soot01;
        public float Brownout01;
        public byte Flags;
    }

    /// <summary>
    /// Zero-GC bridge from registered light sources and global signals to the visor post shader.
    /// </summary>
    [DefaultExecutionOrder(-2550)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Lighting/Screen Space Light Shaft Runtime")]
    public sealed class ScreenSpaceLightShaftRuntime : MonoBehaviour, ILateFrameTickable, IScalabilityChangedEventListener, IDisposable
    {
        private const int MaxTrackedSources = 3;
        private const int TelemetryCapacity = 300;
        private const float FpsDisableThreshold = 40f;
        private const float LoadShedSeconds = 2.5f;
        private const float CameraRetrySeconds = 0.75f;
        private const uint NaNFallbackWarningHash = 0x4C534E41u;
        private const uint RuntimeContextHash = 0x4C534654u;
        private const byte TelemetryFlagLowTier = 1 << 0;
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
        [Tooltip("Low-tier tap count. Shader clamps to 8.")]
        [SerializeField, Range(4f, 8f)] private float lowTierSampleCount = 8f;
        [Tooltip("High-tier tap count. Shader clamps to 16.")]
        [SerializeField, Range(8f, 16f)] private float highTierSampleCount = 16f;
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

        private NativeArray<LightShaftContribution> _topContributions;
        private NativeArray<LightShaftContribution> _historyContributions;
        private NativeArray<LightShaftTelemetryEntry> _telemetry;
        private Camera _renderCamera;
        private float _nextCameraResolveTime;
        private float _loadShedTimer;
        private float _soot01;
        private float _brownout01;
        private int _telemetryWriteIndex;
        private int _lastLightLevelSequence;
        private bool _lowTier;
        private bool _registeredLateFrame;
        private bool _disposed;

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (!isActiveAndEnabled || !Application.isPlaying)
                return;

            EnsureBuffers();
            if (!_topContributions.IsCreated || !_historyContributions.IsCreated || !_telemetry.IsCreated)
            {
                ClearShaderGlobals();
                return;
            }

            UpdateLoadShedState();
            UpdateSignalCoupling();

            if (_renderCamera == null)
                ResolveRenderCamera();

            byte flags = _lowTier ? TelemetryFlagLowTier : (byte)0;
            if (_loadShedTimer > 0f)
                flags |= TelemetryFlagLoadShed;
            if (_renderCamera == null)
                flags |= TelemetryFlagNoCamera;

            if (_loadShedTimer > 0f || _renderCamera == null)
            {
                ClearShaderGlobals();
                RecordTelemetry(0, flags);
                return;
            }

            int activeCount = SelectTopContributions(_renderCamera);
            if (!ApplyHistoryAndValidate(ref activeCount))
            {
                flags |= TelemetryFlagNaN;
                ClearShaderGlobals();
                RecordTelemetry(0, flags);
                DumpBlackbox();
                GlobalTelemetryBus.PublishPerformanceWarning(NaNFallbackWarningHash, RuntimeContextHash, 1f);
                return;
            }

            PushShaderGlobals(activeCount);
            EmitVisualFlareSignals(activeCount);
            RecordTelemetry(activeCount, flags);
        }

        /// <inheritdoc />
        public void OnScalabilityChanged(in ScalabilityChangedEvent payload)
        {
            _lowTier = IsLowTier(payload.CurrentQualityTier);
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
            _lowTier = IsLowTier(GlobalRegistry.ScalabilityTier);
            GlobalSignals.InitializeAllQueues();
            EnsureBuffers();
            ResolveRenderCamera();
            ScalabilityEvents.Register(this);
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

            ScalabilityEvents.Unregister(this);
            ClearShaderGlobals();
            ReleaseBuffers();
        }

        private void OnDestroy()
        {
            if (!_disposed)
                Dispose();
        }

        private void EnsureBuffers()
        {
            if (!_topContributions.IsCreated)
                _topContributions = H8Memory.Allocate<LightShaftContribution>(MaxTrackedSources, SystemID.Vfx, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<LightShaftContribution>[3] - top shaft source SOA - owner: ScreenSpaceLightShaftRuntime

            if (!_historyContributions.IsCreated)
                _historyContributions = H8Memory.Allocate<LightShaftContribution>(MaxTrackedSources, SystemID.Vfx, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<LightShaftContribution>[3] - temporal shaft history - owner: ScreenSpaceLightShaftRuntime

            if (!_telemetry.IsCreated)
                _telemetry = H8Memory.Allocate<LightShaftTelemetryEntry>(TelemetryCapacity, SystemID.Vfx, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<LightShaftTelemetryEntry>[300] - blackbox ring buffer - owner: ScreenSpaceLightShaftRuntime
        }

        private void ReleaseBuffers()
        {
            H8Memory.Release(ref _topContributions, SystemID.Vfx);
            H8Memory.Release(ref _historyContributions, SystemID.Vfx);
            H8Memory.Release(ref _telemetry, SystemID.Vfx);
            _telemetryWriteIndex = 0;
        }

        private void ResolveRenderCamera()
        {
            float now = Time.unscaledTime;
            if (_renderCamera != null || now < _nextCameraResolveTime)
                return;

            _nextCameraResolveTime = now + CameraRetrySeconds;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
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
            if (GlobalSignals.TryGetLatestLightLevelSignal(out LightLevelSignal lightLevel, out int sequence) &&
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

        private int SelectTopContributions(Camera renderCamera)
        {
            ClearTopContributions();

            AbsoluteUniversePosition cameraAup = AbsoluteUniversePosition.FromRuntimePosition(renderCamera.transform.position);
            int sourceCount = ScreenSpaceLightShaftSource.RegisteredCount;
            for (int i = 0; i < sourceCount; i++)
            {
                ScreenSpaceLightShaftSource source = ScreenSpaceLightShaftSource.GetRegisteredAt(i);
                if (source == null || !source.TryGetContribution(renderCamera, in cameraAup, out LightShaftContribution contribution))
                    continue;

                InsertContribution(in contribution);
            }

            int activeCount = 0;
            for (int i = 0; i < MaxTrackedSources; i++)
            {
                if (_topContributions[i].Score > 0.0001f)
                    activeCount++;
            }

            return activeCount;
        }

        private void ClearTopContributions()
        {
            for (int i = 0; i < MaxTrackedSources; i++)
                _topContributions[i] = default;
        }

        private void InsertContribution(in LightShaftContribution contribution)
        {
            for (int slot = 0; slot < MaxTrackedSources; slot++)
            {
                if (contribution.Score <= _topContributions[slot].Score)
                    continue;

                for (int shift = MaxTrackedSources - 1; shift > slot; shift--)
                    _topContributions[shift] = _topContributions[shift - 1];

                _topContributions[slot] = contribution;
                return;
            }
        }

        private bool ApplyHistoryAndValidate(ref int activeCount)
        {
            float blend = math.saturate(historyBlendFactor);
            for (int i = 0; i < MaxTrackedSources; i++)
            {
                LightShaftContribution contribution = _topContributions[i];
                if (i >= activeCount)
                {
                    _historyContributions[i] = default;
                    continue;
                }

                for (int h = 0; h < MaxTrackedSources; h++)
                {
                    LightShaftContribution previous = _historyContributions[h];
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

                _topContributions[i] = contribution;
            }

            for (int i = 0; i < MaxTrackedSources; i++)
                _historyContributions[i] = i < activeCount ? _topContributions[i] : default;

            return true;
        }

        private void PushShaderGlobals(int activeCount)
        {
            activeCount = math.clamp(activeCount, 0, MaxTrackedSources);
            float sampleBudget = _lowTier ? math.min(8f, lowTierSampleCount) : math.min(16f, highTierSampleCount);
            float brownoutStutter = ResolveBrownoutStutter();
            float intensity = shaftIntensityScale * brownoutStutter;

            Shader.SetGlobalVector(_LightShaftParamsId, new Vector4(activeCount, intensity, _soot01, _brownout01));
            Shader.SetGlobalVector(_LightShaftQualityId, new Vector4(math.max(0.01f, emissionThreshold), sampleBudget, math.max(0.001f, depthBiasMeters), _lowTier ? 1f : 0f));
            Shader.SetGlobalFloat(_AtmosphereSootId, _soot01);

            PushContributionGlobals(0, activeCount > 0 ? _topContributions[0] : default);
            PushContributionGlobals(1, activeCount > 1 ? _topContributions[1] : default);
            PushContributionGlobals(2, activeCount > 2 ? _topContributions[2] : default);
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

        private void EmitVisualFlareSignals(int activeCount)
        {
            int frame = Time.frameCount;
            for (int i = 0; i < activeCount; i++)
            {
                LightShaftContribution contribution = _topContributions[i];
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
                SignalBus<VisualFlareSignal>.Push(in signal);
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

        private void RecordTelemetry(int activeCount, byte flags)
        {
            if (!_telemetry.IsCreated)
                return;

            LightShaftContribution primary = activeCount > 0 ? _topContributions[0] : default;
            _telemetry[_telemetryWriteIndex] = new LightShaftTelemetryEntry
            {
                Frame = unchecked((uint)Time.frameCount),
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

        private void DumpBlackbox()
        {
            if (!_telemetry.IsCreated)
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
                    LightShaftTelemetryEntry entry = _telemetry[i];
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

        private static bool IsLowTier(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.Low ||
                   tier == HectonQualityTier.Mx350 ||
                   (tier == HectonQualityTier.Unknown && GlobalRegistry.ScalabilityTierProfileByte == 0);
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
            lowTierSampleCount = math.clamp(lowTierSampleCount, 4f, 8f);
            highTierSampleCount = math.clamp(highTierSampleCount, 8f, 16f);
            historyBlendFactor = math.clamp(historyBlendFactor, 0.35f, 1f);
            baseSoot01 = math.saturate(baseSoot01);
            darknessToSoot = math.max(0f, darknessToSoot);
            shaftIntensityScale = math.max(0f, shaftIntensityScale);
            brownoutRecoveryPerSecond = math.max(0.1f, brownoutRecoveryPerSecond);
        }
#endif
    }
}
