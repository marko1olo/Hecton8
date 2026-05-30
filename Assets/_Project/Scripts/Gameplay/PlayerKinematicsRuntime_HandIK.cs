using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Gameplay
{
    public sealed partial class PlayerKinematicsRuntime
    {
        public const int HandIkHandCount = PlayerHandIkContract.HandCount;
        public const int HandIkMatricesPerHand = PlayerHandIkContract.MatricesPerHand;
        public const int HandIkMatrixCount = PlayerHandIkContract.MatrixCount;
        public const int HandIkTelemetryFrameCount = PlayerHandIkContract.TelemetryFrameCount;
        public const uint HandIkTelemetryMarker = PlayerHandIkContract.TelemetryMarker; // H8IK
        public const float HandIkBudgetMicros = PlayerHandIkContract.BudgetMicros;
        public const string HandIkDumpRelativePath = PlayerHandIkContract.DumpRelativePath;
        public const BufferID HandIkStatesBuffer = BufferID.PlayerHandIkStates;
        public const BufferID HandIkTargetsBuffer = BufferID.PlayerHandIkTargets;
        public const BufferID HandIkBoneMatricesBuffer = BufferID.PlayerHandIkBoneMatrices;
        public const BufferID HandIkTelemetryRingBuffer = BufferID.PlayerHandIkTelemetryRing;
        public const BufferID HandIkTelemetryCursorBuffer = BufferID.PlayerHandIkTelemetryCursor;
        public const BufferID HandIkConfigBuffer = BufferID.PlayerHandIkConfig;
        public const BufferID HandIkPublishedStatesBuffer = BufferID.PlayerHandIkPublishedStates;
        private const uint HandIkJobPinStates = 1u << 0;
        private const uint HandIkJobPinPublishedStates = 1u << 1;
        private const uint HandIkJobPinTargets = 1u << 2;
        private const uint HandIkJobPinBoneMatrices = 1u << 3;
        private const uint HandIkJobPinTelemetryRing = 1u << 4;
        private const uint HandIkJobPinTelemetryCursor = 1u << 5;
        private const uint HandIkJobPinConfig = 1u << 6;
        private const uint HandIkJobPinBridgeStates = 1u << 7;
        private const uint HandIkJobPinBridgeTuning = 1u << 8;

        private VaultBufferBinding<IkHandStateDTO> _handIkStates = new VaultBufferBinding<IkHandStateDTO>(HandIkStatesBuffer, HandIkHandCount, OwnerSystemId);
        private VaultBufferBinding<IkHandStateDTO> _handIkPublishedStates = new VaultBufferBinding<IkHandStateDTO>(HandIkPublishedStatesBuffer, HandIkHandCount, OwnerSystemId);
        private VaultBufferBinding<IkHandTargetDTO> _handIkTargets = new VaultBufferBinding<IkHandTargetDTO>(HandIkTargetsBuffer, HandIkHandCount, OwnerSystemId);
        private VaultBufferBinding<float4x4> _handIkBoneMatrices = new VaultBufferBinding<float4x4>(HandIkBoneMatricesBuffer, HandIkMatrixCount, OwnerSystemId);
        private VaultBufferBinding<IkHandTelemetryEntry> _handIkTelemetry = new VaultBufferBinding<IkHandTelemetryEntry>(HandIkTelemetryRingBuffer, HandIkTelemetryFrameCount, OwnerSystemId);
        private VaultBufferBinding<int> _handIkTelemetryCursor = new VaultBufferBinding<int>(HandIkTelemetryCursorBuffer, 1, OwnerSystemId);
        private VaultBufferBinding<IkHandConfigDTO> _handIkConfig = new VaultBufferBinding<IkHandConfigDTO>(HandIkConfigBuffer, 1, OwnerSystemId);
        private VaultBufferBinding<VRHandStateDTO> _handIkBridgeStates = new VaultBufferBinding<VRHandStateDTO>(
            BufferID.VRInteractionHandStates,
            VRInteractionBridgeContract.HandCount,
            OwnerSystemId);
        private VaultBufferBinding<VRInteractionTuningDTO> _handIkBridgeTuning = new VaultBufferBinding<VRInteractionTuningDTO>(
            BufferID.VRInteractionTuning,
            1,
            OwnerSystemId);

        private GraphicsBuffer _handIkMatrixBufferA;
        private GraphicsBuffer _handIkMatrixBufferB;
        private JobHandle _handIkJobHandle;
        private long _handIkScheduleTimestamp;
        private uint _handIkFrameIndex;
        private int _handIkGpuBufferIndex;
        private IDataVault _handIkJobPinVault;
        private uint _handIkJobPinMask;
        private bool _handIkJobPending;
        private bool _handIkGpuDataValid;
        private bool _handIkGpuDirty;
        private double3 _handIkCachedFloatingOriginAup;
        private byte _handIkHasFloatingOriginSnapshot;

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        public struct IkHandTargetDTO
        {
            [FieldOffset(0)] public double3 PlayerRootAUP;
            [FieldOffset(24)] public double3 ShoulderAUP;
            [FieldOffset(48)] public double3 TargetAUP;
            [FieldOffset(72)] public double3 RawControllerAUP;
            [FieldOffset(96)] public float3 PoleLocal;
            [FieldOffset(108)] public float BlendSecondsRemaining;
            [FieldOffset(112)] public float DeltaTime;
            [FieldOffset(116)] public float GlobalQualityWeight;
            [FieldOffset(120)] public uint TargetHashID;
            [FieldOffset(124)] public uint Flags;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct IkHandConfigDTO
        {
            [FieldOffset(0)] public float DefaultUpperArmLength;
            [FieldOffset(4)] public float DefaultForearmLength;
            [FieldOffset(8)] public float BlendOutSeconds;
            [FieldOffset(12)] public float BlendOutSpeed;
            [FieldOffset(16)] public float MaxFabrikIterations;
            [FieldOffset(20)] public float MinElbowRadians;
            [FieldOffset(24)] public float MaxElbowRadians;
            [FieldOffset(28)] public float GlobalQualityWeightOverride;
            [FieldOffset(32)] public uint SuitHash;
            [FieldOffset(36)] public uint Flags;
            [FieldOffset(40)] public float ShoulderLateralMeters;
            [FieldOffset(44)] public float ShoulderHeightMeters;
            [FieldOffset(48)] public float ShoulderForwardMeters;
            [FieldOffset(52)] private uint _pad0;
            [FieldOffset(56)] private ulong _pad1;
        }

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        public struct IkHandTelemetryEntry
        {
            [FieldOffset(0)] public uint FrameIndex;
            [FieldOffset(4)] public uint ArmsProcessed;
            [FieldOffset(8)] public uint ActiveIterationLimit;
            [FieldOffset(12)] public uint Flags;
            [FieldOffset(16)] public float MaxDistanceErrorMeters;
            [FieldOffset(20)] public float MaxPoleErrorMeters;
            [FieldOffset(24)] public float CompletionMicros;
            [FieldOffset(28)] public float GlobalQualityWeight;
            [FieldOffset(32)] public float3 FirstShoulder;
            [FieldOffset(44)] public float3 FirstElbow;
            [FieldOffset(56)] public float3 FirstWrist;
            [FieldOffset(68)] public float3 FirstTargetLocal;
            [FieldOffset(80)] public uint StateHash;
            [FieldOffset(84)] public uint TargetHash;
            [FieldOffset(88)] public uint NaNCount;
            [FieldOffset(92)] public uint Marker;
            [FieldOffset(96)] private ulong _pad0;
            [FieldOffset(104)] private ulong _pad1;
            [FieldOffset(112)] private ulong _pad2;
            [FieldOffset(120)] private ulong _pad3;
        }

        public static class IkHandFlags
        {
            public const uint TargetValid = PlayerHandIkFlags.TargetValid;
            public const uint IkLocked = PlayerHandIkFlags.IkLocked;
            public const uint FreeTracking = PlayerHandIkFlags.FreeTracking;
            public const uint ReleaseBlend = PlayerHandIkFlags.ReleaseBlend;
            public const uint LeftHand = PlayerHandIkFlags.LeftHand;
            public const uint MockSource = PlayerHandIkFlags.MockSource;
            public const uint NonFinite = PlayerHandIkFlags.NonFinite;
            public const uint QualityScaled = PlayerHandIkFlags.QualityScaled;
            public const uint BudgetExceeded = PlayerHandIkFlags.BudgetExceeded;
            public const uint ConfigMockTargets = PlayerHandIkConfigFlags.MockTargets;
            public const uint ConfigDisableBridgeInput = PlayerHandIkConfigFlags.DisableBridgeInput;
        }

        public readonly ref struct HandIkVaultViews
        {
            public readonly NativeArray<IkHandStateDTO> States;
            public readonly NativeArray<IkHandStateDTO> PublishedStates;
            public readonly NativeArray<IkHandTargetDTO> Targets;
            public readonly NativeArray<float4x4> BoneMatrices;
            public readonly NativeArray<IkHandTelemetryEntry> Telemetry;
            public readonly NativeArray<int> TelemetryCursor;
            public readonly NativeArray<IkHandConfigDTO> Config;

            public HandIkVaultViews(
                NativeArray<IkHandStateDTO> states,
                NativeArray<IkHandStateDTO> publishedStates,
                NativeArray<IkHandTargetDTO> targets,
                NativeArray<float4x4> boneMatrices,
                NativeArray<IkHandTelemetryEntry> telemetry,
                NativeArray<int> telemetryCursor,
                NativeArray<IkHandConfigDTO> config)
            {
                States = states;
                PublishedStates = publishedStates;
                Targets = targets;
                BoneMatrices = boneMatrices;
                Telemetry = telemetry;
                TelemetryCursor = telemetryCursor;
                Config = config;
            }

            public bool IsValid()
            {
                return States.IsCreated &&
                       PublishedStates.IsCreated &&
                       Targets.IsCreated &&
                       BoneMatrices.IsCreated &&
                       Telemetry.IsCreated &&
                       TelemetryCursor.IsCreated &&
                       Config.IsCreated &&
                       States.Length >= HandIkHandCount &&
                       PublishedStates.Length >= HandIkHandCount &&
                       Targets.Length >= HandIkHandCount &&
                       BoneMatrices.Length >= HandIkMatrixCount &&
                       Telemetry.Length >= HandIkTelemetryFrameCount &&
                       TelemetryCursor.Length >= 1 &&
                       Config.Length >= 1;
            }
        }

        private void AllocateHandIkNativeState(IDataVault dataVault)
        {
            _ = _handIkStates.Ensure(dataVault, NativeArrayOptions.UninitializedMemory);
            _ = _handIkPublishedStates.Ensure(dataVault, NativeArrayOptions.ClearMemory);
            _ = _handIkTargets.Ensure(dataVault, NativeArrayOptions.UninitializedMemory);
            _ = _handIkBoneMatrices.Ensure(dataVault, NativeArrayOptions.UninitializedMemory);
            _ = _handIkTelemetry.Ensure(dataVault, NativeArrayOptions.UninitializedMemory);
            _ = _handIkTelemetryCursor.Ensure(dataVault, NativeArrayOptions.ClearMemory);
            _ = _handIkConfig.Ensure(dataVault, NativeArrayOptions.ClearMemory);
            _ = _handIkBridgeStates.TryBindExisting(dataVault);
            _ = _handIkBridgeTuning.TryBindExisting(dataVault);
            EnsureDefaultHandIkConfig();
            EnsureHandIkGraphicsBuffers();
        }

        private void ReleaseHandIkNativeState()
        {
            CompleteHandFabrikIkForTeardown();
            _handIkStates.ReleaseView();
            _handIkPublishedStates.ReleaseView();
            _handIkTargets.ReleaseView();
            _handIkBoneMatrices.ReleaseView();
            _handIkTelemetry.ReleaseView();
            _handIkTelemetryCursor.ReleaseView();
            _handIkConfig.ReleaseView();
            _handIkBridgeStates.ReleaseView();
            _handIkBridgeTuning.ReleaseView();
            ReleaseHandIkGraphicsBuffers();
        }

        private void ResetHandIkSessionState()
        {
            _handIkFrameIndex = 0u;
            _handIkGpuDataValid = false;
            _handIkGpuDirty = false;
            if (_handIkTelemetryCursor.IsCreated && _handIkTelemetryCursor.Length > 0)
                _handIkTelemetryCursor[0] = 0;
        }

        private void ScheduleHandFabrikIk(float deltaTime)
        {
            if (_handIkJobPending || !TryPinHandIkJobBuffers())
                return;

            bool keepJobPins = false;
            try
            {
                if (!TryResolveHandIkViews(out HandIkVaultViews views))
                    return;

                IkHandConfigDTO config = views.Config[0];
                float safeDelta = math.max(0.0001f, SanitizeNonNegative(deltaTime));
                float qualityWeight = ResolveHandIkQuality(config);
                bool mockTargets = (config.Flags & IkHandFlags.ConfigMockTargets) != 0u;
                bool bridgeInputEnabled = (config.Flags & IkHandFlags.ConfigDisableBridgeInput) == 0u;
                NativeArray<VRHandStateDTO> bridgeStates = default;
                NativeArray<VRInteractionTuningDTO> bridgeTuning = default;
                bool bridgeAvailable = false;
                if (bridgeInputEnabled && TryPinHandIkBridgeBuffers())
                {
                    bridgeAvailable = TryResolveHandIkBridgeViews(out bridgeStates, out bridgeTuning);
                    if (!bridgeAvailable)
                        ReleaseHandIkBridgePins();
                }

                if (!mockTargets && !bridgeAvailable)
                    return;

                double3 fallbackRootAup = ResolveHandIkFallbackRootAup();
                if (!math.all(math.isfinite(fallbackRootAup)))
                    return;

                uint frame = ++_handIkFrameIndex;
                double3 rootAup = bridgeAvailable
                    ? ResolveHandIkRootAup(bridgeTuning, fallbackRootAup)
                    : fallbackRootAup;
                JobHandle targetHandle;
                if (mockTargets)
                {
                    targetHandle = new GenerateMockIkTargetsJob
                    {
                        States = views.States,
                        Targets = views.Targets,
                        Config = views.Config,
                        RootAUP = rootAup,
                        DeltaTime = safeDelta,
                        GlobalQualityWeight = qualityWeight,
                        FrameIndex = frame
                    }.Schedule(HandIkHandCount, 1);
                }
                else
                {
                    targetHandle = new BuildHandIkTargetsFromBridgeJob
                    {
                        BridgeStates = bridgeStates,
                        BridgeTuning = bridgeTuning,
                        States = views.States,
                        Targets = views.Targets,
                        Config = views.Config,
                        DeltaTime = safeDelta,
                        GlobalQualityWeight = qualityWeight,
                        FrameIndex = frame,
                        FallbackRootAUP = rootAup
                    }.Schedule(HandIkHandCount, 1);
                }

                JobHandle solveHandle = new EvaluateHandIkJob
                {
                    States = views.States,
                    Targets = views.Targets,
                    Telemetry = views.Telemetry,
                    TelemetryCursor = views.TelemetryCursor,
                    ActiveHandCount = HandIkHandCount,
                    FrameIndex = frame
                }.Schedule(HandIkHandCount, 1, targetHandle);

                _handIkJobHandle = new BuildHandBoneMatricesJob
                {
                    States = views.States,
                    BoneMatrices = views.BoneMatrices,
                    ActiveHandCount = HandIkHandCount
                }.Schedule(HandIkHandCount, 1, solveHandle);
                _handIkJobPending = true;
                _handIkScheduleTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                H8Memory.RegisterActiveJob(OwnerSystemId, _handIkJobHandle);
                keepJobPins = true;
                JobHandle.ScheduleBatchedJobs();
            }
            finally
            {
                if (!keepJobPins)
                    ReleaseHandIkJobPins();
            }
        }

        private bool TryFinalizeHandFabrikIkJob()
        {
            if (!_handIkJobPending)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _handIkJobHandle))
                return false;

            _handIkJobPending = false;
            _handIkGpuDataValid = true;
            _handIkGpuDirty = true;
            try
            {
                PublishHandIkStatesForAnimation();
                float elapsedMicros = ResolveHandIkElapsedMicros(_handIkScheduleTimestamp);
                PatchHandIkCompletionTelemetry(elapsedMicros);
                if (elapsedMicros > HandIkBudgetMicros || LatestHandIkTelemetryHasFault())
                    DumpHandIkTelemetryFaultOnly();
            }
            finally
            {
                ReleaseHandIkJobPins();
            }

            return true;
        }

        private void CompleteHandFabrikIkForTeardown()
        {
            if (!_handIkJobPending)
            {
                ReleaseHandIkJobPins();
                return;
            }

            ForceCompleteHandFabrikIkInPostSimulationWindow();
            _handIkJobPending = false;
            _handIkGpuDataValid = true;
            _handIkGpuDirty = true;
            try
            {
                PublishHandIkStatesForAnimation();
                PatchHandIkCompletionTelemetry(ResolveHandIkElapsedMicros(_handIkScheduleTimestamp));
            }
            finally
            {
                ReleaseHandIkJobPins();
            }
        }

        private void ForceCompleteHandFabrikIkInPostSimulationWindow()
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                DispatcherJobFence.TryComplete(ref _handIkJobHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        private void UploadHandFabrikIkGpuBuffers()
        {
            if (!_handIkGpuDataValid || !_handIkGpuDirty || !_handIkBoneMatrices.IsCreated)
                return;

            EnsureHandIkGraphicsBuffers();
            GraphicsBuffer writeBuffer = _handIkGpuBufferIndex == 0 ? _handIkMatrixBufferA : _handIkMatrixBufferB;
            if (!HandIkGraphicsBufferUpload.HasValidGraphicsBuffer<float4x4>(writeBuffer, HandIkMatrixCount))
                return;

            NativeArray<float4x4> matrices = _handIkBoneMatrices;
            HandIkGraphicsBufferUpload.UploadNativeArray(writeBuffer, matrices, HandIkMatrixCount);
            _handIkGpuBufferIndex ^= 1;
            _handIkGpuDirty = false;
        }

        internal bool TryGetHandIkGraphicsBuffer(out GraphicsBuffer buffer, out int matrixCount)
        {
            matrixCount = HandIkMatrixCount;
            buffer = _handIkGpuBufferIndex == 0 ? _handIkMatrixBufferB : _handIkMatrixBufferA;
            return _handIkGpuDataValid && HandIkGraphicsBufferUpload.HasValidGraphicsBuffer<float4x4>(buffer, HandIkMatrixCount);
        }

        private bool TryResolveHandIkViews(out HandIkVaultViews views)
        {
            NativeArray<IkHandStateDTO> states = _handIkStates;
            NativeArray<IkHandStateDTO> publishedStates = _handIkPublishedStates;
            NativeArray<IkHandTargetDTO> targets = _handIkTargets;
            NativeArray<float4x4> matrices = _handIkBoneMatrices;
            NativeArray<IkHandTelemetryEntry> telemetry = _handIkTelemetry;
            NativeArray<int> cursor = _handIkTelemetryCursor;
            NativeArray<IkHandConfigDTO> config = _handIkConfig;
            views = new HandIkVaultViews(states, publishedStates, targets, matrices, telemetry, cursor, config);
            return views.IsValid();
        }

        private bool TryResolveHandIkBridgeViews(
            out NativeArray<VRHandStateDTO> bridgeStates,
            out NativeArray<VRInteractionTuningDTO> bridgeTuning)
        {
            bridgeStates = _handIkBridgeStates;
            bridgeTuning = _handIkBridgeTuning;
            if (bridgeStates.IsCreated &&
                bridgeStates.Length >= HandIkHandCount &&
                bridgeTuning.IsCreated &&
                bridgeTuning.Length >= 1)
            {
                return true;
            }

            bridgeStates = default;
            bridgeTuning = default;
            return false;
        }

        private bool TryPinHandIkJobBuffers()
        {
            ReleaseHandIkJobPins();
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            bool pinned = false;
            try
            {
                _handIkJobPinVault = vault;
                if (!TryLockHandIkJobBuffer(vault, HandIkStatesBuffer, HandIkJobPinStates) ||
                    !TryLockHandIkJobBuffer(vault, HandIkPublishedStatesBuffer, HandIkJobPinPublishedStates) ||
                    !TryLockHandIkJobBuffer(vault, HandIkTargetsBuffer, HandIkJobPinTargets) ||
                    !TryLockHandIkJobBuffer(vault, HandIkBoneMatricesBuffer, HandIkJobPinBoneMatrices) ||
                    !TryLockHandIkJobBuffer(vault, HandIkTelemetryRingBuffer, HandIkJobPinTelemetryRing) ||
                    !TryLockHandIkJobBuffer(vault, HandIkTelemetryCursorBuffer, HandIkJobPinTelemetryCursor) ||
                    !TryLockHandIkJobBuffer(vault, HandIkConfigBuffer, HandIkJobPinConfig))
                {
                    return false;
                }

                if (!TryResolveHandIkViews(out HandIkVaultViews views) || !views.IsValid())
                    return false;

                pinned = true;
                return true;
            }
            finally
            {
                if (!pinned)
                    ReleaseHandIkJobPins();
            }
        }

        private bool TryPinHandIkBridgeBuffers()
        {
            IDataVault vault = _handIkJobPinVault ?? _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            bool pinned = false;
            try
            {
                if (!TryLockHandIkJobBuffer(vault, BufferID.VRInteractionHandStates, HandIkJobPinBridgeStates) ||
                    !TryLockHandIkJobBuffer(vault, BufferID.VRInteractionTuning, HandIkJobPinBridgeTuning))
                {
                    return false;
                }

                pinned = true;
                return true;
            }
            finally
            {
                if (!pinned)
                    ReleaseHandIkBridgePins();
            }
        }

        private void ReleaseHandIkBridgePins()
        {
            IDataVault vault = _handIkJobPinVault;
            if (vault == null)
                return;

            uint bridgeMask = _handIkJobPinMask & (HandIkJobPinBridgeStates | HandIkJobPinBridgeTuning);
            if (bridgeMask == 0u)
                return;

            TryUnlockHandIkJobBuffer(vault, bridgeMask, HandIkJobPinBridgeTuning, BufferID.VRInteractionTuning);
            TryUnlockHandIkJobBuffer(vault, bridgeMask, HandIkJobPinBridgeStates, BufferID.VRInteractionHandStates);
            _handIkJobPinMask &= ~(HandIkJobPinBridgeStates | HandIkJobPinBridgeTuning);
            if (_handIkJobPinMask == 0u)
                _handIkJobPinVault = null;
        }

        private void ReleaseHandIkJobPins()
        {
            IDataVault vault = _handIkJobPinVault;
            uint pinMask = _handIkJobPinMask;
            _handIkJobPinVault = null;
            _handIkJobPinMask = 0u;
            if (vault == null || pinMask == 0u)
                return;

            TryUnlockHandIkJobBuffer(vault, pinMask, HandIkJobPinBridgeTuning, BufferID.VRInteractionTuning);
            TryUnlockHandIkJobBuffer(vault, pinMask, HandIkJobPinBridgeStates, BufferID.VRInteractionHandStates);
            TryUnlockHandIkJobBuffer(vault, pinMask, HandIkJobPinConfig, HandIkConfigBuffer);
            TryUnlockHandIkJobBuffer(vault, pinMask, HandIkJobPinTelemetryCursor, HandIkTelemetryCursorBuffer);
            TryUnlockHandIkJobBuffer(vault, pinMask, HandIkJobPinTelemetryRing, HandIkTelemetryRingBuffer);
            TryUnlockHandIkJobBuffer(vault, pinMask, HandIkJobPinBoneMatrices, HandIkBoneMatricesBuffer);
            TryUnlockHandIkJobBuffer(vault, pinMask, HandIkJobPinTargets, HandIkTargetsBuffer);
            TryUnlockHandIkJobBuffer(vault, pinMask, HandIkJobPinPublishedStates, HandIkPublishedStatesBuffer);
            TryUnlockHandIkJobBuffer(vault, pinMask, HandIkJobPinStates, HandIkStatesBuffer);
        }

        private bool TryLockHandIkJobBuffer(IDataVault vault, BufferID bufferId, uint pinBit)
        {
            if ((_handIkJobPinMask & pinBit) != 0u)
                return true;

            if (vault == null ||
                (_handIkJobPinVault != null && !ReferenceEquals(_handIkJobPinVault, vault)) ||
                !vault.TryLockBuffer(bufferId, OwnerSystemId))
            {
                return false;
            }

            _handIkJobPinVault = vault;
            _handIkJobPinMask |= pinBit;
            return true;
        }

        private static void TryUnlockHandIkJobBuffer(IDataVault vault, uint pinMask, uint pinBit, BufferID bufferId)
        {
            if ((pinMask & pinBit) != 0u)
                vault.TryUnlockBuffer(bufferId, OwnerSystemId);
        }

        private void EnsureDefaultHandIkConfig()
        {
            if (!_handIkConfig.IsCreated || _handIkConfig.Length < 1)
                return;

            IkHandConfigDTO config = _handIkConfig[0];
            if (config.DefaultUpperArmLength <= 0.0001f || !math.isfinite(config.DefaultUpperArmLength))
                config.DefaultUpperArmLength = 0.34f;
            if (config.DefaultForearmLength <= 0.0001f || !math.isfinite(config.DefaultForearmLength))
                config.DefaultForearmLength = 0.34f;
            if (config.BlendOutSeconds <= 0.0001f || !math.isfinite(config.BlendOutSeconds))
                config.BlendOutSeconds = 0.25f;
            if (config.BlendOutSpeed <= 0.0001f || !math.isfinite(config.BlendOutSpeed))
                config.BlendOutSpeed = 4.0f;
            if (config.MaxFabrikIterations <= 0.0001f || !math.isfinite(config.MaxFabrikIterations))
                config.MaxFabrikIterations = 8.0f;
            if (!math.isfinite(config.GlobalQualityWeightOverride))
                config.GlobalQualityWeightOverride = -1.0f;
            if (config.ShoulderLateralMeters <= 0.0001f || !math.isfinite(config.ShoulderLateralMeters))
                config.ShoulderLateralMeters = 0.18f;
            if (config.ShoulderHeightMeters <= 0.0001f || !math.isfinite(config.ShoulderHeightMeters))
                config.ShoulderHeightMeters = 1.38f;
            if (!math.isfinite(config.ShoulderForwardMeters))
                config.ShoulderForwardMeters = 0.08f;
            _handIkConfig[0] = config;
        }

        private float ResolveHandIkQuality(in IkHandConfigDTO config)
        {
            float overrideValue = config.GlobalQualityWeightOverride;
            if (math.isfinite(overrideValue) && overrideValue >= 0.0f)
                return math.saturate(overrideValue);

            return ReadCachedGlobalQualityWeight01();
        }

        private double3 ResolveHandIkFallbackRootAup()
        {
            if (TryReadPlayerKinematicStateFromVault(out AbsoluteUniversePosition playerAup))
            {
                double3 playerRoot = ToAbsoluteDouble3(in playerAup);
                if (math.all(math.isfinite(playerRoot)))
                    return playerRoot;
            }

            double3 origin = _handIkHasFloatingOriginSnapshot != 0 ? _handIkCachedFloatingOriginAup : double3.zero;
            if (!math.all(math.isfinite(origin)))
                origin = double3.zero;

            float3 fallbackRuntime = ReadPositionSnapshot(ReadLastValidPosition());
            float3 runtimeRoot = SanitizeFloat3(ToFloat3(ResolveBodyRuntimePosition()), fallbackRuntime);
            double3 root = origin + new double3(runtimeRoot.x, runtimeRoot.y, runtimeRoot.z);
            return math.all(math.isfinite(root)) ? root : origin;
        }

        private static double3 ResolveHandIkRootAup(NativeArray<VRInteractionTuningDTO> tuning, double3 fallbackRootAup)
        {
            if (tuning.IsCreated && tuning.Length > 0 && math.all(math.isfinite(tuning[0].PlayerRootAUP)))
                return tuning[0].PlayerRootAUP;

            return fallbackRootAup;
        }

        private void PatchHandIkCompletionTelemetry(float completionMicros)
        {
            if (!_handIkTelemetry.IsCreated || _handIkTelemetry.Length < HandIkTelemetryFrameCount)
                return;

            int cursor = _handIkTelemetryCursor.IsCreated && _handIkTelemetryCursor.Length > 0
                ? math.max(0, _handIkTelemetryCursor[0])
                : (int)(_handIkFrameIndex % HandIkTelemetryFrameCount);
            int index = cursor % _handIkTelemetry.Length;
            IkHandTelemetryEntry entry = _handIkTelemetry[index];
            entry.CompletionMicros = completionMicros;
            if (completionMicros > HandIkBudgetMicros)
                entry.Flags |= IkHandFlags.BudgetExceeded;
            _handIkTelemetry[index] = entry;
        }

        private bool LatestHandIkTelemetryHasFault()
        {
            if (!_handIkTelemetry.IsCreated || _handIkTelemetry.Length < HandIkTelemetryFrameCount)
                return false;

            int cursor = _handIkTelemetryCursor.IsCreated && _handIkTelemetryCursor.Length > 0
                ? math.max(0, _handIkTelemetryCursor[0])
                : (int)(_handIkFrameIndex % HandIkTelemetryFrameCount);
            int index = cursor % _handIkTelemetry.Length;
            IkHandTelemetryEntry entry = _handIkTelemetry[index];
            if (entry.Marker != HandIkTelemetryMarker)
                return false;

            return (entry.Flags & IkHandFlags.NonFinite) != 0u ||
                   entry.NaNCount != 0u ||
                   !math.isfinite(entry.MaxDistanceErrorMeters) ||
                   !math.isfinite(entry.MaxPoleErrorMeters) ||
                   !math.isfinite(entry.GlobalQualityWeight) ||
                   !math.all(math.isfinite(entry.FirstShoulder)) ||
                   !math.all(math.isfinite(entry.FirstElbow)) ||
                   !math.all(math.isfinite(entry.FirstWrist)) ||
                   !math.all(math.isfinite(entry.FirstTargetLocal));
        }

        private void PublishHandIkStatesForAnimation()
        {
            const uint requiredPins = HandIkJobPinStates | HandIkJobPinPublishedStates;
            if ((_handIkJobPinMask & requiredPins) != requiredPins ||
                !_handIkStates.IsCreated ||
                !_handIkPublishedStates.IsCreated)
            {
                return;
            }

            NativeArray<IkHandStateDTO> source = _handIkStates;
            NativeArray<IkHandStateDTO> destination = _handIkPublishedStates;
            int count = math.min(math.min(source.Length, destination.Length), HandIkHandCount);
            for (int i = 0; i < count; i++)
                destination[i] = source[i];
        }

        private void RefreshHandIkFloatingOriginSnapshotCold(double3 origin)
        {
            if (!math.all(math.isfinite(origin)))
                return;

            _handIkCachedFloatingOriginAup = origin;
            _handIkHasFloatingOriginSnapshot = 1;
        }

        private void CaptureHandIkOriginShiftSnapshot(in OriginShiftEventData shiftData)
        {
            if (!math.all(math.isfinite(shiftData.NewTotalOffsetDouble)))
                return;

            _handIkCachedFloatingOriginAup = shiftData.NewTotalOffsetDouble;
            _handIkHasFloatingOriginSnapshot = 1;
        }

        private unsafe bool DumpHandIkTelemetryFaultOnly()
        {
            return _handIkTelemetry.IsCreated && _handIkTelemetry.Length >= HandIkTelemetryFrameCount;
        }

        private static float ResolveHandIkElapsedMicros(long startTicks)
        {
            long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
            if (elapsed <= 0L)
                return 0.0f;

            double micros = (elapsed * 1000000.0d) / System.Diagnostics.Stopwatch.Frequency;
            if (double.IsNaN(micros) || double.IsInfinity(micros) || micros <= 0.0d)
                return 0.0f;

            return micros >= float.MaxValue ? float.MaxValue : (float)micros;
        }

        private void EnsureHandIkGraphicsBuffers()
        {
            if (!HandIkGraphicsBufferUpload.HasValidGraphicsBuffer<float4x4>(_handIkMatrixBufferA, HandIkMatrixCount))
            {
                ReleaseHandIkGraphicsBuffer(ref _handIkMatrixBufferA);
                _handIkMatrixBufferA = HandIkGraphicsBufferUpload.CreateStructuredLockBuffer<float4x4>(HandIkMatrixCount);
            }

            if (!HandIkGraphicsBufferUpload.HasValidGraphicsBuffer<float4x4>(_handIkMatrixBufferB, HandIkMatrixCount))
            {
                ReleaseHandIkGraphicsBuffer(ref _handIkMatrixBufferB);
                _handIkMatrixBufferB = HandIkGraphicsBufferUpload.CreateStructuredLockBuffer<float4x4>(HandIkMatrixCount);
            }
        }

        private void ReleaseHandIkGraphicsBuffers()
        {
            ReleaseHandIkGraphicsBuffer(ref _handIkMatrixBufferA);
            ReleaseHandIkGraphicsBuffer(ref _handIkMatrixBufferB);
            _handIkGpuDataValid = false;
            _handIkGpuDirty = false;
        }

        private static void ReleaseHandIkGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct BuildHandIkTargetsFromBridgeJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<VRHandStateDTO> BridgeStates;
            [ReadOnly, NoAlias] public NativeArray<VRInteractionTuningDTO> BridgeTuning;
            [NoAlias] public NativeArray<IkHandStateDTO> States;
            [NoAlias] public NativeArray<IkHandTargetDTO> Targets;
            [ReadOnly, NoAlias] public NativeArray<IkHandConfigDTO> Config;
            public float DeltaTime;
            public float GlobalQualityWeight;
            public uint FrameIndex;
            public double3 FallbackRootAUP;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)Targets.Length ||
                    (uint)index >= (uint)States.Length ||
                    (uint)index >= (uint)BridgeStates.Length ||
                    !Config.IsCreated ||
                    Config.Length < 1)
                {
                    return;
                }

                IkHandConfigDTO config = Config[0];
                VRHandStateDTO bridge = BridgeStates[index];
                double3 rootAup = BridgeTuning.IsCreated && BridgeTuning.Length > 0 && IsFinite(BridgeTuning[0].PlayerRootAUP)
                    ? BridgeTuning[0].PlayerRootAUP
                    : FallbackRootAUP;
                float side = index == 0 ? -1.0f : 1.0f;
                float3 shoulderLocal = new float3(
                    side * math.max(0.01f, SanitizeNonNegative(config.ShoulderLateralMeters)),
                    math.max(0.1f, SanitizeNonNegative(config.ShoulderHeightMeters)),
                    math.select(config.ShoulderForwardMeters, 0.08f, !math.isfinite(config.ShoulderForwardMeters)));
                double3 shoulderAup = rootAup + new double3(shoulderLocal.x, shoulderLocal.y, shoulderLocal.z);
                double3 targetAup = IsFinite(bridge.ResolvedHandAUP) ? bridge.ResolvedHandAUP : bridge.RawControllerAUP;
                double3 rawAup = IsFinite(bridge.RawControllerAUP) ? bridge.RawControllerAUP : targetAup;
                uint targetFlags = IkHandFlags.TargetValid;
                if (index == 0)
                    targetFlags |= IkHandFlags.LeftHand;

                bool locked = (bridge.InteractionFlags & VRInteractionBridgeContract.StateFlagSocketSnapped) != 0u;
                targetFlags |= locked ? IkHandFlags.IkLocked : IkHandFlags.FreeTracking;
                if (!IsFinite(targetAup) || !IsFinite(rawAup) || !IsFinite(rootAup))
                    targetFlags |= IkHandFlags.NonFinite;

                IkHandTargetDTO target = default;
                target.PlayerRootAUP = rootAup;
                target.ShoulderAUP = shoulderAup;
                target.TargetAUP = targetAup;
                target.RawControllerAUP = rawAup;
                target.PoleLocal = shoulderLocal + new float3(side * 0.32f, -0.45f, 0.16f);
                target.BlendSecondsRemaining = math.max(0.0f, SanitizeNonNegative(config.BlendOutSeconds));
                target.DeltaTime = math.max(0.0001f, SanitizeNonNegative(DeltaTime));
                target.GlobalQualityWeight = math.saturate(math.select(1.0f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
                target.TargetHashID = HashHandTarget(targetAup, rawAup, (uint)index, FrameIndex);
                target.Flags = targetFlags | EncodeIterationLimit(config.MaxFabrikIterations);
                Targets[index] = target;

                IkHandStateDTO state = States[index];
                if (state.UpperArmLength <= 0.0001f || !math.isfinite(state.UpperArmLength))
                    state.UpperArmLength = math.max(0.05f, SanitizeNonNegative(config.DefaultUpperArmLength));
                if (state.ForearmLength <= 0.0001f || !math.isfinite(state.ForearmLength))
                    state.ForearmLength = math.max(0.05f, SanitizeNonNegative(config.DefaultForearmLength));
                if (!IsFinite(state.ShoulderPos))
                    state.ShoulderPos = shoulderLocal;
                if (!IsFinite(state.ElbowPos))
                    state.ElbowPos = shoulderLocal + new float3(side * 0.18f, -state.UpperArmLength, 0.05f);
                if (!IsFinite(state.WristPos))
                    state.WristPos = shoulderLocal + new float3(side * 0.24f, -(state.UpperArmLength + state.ForearmLength), 0.15f);
                state.TargetHashID = target.TargetHashID;
                States[index] = state;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct GenerateMockIkTargetsJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<IkHandStateDTO> States;
            [NoAlias] public NativeArray<IkHandTargetDTO> Targets;
            [ReadOnly, NoAlias] public NativeArray<IkHandConfigDTO> Config;
            public double3 RootAUP;
            public float DeltaTime;
            public float GlobalQualityWeight;
            public uint FrameIndex;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)Targets.Length ||
                    (uint)index >= (uint)States.Length ||
                    !Config.IsCreated ||
                    Config.Length < 1)
                {
                    return;
                }

                IkHandConfigDTO config = Config[0];
                float side = index == 0 ? -1.0f : 1.0f;
                float phase = ((FrameIndex * math.max(0.0001f, DeltaTime)) + index * 0.41f) * 6.28318530718f;
                float waveX = MathLodApproximation.ApproxSinBhaskara(phase);
                float waveY = MathLodApproximation.ApproxSinBhaskara(phase * 2.0f);
                float3 shoulder = new float3(
                    side * math.max(0.01f, SanitizeNonNegative(config.ShoulderLateralMeters)),
                    math.max(0.1f, SanitizeNonNegative(config.ShoulderHeightMeters)),
                    math.select(config.ShoulderForwardMeters, 0.08f, !math.isfinite(config.ShoulderForwardMeters)));
                float3 targetLocal = shoulder + new float3(
                    side * (0.24f + waveX * 0.16f),
                    -0.28f + waveY * 0.08f,
                    0.34f + waveX * waveY * 0.12f);
                double3 shoulderAup = RootAUP + new double3(shoulder.x, shoulder.y, shoulder.z);
                double3 targetAup = RootAUP + new double3(targetLocal.x, targetLocal.y, targetLocal.z);

                IkHandTargetDTO target = default;
                target.PlayerRootAUP = RootAUP;
                target.ShoulderAUP = shoulderAup;
                target.TargetAUP = targetAup;
                target.RawControllerAUP = targetAup;
                target.PoleLocal = shoulder + new float3(side * 0.32f, -0.45f, 0.16f);
                target.BlendSecondsRemaining = math.max(0.0f, SanitizeNonNegative(config.BlendOutSeconds));
                target.DeltaTime = math.max(0.0001f, SanitizeNonNegative(DeltaTime));
                target.GlobalQualityWeight = math.saturate(math.select(1.0f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
                target.TargetHashID = HashHandTarget(targetAup, targetAup, (uint)index, FrameIndex);
                target.Flags = IkHandFlags.TargetValid |
                    IkHandFlags.IkLocked |
                    IkHandFlags.MockSource |
                    EncodeIterationLimit(config.MaxFabrikIterations) |
                    (index == 0 ? IkHandFlags.LeftHand : 0u);
                Targets[index] = target;

                IkHandStateDTO state = States[index];
                state.ShoulderPos = shoulder;
                state.UpperArmLength = math.max(0.05f, SanitizeNonNegative(config.DefaultUpperArmLength));
                state.ForearmLength = math.max(0.05f, SanitizeNonNegative(config.DefaultForearmLength));
                if (!IsFinite(state.ElbowPos))
                    state.ElbowPos = shoulder + new float3(side * 0.18f, -state.UpperArmLength, 0.05f);
                if (!IsFinite(state.WristPos))
                    state.WristPos = shoulder + new float3(side * 0.24f, -(state.UpperArmLength + state.ForearmLength), 0.15f);
                state.TargetHashID = target.TargetHashID;
                States[index] = state;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal unsafe struct EvaluateHandIkJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<IkHandStateDTO> States;
            [ReadOnly, NoAlias] public NativeArray<IkHandTargetDTO> Targets;
            [NoAlias] public NativeArray<IkHandTelemetryEntry> Telemetry;
            [NoAlias] public NativeArray<int> TelemetryCursor;
            public int ActiveHandCount;
            public uint FrameIndex;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)ActiveHandCount ||
                    (uint)index >= (uint)States.Length ||
                    (uint)index >= (uint)Targets.Length)
                {
                    return;
                }

                // SAFETY: States and Targets are separate Vault lanes. Each worker writes one 64B state row by index.
                byte* stateBase = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(States);
                byte* targetBase = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Targets);
                ref IkHandStateDTO state = ref UnsafeUtility.AsRef<IkHandStateDTO>(stateBase + index * UnsafeUtility.SizeOf<IkHandStateDTO>());
                ref readonly IkHandTargetDTO target = ref UnsafeUtility.AsRef<IkHandTargetDTO>(targetBase + index * UnsafeUtility.SizeOf<IkHandTargetDTO>());

                float3 shoulder = ToLocalFloat3(target.ShoulderAUP, target.PlayerRootAUP, state.ShoulderPos);
                float3 targetLocal = ToLocalFloat3(target.TargetAUP, target.PlayerRootAUP, state.WristPos);
                float3 rawLocal = ToLocalFloat3(target.RawControllerAUP, target.PlayerRootAUP, targetLocal);
                float3 poleLocal = SanitizeFloat3(target.PoleLocal, shoulder + new float3(0.0f, -0.35f, 0.16f));
                float upper = math.max(0.05f, SanitizeNonNegative(state.UpperArmLength));
                float fore = math.max(0.05f, SanitizeNonNegative(state.ForearmLength));
                float quality = math.saturate(math.select(1.0f, target.GlobalQualityWeight, math.isfinite(target.GlobalQualityWeight)));
                int maxConfigured = DecodeIterationLimit(target.Flags);
                int iterations = math.clamp((int)math.round(math.lerp(1.0f, maxConfigured, quality)), 1, 8);
                uint flags = target.Flags;
                if (iterations < 8)
                    flags |= IkHandFlags.QualityScaled;

                bool targetFinite = IsFinite(shoulder) && IsFinite(targetLocal) && IsFinite(rawLocal) && IsFinite(poleLocal);
                if (!targetFinite)
                {
                    flags |= IkHandFlags.NonFinite;
                    targetLocal = SanitizeFloat3(targetLocal, state.WristPos);
                    rawLocal = SanitizeFloat3(rawLocal, targetLocal);
                    shoulder = SanitizeFloat3(shoulder, state.ShoulderPos);
                    poleLocal = SanitizeFloat3(poleLocal, shoulder + new float3(0.0f, -0.35f, 0.16f));
                }

                bool lockedNow = (target.Flags & IkHandFlags.IkLocked) != 0u;
                bool wasLocked = (state.Flags & IkHandFlags.IkLocked) != 0u;
                float blendSeconds = DecodeReleaseSeconds(state.Flags, target.BlendSecondsRemaining);
                float blendWindow = math.max(0.0001f, SanitizeNonNegative(target.BlendSecondsRemaining));
                if (lockedNow)
                    blendSeconds = blendWindow;
                else if (wasLocked)
                    blendSeconds = blendWindow;
                else
                    blendSeconds = math.max(0.0f, blendSeconds - math.max(0.0001f, SanitizeNonNegative(target.DeltaTime)));

                float lockWeight = lockedNow ? 1.0f : math.saturate(blendSeconds * math.rcp(blendWindow));
                if (lockWeight > 0.0001f && !lockedNow)
                    flags |= IkHandFlags.ReleaseBlend;
                if (!lockedNow)
                    flags |= IkHandFlags.FreeTracking;

                float3 solvedShoulder = shoulder;
                float3 solvedElbow = SanitizeFloat3(state.ElbowPos, shoulder + new float3(0.0f, -upper, 0.0f));
                float3 solvedWrist = SanitizeFloat3(state.WristPos, shoulder + new float3(0.0f, -(upper + fore), 0.0f));
                SolveFabrikTwoBone(
                    ref solvedShoulder,
                    ref solvedElbow,
                    ref solvedWrist,
                    upper,
                    fore,
                    targetLocal,
                    poleLocal,
                    iterations,
                    out float maxError,
                    out float poleError);

                state.ShoulderPos = shoulder;
                state.ElbowPos = math.lerp(ResolveFreeElbow(shoulder, rawLocal, poleLocal, upper, fore), solvedElbow, lockWeight);
                state.WristPos = math.lerp(rawLocal, solvedWrist, lockWeight);
                state.UpperArmLength = upper;
                state.ForearmLength = fore;
                state.TargetHashID = target.TargetHashID;
                state.Flags = (flags & ~ReleaseSecondsMask) | EncodeReleaseSeconds(blendSeconds, blendWindow);

                WriteTelemetry(index, iterations, maxError, poleError, quality, shoulder, state.ElbowPos, state.WristPos, targetLocal, flags, target.TargetHashID);
            }

            private void WriteTelemetry(
                int handIndex,
                int iterations,
                float maxError,
                float poleError,
                float quality,
                float3 shoulder,
                float3 elbow,
                float3 wrist,
                float3 targetLocal,
                uint flags,
                uint targetHash)
            {
                if (handIndex != 0 ||
                    !Telemetry.IsCreated ||
                    Telemetry.Length < HandIkTelemetryFrameCount ||
                    !TelemetryCursor.IsCreated ||
                    TelemetryCursor.Length < 1)
                {
                    return;
                }

                int slot = (int)(FrameIndex % (uint)math.max(1, Telemetry.Length));
                IkHandTelemetryEntry entry = default;
                entry.FrameIndex = FrameIndex;
                entry.ArmsProcessed = (uint)math.max(0, ActiveHandCount);
                entry.ActiveIterationLimit = (uint)iterations;
                entry.Flags = flags;
                entry.MaxDistanceErrorMeters = SanitizeNonNegative(maxError);
                entry.MaxPoleErrorMeters = SanitizeNonNegative(poleError);
                entry.GlobalQualityWeight = quality;
                entry.FirstShoulder = shoulder;
                entry.FirstElbow = elbow;
                entry.FirstWrist = wrist;
                entry.FirstTargetLocal = targetLocal;
                entry.StateHash = MixHash(math.hash(shoulder), math.hash(elbow), math.hash(wrist), flags);
                entry.TargetHash = targetHash;
                entry.NaNCount = IsFinite(shoulder) && IsFinite(elbow) && IsFinite(wrist) ? 0u : 1u;
                entry.Marker = HandIkTelemetryMarker;
                Telemetry[slot] = entry;
                TelemetryCursor[0] = slot;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        internal struct BuildHandBoneMatricesJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<IkHandStateDTO> States;
            [NoAlias] public NativeArray<float4x4> BoneMatrices;
            public int ActiveHandCount;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)ActiveHandCount ||
                    (uint)index >= (uint)States.Length)
                {
                    return;
                }

                int matrixStart = index * HandIkMatricesPerHand;
                if (matrixStart + 2 >= BoneMatrices.Length)
                    return;

                IkHandStateDTO state = States[index];
                float3 shoulder = SanitizeFloat3(state.ShoulderPos, float3.zero);
                float3 elbow = SanitizeFloat3(state.ElbowPos, shoulder + new float3(0.0f, -0.25f, 0.0f));
                float3 wrist = SanitizeFloat3(state.WristPos, elbow + new float3(0.0f, -0.25f, 0.0f));
                quaternion shoulderRotation = SafeLookRotation(elbow - shoulder, ResolveUp(shoulder, elbow, wrist));
                quaternion elbowRotation = SafeLookRotation(wrist - elbow, ResolveUp(shoulder, elbow, wrist));
                quaternion wristRotation = SafeLookRotation(wrist - elbow, new float3(0.0f, 1.0f, 0.0f));
                BoneMatrices[matrixStart] = float4x4.TRS(shoulder, shoulderRotation, new float3(1.0f));
                BoneMatrices[matrixStart + 1] = float4x4.TRS(elbow, elbowRotation, new float3(1.0f));
                BoneMatrices[matrixStart + 2] = float4x4.TRS(wrist, wristRotation, new float3(1.0f));
            }
        }

        #if UNITY_EDITOR
        public static class HandIkProfileCsvParser
        {
            public static int Parse(ReadOnlySpan<byte> bytes, NativeArray<IkHandConfigDTO> output)
            {
                if (!output.IsCreated || output.Length == 0 || bytes.Length == 0)
                    return 0;

                int write = 0;
                int lineStart = 0;
                for (int i = 0; i <= bytes.Length; i++)
                {
                    if (i < bytes.Length && bytes[i] != (byte)'\n')
                        continue;

                    int lineEnd = i;
                    if (lineEnd > lineStart && bytes[lineEnd - 1] == (byte)'\r')
                        lineEnd--;

                    if (TryParseLine(bytes.Slice(lineStart, lineEnd - lineStart), out IkHandConfigDTO config))
                    {
                        output[write++] = config;
                        if (write >= output.Length)
                            return write;
                    }

                    lineStart = i + 1;
                }

                return write;
            }

            private static bool TryParseLine(ReadOnlySpan<byte> line, out IkHandConfigDTO config)
            {
                config = default;
                if (line.Length == 0 || line[0] == (byte)'#')
                    return false;

                int field = 0;
                int start = 0;
                for (int i = 0; i <= line.Length; i++)
                {
                    if (i < line.Length && line[i] != (byte)',')
                        continue;

                    ReadOnlySpan<byte> token = Trim(line.Slice(start, i - start));
                    if (field == 0)
                    {
                        config.SuitHash = HashToken(token);
                    }
                    else if (field == 1)
                    {
                        if (!TryParseFloat(token, out config.DefaultUpperArmLength))
                            return false;
                    }
                    else if (field == 2)
                    {
                        if (!TryParseFloat(token, out config.DefaultForearmLength))
                            return false;
                    }
                    else if (field == 3)
                    {
                        if (!TryParseFloat(token, out config.MinElbowRadians))
                            return false;
                    }
                    else if (field == 4)
                    {
                        if (!TryParseFloat(token, out config.MaxElbowRadians))
                            return false;
                    }

                    field++;
                    start = i + 1;
                }

                if (field < 5 || config.DefaultUpperArmLength <= 0.0f || config.DefaultForearmLength <= 0.0f)
                    return false;

                config.BlendOutSeconds = 0.25f;
                config.BlendOutSpeed = 4.0f;
                config.MaxFabrikIterations = 8.0f;
                config.GlobalQualityWeightOverride = -1.0f;
                config.ShoulderLateralMeters = 0.18f;
                config.ShoulderHeightMeters = 1.38f;
                config.ShoulderForwardMeters = 0.08f;
                return true;
            }

            private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> token)
            {
                int start = 0;
                int end = token.Length;
                while (start < end && token[start] <= (byte)' ')
                    start++;
                while (end > start && token[end - 1] <= (byte)' ')
                    end--;
                return token.Slice(start, end - start);
            }

            private static uint HashToken(ReadOnlySpan<byte> token)
            {
                uint hash = 2166136261u;
                for (int i = 0; i < token.Length; i++)
                    hash = (hash ^ token[i]) * 16777619u;
                return hash == 0u ? 1u : hash;
            }

            private static bool TryParseFloat(ReadOnlySpan<byte> token, out float value)
            {
                value = 0.0f;
                if (token.Length == 0)
                    return false;

                int index = 0;
                float sign = 1.0f;
                if (token[index] == (byte)'-')
                {
                    sign = -1.0f;
                    index++;
                }
                else if (token[index] == (byte)'+')
                {
                    index++;
                }

                float integer = 0.0f;
                bool hasDigit = false;
                while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                {
                    integer = integer * 10.0f + (token[index] - (byte)'0');
                    index++;
                    hasDigit = true;
                }

                float fractional = 0.0f;
                float scale = 1.0f;
                if (index < token.Length && token[index] == (byte)'.')
                {
                    index++;
                    while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                    {
                        fractional = fractional * 10.0f + (token[index] - (byte)'0');
                        scale *= 10.0f;
                        index++;
                        hasDigit = true;
                    }
                }

                if (!hasDigit || index != token.Length)
                    return false;

                value = sign * (integer + fractional / scale);
                return math.isfinite(value);
            }
        }
        #endif

        private static class HandIkGraphicsBufferUpload
        {
            public static GraphicsBuffer CreateStructuredLockBuffer<T>(int count) where T : unmanaged
            {
                return new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    count,
                    UnsafeUtility.SizeOf<T>());
            }

            public static void UploadNativeArray<T>(GraphicsBuffer destination, NativeArray<T> source, int count) where T : unmanaged
            {
                int safeCount = ResolveSafeWriteCount<T>(destination, source.IsCreated ? source.Length : 0, count);
                if (safeCount <= 0)
                    return;

                NativeArray<T> mapped = destination.LockBufferForWrite<T>(0, safeCount);
                try
                {
                    unsafe
                    {
                        void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
                        void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                        UnsafeUtility.MemCpy(destinationPtr, sourcePtr, (long)UnsafeUtility.SizeOf<T>() * safeCount);
                    }
                }
                finally
                {
                    destination.UnlockBufferAfterWrite<T>(safeCount);
                }
            }

            public static bool HasValidGraphicsBuffer<T>(GraphicsBuffer buffer, int requiredCount) where T : unmanaged
            {
                return buffer != null &&
                       buffer.IsValid() &&
                       buffer.count >= requiredCount &&
                       buffer.stride == UnsafeUtility.SizeOf<T>();
            }

            private static int ResolveSafeWriteCount<T>(GraphicsBuffer destination, int sourceLength, int requestedCount) where T : unmanaged
            {
                if (destination == null || requestedCount <= 0 || sourceLength <= 0 || destination.count <= 0)
                    return 0;

                int stride = UnsafeUtility.SizeOf<T>();
                if (destination.stride != stride)
                    return 0;

                return math.min(math.min(requestedCount, sourceLength), destination.count);
            }
        }

        private const uint ReleaseSecondsMask = 0x0FFF0000u;
        private const int ReleaseSecondsShift = 16;
        private const uint IterationLimitMask = 0xF0000000u;
        private const int IterationLimitShift = 28;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SolveFabrikTwoBone(
            ref float3 shoulder,
            ref float3 elbow,
            ref float3 wrist,
            float upperLength,
            float forearmLength,
            float3 target,
            float3 pole,
            int iterations,
            out float maxError,
            out float poleError)
        {
            shoulder = SanitizeFloat3(shoulder, float3.zero);
            elbow = SanitizeFloat3(elbow, shoulder + new float3(0.0f, -upperLength, 0.0f));
            wrist = SanitizeFloat3(wrist, elbow + new float3(0.0f, -forearmLength, 0.0f));
            target = SanitizeFloat3(target, wrist);
            pole = SanitizeFloat3(pole, shoulder + new float3(0.0f, -0.35f, 0.16f));
            upperLength = math.max(0.0001f, SanitizeNonNegative(upperLength));
            forearmLength = math.max(0.0001f, SanitizeNonNegative(forearmLength));
            iterations = math.clamp(iterations, 1, 8);

            float3 root = shoulder;
            float totalLength = upperLength + forearmLength;
            float3 rootToTarget = target - root;
            float distanceSq = math.lengthsq(rootToTarget);
            if (!math.isfinite(distanceSq) || distanceSq <= 0.000001f)
            {
                maxError = 0.0f;
                poleError = 0.0f;
                return;
            }

            if (distanceSq >= totalLength * totalLength)
            {
                float3 direction = SafeNormalize(rootToTarget, new float3(0.0f, 0.0f, 1.0f));
                elbow = root + direction * upperLength;
                wrist = elbow + direction * forearmLength;
            }
            else
            {
                for (int i = 0; i < iterations; i++)
                {
                    wrist = target;
                    float3 lowerDirection = SafeNormalize(elbow - wrist, new float3(0.0f, 0.0f, -1.0f));
                    elbow = wrist + lowerDirection * forearmLength;
                    float3 upperDirection = SafeNormalize(shoulder - elbow, new float3(0.0f, 0.0f, -1.0f));
                    shoulder = elbow + upperDirection * upperLength;
                    shoulder = root;
                    upperDirection = SafeNormalize(elbow - shoulder, new float3(0.0f, -1.0f, 0.0f));
                    elbow = shoulder + upperDirection * upperLength;
                    lowerDirection = SafeNormalize(wrist - elbow, new float3(0.0f, -1.0f, 0.0f));
                    wrist = elbow + lowerDirection * forearmLength;
                }
            }

            ApplyPoleConstraint(root, ref elbow, ref wrist, upperLength, forearmLength, pole, out poleError);
            maxError = FastLength(wrist - target);
            shoulder = root;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyPoleConstraint(
            float3 shoulder,
            ref float3 elbow,
            ref float3 wrist,
            float upperLength,
            float forearmLength,
            float3 pole,
            out float poleError)
        {
            float3 axis = SafeNormalize(wrist - shoulder, new float3(0.0f, 0.0f, 1.0f));
            float3 poleVector = pole - shoulder;
            float3 projectedPole = poleVector - axis * math.dot(poleVector, axis);
            float3 poleDirection = SafeNormalize(projectedPole, new float3(0.0f, -1.0f, 0.0f));
            float3 shoulderToElbow = elbow - shoulder;
            float alongAxis = math.dot(shoulderToElbow, axis);
            float3 radial = shoulderToElbow - axis * alongAxis;
            float radialLength = FastLength(radial);
            float3 constrainedElbow = shoulder + axis * alongAxis + poleDirection * radialLength;
            poleError = FastLength(constrainedElbow - elbow);
            elbow = constrainedElbow;
            float3 upperDir = SafeNormalize(elbow - shoulder, axis);
            elbow = shoulder + upperDir * upperLength;
            float3 lowerDir = SafeNormalize(wrist - elbow, axis);
            wrist = elbow + lowerDir * forearmLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveFreeElbow(float3 shoulder, float3 rawWrist, float3 pole, float upperLength, float forearmLength)
        {
            float3 wristDirection = SafeNormalize(rawWrist - shoulder, new float3(0.0f, 0.0f, 1.0f));
            float reach = math.min(FastLength(rawWrist - shoulder), math.max(0.0001f, upperLength + forearmLength - 0.0001f));
            float along = math.clamp(
                ((upperLength * upperLength) + (reach * reach) - (forearmLength * forearmLength)) * math.rcp(math.max(0.0001f, 2.0f * reach)),
                0.0f,
                upperLength);
            float radialSq = math.max(0.0f, upperLength * upperLength - along * along);
            float radial = radialSq * math.rsqrt(math.max(radialSq, 0.000001f));
            float3 poleDirection = pole - shoulder;
            poleDirection -= wristDirection * math.dot(poleDirection, wristDirection);
            poleDirection = SafeNormalize(poleDirection, new float3(0.0f, -1.0f, 0.0f));
            return shoulder + wristDirection * along + poleDirection * radial;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ToLocalFloat3(double3 absoluteAup, double3 rootAup, float3 fallback)
        {
            if (!IsFinite(absoluteAup) || !IsFinite(rootAup))
                return fallback;

            double3 localDouble = absoluteAup - rootAup;
            float3 local = new float3((float)localDouble.x, (float)localDouble.y, (float)localDouble.z);
            return SanitizeFloat3(local, fallback);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint EncodeReleaseSeconds(float seconds, float window)
        {
            float normalized = math.saturate(seconds * math.rcp(math.max(0.0001f, window)));
            uint encoded = (uint)math.clamp((int)math.round(normalized * 4095.0f), 0, 4095);
            return encoded << ReleaseSecondsShift;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float DecodeReleaseSeconds(uint flags, float window)
        {
            uint encoded = (flags & ReleaseSecondsMask) >> ReleaseSecondsShift;
            return (encoded * math.rcp(4095.0f)) * math.max(0.0001f, window);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint EncodeIterationLimit(float maxIterations)
        {
            uint encoded = (uint)math.clamp((int)math.round(math.max(1.0f, maxIterations)), 1, 8);
            return encoded << IterationLimitShift;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int DecodeIterationLimit(uint flags)
        {
            uint encoded = (flags & IterationLimitMask) >> IterationLimitShift;
            return math.clamp(encoded == 0u ? 8 : (int)encoded, 1, 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static quaternion SafeLookRotation(float3 forward, float3 up)
        {
            forward = SafeNormalize(forward, new float3(0.0f, 0.0f, 1.0f));
            up = SafeNormalize(up, new float3(0.0f, 1.0f, 0.0f));
            return quaternion.LookRotationSafe(forward, up);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveUp(float3 shoulder, float3 elbow, float3 wrist)
        {
            float3 upper = elbow - shoulder;
            float3 lower = wrist - elbow;
            float3 normal = math.cross(upper, lower);
            return SafeNormalize(normal, new float3(0.0f, 1.0f, 0.0f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastLength(float3 value)
        {
            float lengthSq = math.lengthsq(value);
            return math.isfinite(lengthSq) && lengthSq > 0.000001f
                ? lengthSq * math.rsqrt(math.max(lengthSq, 0.000001f))
                : 0.0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(double3 value)
        {
            return math.all(math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashHandTarget(double3 targetAup, double3 rawAup, uint handIndex, uint frame)
        {
            uint hash = 2166136261u;
            MixQuantizedAup(ref hash, targetAup);
            MixQuantizedAup(ref hash, rawAup);
            hash = Mix(hash, handIndex);
            hash = Mix(hash, frame);
            return hash == 0u ? 1u : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MixQuantizedAup(ref uint hash, double3 value)
        {
            hash = Mix(hash, QuantizeMillimeters(value.x));
            hash = Mix(hash, QuantizeMillimeters(value.y));
            hash = Mix(hash, QuantizeMillimeters(value.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint QuantizeMillimeters(double value)
        {
            if (!math.isfinite(value))
                return 0u;

            double scaled = math.clamp(value * 1000d, -2147483648d, 2147483647d);
            return (uint)(int)math.round(scaled);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MixHash(uint a, uint b, uint c, uint d)
        {
            return Mix(Mix(Mix(a, b), c), d);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }
    }
}
