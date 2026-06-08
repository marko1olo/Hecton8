using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Tools;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Hecton8.Gameplay
{
    /// <summary>VR horizon presentation state consumed by the late-latch visual path. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VRSomaticComfortDTO
    {
        [FieldOffset(0)] public quaternion StabilizedRotation;
        [FieldOffset(16)] public float FovTunnelScalar;
        [FieldOffset(20)] public float PitchDampening;
        [FieldOffset(24)] public uint ComfortFlags;
        [FieldOffset(28)] private byte _pad0;
        [FieldOffset(29)] private byte _pad1;
        [FieldOffset(30)] private byte _pad2;
        [FieldOffset(31)] private byte _pad3;
    }

    /// <summary>Fixed 300-frame horizon-lock blackbox row. Size: 96 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct SomaticTelemetryEntry
    {
        [FieldOffset(0)] public quaternion StabilizedRotation;
        [FieldOffset(16)] public float4 QuaternionDelta;
        [FieldOffset(32)] public float3 RawAngularVelocity;
        [FieldOffset(44)] public float FovTunnelScalar;
        [FieldOffset(48)] public float PitchDampening;
        [FieldOffset(52)] public float BurstExecutionMicroseconds;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public uint Flags;
        [FieldOffset(64)] public uint StateHash;
        [FieldOffset(68)] public uint AupHash;
        [FieldOffset(72)] private byte _pad0;
        [FieldOffset(73)] private byte _pad1;
        [FieldOffset(74)] private byte _pad2;
        [FieldOffset(75)] private byte _pad3;
        [FieldOffset(76)] private byte _pad4;
        [FieldOffset(77)] private byte _pad5;
        [FieldOffset(78)] private byte _pad6;
        [FieldOffset(79)] private byte _pad7;
        [FieldOffset(80)] private byte _pad8;
        [FieldOffset(81)] private byte _pad9;
        [FieldOffset(82)] private byte _pad10;
        [FieldOffset(83)] private byte _pad11;
        [FieldOffset(84)] private byte _pad12;
        [FieldOffset(85)] private byte _pad13;
        [FieldOffset(86)] private byte _pad14;
        [FieldOffset(87)] private byte _pad15;
        [FieldOffset(88)] private byte _pad16;
        [FieldOffset(89)] private byte _pad17;
        [FieldOffset(90)] private byte _pad18;
        [FieldOffset(91)] private byte _pad19;
        [FieldOffset(92)] private byte _pad20;
        [FieldOffset(93)] private byte _pad21;
        [FieldOffset(94)] private byte _pad22;
        [FieldOffset(95)] private byte _pad23;
    }

    /// <summary>Gameplay-owned presentation mirror of the physics KCC state. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VRSomaticKinematicStateMirrorDTO
    {
        [FieldOffset(0)] public double3 AUP_Position;
        [FieldOffset(24)] public float3 Velocity;
        [FieldOffset(36)] public float3 AngularVelocity;
        [FieldOffset(48)] public float Mass;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public float DragCoefficient;
        [FieldOffset(60)] public byte RestingFrameCount;
        [FieldOffset(61)] public byte DeepSleepTickCount;
        [FieldOffset(62)] public byte SleepMaterialIndex;
        [FieldOffset(63)] public byte _pad0;
    }

    /// <summary>
    /// VR-only somatic suit provider. PC/console code reads <see cref="IVRSomaticProvider"/> through GlobalRegistry.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/VR Somatic Provider")]
    public sealed partial class VRSomaticProvider : MonoBehaviour, IVRSomaticProvider, IUpdatable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private const int HeadCollisionCommandCount = 6;
        private const int HandCount = 2;
        private const float Pi = 3.14159265359f;
        private const float TwoPi = 6.28318530718f;
        private const float HalfPi = 1.57079632679f;
        private const float DegreesToRadians = 0.01745329252f;
        private const float HorizonLockStartSinSq = 0.0669873f;
        private const float HorizonLockMaxCorrectionRadians = 0.2617994f;
        private const float MinimumDeltaTime = 0.0001f;
        private const float ShaderPublishEpsilon = 0.0001f;
        private const float AudioPublishEpsilon = 0.001f;
        private const float LowPassPublishEpsilonHz = 1f;
        private const float PlayerSignalSampleIntervalSeconds = 0.05f;
        private const float QuaternionLengthSqMinimum = 0.25f;
        private const float QuaternionLengthSqMaximum = 2.25f;
        private const float QuaternionUnitLengthSqEpsilon = 0.015625f;
        private const float HitNormalLengthSqMinimum = 0.25f;
        private const float HitNormalLengthSqMaximum = 2.25f;
        private const float HitNormalUnitLengthSqEpsilon = 0.015625f;
        private const float MinimumNearFieldDistanceMeters = 0.01f;
        private const float MinimumHeadCapsuleRadiusMeters = 0.01f;
        private const float MinimumHeadCapsuleHalfHeightMeters = 0.01f;
        private const float MinimumImpactDebounceSeconds = 0.02f;
        private const float HeadTrackingJumpDistanceMetersSq = 1.44f;
        private const float HeadTrackingJumpAngularRadians = 1.35f;
        private const float MaxSomaticHeadLinearSpeedMetersPerSecond = 12f;
        private const float MaxSomaticHeadAngularSpeedRadiansPerSecond = 18f;
        private const float MaxSomaticHeadAngularJerkRadiansPerSecondCubed = 1440f;
        private const byte HapticPriorityComfort = 2;
        private const byte LeftMotorMask = 0b0001;
        private const byte RightMotorMask = 0b0010;
        private const byte BothMotorMask = LeftMotorMask | RightMotorMask;
        private const byte HapticPriorityCritical = ToolHapticsRuntime.PriorityCritical;
        private const byte HapticBlendAdditive = ToolHapticsRuntime.BlendModeAdditive;
        private const float AupCellSizeMeters = HectonPhysicsContract.AupSectorSizeMetersFloat;
        private const float HapticSideThreshold = 0.2f;
        private const float JerkEventDebounceSeconds = 0.2f;
        private const float VrComfortTelemetryStep01 = 0.05f;
        private const float PressureFallbackComfortVignetteMaximum = 0.52f;
        private const float PressureFallbackAccelerationSoftTunnelStartRadS2 = 42f;
        private const float PressureFallbackAccelerationEmergencyClampRadS2 = 150f;
        private const float PressureFallbackAccelerationReleaseBelowRadS2 = 24f;
        private const float PressureFallbackAccelerationReleaseHysteresisSeconds = 0.25f;
        private const float PressureFallbackVignetteAttackSlewPerFrame = 0.055f;
        private const float PressureFallbackVignetteReleaseSlewPerFrame = 0.025f;
        private const float PressureFallbackFrameSafetyDeltaSeconds = 0.01667f;
        private const float PressureFallbackFrameSafetyMinOpacity = 0.12f;
        private const int PressureFallbackFrameSafetyConsecutiveFrames = 2;
        private const int PressureFallbackFrameSafetyReleaseStableFrames = 12;
        private const float NominalFrameSafetyDeltaSeconds = 0.01389f;
        private const float NominalFrameSafetyMinOpacity = 0.10f;
        private const int NominalFrameSafetyConsecutiveFrames = 2;
        private const int NominalFrameSafetyReleaseStableFrames = 12;
        private const float KccAngularVelocityMaxRadiansPerSecond = 16f;
        private const float KccAngularAccelerationMaxRadiansPerSecondSq = 240f;
        private const int KccVelocitySignalStaleFrameLimit = 12;
        private const uint VrComfortTelemetryContextHash = 0x56524346u; // VRCF
        private const uint VrComfortJerkEventHash = 0x4A524B31u; // JRK1
        private const uint VrComfortMaxVignetteHash = 0x4D565231u; // MVR1
        private const uint BlackBoxMagic = 0x5652534Du; // VRSM
        private const uint BlackBoxDumpFaultHash = 0x56524446u; // VRDF
        private const uint ComfortDumpFaultHash = 0x56434446u; // VCDF
        private const uint BlackBoxVersion = 3u;
        private const int BlackBoxFrameCapacity = 300;
        private const ushort BlackBoxFlagActive = 1 << 0;
        private const ushort BlackBoxFlagNonFinite = 1 << 1;
        private const ushort BlackBoxFlagLeftGhost = 1 << 2;
        private const ushort BlackBoxFlagRightGhost = 1 << 3;
        private const ushort BlackBoxFlagNearCollision = 1 << 6;
        private const ushort BlackBoxFlagAupShiftSeen = 1 << 7;
        private const ushort BlackBoxFlagFramePressure = 1 << 9;
        private const ushort BlackBoxFlagProtectiveFallback = 1 << 10;
        private const ushort BlackBoxFlagAccelerationTunnel = 1 << 11;
        private const ushort BlackBoxFlagKccSignal = 1 << 12;
        private const ushort BlackBoxFlagKccAccelerationTunnel = 1 << 13;
        private const ushort BlackBoxFlagDynamicHorizonLock = 1 << 14;
        private const int BlackBoxDumpHeaderSizeBytes = 16;
        private const int BlackBoxDumpEntrySizeBytes = 128;
        private const string BlackBoxDumpFileName = "Dump_1335_SomaticComfort.bin";
        private const uint StateHeadCollisionReady = 1u << 0;
        private const uint StateRegisteredService = 1u << 1;
        private const uint StateRegisteredUpdate = 1u << 2;
        private const uint StateRegisteredLateFrame = 1u << 3;
        private const uint StateHasPreviousHeadPose = 1u << 4;
        private const uint StateSubscribedXRRuntime = 1u << 5;
        private const uint StateBreathingAudioStaticApplied = 1u << 6;
        private const uint StateBreathingLowPassStaticApplied = 1u << 7;
        private const uint StateBreathingSourcePlaying = 1u << 8;
        private const uint StateHandsInitialized = 1u << 11;
        private const uint StateRootInitialized = 1u << 12;
        private const uint StateHasPreviousKccPlanarDirection = 1u << 13;
        private const uint StateRegisteredHotSwap = 1u << 14;
        private const uint StateRootPoseDirty = 1u << 15;
        private const uint StateChestSocketPoseDirty = 1u << 16;
        private const uint StateVisorHudPoseDirty = 1u << 17;
        private const uint StateQueuedPresentationPoseMask =
            StateRootPoseDirty |
            StateChestSocketPoseDirty |
            StateVisorHudPoseDirty;
        private const SystemID VaultOwnerSystem = SystemID.GameplayPlayer;

        private static readonly int NearCollisionIntensityId = Shader.PropertyToID("_HectonVRNearCollisionIntensity");
        private static readonly int SomaticCondensationId = Shader.PropertyToID("_HectonVRSomaticCondensation");
        private static readonly int SomaticStateId = Shader.PropertyToID("_HectonVRSomaticState");
        private static readonly int VrComfortJerkStateId = Shader.PropertyToID("_HectonVRComfortJerkState");
        private static readonly int VrComfortKccStateId = Shader.PropertyToID("_HectonVRComfortKccState");
        private static readonly int VrSomaticComfortStateId = Shader.PropertyToID("_HectonVRSomaticComfortState");
        private static readonly int VrComfortVignetteId = Shader.PropertyToID("_VRComfortVignette");
        private static readonly WaitCallback BlackBoxDumpWorker = WriteBlackBoxDumpWorker;

        private struct VaultBufferView<T> where T : struct
        {
            private IDataVault _vault;
            private IDataVault _writeLockVault;
            private VaultGenerationHandle<T> _handle;
            private BufferID _bufferId;
            private int _requiredLength;

            public static VaultBufferView<T> Create(
                IDataVault vault,
                BufferID bufferId,
                int requiredLength,
                NativeArrayOptions options)
            {
                if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                    return default;

                VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                    bufferId,
                    requiredLength,
                    VaultOwnerSystem,
                    options);

                return new VaultBufferView<T>
                {
                    _vault = vault,
                    _writeLockVault = null,
                    _handle = handle,
                    _bufferId = bufferId,
                    _requiredLength = requiredLength
                };
            }

            public bool IsCreated => TryReadOnlyNativeArray(out _);

            public int Length
            {
                get
                {
                    return TryReadOnlyNativeArray(out NativeArray<T>.ReadOnly array) ? array.Length : 0;
                }
            }

            public bool TryReadOnlyNativeArray(out NativeArray<T>.ReadOnly array)
            {
                array = default;
                return IsHandleValid() &&
                       _vault != null &&
                       !_vault.IsCompactionFenceActive &&
                       _vault.TryReadOnlyHandle(in _handle, out array) &&
                       !_vault.IsCompactionFenceActive &&
                       array.IsCreated &&
                       array.Length >= _requiredLength;
            }

            public bool TryAcquireWriteNativeArray(out NativeArray<T> array)
            {
                array = default;
                IDataVault vault = _vault;
                if (!IsHandleValid() ||
                    vault == null ||
                    _writeLockVault != null ||
                    vault.IsCompactionFenceActive ||
                    !vault.TryAcquireWriteLock(in _handle, VaultOwnerSystem, out array))
                {
                    return false;
                }

                bool keepLock = false;
                try
                {
                    if (vault.IsCompactionFenceActive ||
                        !array.IsCreated ||
                        array.Length < _requiredLength)
                    {
                        return false;
                    }

                    _writeLockVault = vault;
                    keepLock = true;
                    return true;
                }
                finally
                {
                    if (!keepLock)
                    {
                        vault.ReleaseWriteLock(in _handle, VaultOwnerSystem);
                        array = default;
                    }
                }
            }

            public void ReleaseWriteNativeArray()
            {
                IDataVault vault = _writeLockVault;
                if (vault == null)
                    return;

                _writeLockVault = null;
                if (IsHandleValid())
                    vault.ReleaseWriteLock(in _handle, VaultOwnerSystem);
            }

            public void Release()
            {
                IDataVault writeLockVault = _writeLockVault;
                if (writeLockVault != null && IsHandleValid())
                    writeLockVault.ReleaseWriteLock(in _handle, VaultOwnerSystem);

                _writeLockVault = null;
                if (IsHandleValid() && _vault != null)
                    _vault.ReleaseBuffer(in _handle);

                _vault = null;
                _handle = default;
                _bufferId = default;
                _requiredLength = 0;
            }

            private bool IsHandleValid()
            {
                return _requiredLength > 0 &&
                       _handle.BufferID == unchecked((uint)(int)_bufferId) &&
                       _handle.SystemID == (uint)VaultOwnerSystem &&
                       _handle.Generation != 0u;
            }
        }

        [Header("Rig")]
        [SerializeField] private Transform hmdTransform;
        [SerializeField] private Transform visorHudRoot;
        [SerializeField] private Transform pdaChestSocket;
        [SerializeField] private Transform flareToolChestSocket;

        [Header("Collision")]
        [SerializeField] private LayerMask nearFieldCollisionMask =
            HectonLayerMasks.BaseModuleLayerMask |
            HectonLayerMasks.VoxelCaveLayerMask |
            HectonLayerMasks.TerrainLayerMask;
        [SerializeField, Range(0.05f, 0.25f)] private float nearFieldDistanceMeters = 0.15f;
#pragma warning disable CS0414
        [SerializeField, Range(0.02f, 0.12f)] private float headCapsuleRadiusMeters = 0.055f;
        [SerializeField, Range(0.01f, 0.12f)] private float headCapsuleHalfHeightMeters = 0.045f;
#pragma warning restore CS0414
        [SerializeField, Range(1f, 60f)] private float nearFieldFadeSharpness = 22f;

        [Header("Haptics")]
        [SerializeField, Range(0f, 8f)] private float impactSpeedThresholdMetersPerSecond = 2f;
        [SerializeField, Range(0.02f, 0.35f)] private float impactHapticDurationSeconds = 0.14f;
        [SerializeField, Range(0.5f, 10f)] private float impactHapticDecayRate = 4.4f;
        [SerializeField, Range(0.02f, 0.25f)] private float impactHapticDebounceSeconds = 0.08f;
        [SerializeField, Range(0f, 1f)] private float maxLowFrequencyImpact = 0.55f;
        [SerializeField, Range(0f, 1f)] private float maxHighFrequencyImpact = 0.88f;

        [Header("Helmet")]
        [SerializeField] private bool applyVisorHudHeadLag = true;
        [SerializeField, Range(0f, 1f)] private float visorLagMaximumBlend = 0.62f;
        [SerializeField, Range(0.25f, 12f)] private float visorLagAngularSpeedForFull = 5.25f;
        [SerializeField, Range(30f, 960f), Tooltip("Angular jerk where VR comfort clamps visor HUD response.")]
        private float rotationJerkLimitRadiansPerSecondCubed = 320f;
        [SerializeField, Range(0f, 1f), Tooltip("Maximum extra visor HUD blend used to cull rotation jerk.")]
        private float rotationJerkCullMaximumBlend = 0.42f;
        [SerializeField, Range(1f, 40f), Tooltip("Smoothing sharpness for rotation jerk comfort culling.")]
        private float rotationJerkCullSharpness = 18f;
        [SerializeField, Range(0f, 1f), Tooltip("Extra vignette contributed by severe rotation jerk.")]
        private float rotationJerkVignetteContribution = 0.28f;
        [SerializeField, Range(2f, 40f), Tooltip("Smoothing sharpness for the decoupled somatic root.")]
        private float rootRotationSmoothingSharpness = 14f;
        [SerializeField, Range(0.25f, 12f), Tooltip("Head angular speed where the comfort vignette begins.")]
        private float comfortVignetteAngularSpeedStart = 2.6f;
        [SerializeField, Range(1f, 18f), Tooltip("Head angular speed where the comfort vignette reaches full value.")]
        private float comfortVignetteAngularSpeedFull = 8f;
        [SerializeField, Range(0f, 1f), Tooltip("Maximum scalar written to the VR comfort vignette globals.")]
        private float comfortVignetteMaximum = 0.46f;
        [SerializeField, Range(10f, 180f), Tooltip("Angular acceleration where the somatic tunnel starts.")]
        private float comfortAccelerationSoftTunnelStartRadS2 = 50f;
        [SerializeField, Range(20f, 240f), Tooltip("Angular acceleration where the somatic tunnel reaches maximum opacity.")]
        private float comfortAccelerationEmergencyClampRadS2 = 180f;
        [SerializeField, Range(0f, 120f), Tooltip("Angular acceleration below which the acceleration tunnel can release after hysteresis.")]
        private float comfortAccelerationReleaseBelowRadS2 = 30f;
        [SerializeField, Range(0f, 1f), Tooltip("Seconds acceleration must stay below release threshold before tunnel release.")]
        private float comfortAccelerationReleaseHysteresisSeconds = 0.22f;
        [SerializeField, Range(0.001f, 0.1f), Tooltip("Maximum acceleration tunnel opacity increase per VR frame.")]
        private float comfortVignetteAttackSlewPerFrame = 0.05f;
        [SerializeField, Range(0.001f, 0.1f), Tooltip("Maximum acceleration tunnel opacity decrease per VR frame.")]
        private float comfortVignetteReleaseSlewPerFrame = 0.022f;

        [Header("KCC Comfort")]
        [SerializeField, Range(0.01f, 1.5f), Tooltip("Minimum KCC planar speed required before body-turn angular acceleration affects VR comfort.")]
        private float kccMinimumPlanarSpeedMetersPerSecond = 0.18f;
        [SerializeField, Range(5f, 180f), Tooltip("KCC angular acceleration where camera-independent FOV narrowing begins.")]
        private float kccAngularAccelerationSoftTunnelStartRadS2 = 34f;
        [SerializeField, Range(20f, 280f), Tooltip("KCC angular acceleration where camera-independent FOV narrowing reaches its maximum contribution.")]
        private float kccAngularAccelerationEmergencyClampRadS2 = 140f;
        [SerializeField, Range(0f, 120f), Tooltip("KCC angular acceleration below which the KCC tunnel can release after hysteresis.")]
        private float kccAngularAccelerationReleaseBelowRadS2 = 18f;
        [SerializeField, Range(0f, 1f), Tooltip("Seconds KCC acceleration must stay below release threshold before KCC tunnel release.")]
        private float kccAccelerationReleaseHysteresisSeconds = 0.18f;
        [SerializeField, Range(0f, 1f), Tooltip("Maximum additional FOV narrowing contributed by KCC body-turn acceleration.")]
        private float kccAngularAccelerationVignetteContribution = 0.34f;
        [SerializeField, Range(10f, 240f), Tooltip("KCC angular acceleration that reaches full dynamic horizon lock assistance.")]
        private float kccHorizonLockFullAccelerationRadS2 = 95f;
        [SerializeField, Range(0f, 1f), Tooltip("Maximum horizon lock assistance contributed by KCC body-turn acceleration.")]
        private float kccHorizonLockMaximum01 = 0.72f;

        [Header("Chest Sockets")]
        [SerializeField] private Vector3 pdaChestOffset = new Vector3(-0.18f, -0.34f, 0.22f);
        [SerializeField] private Vector3 pdaChestRotationEuler = new Vector3(8f, -12f, -6f);
        [SerializeField] private Vector3 flareToolChestOffset = new Vector3(0.18f, -0.36f, 0.19f);
        [SerializeField] private Vector3 flareToolChestRotationEuler = new Vector3(10f, 14f, 8f);
        [SerializeField, Range(3f, 12f), Tooltip("Head speed where a short somatic haptic anchor begins.")]
        private float velocityHapticThresholdMetersPerSecond = 5f;
        [SerializeField, Range(0.03f, 0.25f), Tooltip("Minimum seconds between velocity haptic anchors.")]
        private float velocityHapticIntervalSeconds = 0.12f;
        [SerializeField, Range(0.01f, 0.2f), Tooltip("Duration of each velocity haptic anchor pulse.")]
        private float velocityHapticDurationSeconds = 0.075f;

        [Header("Physical Hands")]
        [SerializeField, Range(2f, 80f)] private float handSpringForce = 24f;
        [SerializeField, Range(0.08f, 0.5f)] private float ghostHandDistanceMeters = 0.2f;
        [SerializeField, FormerlySerializedAs("reduceGhostHandsAtLowQuality"), FormerlySerializedAs("disableGhostHandsOnLowTier")] private bool scaleGhostHandToleranceByQuality = true;

        [Header("Breathing Audio")]
        [SerializeField] private AudioSource breathingSource;
        [SerializeField] private AudioLowPassFilter breathingLowPassFilter;
        [SerializeField, Range(0f, 1f)] private float breathingBaseVolume = 0.12f;
        [SerializeField, Range(0f, 1f)] private float breathingStressVolume = 0.46f;
        [SerializeField, Range(0.5f, 2f)] private float breathingMinimumPitch = 0.92f;
        [SerializeField, Range(0.5f, 2f)] private float breathingMaximumPitch = 1.22f;
        [SerializeField, Range(200f, 22000f)] private float breathingLowPassOpenHz = 18000f;
        [SerializeField, Range(200f, 22000f)] private float breathingLowPassClosedHz = 680f;

        private VaultBufferView<HeadCastSample> _headCollisionSamples;
        private VaultBufferView<VRSomaticRootSyncInput> _rootSyncInput;
        private VaultBufferView<VRSomaticRootSyncOutput> _rootSyncOutput;
        private VaultBufferView<float3> HandTargets;
        private VaultBufferView<float3> HandPhysicalPositions;
        private VaultBufferView<VRSomaticBlackBoxEntry> _blackBox;
        private IDataVault _dataVault;
        private Hecton8.Core.Contracts.IVoxelSonarSdfReadModel _voxelSdfReadModel;
        private JobHandle _headCollisionDisposeHandle;
        private uint _stateFlags;
        private Vector3 _previousHeadPosition;
        private Quaternion _previousHeadRotation = Quaternion.identity;
        private Quaternion _headRotationFrame1 = Quaternion.identity;
        private Quaternion _headRotationFrame2 = Quaternion.identity;
        private Quaternion _headRotationFrame3 = Quaternion.identity;
        private Quaternion _torsoRotation = Quaternion.identity;
        private Quaternion _pdaSocketLocalRotation = Quaternion.identity;
        private Quaternion _flareSocketLocalRotation = Quaternion.identity;
        private quaternion _lastRootRotation = quaternion.identity;
        private Transform _fallbackHmdTransform;
        private Transform _decoupledRootTransform;
        private Camera _cachedPlayerCamera;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private float _headLinearSpeedMetersPerSecond;
        private float _headAngularSpeedRadiansPerSecond;
        private float _headAngularAccelerationRadiansPerSecondSq;
        private float3 _previousHeadAngularVelocityRadiansPerSecond;
        private float3 _previousHeadAngularAccelerationRadiansPerSecondSq;
        private float _headAngularJerkRadiansPerSecondCubed;
        private float _headAngularJerk01;
        private float _accelerationComfortVignette01;
        private float _accelerationReleaseBelowTimer;
        private float2 _previousKccPlanarDirection;
        private float _previousKccAngularVelocityRadiansPerSecond;
        private float _kccAngularVelocityRadiansPerSecond;
        private float _kccAngularAccelerationRadiansPerSecondSq;
        private float _kccAngularComfortVignette01;
        private float _kccHorizonLock01;
        private float _kccAccelerationReleaseBelowTimer;
        private float _globalQualityWeight01 = 1f;
        private uint _lastConsumedKccVelocitySequence;
        private uint _lastConsumedKccVelocityFrame;
        private uint _lastConsumedKccVelocitySourceId;
        private int _comfortFramePressureConsecutiveFrames;
        private int _comfortFramePressureStableFrames;
        private float _jerkCullBlend01;
        private uint _jerkEventCount;
        private uint _lastTelemetryJerkEventCount;
        private float _jerkEventCooldownRemaining;
        private float _maxSomaticComfortVignetteTelemetry01;
        private float _lastSomaticComfortVignetteTelemetry01;
        private float _velocityHapticCooldownRemaining;
        private float _lastTickDeltaTime;
        private float _impactHapticCooldownRemaining;
        private float _nearFieldCollision01;
        private float _playerStress01;
        private float _oxygen01 = 1f;
        private float _depthMeters;
        private float _condensation01;
        private float _lastPublishedNearCollision01 = float.PositiveInfinity;
        private float _lastPublishedCondensation01 = float.PositiveInfinity;
        private float _lastPublishedBreathingVolume = float.PositiveInfinity;
        private float _lastPublishedBreathingPitch = float.PositiveInfinity;
        private float _lastPublishedBreathingLowPassHz = float.PositiveInfinity;
        private float _lastPublishedBreathingLowPassQ = float.PositiveInfinity;
        private float _lastPublishedComfortVignette01 = float.PositiveInfinity;
        private float _playerSignalSampleRemaining;
        private Vector4 _lastPublishedSomaticState = Vector4.positiveInfinity;
        private Vector4 _lastPublishedJerkState = Vector4.positiveInfinity;
        private Vector4 _lastPublishedKccComfortState = Vector4.positiveInfinity;
        private Vector3 _pendingRootSyncPosition;
        private Quaternion _pendingRootSyncRotation = Quaternion.identity;
        private Vector3 _pendingVisorHudPosition;
        private Quaternion _pendingVisorHudRotation = Quaternion.identity;
        private bool _somaticShaderStateDirty;
        private bool _comfortVignetteShaderDirty;
        private bool _breathingAudioDirty;
        private bool _pendingBreathingStaticApply;
        private bool _pendingBreathingLowPassStaticApply;
        private bool _pendingBreathingLowPassDisable;
        private bool _pendingBreathingPlay;
        private bool _pendingBreathingStop;
        private bool _pendingBreathingVolumeDirty;
        private bool _pendingBreathingPitchDirty;
        private bool _pendingBreathingLowPassHzDirty;
        private bool _pendingBreathingLowPassQDirty;
        private bool _pendingVelocityAnchorHapticDirty;
        private Vector4 _pendingSomaticState;
        private Vector4 _pendingJerkState;
        private Vector4 _pendingKccComfortState;
        private float _pendingNearCollision01;
        private float _pendingCondensation01;
        private float _pendingComfortVignette01;
        private float _pendingBreathingVolume;
        private float _pendingBreathingPitch;
        private float _pendingBreathingLowPassHz;
        private float _pendingBreathingLowPassQ;
        private SomaticHapticRequest _pendingVelocityAnchorHaptic;
        private uint _lastObservedAupShiftSequence;
        private uint _handGhostMask;
        private int _blackBoxCursor;
        private int _blackBoxLastRecordedFrame = -1;
        private bool _blackBoxDumped;
        // COLD ALLOC: VRSomaticBlackBoxEntry[300] - fixed fault snapshot for async dump handoff - owner: VRSomaticProvider
        private readonly VRSomaticBlackBoxEntry[] _blackBoxDumpSnapshot = new VRSomaticBlackBoxEntry[BlackBoxFrameCapacity];
        private string _blackBoxDumpPathCold;
        private int _blackBoxDumpSnapshotCount;
        private int _blackBoxDumpInFlight;
        private int _blackBoxDumpFaultPending;
        private int _blackBoxDumpFaultHResult;
        private bool _comfortFramePressureActive;
        private float _comfortPressureFallbackWeight01;
        private VRSomaticChestSocketPose _pdaSocketPose;
        private VRSomaticChestSocketPose _flareSocketPose;
        private VRSomaticCollisionState _collisionState;
        private VRSomaticSnapshot _snapshot = VRSomaticSnapshot.Inactive;

        private struct SomaticHapticRequest
        {
            public float LowFrequencyIntensity;
            public float HighFrequencyIntensity;
            public float DurationSeconds;
            public float DecayRate;
            public byte Priority;
            public byte MotorMask;
            public byte BlendMode;
        }

        public bool IsActive => _snapshot.IsActive;
        public VRSomaticSnapshot CurrentSnapshot => _snapshot;
        public uint HandGhostMask => _handGhostMask;

        public void BindRig(
            Transform hmdTransform,
            Transform visorHudRoot,
            Transform pdaChestSocket,
            Transform flareToolChestSocket,
            AudioSource breathingSource,
            AudioLowPassFilter breathingLowPassFilter)
        {
            AudioSource resolvedBreathingSource = breathingSource != null ? breathingSource : this.breathingSource;
            AudioLowPassFilter resolvedLowPassFilter = breathingLowPassFilter != null ? breathingLowPassFilter : this.breathingLowPassFilter;
            bool breathingBindingChanged =
                !ReferenceEquals(this.breathingSource, resolvedBreathingSource) ||
                !ReferenceEquals(this.breathingLowPassFilter, resolvedLowPassFilter);
            if (breathingBindingChanged && this.breathingSource != null && (_stateFlags & StateBreathingSourcePlaying) != 0u)
            {
                this.breathingSource.Stop();
                _stateFlags &= ~StateBreathingSourcePlaying;
            }
            if (breathingBindingChanged &&
                this.breathingLowPassFilter != null &&
                !ReferenceEquals(this.breathingLowPassFilter, resolvedLowPassFilter) &&
                this.breathingLowPassFilter.enabled)
            {
                this.breathingLowPassFilter.enabled = false;
            }

            this.hmdTransform = hmdTransform;
            this.visorHudRoot = visorHudRoot;
            this.pdaChestSocket = pdaChestSocket;
            this.flareToolChestSocket = flareToolChestSocket;
            this.breathingSource = resolvedBreathingSource;
            this.breathingLowPassFilter = resolvedLowPassFilter;
            _fallbackHmdTransform = null;
            if (breathingBindingChanged)
                ResetBreathingAudioPublishCache();
        }

        public void BindDecoupledRoot(Transform vrRootTransform)
        {
            _decoupledRootTransform = vrRootTransform;
            _lastObservedAupShiftSequence = 0u;
        }

        public bool TryGetChestSocket(VRSomaticChestSocketId socketId, out VRSomaticChestSocketPose socketPose)
        {
            if (!_snapshot.IsActive)
            {
                socketPose = default;
                return false;
            }

            socketPose = socketId == VRSomaticChestSocketId.FlareTool
                ? _flareSocketPose
                : _pdaSocketPose;
            return true;
        }

        public bool TryGetHandPose(byte handIndex, out VRSomaticHandPose handPose)
        {
            handPose = default;
            if (!_snapshot.IsActive ||
                handIndex >= HandCount ||
                !HandTargets.IsCreated ||
                !HandPhysicalPositions.IsCreated ||
                !HandTargets.TryReadOnlyNativeArray(out NativeArray<float3>.ReadOnly handTargets) ||
                !HandPhysicalPositions.TryReadOnlyNativeArray(out NativeArray<float3>.ReadOnly handPhysicalPositions) ||
                handTargets.Length <= handIndex ||
                handPhysicalPositions.Length <= handIndex)
            {
                return false;
            }

            float3 target = handTargets[handIndex];
            float3 physical = handPhysicalPositions[handIndex];
            float3 separation = target - physical;
            float separationSq = math.lengthsq(separation);
            if (!IsFiniteFloat3(target) || !IsFiniteFloat3(physical) || !math.isfinite(separationSq))
                return false;

            InputDispatcher dispatcher = null;
            InputDispatcher.TryResolveActiveRuntime(ref dispatcher);
            bool hasTracking = dispatcher != null &&
                               dispatcher.TryGetXRInputState(handIndex, out XRInputState state) &&
                               state.IsTracked != 0;
            bool ghostVisible = (_handGhostMask & (1u << handIndex)) != 0u;
            handPose = new VRSomaticHandPose(
                handIndex,
                hasTracking,
                ghostVisible,
                ToVector3(target),
                ToVector3(physical),
                separationSq);
            return true;
        }

        public bool TryGetNearFieldCollision(out VRSomaticCollisionState collisionState)
        {
            collisionState = _collisionState;
            return _snapshot.IsActive && _collisionState.HasContact && _collisionState.Intensity01 > 0.001f;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!IsFiniteVector(shiftOffset) || !math.isfinite(shiftSqrMagnitude) || shiftSqrMagnitude <= 0.000001f)
                return;

            CompleteSomaticComfortForBarrier();
            _stateFlags &= ~(StateHeadCollisionReady | StateHasPreviousHeadPose | StateRootInitialized | StateQueuedPresentationPoseMask);
            _lastObservedAupShiftSequence = shiftData.Sequence;
            _headLinearSpeedMetersPerSecond = 0f;
            _headAngularSpeedRadiansPerSecond = 0f;
            _headAngularAccelerationRadiansPerSecondSq = 0f;
            _previousHeadAngularVelocityRadiansPerSecond = float3.zero;
            _previousHeadAngularAccelerationRadiansPerSecondSq = float3.zero;
            _headAngularJerkRadiansPerSecondCubed = 0f;
            _headAngularJerk01 = 0f;
            _accelerationComfortVignette01 = 0f;
            ResetKccAngularComfortState();
            ResetSomaticComfortStateForShift();
            _accelerationReleaseBelowTimer = 0f;
            ResetComfortFramePressureState();
            _jerkCullBlend01 = 0f;
            _jerkEventCooldownRemaining = 0f;
            _nearFieldCollision01 = 0f;
            _collisionState = default;
            _lastPublishedNearCollision01 = float.PositiveInfinity;
            PublishComfortVignette(0f);
            PublishShaderState();

            float3 shift = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
            if (HandTargets.TryAcquireWriteNativeArray(out NativeArray<float3> handTargets))
            {
                try
                {
                    RebaseHandArray(handTargets, shift);
                }
                finally
                {
                    HandTargets.ReleaseWriteNativeArray();
                }
            }

            if (!HandPhysicalPositions.TryAcquireWriteNativeArray(out NativeArray<float3> handPhysicalPositions))
                return;

            try
            {
                RebaseHandArray(handPhysicalPositions, shift);
            }
            finally
            {
                HandPhysicalPositions.ReleaseWriteNativeArray();
            }
        }

        public void Tick(float deltaTime)
        {
            FlushPendingBlackBoxDumpFault();
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            _lastTickDeltaTime = safeDeltaTime;
            AdvanceSomaticTimers(safeDeltaTime);

            if (!TryResolveActiveHmd(out Transform activeHmd))
            {
                ApplyInactiveState(safeDeltaTime);
                return;
            }

            activeHmd.GetPositionAndRotation(out Vector3 headPosition, out Quaternion headRotation);
            bool hasFiniteHeadPosition = IsFiniteVector(headPosition);
            bool hasFiniteHeadRotation = TrySanitizeQuaternion(headRotation, out Quaternion sanitizedHeadRotation);
            if (!hasFiniteHeadPosition || !hasFiniteHeadRotation)
            {
                RecordBlackBoxFrame(headPosition, hasFiniteHeadRotation ? sanitizedHeadRotation : headRotation, BlackBoxFlagNonFinite);
                ApplyInactiveState(safeDeltaTime);
                return;
            }

            headRotation = sanitizedHeadRotation;
            if (!TryResolveXrCachedHeadAup(headPosition, out AbsoluteUniversePosition headAup) &&
                !TryResolveRuntimeAup(headPosition, out headAup))
            {
                RecordBlackBoxFrame(headPosition, headRotation, BlackBoxFlagNonFinite);
                ApplyInactiveState(safeDeltaTime);
                return;
            }
            ResetHeadMotionIfAupShifted(headPosition, headRotation);
            UpdateHeadMotion(headPosition, headRotation, safeDeltaTime);
            UpdateKccAngularComfortState(safeDeltaTime);
            ScheduleSomaticComfortKernel(in headAup, headRotation, safeDeltaTime);
            if (_decoupledRootTransform == null)
                PublishComfortVignette(_kccAngularComfortVignette01);

            UpdateRootSyncDirect(headPosition, headRotation, safeDeltaTime);
            UpdateHandKinematicsDirect(headPosition, headRotation, safeDeltaTime);
            RefreshPlayerSignalsIfDue();
            UpdateChestSockets(in headAup, headRotation);
            Quaternion visorRotation = ResolveVisorHudRotation(headPosition, headRotation);
            RefreshNearFieldCollisionQueryAvailability(safeDeltaTime);
            UpdateBreathingAudio();
            UpdateCondensation();
            PublishSnapshot(in headAup, headPosition, headRotation, visorRotation);
            PublishShaderState();
            TryEmitVelocityAnchorHaptics();
            PublishComfortTelemetry();
            PrepareHeadCollisionSamples(headPosition, headRotation);
            RecordBlackBoxFrame(headPosition, headRotation, 0);
        }

        public void LateFrameTick()
        {
            CompleteSomaticComfortIfReady();

            if ((_stateFlags & StateHeadCollisionReady) == 0u)
            {
                FlushQueuedPresentationOutputs();
                RefreshLateFrameRegistration();
                return;
            }

            _stateFlags &= ~StateHeadCollisionReady;
            if (!_snapshot.IsActive || !CanRunHeadCollisionQuery())
            {
                FadeNearFieldCollisionToZero(_lastTickDeltaTime);
                PublishShaderState();
                FlushQueuedPresentationOutputs();
                RefreshLateFrameRegistration();
                return;
            }

            ConsumeHeadCollisionSamples();
            PublishShaderState();
            FlushQueuedPresentationOutputs();
            RefreshLateFrameRegistration();
        }

        private void Awake()
        {
            ValidateNativeLayouts();
            CacheSocketRotations();
            RefreshCachedGlobalState();
            CacheBlackBoxDumpPathCold();
        }

        private void OnEnable()
        {
            CacheSocketRotations();
            RefreshCachedGlobalState();
            TrySubscribeXRRuntime();
            HectonFloatingOrigin.RegisterListener(this);
            CacheDataVaultCold();
            CacheBlackBoxDumpPathCold();
            TryRegisterHotSwap();
            RefreshRuntimeRegistration(IsVRSomaticRuntimeActive());
        }

        private void Start()
        {
            if (Application.isPlaying && IsVRSomaticRuntimeActive())
                TryRegisterLateFrame();
        }

        private void OnDisable()
        {
            ReleaseRuntimeState();
        }

        private void OnDestroy()
        {
            ReleaseRuntimeState();
        }

        private void ReleaseRuntimeState()
        {
            if (!Application.isPlaying)
            {
                _playerRuntimeContext = null;
                _cachedPlayerCamera = null;
                ResetSomaticComfortBuffers();
                DisposeNativeBuffers();
                return;
            }

            bool hadRuntimeState = HasRuntimeRegistrationOrActiveSnapshot();
            TryUnsubscribeXRRuntime();
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregisterLateFrame();
            TryUnregisterUpdate();
            TryUnregisterService();
            TryUnregisterHotSwap();
            ClearQueuedVelocityAnchorHaptic();
            ApplyInactiveState(0f, hadRuntimeState);
            _playerRuntimeContext = null;
            _cachedPlayerCamera = null;
            ResetSomaticComfortBuffers();
            DisposeNativeBuffers();
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    DisposeNativeBuffers();
                    _dataVault = currentService as IDataVault;
                    if (Application.isPlaying && IsVRSomaticRuntimeActive())
                        EnsureNativeBuffers();
                    break;

                case GlobalRegistryServiceSlot.VoxelEngineRuntime:
                    _voxelSdfReadModel = currentService as Hecton8.Core.Contracts.IVoxelSonarSdfReadModel;
                    break;

                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    _cachedPlayerCamera = _playerRuntimeContext != null ? _playerRuntimeContext.PlayerCamera : null;
                    break;

                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterUpdate();
                    TryUnregisterLateFrame();
                    if (Application.isPlaying && IsVRSomaticRuntimeActive())
                    {
                        TryRegisterUpdate();
                        TryRegisterLateFrame();
                    }
                    break;
            }
        }

        private void OnValidate()
        {
            ValidateNativeLayouts();
            CacheSocketRotations();
        }

        private void RefreshCachedGlobalState()
        {
            _globalQualityWeight01 = ResolveGlobalQualityWeight01();
            _voxelSdfReadModel = GlobalRegistry.VoxelSonarSdf;
            CachePlayerRuntimeContextCold();
            RefreshComfortProfileSelection();
            IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            if (runtimeContext == null)
                runtimeContext = _playerRuntimeContext;
            _cachedPlayerCamera = runtimeContext != null ? runtimeContext.PlayerCamera : null;
        }

        private void TrySubscribeXRRuntime()
        {
            if ((_stateFlags & StateSubscribedXRRuntime) != 0u || !Application.isPlaying)
                return;

            HectonXRRuntimeState.XRActiveChanged -= HandleXRActiveChanged;
            HectonXRRuntimeState.XRActiveChanged += HandleXRActiveChanged;
            _stateFlags |= StateSubscribedXRRuntime;
        }

        private void TryUnsubscribeXRRuntime()
        {
            if ((_stateFlags & StateSubscribedXRRuntime) == 0u)
                return;

            HectonXRRuntimeState.XRActiveChanged -= HandleXRActiveChanged;
            _stateFlags &= ~StateSubscribedXRRuntime;
        }

        private void HandleXRActiveChanged(bool isActive)
        {
            RefreshRuntimeRegistration(isActive);
        }

        private void RefreshRuntimeRegistration(bool isActive)
        {
            if (!Application.isPlaying)
                return;

            if (!isActive)
            {
                _comfortPressureFallbackWeight01 = 0f;
                bool hadRuntimeState = HasRuntimeRegistrationOrActiveSnapshot();
                TryUnregisterLateFrame();
                TryUnregisterUpdate();
                TryUnregisterService();
                ApplyInactiveState(0f, hadRuntimeState);
                DisposeNativeBuffers();
                return;
            }

            RefreshCachedGlobalState();
            EnsureNativeBuffers();
            TryRegisterService();
            TryRegisterUpdate();
            TryRegisterLateFrame();
        }

        private void TryRegisterService()
        {
            if ((_stateFlags & StateRegisteredService) != 0u || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterVRSomaticProvider(this);
            _stateFlags |= StateRegisteredService;
        }

        private void TryRegisterHotSwap()
        {
            if ((_stateFlags & StateRegisteredHotSwap) != 0u || !Application.isPlaying)
                return;

            if (!GlobalRegistry.TryRegisterHotSwapListener(this))
                return;

            _stateFlags |= StateRegisteredHotSwap;
        }

        private void TryUnregisterHotSwap()
        {
            if ((_stateFlags & StateRegisteredHotSwap) == 0u)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _stateFlags &= ~StateRegisteredHotSwap;
        }

        private void TryUnregisterService()
        {
            if ((_stateFlags & StateRegisteredService) == 0u)
                return;

            GlobalRegistry.UnregisterVRSomaticProvider(this);
            _stateFlags &= ~StateRegisteredService;
        }

        private void TryRegisterUpdate()
        {
            if ((_stateFlags & StateRegisteredUpdate) != 0u || !Application.isPlaying)
                return;

            if (GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player))
                _stateFlags |= StateRegisteredUpdate;
        }

        private void TryUnregisterUpdate()
        {
            if ((_stateFlags & StateRegisteredUpdate) == 0u)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _stateFlags &= ~StateRegisteredUpdate;
        }

        private void TryRegisterLateFrame()
        {
            if ((_stateFlags & StateRegisteredLateFrame) != 0u || !Application.isPlaying)
                return;

            if (GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player))
                _stateFlags |= StateRegisteredLateFrame;
        }

        private void TryUnregisterLateFrame()
        {
            if ((_stateFlags & StateRegisteredLateFrame) == 0u)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _stateFlags &= ~StateRegisteredLateFrame;
        }

        private void RefreshLateFrameRegistration()
        {
            if (!Application.isPlaying)
                return;

            if (!IsVRSomaticRuntimeActive())
                TryUnregisterLateFrame();
        }

        private bool HasRuntimeRegistrationOrActiveSnapshot()
        {
            const uint runtimeMask =
                StateHeadCollisionReady |
                StateRegisteredService |
                StateRegisteredUpdate |
                StateRegisteredLateFrame |
                StateQueuedPresentationPoseMask;
            return (_stateFlags & runtimeMask) != 0u || _snapshot.IsActive;
        }

        private bool TryResolveActiveHmd(out Transform activeHmd)
        {
            activeHmd = hmdTransform;
            if (!Application.isPlaying || !IsVRSomaticRuntimeActive())
                return false;

            if (activeHmd != null)
                return true;

            IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            Camera playerCamera = runtimeContext != null ? runtimeContext.PlayerCamera : null;

            if (playerCamera == null)
            {
                playerCamera = _cachedPlayerCamera;
            }

            if (playerCamera == null)
            {
                _fallbackHmdTransform = null;
                return false;
            }

            Transform resolvedHmd = playerCamera.transform;
            if (!ReferenceEquals(_fallbackHmdTransform, resolvedHmd))
                _fallbackHmdTransform = resolvedHmd;

            activeHmd = _fallbackHmdTransform;
            return activeHmd != null;
        }

        private static bool IsVRSomaticRuntimeActive()
        {
            return HectonXRRuntimeState.IsXRActive;
        }

        private void RefreshComfortProfileSelection()
        {
            float qualityPressure01 = 1f - ResolveGlobalQualityWeight01();
            float frameInterval = math.select(
                NominalFrameSafetyDeltaSeconds,
                HectonXRRuntimeState.FrameIntervalSeconds,
                math.isfinite(HectonXRRuntimeState.FrameIntervalSeconds));
            float framePressure01 = math.saturate(
                (frameInterval - NominalFrameSafetyDeltaSeconds) *
                math.rcp(math.max(PressureFallbackFrameSafetyDeltaSeconds - NominalFrameSafetyDeltaSeconds, MinimumDeltaTime)));
            _comfortPressureFallbackWeight01 = Smoothstep01(math.max(Sanitize01(qualityPressure01, 0f), framePressure01));
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static bool TrySanitizeQuaternion(Quaternion value, out Quaternion sanitized)
        {
            float4 q = new float4(value.x, value.y, value.z, value.w);
            float lengthSq = math.lengthsq(q);
            if (!math.all(math.isfinite(q)) ||
                !math.isfinite(lengthSq) ||
                lengthSq < QuaternionLengthSqMinimum ||
                lengthSq > QuaternionLengthSqMaximum)
            {
                sanitized = Quaternion.identity;
                return false;
            }

            if (math.abs(lengthSq - 1f) > QuaternionUnitLengthSqEpsilon)
                q *= ApproximateInverseLengthNoSqrt(lengthSq);

            sanitized = new Quaternion(q.x, q.y, q.z, q.w);
            return true;
        }

        private void UpdateHeadMotion(Vector3 headPosition, Quaternion headRotation, float deltaTime)
        {
            if ((_stateFlags & StateHasPreviousHeadPose) == 0u)
            {
                ResetHeadMotionHistoryAndPublishedComfort(headPosition, headRotation);
                return;
            }

            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(deltaTime, MinimumDeltaTime) : MinimumDeltaTime;
            float invSafeDeltaTime = math.rcp(safeDeltaTime);
            Vector3 headDelta = headPosition - _previousHeadPosition;
            float headDeltaSq =
                (headDelta.x * headDelta.x) +
                (headDelta.y * headDelta.y) +
                (headDelta.z * headDelta.z);
            float angularDelta = ApproximateAngularDeltaRadiansNoAcos(_previousHeadRotation, headRotation);
            if (!math.isfinite(headDeltaSq) ||
                headDeltaSq > HeadTrackingJumpDistanceMetersSq ||
                angularDelta > HeadTrackingJumpAngularRadians)
            {
                ResetHeadMotionHistoryAndPublishedComfort(headPosition, headRotation);
                return;
            }

            _headLinearSpeedMetersPerSecond = math.min(
                ApproximateMagnitudeNoSqrt(headDelta) * invSafeDeltaTime,
                MaxSomaticHeadLinearSpeedMetersPerSecond);

            float3 headAngularVelocity = ResolveAngularVelocityRadiansPerSecond(
                _previousHeadRotation,
                headRotation,
                angularDelta,
                invSafeDeltaTime);
            float angularSpeed = ApproximateMagnitudeNoSqrt(headAngularVelocity);
            if (angularSpeed > MaxSomaticHeadAngularSpeedRadiansPerSecond)
            {
                float clampScale = MaxSomaticHeadAngularSpeedRadiansPerSecond * math.rcp(math.max(angularSpeed, 0.0001f));
                headAngularVelocity *= clampScale;
                angularSpeed = MaxSomaticHeadAngularSpeedRadiansPerSecond;
            }

            _headAngularSpeedRadiansPerSecond = angularSpeed;
            UpdateRotationJerkState(safeDeltaTime, headAngularVelocity);

            _headRotationFrame3 = _headRotationFrame2;
            _headRotationFrame2 = _headRotationFrame1;
            _headRotationFrame1 = headRotation;
            _previousHeadPosition = headPosition;
            _previousHeadRotation = headRotation;
        }

        private void ResetHeadMotionHistory(Vector3 headPosition, Quaternion headRotation)
        {
            _previousHeadPosition = headPosition;
            _previousHeadRotation = headRotation;
            _headRotationFrame1 = headRotation;
            _headRotationFrame2 = headRotation;
            _headRotationFrame3 = headRotation;
            _stateFlags |= StateHasPreviousHeadPose;
            _headLinearSpeedMetersPerSecond = 0f;
            _headAngularSpeedRadiansPerSecond = 0f;
            _headAngularAccelerationRadiansPerSecondSq = 0f;
            _previousHeadAngularVelocityRadiansPerSecond = float3.zero;
            _previousHeadAngularAccelerationRadiansPerSecondSq = float3.zero;
            _headAngularJerkRadiansPerSecondCubed = 0f;
            _headAngularJerk01 = 0f;
            _accelerationComfortVignette01 = 0f;
            ResetKccAngularComfortState();
            _accelerationReleaseBelowTimer = 0f;
            ResetComfortFramePressureState();
            _jerkCullBlend01 = 0f;
        }

        private void ResetHeadMotionHistoryAndPublishedComfort(Vector3 headPosition, Quaternion headRotation)
        {
            ResetHeadMotionHistory(headPosition, headRotation);
            PublishComfortVignette(0f);
            PublishShaderState();
        }

        private void ResetHeadMotionIfAupShifted(Vector3 headPosition, Quaternion headRotation)
        {
            uint currentShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            if (_lastObservedAupShiftSequence == currentShiftSequence)
                return;

            _lastObservedAupShiftSequence = currentShiftSequence;
            ResetHeadMotionHistoryAndPublishedComfort(headPosition, headRotation);
            ResetKccAngularComfortState();
        }

        private void UpdateRotationJerkState(float deltaTime, float3 headAngularVelocityRadiansPerSecond)
        {
            float safeDeltaTime = math.max(deltaTime, MinimumDeltaTime);
            float invSafeDeltaTime = math.rcp(safeDeltaTime);
            float3 angularVelocity = SanitizeFiniteFloat3(headAngularVelocityRadiansPerSecond);
            float3 angularAcceleration = (angularVelocity - _previousHeadAngularVelocityRadiansPerSecond) * invSafeDeltaTime;
            if (!IsFiniteFloat3(angularAcceleration))
                angularAcceleration = float3.zero;
            float angularAccelerationMagnitude = ApproximateMagnitudeNoSqrt(angularAcceleration);
            if (!math.isfinite(angularAccelerationMagnitude))
                angularAccelerationMagnitude = 0f;
            _headAngularAccelerationRadiansPerSecondSq = angularAccelerationMagnitude;
            UpdateAccelerationComfortState(safeDeltaTime, angularAccelerationMagnitude);

            float3 angularJerkVector = (angularAcceleration - _previousHeadAngularAccelerationRadiansPerSecondSq) * invSafeDeltaTime;
            float angularJerk = ApproximateMagnitudeNoSqrt(angularJerkVector);
            if (!math.isfinite(angularJerk))
                angularJerk = 0f;

            _headAngularJerkRadiansPerSecondCubed = math.min(angularJerk, MaxSomaticHeadAngularJerkRadiansPerSecondCubed);
            float jerkLimit = SanitizeMinimum(rotationJerkLimitRadiansPerSecondCubed, 1f);
            float targetJerk01 = math.saturate(_headAngularJerkRadiansPerSecondCubed * math.rcp(jerkLimit));
            float blend = ResolveCinematicBlendApprox(SanitizeMinimum(rotationJerkCullSharpness, 1f), safeDeltaTime);
            _headAngularJerk01 = math.lerp(_headAngularJerk01, targetJerk01, blend);
            if (targetJerk01 >= 1f && _jerkEventCooldownRemaining <= 0f)
            {
                _jerkEventCount++;
                _jerkEventCooldownRemaining = JerkEventDebounceSeconds;
            }

            _previousHeadAngularVelocityRadiansPerSecond = angularVelocity;
            _previousHeadAngularAccelerationRadiansPerSecondSq = angularAcceleration;
        }

        private void UpdateAccelerationComfortState(float deltaTime, float angularAccelerationRadS2)
        {
            UpdateComfortFramePressureState(deltaTime);

            float softStart = SanitizeMinimum(ResolveComfortAccelerationSoftTunnelStartRadS2(), 0.01f);
            float emergencyClamp = math.max(softStart + 0.01f, SanitizeMinimum(ResolveComfortAccelerationEmergencyClampRadS2(), softStart + 0.01f));
            float releaseBelow = math.min(softStart, SanitizeNonNegative(ResolveComfortAccelerationReleaseBelowRadS2()));
            float hysteresisSeconds = SanitizeNonNegative(ResolveComfortAccelerationReleaseHysteresisSeconds());
            float safeAcceleration = SanitizeNonNegative(angularAccelerationRadS2);

            if (safeAcceleration <= releaseBelow)
                _accelerationReleaseBelowTimer = math.min(hysteresisSeconds, _accelerationReleaseBelowTimer + math.max(deltaTime, 0f));
            else
                _accelerationReleaseBelowTimer = 0f;

            bool canRelease = _accelerationReleaseBelowTimer >= hysteresisSeconds;
            float target = 0f;
            if (safeAcceleration > softStart || !canRelease)
            {
                float clampedAcceleration = math.min(safeAcceleration, emergencyClamp);
                float acceleration01 = math.saturate((clampedAcceleration - softStart) * math.rcp(math.max(0.001f, emergencyClamp - softStart)));
                target = Smoothstep01(acceleration01) * Sanitize01(ResolveComfortVignetteMaximum(), 0f);
                if (!canRelease && target < _accelerationComfortVignette01)
                    target = _accelerationComfortVignette01;
            }

            float framePressureTarget = _comfortFramePressureActive
                ? Sanitize01(ResolveComfortFrameSafetyMinOpacity(), 0f)
                : 0f;
            target = math.max(target, framePressureTarget);
            float maxDelta = target > _accelerationComfortVignette01
                ? math.min(SanitizeMinimum(ResolveComfortVignetteAttackSlewPerFrame(), 0.001f), 0.1f)
                : math.min(SanitizeMinimum(ResolveComfortVignetteReleaseSlewPerFrame(), 0.001f), 0.1f);
            float delta = math.clamp(target - _accelerationComfortVignette01, -maxDelta, maxDelta);
            _accelerationComfortVignette01 = Sanitize01(_accelerationComfortVignette01 + delta, 0f);
        }

        private void UpdateComfortFramePressureState(float deltaTime)
        {
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(deltaTime, 0f) : 0f;
            float frameSafetyDeltaSeconds = ResolveComfortFrameSafetyDeltaSeconds();
            int consecutiveFrames = math.max(1, ResolveComfortFrameSafetyConsecutiveFrames());
            int releaseStableFrames = math.max(1, ResolveComfortFrameSafetyReleaseStableFrames());
            if (safeDeltaTime > frameSafetyDeltaSeconds)
            {
                _comfortFramePressureConsecutiveFrames = math.min(consecutiveFrames, _comfortFramePressureConsecutiveFrames + 1);
                _comfortFramePressureStableFrames = 0;
                if (_comfortFramePressureConsecutiveFrames >= consecutiveFrames)
                    _comfortFramePressureActive = true;
                return;
            }

            _comfortFramePressureConsecutiveFrames = 0;
            if (!_comfortFramePressureActive)
            {
                _comfortFramePressureStableFrames = 0;
                return;
            }

            _comfortFramePressureStableFrames = math.min(releaseStableFrames, _comfortFramePressureStableFrames + 1);
            if (_comfortFramePressureStableFrames >= releaseStableFrames)
            {
                _comfortFramePressureStableFrames = 0;
                _comfortFramePressureActive = false;
            }
        }

        private void ResetComfortFramePressureState()
        {
            _comfortFramePressureConsecutiveFrames = 0;
            _comfortFramePressureStableFrames = 0;
            _comfortFramePressureActive = false;
        }

        private void UpdateKccAngularComfortState(float deltaTime)
        {
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(deltaTime, MinimumDeltaTime) : MinimumDeltaTime;
            _globalQualityWeight01 = ResolveGlobalQualityWeight01();
            RefreshComfortProfileSelection();
            if (!TryResolveLatestKccVelocitySignal(out KccVelocitySignal signal))
            {
                ReleaseKccAngularComfortState(safeDeltaTime, _lastConsumedKccVelocityFrame == 0u || IsKccVelocitySignalStale(_lastConsumedKccVelocityFrame));
                return;
            }

            if (signal.Sequence == 0u || IsKccVelocitySignalStale(signal.Frame))
            {
                ReleaseKccAngularComfortState(safeDeltaTime, true);
                return;
            }

            if (signal.Sequence == _lastConsumedKccVelocitySequence &&
                signal.Frame == _lastConsumedKccVelocityFrame &&
                signal.SourceId == _lastConsumedKccVelocitySourceId)
            {
                ReleaseKccAngularComfortState(safeDeltaTime, false);
                return;
            }

            uint previousSignalFrame = _lastConsumedKccVelocityFrame;
            _lastConsumedKccVelocitySequence = signal.Sequence;
            _lastConsumedKccVelocityFrame = signal.Frame;
            _lastConsumedKccVelocitySourceId = signal.SourceId;
            if (!TryResolveKccPlanarDirection(in signal, out float2 planarDirection))
            {
                ReleaseKccAngularComfortState(safeDeltaTime, true);
                return;
            }

            if ((_stateFlags & StateHasPreviousKccPlanarDirection) == 0u)
            {
                _previousKccPlanarDirection = planarDirection;
                _previousKccAngularVelocityRadiansPerSecond = 0f;
                _stateFlags |= StateHasPreviousKccPlanarDirection;
                ReleaseKccAngularComfortState(safeDeltaTime, false);
                return;
            }

            float signalDeltaTime = ResolveKccSignalDeltaTime(previousSignalFrame, signal.Frame, safeDeltaTime);
            float signedYawDelta = ResolveSignedPlanarAngleRadians(_previousKccPlanarDirection, planarDirection);
            float angularVelocity = signedYawDelta * math.rcp(signalDeltaTime);
            angularVelocity = math.clamp(angularVelocity, -KccAngularVelocityMaxRadiansPerSecond, KccAngularVelocityMaxRadiansPerSecond);
            float angularAcceleration = (angularVelocity - _previousKccAngularVelocityRadiansPerSecond) * math.rcp(signalDeltaTime);
            float angularAccelerationMagnitude = math.min(
                math.abs(math.select(angularAcceleration, 0f, !math.isfinite(angularAcceleration))),
                KccAngularAccelerationMaxRadiansPerSecondSq);

            _previousKccPlanarDirection = planarDirection;
            _previousKccAngularVelocityRadiansPerSecond = angularVelocity;
            _kccAngularVelocityRadiansPerSecond = angularVelocity;
            _kccAngularAccelerationRadiansPerSecondSq = angularAccelerationMagnitude;
            UpdateKccAccelerationComfortOutput(safeDeltaTime, angularAccelerationMagnitude);
        }

        private void ReleaseKccAngularComfortState(float deltaTime, bool clearDirectionHistory)
        {
            if (clearDirectionHistory)
            {
                _stateFlags &= ~StateHasPreviousKccPlanarDirection;
                _previousKccPlanarDirection = float2.zero;
                _previousKccAngularVelocityRadiansPerSecond = 0f;
            }

            float blend = ResolveCinematicBlendApprox(18f, deltaTime);
            _kccAngularVelocityRadiansPerSecond = math.lerp(_kccAngularVelocityRadiansPerSecond, 0f, blend);
            _kccAngularAccelerationRadiansPerSecondSq = math.lerp(_kccAngularAccelerationRadiansPerSecondSq, 0f, blend);
            UpdateKccAccelerationComfortOutput(deltaTime, 0f);
        }

        private void ResetKccAngularComfortState()
        {
            _stateFlags &= ~StateHasPreviousKccPlanarDirection;
            _previousKccPlanarDirection = float2.zero;
            _previousKccAngularVelocityRadiansPerSecond = 0f;
            _kccAngularVelocityRadiansPerSecond = 0f;
            _kccAngularAccelerationRadiansPerSecondSq = 0f;
            _kccAngularComfortVignette01 = 0f;
            _kccHorizonLock01 = 0f;
            _kccAccelerationReleaseBelowTimer = 0f;
            _lastConsumedKccVelocitySequence = 0u;
            _lastConsumedKccVelocityFrame = 0u;
            _lastConsumedKccVelocitySourceId = 0u;
        }

        private void UpdateKccAccelerationComfortOutput(float deltaTime, float angularAccelerationRadS2)
        {
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(deltaTime, 0f) : 0f;
            float quality = Sanitize01(_globalQualityWeight01, 1f);
            float lowAssist01 = 1f - quality;
            float softStart = SanitizeMinimum(kccAngularAccelerationSoftTunnelStartRadS2, 0.01f) * math.lerp(0.82f, 1.12f, quality);
            float emergencyClamp = SanitizeMinimum(kccAngularAccelerationEmergencyClampRadS2, softStart + 0.01f) * math.lerp(0.88f, 1.10f, quality);
            emergencyClamp = math.max(softStart + 0.01f, emergencyClamp);
            float releaseBelow = math.min(softStart, SanitizeNonNegative(kccAngularAccelerationReleaseBelowRadS2));
            float hysteresisSeconds = SanitizeNonNegative(kccAccelerationReleaseHysteresisSeconds);
            float safeAcceleration = SanitizeNonNegative(angularAccelerationRadS2);

            if (safeAcceleration <= releaseBelow)
                _kccAccelerationReleaseBelowTimer = math.min(hysteresisSeconds, _kccAccelerationReleaseBelowTimer + safeDeltaTime);
            else
                _kccAccelerationReleaseBelowTimer = 0f;

            bool canRelease = _kccAccelerationReleaseBelowTimer >= hysteresisSeconds;
            float targetVignette = 0f;
            if (safeAcceleration > softStart || !canRelease)
            {
                float acceleration01 = math.saturate((math.min(safeAcceleration, emergencyClamp) - softStart) * math.rcp(math.max(0.001f, emergencyClamp - softStart)));
                float maximum = Sanitize01(kccAngularAccelerationVignetteContribution, 0f) * math.lerp(1.15f, 0.85f, quality);
                targetVignette = Smoothstep01(acceleration01) * maximum;
                if (!canRelease && targetVignette < _kccAngularComfortVignette01)
                    targetVignette = _kccAngularComfortVignette01;
            }

            float fullLockAcceleration = SanitizeMinimum(kccHorizonLockFullAccelerationRadS2, 0.01f) * math.lerp(0.85f, 1.15f, quality);
            float horizonTarget = Smoothstep01(math.saturate(safeAcceleration * math.rcp(fullLockAcceleration))) *
                                  Sanitize01(kccHorizonLockMaximum01, 0f) *
                                  math.lerp(1.10f, 0.92f, quality);
            horizonTarget = math.max(horizonTarget, targetVignette * math.lerp(0.28f, 0.18f, lowAssist01));

            float maxDelta = targetVignette > _kccAngularComfortVignette01
                ? math.min(SanitizeMinimum(ResolveComfortVignetteAttackSlewPerFrame(), 0.001f), 0.1f)
                : math.min(SanitizeMinimum(ResolveComfortVignetteReleaseSlewPerFrame(), 0.001f), 0.1f);
            float vignetteDelta = math.clamp(targetVignette - _kccAngularComfortVignette01, -maxDelta, maxDelta);
            _kccAngularComfortVignette01 = Sanitize01(_kccAngularComfortVignette01 + vignetteDelta, 0f);

            float horizonBlend = ResolveCinematicBlendApprox(math.lerp(10f, 22f, quality), math.max(safeDeltaTime, MinimumDeltaTime));
            _kccHorizonLock01 = Sanitize01(math.lerp(_kccHorizonLock01, horizonTarget, horizonBlend), 0f);
        }

        private static bool TryResolveLatestKccVelocitySignal(out KccVelocitySignal signal)
        {
            global::System.ReadOnlySpan<KccVelocitySignal> signals = SignalBus<KccVelocitySignal>.GetFrameSnapshot();
            signal = default;
            if (signals.Length == 0)
                return false;

            bool found = false;
            for (int i = 0; i < signals.Length; i++)
            {
                KccVelocitySignal candidate = signals[i];
                if (candidate.Sequence == 0u)
                    continue;

                if (!found || IsKccVelocitySignalNewer(in candidate, in signal))
                {
                    signal = candidate;
                    found = true;
                }
            }

            return found;
        }

        private static bool IsKccVelocitySignalNewer(in KccVelocitySignal candidate, in KccVelocitySignal current)
        {
            if (candidate.Frame != current.Frame)
                return candidate.Frame > current.Frame;

            if (candidate.Sequence != current.Sequence)
                return candidate.Sequence > current.Sequence;

            return candidate.SourceId >= current.SourceId;
        }

        private bool TryResolveKccPlanarDirection(in KccVelocitySignal signal, out float2 direction)
        {
            float2 planar = new float2(signal.Velocity.x, signal.Velocity.z);
            float speedSq = math.lengthsq(planar);
            float speedMinimum = SanitizeMinimum(kccMinimumPlanarSpeedMetersPerSecond, 0.01f);
            if (!math.isfinite(speedSq) || speedSq < speedMinimum * speedMinimum)
            {
                direction = float2.zero;
                return false;
            }

            direction = planar * math.rsqrt(math.max(speedSq, 0.000001f));
            if (math.all(math.isfinite(direction)))
                return true;

            direction = float2.zero;
            return false;
        }

        private static float ResolveSignedPlanarAngleRadians(float2 previousDirection, float2 currentDirection)
        {
            float cross = (previousDirection.x * currentDirection.y) - (previousDirection.y * currentDirection.x);
            float dot = math.clamp(math.dot(previousDirection, currentDirection), -1f, 1f);
            if (!math.isfinite(cross) || !math.isfinite(dot))
                return 0f;

            return MathLodApproximation.ApproxAtan2Fast(cross, dot);
        }

        private static bool IsKccVelocitySignalStale(uint signalFrame)
        {
            if (signalFrame == 0u)
                return true;

            int currentFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (signalFrame > (uint)currentFrame)
                return false;

            return (uint)currentFrame - signalFrame > KccVelocitySignalStaleFrameLimit;
        }

        private static float ResolveKccSignalDeltaTime(uint previousSignalFrame, uint currentSignalFrame, float fallbackDeltaTime)
        {
            float safeFallback = math.isfinite(fallbackDeltaTime) ? math.max(fallbackDeltaTime, MinimumDeltaTime) : MinimumDeltaTime;
            if (previousSignalFrame == 0u)
                return safeFallback;

            if (currentSignalFrame > previousSignalFrame)
            {
                uint frameDelta = math.min(currentSignalFrame - previousSignalFrame, (uint)KccVelocitySignalStaleFrameLimit);
                return math.max(MinimumDeltaTime, safeFallback * frameDelta);
            }

            return safeFallback;
        }

        private float ResolveComfortVignetteMaximum()
        {
            return math.lerp(comfortVignetteMaximum, PressureFallbackComfortVignetteMaximum, Sanitize01(_comfortPressureFallbackWeight01, 0f));
        }

        private float ResolveComfortAccelerationSoftTunnelStartRadS2()
        {
            return math.lerp(comfortAccelerationSoftTunnelStartRadS2, PressureFallbackAccelerationSoftTunnelStartRadS2, Sanitize01(_comfortPressureFallbackWeight01, 0f));
        }

        private float ResolveComfortAccelerationEmergencyClampRadS2()
        {
            return math.lerp(comfortAccelerationEmergencyClampRadS2, PressureFallbackAccelerationEmergencyClampRadS2, Sanitize01(_comfortPressureFallbackWeight01, 0f));
        }

        private float ResolveComfortAccelerationReleaseBelowRadS2()
        {
            return math.lerp(comfortAccelerationReleaseBelowRadS2, PressureFallbackAccelerationReleaseBelowRadS2, Sanitize01(_comfortPressureFallbackWeight01, 0f));
        }

        private float ResolveComfortAccelerationReleaseHysteresisSeconds()
        {
            return math.lerp(comfortAccelerationReleaseHysteresisSeconds, PressureFallbackAccelerationReleaseHysteresisSeconds, Sanitize01(_comfortPressureFallbackWeight01, 0f));
        }

        private float ResolveComfortVignetteAttackSlewPerFrame()
        {
            return math.lerp(comfortVignetteAttackSlewPerFrame, PressureFallbackVignetteAttackSlewPerFrame, Sanitize01(_comfortPressureFallbackWeight01, 0f));
        }

        private float ResolveComfortVignetteReleaseSlewPerFrame()
        {
            return math.lerp(comfortVignetteReleaseSlewPerFrame, PressureFallbackVignetteReleaseSlewPerFrame, Sanitize01(_comfortPressureFallbackWeight01, 0f));
        }

        private float ResolveComfortFrameSafetyDeltaSeconds()
        {
            return math.lerp(NominalFrameSafetyDeltaSeconds, PressureFallbackFrameSafetyDeltaSeconds, Sanitize01(_comfortPressureFallbackWeight01, 0f));
        }

        private float ResolveComfortFrameSafetyMinOpacity()
        {
            return math.lerp(NominalFrameSafetyMinOpacity, PressureFallbackFrameSafetyMinOpacity, Sanitize01(_comfortPressureFallbackWeight01, 0f));
        }

        private int ResolveComfortFrameSafetyConsecutiveFrames()
        {
            return (int)math.round(math.lerp(
                (float)NominalFrameSafetyConsecutiveFrames,
                (float)PressureFallbackFrameSafetyConsecutiveFrames,
                Sanitize01(_comfortPressureFallbackWeight01, 0f)));
        }

        private int ResolveComfortFrameSafetyReleaseStableFrames()
        {
            return (int)math.round(math.lerp(
                (float)NominalFrameSafetyReleaseStableFrames,
                (float)PressureFallbackFrameSafetyReleaseStableFrames,
                Sanitize01(_comfortPressureFallbackWeight01, 0f)));
        }

        private void RefreshPlayerSignalsIfDue()
        {
            if (_playerSignalSampleRemaining > 0f)
                return;

            _playerSignalSampleRemaining = PlayerSignalSampleIntervalSeconds;
            ResolvePlayerSignals(out _playerStress01, out _oxygen01, out _depthMeters);
        }

        private void AdvanceSomaticTimers(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            if (_playerSignalSampleRemaining > 0f)
                _playerSignalSampleRemaining = math.max(0f, _playerSignalSampleRemaining - deltaTime);
            if (_impactHapticCooldownRemaining > 0f)
                _impactHapticCooldownRemaining = math.max(0f, _impactHapticCooldownRemaining - deltaTime);
            if (_jerkEventCooldownRemaining > 0f)
                _jerkEventCooldownRemaining = math.max(0f, _jerkEventCooldownRemaining - deltaTime);
            if (_velocityHapticCooldownRemaining > 0f)
                _velocityHapticCooldownRemaining = math.max(0f, _velocityHapticCooldownRemaining - deltaTime);
        }

        private void ResolvePlayerSignals(out float stress01, out float oxygen01, out float depthMeters)
        {
            stress01 = 0f;
            oxygen01 = 1f;
            depthMeters = 0f;

            IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            if (runtimeContext == null)
                runtimeContext = _playerRuntimeContext;
            if (runtimeContext == null)
                return;

            bool hasPublishedMovement = runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState);
            bool hasPublishedSurvival = runtimeContext.TryGetSurvivalRuntimeState(out PlayerSurvivalRuntimeState survivalState);
            bool hasSurvival = (survivalState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasSurvival) != 0u;
            bool hasMovementDepth =
                hasPublishedMovement &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasMovement) != 0u &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                math.isfinite(movementState.DepthMeters);

            depthMeters = hasMovementDepth ? SanitizeNonNegative(movementState.DepthMeters) : 0f;
            if (hasPublishedSurvival && hasSurvival)
            {
                oxygen01 = Sanitize01(survivalState.OxygenNormalized, 1f);
                stress01 = math.max(
                    1f - oxygen01,
                    math.max(
                        Sanitize01(survivalState.PressureExposureSeverity01, 0f),
                        math.max(
                            Sanitize01(survivalState.ThermalStressSeverity01, 0f),
                            math.max(
                                Sanitize01(survivalState.RapidAscentRisk01, 0f),
                                Sanitize01(survivalState.NitrogenNarcosis01, 0f)))));
            }

            if (hasMovementDepth)
                stress01 = math.max(stress01, Sanitize01(movementState.UnderwaterStressIntensity01, 0f));

            HectonPlayerMovement movement = runtimeContext.PlayerMovement;
            if (movement != null)
            {
                if (!hasMovementDepth && math.isfinite(movement.CurrentDepth))
                    depthMeters = math.max(depthMeters, SanitizeNonNegative(movement.CurrentDepth));

                stress01 = math.max(
                    stress01,
                    math.max(
                        Sanitize01(movement.CurrentHullStress01, 0f),
                        Sanitize01(movement.CurrentUnderwaterStressIntensity01, 0f)));
            }

            HectonSurvivalSystem survival = runtimeContext.SurvivalSystem;
            if (survival != null)
            {
                if (!hasMovementDepth && math.isfinite(survival.Depth))
                    depthMeters = math.max(depthMeters, SanitizeNonNegative(survival.Depth));
                if (!hasSurvival)
                {
                    oxygen01 = Sanitize01(survival.OxygenNormalized, 1f);
                    stress01 = math.max(
                        stress01,
                        math.max(
                            1f - oxygen01,
                            math.max(
                                Sanitize01(survival.PressureExposureSeverity01, 0f),
                                Sanitize01(survival.ThermalStressSeverity01, 0f))));
                }
            }

            stress01 = Sanitize01(stress01, 0f);
            oxygen01 = Sanitize01(oxygen01, 1f);
            depthMeters = SanitizeNonNegative(depthMeters);
        }

        private void UpdateChestSockets(in AbsoluteUniversePosition headAup, Quaternion headRotation)
        {
            _torsoRotation = ResolveTorsoYawFromQuaternionNoTrig(headRotation, _torsoRotation);

            _pdaSocketPose = ResolveSocketPose(in headAup, pdaChestOffset, _pdaSocketLocalRotation);
            _flareSocketPose = ResolveSocketPose(in headAup, flareToolChestOffset, _flareSocketLocalRotation);

            if (pdaChestSocket != null || flareToolChestSocket != null)
            {
                _stateFlags |= StateChestSocketPoseDirty;
            }
        }

        private VRSomaticChestSocketPose ResolveSocketPose(
            in AbsoluteUniversePosition headAup,
            Vector3 localOffset,
            Quaternion localRotation)
        {
            Vector3 socketOffset = RotateYawOffsetNoMatrix(localOffset, _torsoRotation);
            AbsoluteUniversePosition socketAup = OffsetAupLocal(in headAup, socketOffset);
            Vector3 socketPosition = socketAup.ToRuntimeFloat3();
            Quaternion socketRotation = _torsoRotation * localRotation;
            return new VRSomaticChestSocketPose(
                socketAup,
                socketPosition,
                socketRotation);
        }

        private Quaternion ResolveVisorHudRotation(Vector3 headPosition, Quaternion headRotation)
        {
            Quaternion laggedRotation = headRotation;
            if (applyVisorHudHeadLag)
            {
                float angular01 = math.saturate(_headAngularSpeedRadiansPerSecond / SanitizeMinimum(visorLagAngularSpeedForFull, 0.25f));
                float lagBlend = math.saturate(angular01 * Sanitize01(visorLagMaximumBlend, 0f));
                laggedRotation = ApproximateNlerpNoSqrt(headRotation, _headRotationFrame3, lagBlend);
            }

            float jerkCull01 = math.saturate(_headAngularJerk01 * Sanitize01(rotationJerkCullMaximumBlend, 0f));
            _jerkCullBlend01 = math.lerp(
                _jerkCullBlend01,
                jerkCull01,
                ResolveCinematicBlendApprox(SanitizeMinimum(rotationJerkCullSharpness, 1f), _lastTickDeltaTime));
            if (_jerkCullBlend01 > 0.001f)
                laggedRotation = ApproximateNlerpNoSqrt(laggedRotation, _headRotationFrame3, _jerkCullBlend01);

            if (visorHudRoot != null)
            {
                _pendingVisorHudPosition = headPosition;
                _pendingVisorHudRotation = laggedRotation;
                _stateFlags |= StateVisorHudPoseDirty;
            }

            return laggedRotation;
        }

        private void UpdateBreathingAudio()
        {
            if (breathingSource == null)
                return;

            float stress01 = Sanitize01(_playerStress01, 0f);
            float oxygen01 = Sanitize01(_oxygen01, 1f);
            float nearField01 = Sanitize01(_nearFieldCollision01, 0f);
            float oxygenDanger01 = 1f - oxygen01;
            float depth01 = math.saturate(SanitizeNonNegative(_depthMeters) / 1400f);
            float drive01 = math.saturate(math.max(stress01, math.max(oxygenDanger01 * 1.15f, nearField01 * 0.5f)));

            if ((_stateFlags & StateBreathingAudioStaticApplied) == 0u)
            {
                _pendingBreathingStaticApply = true;
                _breathingAudioDirty = true;
            }

            float targetVolume = math.lerp(Sanitize01(breathingBaseVolume, 0f), Sanitize01(breathingStressVolume, 0f), drive01);
            if (math.abs(targetVolume - _lastPublishedBreathingVolume) > AudioPublishEpsilon)
            {
                _pendingBreathingVolume = targetVolume;
                _pendingBreathingVolumeDirty = true;
                _breathingAudioDirty = true;
            }

            float targetPitch = math.lerp(SanitizeMinimum(breathingMinimumPitch, 0.5f), SanitizeMinimum(breathingMaximumPitch, 0.5f), math.max(stress01, oxygenDanger01));
            if (math.abs(targetPitch - _lastPublishedBreathingPitch) > AudioPublishEpsilon)
            {
                _pendingBreathingPitch = targetPitch;
                _pendingBreathingPitchDirty = true;
                _breathingAudioDirty = true;
            }

            if (breathingLowPassFilter != null)
            {
                float lowPass01 = math.saturate(math.max(oxygenDanger01, depth01 * 0.55f));
                if ((_stateFlags & StateBreathingLowPassStaticApplied) == 0u)
                {
                    _pendingBreathingLowPassStaticApply = true;
                    _breathingAudioDirty = true;
                }

                float openCutoffHz = SanitizeAudioCutoffHz(breathingLowPassOpenHz);
                float closedCutoffHz = SanitizeAudioCutoffHz(breathingLowPassClosedHz);
                float targetCutoffHz = math.lerp(math.max(openCutoffHz, closedCutoffHz), math.min(openCutoffHz, closedCutoffHz), lowPass01);
                if (math.abs(targetCutoffHz - _lastPublishedBreathingLowPassHz) > LowPassPublishEpsilonHz)
                {
                    _pendingBreathingLowPassHz = targetCutoffHz;
                    _pendingBreathingLowPassHzDirty = true;
                    _breathingAudioDirty = true;
                }

                float targetResonanceQ = math.lerp(1f, 1.65f, lowPass01);
                if (math.abs(targetResonanceQ - _lastPublishedBreathingLowPassQ) > AudioPublishEpsilon)
                {
                    _pendingBreathingLowPassQ = targetResonanceQ;
                    _pendingBreathingLowPassQDirty = true;
                    _breathingAudioDirty = true;
                }
            }

            if ((_stateFlags & StateBreathingSourcePlaying) == 0u && breathingSource.clip != null)
            {
                _pendingBreathingPlay = true;
                _breathingAudioDirty = true;
            }

        }

        private void UpdateCondensation()
        {
            float oxygenDanger01 = 1f - Sanitize01(_oxygen01, 1f);
            float depth01 = math.saturate(SanitizeNonNegative(_depthMeters) / 1400f);
            float target = math.saturate((Sanitize01(_playerStress01, 0f) * 0.58f) + (oxygenDanger01 * 0.32f) + (depth01 * 0.28f));
            float blend = ResolveCinematicBlendApprox(8f, _lastTickDeltaTime);
            _condensation01 = math.lerp(_condensation01, target, blend);
        }

        private void PublishSnapshot(
            in AbsoluteUniversePosition headAup,
            Vector3 headPosition,
            Quaternion headRotation,
            Quaternion visorRotation)
        {
            _snapshot = new VRSomaticSnapshot(
                true,
                headAup,
                headPosition,
                headRotation,
                visorRotation,
                _playerStress01,
                _oxygen01,
                _depthMeters,
                _nearFieldCollision01,
                _condensation01);
        }

        private void UpdateRootSyncDirect(Vector3 headPosition, Quaternion headRotation, float deltaTime)
        {
            if (_decoupledRootTransform == null)
                return;

            if (!_rootSyncInput.IsCreated || !_rootSyncOutput.IsCreated)
                EnsureNativeBuffers();
            if (!_rootSyncInput.IsCreated || !_rootSyncOutput.IsCreated)
                return;
            quaternion sanitizedHeadRotation = ResolveSomaticStabilizedRootRotation(headRotation);
            quaternion previousRootRotation = (_stateFlags & StateRootInitialized) != 0u
                ? _lastRootRotation
                : sanitizedHeadRotation;
            VRSomaticRootSyncInput input = new VRSomaticRootSyncInput
            {
                HeadPosition = new float3(headPosition.x, headPosition.y, headPosition.z),
                HeadRotation = sanitizedHeadRotation,
                PreviousRootRotation = previousRootRotation,
                DeltaTime = math.max(deltaTime, MinimumDeltaTime),
                HeadAngularSpeed = SanitizeNonNegative(_headAngularSpeedRadiansPerSecond),
                RootRotationSharpness = SanitizeMinimum(rootRotationSmoothingSharpness, 1f),
                VignetteAngularSpeedStart = SanitizeMinimum(comfortVignetteAngularSpeedStart, 0.01f),
                VignetteAngularSpeedFull = SanitizeMinimum(comfortVignetteAngularSpeedFull, 0.02f),
                VignetteMaximum = Sanitize01(ResolveComfortVignetteMaximum(), 0f),
                AccelerationVignette01 = math.max(
                    math.max(Sanitize01(_accelerationComfortVignette01, 0f), Sanitize01(_kccAngularComfortVignette01, 0f)),
                    Sanitize01(_somaticFovTunnelingIntensity01, 0f)),
                KccHorizonLock01 = math.max(Sanitize01(_kccHorizonLock01, 0f), Sanitize01(_somaticHorizonLockBlend01, 0f))
            };

            VRSomaticRootSyncOutput output = ResolveRootSyncOutput(in input);
            if (!_rootSyncInput.TryAcquireWriteNativeArray(out NativeArray<VRSomaticRootSyncInput> rootSyncInput))
            {
                return;
            }

            try
            {
                if (rootSyncInput.Length == 0)
                    return;

                rootSyncInput[0] = input;
            }
            finally
            {
                _rootSyncInput.ReleaseWriteNativeArray();
            }

            if (!_rootSyncOutput.TryAcquireWriteNativeArray(out NativeArray<VRSomaticRootSyncOutput> rootSyncOutput))
            {
                return;
            }

            try
            {
                if (rootSyncOutput.Length == 0)
                    return;

                rootSyncOutput[0] = output;
            }
            finally
            {
                _rootSyncOutput.ReleaseWriteNativeArray();
            }

            ApplyRootSyncOutput(in output);
        }

        private void ApplyRootSyncOutput(in VRSomaticRootSyncOutput output)
        {
            if (!math.all(math.isfinite(output.RootPosition)) || !IsFiniteQuaternion(output.RootRotation))
            {
                RecordBlackBoxFrame(_previousHeadPosition, _previousHeadRotation, BlackBoxFlagNonFinite);
                return;
            }

            _lastRootRotation = output.RootRotation;
            _stateFlags |= StateRootInitialized;
            if (_decoupledRootTransform != null)
            {
                _pendingRootSyncPosition = ToVector3(output.RootPosition);
                _pendingRootSyncRotation = ToQuaternion(output.RootRotation.value);
                _stateFlags |= StateRootPoseDirty;
            }

            PublishComfortVignette(output.ComfortVignette01);
        }

        private void UpdateHandKinematicsDirect(Vector3 headPosition, Quaternion headRotation, float deltaTime)
        {
            if (!HandTargets.IsCreated || !HandPhysicalPositions.IsCreated)
                EnsureNativeBuffers();
            if (!HandTargets.IsCreated || !HandPhysicalPositions.IsCreated)
                return;

            if (!HandTargets.TryAcquireWriteNativeArray(out NativeArray<float3> handTargets))
                return;

            try
            {
                if (handTargets.Length < HandCount)
                    return;

                CaptureHandTargets(handTargets, headPosition, headRotation);
            }
            finally
            {
                HandTargets.ReleaseWriteNativeArray();
            }

            if (!HandTargets.TryReadOnlyNativeArray(out NativeArray<float3>.ReadOnly handTargetsReadOnly) ||
                handTargetsReadOnly.Length < HandCount ||
                !HandPhysicalPositions.TryAcquireWriteNativeArray(out NativeArray<float3> handPhysicalPositions))
            {
                return;
            }

            try
            {
                if (handPhysicalPositions.Length < HandCount)
                    return;

                if ((_stateFlags & StateHandsInitialized) == 0u)
                {
                    for (int i = 0; i < HandCount; i++)
                        handPhysicalPositions[i] = handTargetsReadOnly[i];

                    _stateFlags |= StateHandsInitialized;
                }

                float safeDeltaTime = math.max(deltaTime, MinimumDeltaTime);
                float springForce = SanitizeMinimum(handSpringForce, 1f);
                for (int i = 0; i < HandCount; i++)
                    handPhysicalPositions[i] = ResolveHandPhysicalPosition(handTargetsReadOnly[i], handPhysicalPositions[i], safeDeltaTime, springForce);

                _handGhostMask = ResolveHandGhostMask(handTargetsReadOnly, handPhysicalPositions);
            }
            finally
            {
                HandPhysicalPositions.ReleaseWriteNativeArray();
            }
        }

        private void CaptureHandTargets(NativeArray<float3> handTargets, Vector3 headPosition, Quaternion headRotation)
        {
            InputDispatcher dispatcher = null;
            InputDispatcher.TryResolveActiveRuntime(ref dispatcher);
            handTargets[0] = ResolveHandTarget(handTargets, dispatcher, 0, headPosition, headRotation, -0.22f);
            handTargets[1] = ResolveHandTarget(handTargets, dispatcher, 1, headPosition, headRotation, 0.22f);
        }

        private float3 ResolveHandTarget(
            NativeArray<float3> handTargets,
            InputDispatcher dispatcher,
            byte handIndex,
            Vector3 headPosition,
            Quaternion headRotation,
            float lateralOffset)
        {
            if (dispatcher != null &&
                dispatcher.TryGetXRInputState(handIndex, out XRInputState state) &&
                IsFiniteFloat3(state.GripPositionWS))
            {
                return state.GripPositionWS;
            }

            if ((_stateFlags & StateHandsInitialized) != 0u &&
                handTargets.IsCreated &&
                handIndex < handTargets.Length &&
                IsFiniteFloat3(handTargets[handIndex]))
            {
                return handTargets[handIndex];
            }

            return ResolveFallbackHandTarget(headPosition, headRotation, lateralOffset);
        }

        private uint ResolveHandGhostMask(NativeArray<float3> handTargets, NativeArray<float3> handPhysicalPositions)
        {
            if (!handTargets.IsCreated || !handPhysicalPositions.IsCreated ||
                handTargets.Length < HandCount ||
                handPhysicalPositions.Length < HandCount)
            {
                return 0u;
            }

            float baseThreshold = SanitizeMinimum(ghostHandDistanceMeters, 0.01f);
            float qualityCurve = Smoothstep01(_globalQualityWeight01);
            float threshold = scaleGhostHandToleranceByQuality
                ? baseThreshold * math.lerp(2.5f, 1f, qualityCurve)
                : baseThreshold;
            float thresholdSq = threshold * threshold;
            uint mask = 0u;
            for (int i = 0; i < HandCount; i++)
            {
                float3 delta = handTargets[i] - handPhysicalPositions[i];
                float distanceSq = math.lengthsq(delta);
                if (math.isfinite(distanceSq) && distanceSq > thresholdSq)
                    mask |= 1u << i;
            }

            return mask;
        }

        private uint ResolveHandGhostMask(NativeArray<float3>.ReadOnly handTargets, NativeArray<float3> handPhysicalPositions)
        {
            if (!handTargets.IsCreated || !handPhysicalPositions.IsCreated ||
                handTargets.Length < HandCount ||
                handPhysicalPositions.Length < HandCount)
            {
                return 0u;
            }

            float baseThreshold = SanitizeMinimum(ghostHandDistanceMeters, 0.01f);
            float qualityCurve = Smoothstep01(_globalQualityWeight01);
            float threshold = scaleGhostHandToleranceByQuality
                ? baseThreshold * math.lerp(2.5f, 1f, qualityCurve)
                : baseThreshold;
            float thresholdSq = threshold * threshold;
            uint mask = 0u;
            for (int i = 0; i < HandCount; i++)
            {
                float3 delta = handTargets[i] - handPhysicalPositions[i];
                float distanceSq = math.lengthsq(delta);
                if (math.isfinite(distanceSq) && distanceSq > thresholdSq)
                    mask |= 1u << i;
            }

            return mask;
        }

        private static float3 ResolveFallbackHandTarget(Vector3 headPosition, Quaternion headRotation, float lateralOffset)
        {
            float3 offset = new float3(lateralOffset, -0.32f, 0.42f);
            float3 rotated = math.rotate((quaternion)headRotation, offset);
            return new float3(headPosition.x, headPosition.y, headPosition.z) + rotated;
        }

        private void PrepareHeadCollisionSamples(Vector3 headPosition, Quaternion headRotation)
        {
            if (!HasHeadCollisionBuffers() ||
                !CanRunHeadCollisionQuery() ||
                !IsFiniteVector(headPosition))
            {
                FadeNearFieldCollisionToZero(_lastTickDeltaTime);
                return;
            }

            Hecton8.Core.Contracts.IVoxelSonarSdfReadModel readModel = _voxelSdfReadModel;
            if (readModel == null)
            {
                ClearHeadCollisionSamples();
                FadeNearFieldCollisionToZero(_lastTickDeltaTime);
                return;
            }

            bool samplesLocked = false;
            try
            {
                if (!_headCollisionSamples.TryAcquireWriteNativeArray(out NativeArray<HeadCastSample> headCollisionSamples))
                {
                    FadeNearFieldCollisionToZero(_lastTickDeltaTime);
                    return;
                }

                samplesLocked = true;
                if (headCollisionSamples.Length < HeadCollisionCommandCount)
                {
                    FadeNearFieldCollisionToZero(_lastTickDeltaTime);
                    return;
                }

                float3 origin = new float3(headPosition.x, headPosition.y, headPosition.z);
                quaternion rotation = TrySanitizeQuaternion(headRotation, out Quaternion sanitizedHeadRotation)
                    ? new quaternion(sanitizedHeadRotation.x, sanitizedHeadRotation.y, sanitizedHeadRotation.z, sanitizedHeadRotation.w)
                    : quaternion.identity;
                float nearFieldDistance = SanitizeMinimum(nearFieldDistanceMeters, MinimumNearFieldDistanceMeters);
                float stepMeters = ResolveNearFieldSdfStepMeters(nearFieldDistance, _globalQualityWeight01);
                for (int i = 0; i < HeadCollisionCommandCount; i++)
                {
                    float localSide;
                    float3 direction = ResolveHeadSdfProbeDirection(rotation, i, out localSide);
                    headCollisionSamples[i] = BuildHeadSdfSample(
                        readModel,
                        origin,
                        direction,
                        nearFieldDistance,
                        stepMeters,
                        localSide);
                }
            }
            finally
            {
                if (samplesLocked)
                    _headCollisionSamples.ReleaseWriteNativeArray();
            }

            _stateFlags |= StateHeadCollisionReady;
        }

        private static HeadCastSample BuildHeadSdfSample(
            Hecton8.Core.Contracts.IVoxelSonarSdfReadModel readModel,
            float3 origin,
            float3 direction,
            float maxDistance,
            float stepMeters,
            float localSide)
        {
            HeadCastSample sample = new HeadCastSample
            {
                LocalSide = localSide
            };

            if (readModel == null ||
                !math.all(math.isfinite(origin)) ||
                !math.all(math.isfinite(direction)) ||
                !math.isfinite(maxDistance) ||
                maxDistance <= 0f ||
                !VoxelSonarSdfMath.TryResolveNearestSdfSurface(
                    readModel,
                    origin,
                    direction,
                    maxDistance,
                    stepMeters,
                    out VoxelSonarSdfRaycastHit hit))
            {
                return sample;
            }

            float normalSq = math.lengthsq(hit.Normal);
            bool hasHit =
                (hit.Flags & VoxelSonarSdfRaycastHit.FlagHit) != 0u &&
                hit.Distance >= 0f &&
                hit.Distance <= maxDistance &&
                math.isfinite(hit.Distance) &&
                math.all(math.isfinite(hit.Point)) &&
                math.all(math.isfinite(hit.Normal)) &&
                math.isfinite(normalSq) &&
                normalSq >= HitNormalLengthSqMinimum &&
                normalSq <= HitNormalLengthSqMaximum;
            if (!hasHit)
                return sample;

            float inverseNormalLength = math.abs(normalSq - 1f) <= HitNormalUnitLengthSqEpsilon
                ? 1f
                : ApproximateInverseLengthNoSqrt(normalSq);
            sample.HasHit = 1;
            sample.Distance = math.max(0f, hit.Distance);
            sample.Point = hit.Point;
            sample.Normal = hit.Normal * inverseNormalLength;
            return sample;
        }

        private void ClearHeadCollisionSamples()
        {
            if (!_headCollisionSamples.IsCreated)
                return;

            bool samplesLocked = false;
            try
            {
                if (!_headCollisionSamples.TryAcquireWriteNativeArray(out NativeArray<HeadCastSample> headCollisionSamples))
                {
                    return;
                }

                samplesLocked = true;
                if (headCollisionSamples.Length < HeadCollisionCommandCount)
                    return;

                for (int i = 0; i < HeadCollisionCommandCount; i++)
                    headCollisionSamples[i] = default;
            }
            finally
            {
                if (samplesLocked)
                    _headCollisionSamples.ReleaseWriteNativeArray();
            }
        }

        private static float ResolveNearFieldSdfStepMeters(float nearFieldDistance, float qualityWeight01)
        {
            float quality = math.saturate(math.isfinite(qualityWeight01) ? qualityWeight01 : 1f);
            float coarseStep = math.max(0.05f, nearFieldDistance * 0.5f);
            float fineStep = math.max(0.025f, nearFieldDistance * 0.2f);
            return math.lerp(coarseStep, fineStep, quality);
        }

        private static float3 ResolveHeadSdfProbeDirection(quaternion rotation, int index, out float localSide)
        {
            localSide = 0f;
            float3 localDirection;
            switch (index)
            {
                case 0:
                    localDirection = new float3(0f, 0f, 1f);
                    break;
                case 1:
                    localDirection = new float3(0f, 0f, -1f);
                    break;
                case 2:
                    localSide = 1f;
                    localDirection = new float3(1f, 0f, 0f);
                    break;
                case 3:
                    localSide = -1f;
                    localDirection = new float3(-1f, 0f, 0f);
                    break;
                case 4:
                    localDirection = new float3(0f, 1f, 0f);
                    break;
                default:
                    localDirection = new float3(0f, -1f, 0f);
                    break;
            }

            return math.normalizesafe(math.rotate(rotation, localDirection), new float3(0f, 0f, 1f));
        }

        private void RefreshNearFieldCollisionQueryAvailability(float deltaTime)
        {
            if (CanRunHeadCollisionQuery())
                return;

            FadeNearFieldCollisionToZero(deltaTime);
        }

        private bool CanRunHeadCollisionQuery()
        {
            return nearFieldCollisionMask.value != 0 &&
                   math.isfinite(nearFieldDistanceMeters) &&
                   nearFieldDistanceMeters >= MinimumNearFieldDistanceMeters;
        }

        private void FadeNearFieldCollisionToZero(float deltaTime)
        {
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            if (safeDeltaTime <= 0f)
            {
                _nearFieldCollision01 = 0f;
            }
            else
            {
                float blend = ResolveCinematicBlendApprox(nearFieldFadeSharpness, safeDeltaTime);
                _nearFieldCollision01 = math.lerp(_nearFieldCollision01, 0f, blend);
                if (_nearFieldCollision01 <= ShaderPublishEpsilon)
                    _nearFieldCollision01 = 0f;
            }

            _collisionState = default;
        }

        private void ConsumeHeadCollisionSamples()
        {
            if (!HasHeadCollisionBuffers())
            {
                _collisionState = default;
                _nearFieldCollision01 = 0f;
                return;
            }
            if (!_headCollisionSamples.TryReadOnlyNativeArray(out NativeArray<HeadCastSample>.ReadOnly headCollisionSamples) ||
                headCollisionSamples.Length < HeadCollisionCommandCount)
            {
                _collisionState = default;
                _nearFieldCollision01 = 0f;
                return;
            }

            bool hasContact = false;
            HeadCastSample bestSample = default;
            float nearFieldDistance = SanitizeMinimum(nearFieldDistanceMeters, MinimumNearFieldDistanceMeters);
            float bestDistance = nearFieldDistance;
            for (int i = 0; i < HeadCollisionCommandCount; i++)
            {
                HeadCastSample sample = headCollisionSamples[i];
                if (sample.HasHit == 0 ||
                    !math.isfinite(sample.Distance) ||
                    sample.Distance < 0f ||
                    sample.Distance > bestDistance)
                {
                    continue;
                }

                bestDistance = sample.Distance;
                bestSample = sample;
                hasContact = true;
            }

            float targetIntensity = 0f;
            if (hasContact)
                targetIntensity = 1f - math.saturate(bestDistance / nearFieldDistance);

            float blend = ResolveCinematicBlendApprox(nearFieldFadeSharpness, _lastTickDeltaTime);
            _nearFieldCollision01 = math.lerp(_nearFieldCollision01, targetIntensity, blend);

            if (!hasContact)
            {
                _collisionState = default;
                return;
            }

            Vector3 normal = (Vector3)bestSample.Normal;
            Vector3 point = (Vector3)bestSample.Point;
            AbsoluteUniversePosition headAup = _snapshot.HeadAup;
            AbsoluteUniversePosition contactAup = OffsetAupLocal(in headAup, point - _snapshot.HeadRuntimePosition);
            _collisionState = new VRSomaticCollisionState(
                true,
                contactAup,
                point,
                normal,
                bestDistance,
                _nearFieldCollision01,
                _headLinearSpeedMetersPerSecond);

            TryEmitImpactHaptics(bestSample.LocalSide, _nearFieldCollision01);
        }

        private void TryEmitImpactHaptics(float localSide, float intensity01)
        {
            float impactThreshold = SanitizeMinimum(impactSpeedThresholdMetersPerSecond, 0.01f);
            if (_headLinearSpeedMetersPerSecond < impactThreshold)
                return;

            if (_impactHapticCooldownRemaining > 0f)
                return;

            float speedSpan = math.max(impactThreshold, 0.25f);
            float speed01 = math.saturate((_headLinearSpeedMetersPerSecond - impactThreshold) / speedSpan);
            float impact01 = math.saturate(math.max(intensity01, speed01));
            byte motorMask = ResolveDirectionalMotorMask(localSide);
            ToolHapticsRuntime.TryEnqueueCommand(
                Sanitize01(maxLowFrequencyImpact, 0f) * impact01,
                Sanitize01(maxHighFrequencyImpact, 0f) * impact01,
                SanitizeMinimum(impactHapticDurationSeconds, 0.02f),
                SanitizeMinimum(impactHapticDecayRate, 0f),
                HapticPriorityCritical,
                motorMask,
                HapticBlendAdditive);
            _impactHapticCooldownRemaining = SanitizeMinimum(impactHapticDebounceSeconds, MinimumImpactDebounceSeconds);
        }

        private static byte ResolveDirectionalMotorMask(float localSide)
        {
            if (localSide > HapticSideThreshold)
                return RightMotorMask;
            if (localSide < -HapticSideThreshold)
                return LeftMotorMask;
            return BothMotorMask;
        }

        private void ApplyInactiveState(float deltaTime, bool publishShaderState = true)
        {
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            if (safeDeltaTime <= 0f)
            {
                _nearFieldCollision01 = 0f;
                _condensation01 = 0f;
                InvalidateShaderPublishCache();
            }
            else
            {
                float blend = ResolveCinematicBlendApprox(nearFieldFadeSharpness, safeDeltaTime);
                _nearFieldCollision01 = math.lerp(_nearFieldCollision01, 0f, blend);
                _condensation01 = math.lerp(_condensation01, 0f, blend);
            }

            _playerStress01 = 0f;
            _oxygen01 = 1f;
            _depthMeters = 0f;
            _headLinearSpeedMetersPerSecond = 0f;
            _headAngularSpeedRadiansPerSecond = 0f;
            _headAngularAccelerationRadiansPerSecondSq = 0f;
            _previousHeadAngularVelocityRadiansPerSecond = float3.zero;
            _previousHeadAngularAccelerationRadiansPerSecondSq = float3.zero;
            _headAngularJerkRadiansPerSecondCubed = 0f;
            _headAngularJerk01 = 0f;
            _accelerationComfortVignette01 = 0f;
            ResetKccAngularComfortState();
            _accelerationReleaseBelowTimer = 0f;
            ResetComfortFramePressureState();
            _jerkCullBlend01 = 0f;
            _jerkEventCooldownRemaining = 0f;
            _playerSignalSampleRemaining = 0f;
            _impactHapticCooldownRemaining = 0f;
            _velocityHapticCooldownRemaining = 0f;
            _fallbackHmdTransform = null;
            _handGhostMask = 0u;
            _collisionState = default;
            _snapshot = VRSomaticSnapshot.Inactive;
            _stateFlags &= ~(
                StateHasPreviousHeadPose |
                StateHandsInitialized |
                StateRootInitialized |
                StateHasPreviousKccPlanarDirection |
                StateQueuedPresentationPoseMask);
            PublishComfortVignette(0f);
            if (breathingSource != null)
            {
                if ((_stateFlags & StateBreathingSourcePlaying) != 0u)
                {
                    _pendingBreathingStop = true;
                    _breathingAudioDirty = true;
                    _stateFlags &= ~StateBreathingSourcePlaying;
                }

                _pendingBreathingVolume = 0f;
                _pendingBreathingVolumeDirty = true;
                _breathingAudioDirty = true;
            }
            if (breathingLowPassFilter != null)
            {
                _pendingBreathingLowPassDisable = true;
                _breathingAudioDirty = true;
                _stateFlags &= ~StateBreathingLowPassStaticApplied;
            }
            if (publishShaderState)
                PublishShaderState();

            if (_blackBox.IsCreated)
                RecordBlackBoxFrame(Vector3.zero, Quaternion.identity, 0);
        }

        private void InvalidateShaderPublishCache()
        {
            _lastPublishedNearCollision01 = float.PositiveInfinity;
            _lastPublishedCondensation01 = float.PositiveInfinity;
            _lastPublishedComfortVignette01 = float.PositiveInfinity;
            _lastPublishedSomaticState = Vector4.positiveInfinity;
            _lastPublishedJerkState = Vector4.positiveInfinity;
            _lastPublishedKccComfortState = Vector4.positiveInfinity;
            InvalidateSomaticComfortPublishCache();
        }

        private void ResetBreathingAudioPublishCache()
        {
            _stateFlags &= ~(StateBreathingAudioStaticApplied | StateBreathingLowPassStaticApplied | StateBreathingSourcePlaying);
            _lastPublishedBreathingVolume = float.PositiveInfinity;
            _lastPublishedBreathingPitch = float.PositiveInfinity;
            _lastPublishedBreathingLowPassHz = float.PositiveInfinity;
            _lastPublishedBreathingLowPassQ = float.PositiveInfinity;
        }

        private void PublishShaderState()
        {
            _pendingNearCollision01 = Sanitize01(_nearFieldCollision01, 0f);
            _pendingCondensation01 = Sanitize01(_condensation01, 0f);
            _pendingSomaticState = new Vector4(
                Sanitize01(_playerStress01, 0f),
                Sanitize01(_oxygen01, 1f),
                SanitizeNonNegative(_depthMeters),
                SanitizeNonNegative(_headLinearSpeedMetersPerSecond));
            _pendingJerkState = new Vector4(
                Sanitize01(_headAngularJerk01, 0f),
                Sanitize01(_jerkCullBlend01, 0f),
                SanitizeNonNegative(_headAngularJerkRadiansPerSecondCubed),
                Sanitize01(rotationJerkVignetteContribution, 0f));
            _pendingKccComfortState = new Vector4(
                math.isfinite(_kccAngularVelocityRadiansPerSecond) ? _kccAngularVelocityRadiansPerSecond : 0f,
                SanitizeNonNegative(_kccAngularAccelerationRadiansPerSecondSq),
                Sanitize01(_kccAngularComfortVignette01, 0f),
                Sanitize01(_kccHorizonLock01, 0f));
            _somaticShaderStateDirty = true;
            PublishSomaticComfortShaderState();
        }

        private void PublishComfortVignette(float vignette01)
        {
            float sanitized = math.max(
                math.max(Sanitize01(vignette01, 0f), Sanitize01(_kccAngularComfortVignette01, 0f)),
                Sanitize01(_somaticFovTunnelingIntensity01, 0f));
            if (math.abs(sanitized - _lastPublishedComfortVignette01) <= ShaderPublishEpsilon)
                return;

            _pendingComfortVignette01 = sanitized;
            _comfortVignetteShaderDirty = true;
        }

        private void FlushQueuedPresentationOutputs()
        {
            FlushQueuedTransformPoses();
            FlushQueuedShaderState();
            FlushQueuedSomaticComfortShaderState();
            FlushQueuedComfortVignette();
            FlushQueuedBreathingAudio();
            FlushQueuedVelocityAnchorHaptic();
        }

        private void FlushQueuedTransformPoses()
        {
            uint poseFlags = _stateFlags & StateQueuedPresentationPoseMask;
            if (poseFlags == 0u)
                return;

            _stateFlags &= ~StateQueuedPresentationPoseMask;

            if ((poseFlags & StateRootPoseDirty) != 0u)
            {
                Transform root = _decoupledRootTransform;
                if (root != null)
                    root.SetPositionAndRotation(_pendingRootSyncPosition, _pendingRootSyncRotation);
            }

            if ((poseFlags & StateChestSocketPoseDirty) != 0u)
            {
                Transform pdaSocket = pdaChestSocket;
                if (pdaSocket != null)
                    pdaSocket.SetPositionAndRotation(_pdaSocketPose.RuntimePosition, _pdaSocketPose.RuntimeRotation);

                Transform flareSocket = flareToolChestSocket;
                if (flareSocket != null)
                    flareSocket.SetPositionAndRotation(_flareSocketPose.RuntimePosition, _flareSocketPose.RuntimeRotation);
            }

            if ((poseFlags & StateVisorHudPoseDirty) != 0u)
            {
                Transform visorRoot = visorHudRoot;
                if (visorRoot != null)
                    visorRoot.SetPositionAndRotation(_pendingVisorHudPosition, _pendingVisorHudRotation);
            }
        }

        private void FlushQueuedShaderState()
        {
            if (!_somaticShaderStateDirty)
                return;

            _somaticShaderStateDirty = false;
            float nearCollision01 = _pendingNearCollision01;
            if (math.abs(nearCollision01 - _lastPublishedNearCollision01) > ShaderPublishEpsilon)
            {
                Shader.SetGlobalFloat(NearCollisionIntensityId, nearCollision01);
                _lastPublishedNearCollision01 = nearCollision01;
            }

            float condensation01 = _pendingCondensation01;
            if (math.abs(condensation01 - _lastPublishedCondensation01) > ShaderPublishEpsilon)
            {
                Shader.SetGlobalFloat(SomaticCondensationId, condensation01);
                _lastPublishedCondensation01 = condensation01;
            }

            if (!Approximately(in _pendingSomaticState, in _lastPublishedSomaticState))
            {
                Shader.SetGlobalVector(SomaticStateId, _pendingSomaticState);
                _lastPublishedSomaticState = _pendingSomaticState;
            }

            if (!Approximately(in _pendingJerkState, in _lastPublishedJerkState))
            {
                Shader.SetGlobalVector(VrComfortJerkStateId, _pendingJerkState);
                _lastPublishedJerkState = _pendingJerkState;
            }

            if (!Approximately(in _pendingKccComfortState, in _lastPublishedKccComfortState))
            {
                Shader.SetGlobalVector(VrComfortKccStateId, _pendingKccComfortState);
                _lastPublishedKccComfortState = _pendingKccComfortState;
            }
        }

        private void FlushQueuedComfortVignette()
        {
            if (!_comfortVignetteShaderDirty)
                return;

            _comfortVignetteShaderDirty = false;
            float sanitized = _pendingComfortVignette01;
            if (math.abs(sanitized - _lastPublishedComfortVignette01) <= ShaderPublishEpsilon)
                return;

            Shader.SetGlobalFloat(VrComfortVignetteId, sanitized);
            _lastPublishedComfortVignette01 = sanitized;
            PublishSomaticComfortVignetteTelemetry(sanitized);
        }

        private void FlushQueuedBreathingAudio()
        {
            if (!_breathingAudioDirty)
                return;

            _breathingAudioDirty = false;
            AudioSource source = breathingSource;
            if (source != null)
            {
                if (_pendingBreathingStop)
                {
                    _pendingBreathingStop = false;
                    source.Stop();
                }

                if (_pendingBreathingStaticApply && (_stateFlags & StateBreathingAudioStaticApplied) == 0u)
                {
                    source.spatialBlend = 0f;
                    source.panStereo = 0f;
                    source.loop = true;
                    _stateFlags |= StateBreathingAudioStaticApplied;
                }

                if (_pendingBreathingVolumeDirty)
                {
                    source.volume = _pendingBreathingVolume;
                    _lastPublishedBreathingVolume = _pendingBreathingVolume;
                }

                if (_pendingBreathingPitchDirty)
                {
                    source.pitch = _pendingBreathingPitch;
                    _lastPublishedBreathingPitch = _pendingBreathingPitch;
                }

                if (_pendingBreathingPlay && (_stateFlags & StateBreathingSourcePlaying) == 0u && source.clip != null)
                {
                    if (!source.isPlaying)
                        source.Play();

                    _stateFlags |= StateBreathingSourcePlaying;
                }
            }

            AudioLowPassFilter lowPass = breathingLowPassFilter;
            if (lowPass != null)
            {
                if (_pendingBreathingLowPassDisable)
                    lowPass.enabled = false;
                else if (_pendingBreathingLowPassStaticApply && (_stateFlags & StateBreathingLowPassStaticApplied) == 0u)
                {
                    lowPass.enabled = true;
                    _stateFlags |= StateBreathingLowPassStaticApplied;
                }

                if (_pendingBreathingLowPassHzDirty)
                {
                    lowPass.cutoffFrequency = _pendingBreathingLowPassHz;
                    _lastPublishedBreathingLowPassHz = _pendingBreathingLowPassHz;
                }

                if (_pendingBreathingLowPassQDirty)
                {
                    lowPass.lowpassResonanceQ = _pendingBreathingLowPassQ;
                    _lastPublishedBreathingLowPassQ = _pendingBreathingLowPassQ;
                }
            }

            _pendingBreathingStaticApply = false;
            _pendingBreathingLowPassStaticApply = false;
            _pendingBreathingLowPassDisable = false;
            _pendingBreathingPlay = false;
            _pendingBreathingStop = false;
            _pendingBreathingVolumeDirty = false;
            _pendingBreathingPitchDirty = false;
            _pendingBreathingLowPassHzDirty = false;
            _pendingBreathingLowPassQDirty = false;
        }

        private void PublishSomaticComfortVignetteTelemetry(float vignette01)
        {
            float sanitized = Sanitize01(vignette01, 0f);
            if (sanitized <= _maxSomaticComfortVignetteTelemetry01)
                return;

            _maxSomaticComfortVignetteTelemetry01 = sanitized;
            if (_maxSomaticComfortVignetteTelemetry01 - _lastSomaticComfortVignetteTelemetry01 < VrComfortTelemetryStep01)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                VrComfortMaxVignetteHash,
                VrComfortTelemetryContextHash,
                _maxSomaticComfortVignetteTelemetry01);
            _lastSomaticComfortVignetteTelemetry01 = _maxSomaticComfortVignetteTelemetry01;
        }

        private void TryEmitVelocityAnchorHaptics()
        {
            float threshold = SanitizeMinimum(velocityHapticThresholdMetersPerSecond, 0.01f);
            if (_headLinearSpeedMetersPerSecond < threshold || _velocityHapticCooldownRemaining > 0f)
                return;

            float speed01 = math.saturate((_headLinearSpeedMetersPerSecond - threshold) * math.rcp(math.max(threshold, 0.25f)));
            float lowFrequency = math.lerp(0.035f, 0.16f, speed01);
            float highFrequency = math.lerp(0.015f, 0.09f, speed01);
            _pendingVelocityAnchorHaptic.LowFrequencyIntensity = lowFrequency;
            _pendingVelocityAnchorHaptic.HighFrequencyIntensity = highFrequency;
            _pendingVelocityAnchorHaptic.DurationSeconds = SanitizeMinimum(velocityHapticDurationSeconds, 0.01f);
            _pendingVelocityAnchorHaptic.DecayRate = 0f;
            _pendingVelocityAnchorHaptic.Priority = HapticPriorityComfort;
            _pendingVelocityAnchorHaptic.MotorMask = BothMotorMask;
            _pendingVelocityAnchorHaptic.BlendMode = HapticBlendAdditive;
            _pendingVelocityAnchorHapticDirty = true;
            _velocityHapticCooldownRemaining = SanitizeMinimum(velocityHapticIntervalSeconds, 0.03f);
        }

        private void FlushQueuedVelocityAnchorHaptic()
        {
            if (!_pendingVelocityAnchorHapticDirty)
                return;

            _pendingVelocityAnchorHapticDirty = false;
            SomaticHapticRequest request = _pendingVelocityAnchorHaptic;
            _pendingVelocityAnchorHaptic = default;
            ToolHapticsRuntime.TryEnqueueCommand(
                request.LowFrequencyIntensity,
                request.HighFrequencyIntensity,
                request.DurationSeconds,
                request.DecayRate,
                request.Priority,
                request.MotorMask,
                request.BlendMode);
        }

        private void ClearQueuedVelocityAnchorHaptic()
        {
            _pendingVelocityAnchorHapticDirty = false;
            _pendingVelocityAnchorHaptic = default;
        }

        private void PublishComfortTelemetry()
        {
            if (_jerkEventCount != _lastTelemetryJerkEventCount)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    VrComfortJerkEventHash,
                    VrComfortTelemetryContextHash,
                    _jerkEventCount);
                _lastTelemetryJerkEventCount = _jerkEventCount;
            }
        }

        private void RecordBlackBoxFrame(Vector3 headPosition, Quaternion headRotation, ushort extraFlags)
        {
            if (!_blackBox.IsCreated)
            {
                if (!_headCollisionDisposeHandle.IsCompleted || !Application.isPlaying)
                    return;

                EnsureBlackBoxBuffer();
            }
            float4 headRotationValue = new float4(headRotation.x, headRotation.y, headRotation.z, headRotation.w);
            bool hasFiniteRotation = math.all(math.isfinite(headRotationValue));
            float rotationLengthSq = math.lengthsq(headRotationValue);
            bool hasValidRotationLength = math.isfinite(rotationLengthSq) &&
                                          rotationLengthSq >= QuaternionLengthSqMinimum &&
                                          rotationLengthSq <= QuaternionLengthSqMaximum;
            ushort flags = ResolveBlackBoxFlags(extraFlags);
            if (!IsFiniteVector(headPosition) || !hasFiniteRotation || !hasValidRotationLength)
                flags |= BlackBoxFlagNonFinite;

            float leftHandSeparationSq = ResolveHandSeparationSq(0);
            float rightHandSeparationSq = ResolveHandSeparationSq(1);
            float kccAngularVelocity = math.isfinite(_kccAngularVelocityRadiansPerSecond) ? _kccAngularVelocityRadiansPerSecond : 0f;
            float kccAngularAcceleration = SanitizeNonNegative(_kccAngularAccelerationRadiansPerSecondSq);
            float kccComfortVignette = Sanitize01(_kccAngularComfortVignette01, 0f);
            float kccHorizonLock = Sanitize01(_kccHorizonLock01, 0f);
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            bool mergePreviousFlags = _blackBoxCursor > 0 && _blackBoxLastRecordedFrame == frame;
            int index = mergePreviousFlags
                ? (_blackBoxCursor - 1) % BlackBoxFrameCapacity
                : _blackBoxCursor % BlackBoxFrameCapacity;

            VRSomaticBlackBoxEntry entry = new VRSomaticBlackBoxEntry
            {
                Frame = frame,
                StateHash = ResolveBlackBoxStateHash(
                    headPosition,
                    headRotationValue,
                    leftHandSeparationSq,
                    rightHandSeparationSq,
                    kccAngularVelocity,
                    kccAngularAcceleration,
                    kccComfortVignette,
                    kccHorizonLock,
                    _lastConsumedKccVelocitySequence,
                    _lastConsumedKccVelocityFrame,
                    _lastConsumedKccVelocitySourceId,
                    flags),
                Flags = flags,
                HandGhostMask = (ushort)(_handGhostMask & 0xFFFFu),
                HeadPosition = new float3(headPosition.x, headPosition.y, headPosition.z),
                HeadRotation = headRotationValue,
                NearCollision01 = Sanitize01(_nearFieldCollision01, 0f),
                ComfortVignette01 = Sanitize01(_lastPublishedComfortVignette01, 0f),
                LeftHandSeparationSq = leftHandSeparationSq,
                RightHandSeparationSq = rightHandSeparationSq,
                HeadAngularSpeedRadiansPerSecond = SanitizeNonNegative(_headAngularSpeedRadiansPerSecond),
                AupShiftSequence = _lastObservedAupShiftSequence,
                KccAngularVelocityRadiansPerSecond = kccAngularVelocity,
                KccAngularAccelerationRadiansPerSecondSq = kccAngularAcceleration,
                KccComfortVignette01 = kccComfortVignette,
                KccHorizonLock01 = kccHorizonLock,
                KccVelocitySequence = _lastConsumedKccVelocitySequence,
                KccVelocityFrame = _lastConsumedKccVelocityFrame,
                KccVelocitySourceId = _lastConsumedKccVelocitySourceId,
                Reserved0 = 0u,
                Reserved1 = 0ul
            };

            bool blackBoxLocked = false;
            try
            {
                if (!_blackBox.TryAcquireWriteNativeArray(out NativeArray<VRSomaticBlackBoxEntry> blackBox))
                {
                    return;
                }

                blackBoxLocked = true;
                if (blackBox.Length < BlackBoxFrameCapacity)
                    return;

                if (mergePreviousFlags)
                {
                    entry.Flags = (ushort)(entry.Flags | blackBox[index].Flags);
                }
                else
                {
                    _blackBoxCursor++;
                    _blackBoxLastRecordedFrame = frame;
                }

                blackBox[index] = entry;
            }
            finally
            {
                if (blackBoxLocked)
                    _blackBox.ReleaseWriteNativeArray();
            }

            if ((flags & BlackBoxFlagNonFinite) != 0)
                DumpBlackBoxOnce();
        }

        private ushort ResolveBlackBoxFlags(ushort extraFlags)
        {
            uint flags = extraFlags;
            if (_snapshot.IsActive)
                flags |= BlackBoxFlagActive;
            if ((_handGhostMask & 1u) != 0u)
                flags |= BlackBoxFlagLeftGhost;
            if ((_handGhostMask & 2u) != 0u)
                flags |= BlackBoxFlagRightGhost;
            if (_collisionState.HasContact || _nearFieldCollision01 > 0.001f)
                flags |= BlackBoxFlagNearCollision;
            if (_lastObservedAupShiftSequence != 0u)
                flags |= BlackBoxFlagAupShiftSeen;
            if (_comfortFramePressureActive)
                flags |= BlackBoxFlagFramePressure;
            if (_comfortPressureFallbackWeight01 > 0.001f)
                flags |= BlackBoxFlagProtectiveFallback;
            if (_accelerationComfortVignette01 > 0.001f)
                flags |= BlackBoxFlagAccelerationTunnel;
            if (_lastConsumedKccVelocitySequence != 0u)
                flags |= BlackBoxFlagKccSignal;
            if (_kccAngularComfortVignette01 > 0.001f)
                flags |= BlackBoxFlagKccAccelerationTunnel;
            if (_kccHorizonLock01 > 0.001f)
                flags |= BlackBoxFlagDynamicHorizonLock;

            return (ushort)(flags & 0xFFFFu);
        }

        private float ResolveHandSeparationSq(int index)
        {
            if (!HandTargets.IsCreated ||
                !HandPhysicalPositions.IsCreated ||
                !HandTargets.TryReadOnlyNativeArray(out NativeArray<float3>.ReadOnly handTargets) ||
                !HandPhysicalPositions.TryReadOnlyNativeArray(out NativeArray<float3>.ReadOnly handPhysicalPositions) ||
                index < 0 ||
                index >= HandCount ||
                handTargets.Length <= index ||
                handPhysicalPositions.Length <= index)
            {
                return 0f;
            }

            float3 delta = handTargets[index] - handPhysicalPositions[index];
            float distanceSq = math.lengthsq(delta);
            return math.isfinite(distanceSq) ? distanceSq : 0f;
        }

        private static uint ResolveBlackBoxStateHash(
            Vector3 headPosition,
            float4 headRotation,
            float leftHandSeparationSq,
            float rightHandSeparationSq,
            float kccAngularVelocity,
            float kccAngularAcceleration,
            float kccComfortVignette,
            float kccHorizonLock,
            uint kccVelocitySequence,
            uint kccVelocityFrame,
            uint kccVelocitySourceId,
            ushort flags)
        {
            uint hash = 2166136261u;
            hash = MixHash(hash, math.asuint(headPosition.x));
            hash = MixHash(hash, math.asuint(headPosition.y));
            hash = MixHash(hash, math.asuint(headPosition.z));
            hash = MixHash(hash, math.asuint(headRotation.x));
            hash = MixHash(hash, math.asuint(headRotation.y));
            hash = MixHash(hash, math.asuint(headRotation.z));
            hash = MixHash(hash, math.asuint(headRotation.w));
            hash = MixHash(hash, math.asuint(leftHandSeparationSq));
            hash = MixHash(hash, math.asuint(rightHandSeparationSq));
            hash = MixHash(hash, math.asuint(kccAngularVelocity));
            hash = MixHash(hash, math.asuint(kccAngularAcceleration));
            hash = MixHash(hash, math.asuint(kccComfortVignette));
            hash = MixHash(hash, math.asuint(kccHorizonLock));
            hash = MixHash(hash, kccVelocitySequence);
            hash = MixHash(hash, kccVelocityFrame);
            hash = MixHash(hash, kccVelocitySourceId);
            return MixHash(hash, flags);
        }

        private static uint MixHash(uint hash, uint value)
        {
            unchecked
            {
                return (hash ^ value) * 16777619u;
            }
        }

        private void DumpBlackBoxOnce()
        {
            if (_blackBoxDumped ||
                !_blackBox.IsCreated ||
                _blackBox.Length < BlackBoxFrameCapacity ||
                string.IsNullOrEmpty(_blackBoxDumpPathCold) ||
                Interlocked.CompareExchange(ref _blackBoxDumpInFlight, 1, 0) != 0)
            {
                return;
            }

            int count = math.min(_blackBoxCursor, BlackBoxFrameCapacity);
            int start = _blackBoxCursor - count;
            if (count <= 0 || !TryStageBlackBoxDumpSnapshot(start, count))
            {
                Interlocked.Exchange(ref _blackBoxDumpInFlight, 0);
                return;
            }

            _blackBoxDumped = true;
            if (!ThreadPool.QueueUserWorkItem(BlackBoxDumpWorker, this))
            {
                _blackBoxDumped = false;
                Interlocked.Exchange(ref _blackBoxDumpInFlight, 0);
                PublishBlackBoxDumpFault(unchecked((int)0xD00D1335u));
            }
        }

        private void CacheBlackBoxDumpPathCold()
        {
            if (!string.IsNullOrEmpty(_blackBoxDumpPathCold))
                return;

            try
            {
                string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    Application.dataPath,
                    "..",
                    "Docs",
                    "AgentLogs",
                    BlackBoxDumpFileName));
                _blackBoxDumpPathCold = path;
            }
            catch (System.ArgumentException)
            {
                _blackBoxDumpPathCold = null;
            }
            catch (System.NotSupportedException)
            {
                _blackBoxDumpPathCold = null;
            }
            catch (System.IO.PathTooLongException)
            {
                _blackBoxDumpPathCold = null;
            }
        }

        private bool TryStageBlackBoxDumpSnapshot(int start, int count)
        {
            if (count <= 0 ||
                count > BlackBoxFrameCapacity ||
                !_blackBox.TryReadOnlyNativeArray(out NativeArray<VRSomaticBlackBoxEntry>.ReadOnly blackBox) ||
                blackBox.Length < BlackBoxFrameCapacity)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                _blackBoxDumpSnapshot[i] = blackBox[(start + i) % BlackBoxFrameCapacity];
            }

            Volatile.Write(ref _blackBoxDumpSnapshotCount, count);
            return true;
        }

        private static void WriteBlackBoxDumpWorker(object state)
        {
            VRSomaticProvider provider = state as VRSomaticProvider;
            if (provider == null)
                return;

            try
            {
                provider.TryWriteBlackBoxSnapshotCold();
            }
            finally
            {
                Interlocked.Exchange(ref provider._blackBoxDumpInFlight, 0);
            }
        }

        private unsafe void TryWriteBlackBoxSnapshotCold()
        {
            string path = _blackBoxDumpPathCold;
            if (string.IsNullOrEmpty(path))
                return;

            const string dumpPayloadLabel = "VRSomaticProvider.BlackBoxDumpPayload";
            NativeArray<byte> payload = default;
            try
            {
                int count = math.clamp(Volatile.Read(ref _blackBoxDumpSnapshotCount), 0, BlackBoxFrameCapacity);
                int byteCount = BlackBoxDumpHeaderSizeBytes + count * BlackBoxDumpEntrySizeBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(VRSomaticProvider),
                    dumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);

                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                Span<byte> bytes = new Span<byte>(target, byteCount);
                WriteBlackBoxHeader(bytes.Slice(0, BlackBoxDumpHeaderSizeBytes), count);

                int offset = BlackBoxDumpHeaderSizeBytes;
                for (int i = 0; i < count; i++)
                {
                    WriteBlackBoxEntry(bytes.Slice(offset, BlackBoxDumpEntrySizeBytes), in _blackBoxDumpSnapshot[i]);
                    offset += BlackBoxDumpEntrySizeBytes;
                }

                if (!NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount))
                    StageBlackBoxDumpFault(unchecked((int)0x80004005));
            }
            catch (System.ObjectDisposedException exception)
            {
                StageBlackBoxDumpFault(exception.HResult);
            }
            catch (System.IO.IOException exception)
            {
                StageBlackBoxDumpFault(exception.HResult);
            }
            catch (System.UnauthorizedAccessException exception)
            {
                StageBlackBoxDumpFault(exception.HResult);
            }
            catch (System.ArgumentException exception)
            {
                StageBlackBoxDumpFault(exception.HResult);
            }
            catch (System.NotSupportedException exception)
            {
                StageBlackBoxDumpFault(exception.HResult);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(VRSomaticProvider),
                    dumpPayloadLabel);
            }
        }

        private void StageBlackBoxDumpFault(int hResult)
        {
            Interlocked.Exchange(ref _blackBoxDumpFaultHResult, hResult);
            Interlocked.Exchange(ref _blackBoxDumpFaultPending, 1);
        }

        private void FlushPendingBlackBoxDumpFault()
        {
            if (Volatile.Read(ref _blackBoxDumpFaultPending) == 0)
                return;

            if (Interlocked.Exchange(ref _blackBoxDumpFaultPending, 0) == 0)
                return;

            PublishBlackBoxDumpFault(Interlocked.Exchange(ref _blackBoxDumpFaultHResult, 0));
        }

        private static void WriteBlackBoxHeader(Span<byte> destination, int entryCount)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), BlackBoxMagic);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), BlackBoxVersion);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(8, 4), BlackBoxFrameCapacity);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(12, 4), entryCount);
        }

        private static void WriteBlackBoxEntry(Span<byte> destination, in VRSomaticBlackBoxEntry entry)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(0, 4), entry.Frame);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), entry.StateHash);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(8, 2), entry.Flags);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(10, 2), entry.HandGhostMask);
            WriteFloatLittleEndian(destination.Slice(12, 4), entry.HeadPosition.x);
            WriteFloatLittleEndian(destination.Slice(16, 4), entry.HeadPosition.y);
            WriteFloatLittleEndian(destination.Slice(20, 4), entry.HeadPosition.z);
            WriteFloatLittleEndian(destination.Slice(24, 4), entry.HeadRotation.x);
            WriteFloatLittleEndian(destination.Slice(28, 4), entry.HeadRotation.y);
            WriteFloatLittleEndian(destination.Slice(32, 4), entry.HeadRotation.z);
            WriteFloatLittleEndian(destination.Slice(36, 4), entry.HeadRotation.w);
            WriteFloatLittleEndian(destination.Slice(40, 4), entry.NearCollision01);
            WriteFloatLittleEndian(destination.Slice(44, 4), entry.ComfortVignette01);
            WriteFloatLittleEndian(destination.Slice(48, 4), entry.LeftHandSeparationSq);
            WriteFloatLittleEndian(destination.Slice(52, 4), entry.RightHandSeparationSq);
            WriteFloatLittleEndian(destination.Slice(56, 4), entry.HeadAngularSpeedRadiansPerSecond);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(60, 4), entry.AupShiftSequence);
            WriteFloatLittleEndian(destination.Slice(64, 4), entry.KccAngularVelocityRadiansPerSecond);
            WriteFloatLittleEndian(destination.Slice(68, 4), entry.KccAngularAccelerationRadiansPerSecondSq);
            WriteFloatLittleEndian(destination.Slice(72, 4), entry.KccComfortVignette01);
            WriteFloatLittleEndian(destination.Slice(76, 4), entry.KccHorizonLock01);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(80, 4), entry.KccVelocitySequence);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(84, 4), entry.KccVelocityFrame);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(88, 4), entry.KccVelocitySourceId);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(92, 4), entry.Reserved0);
            BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(96, 8), entry.Reserved1);
            BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(104, 8), 0ul);
            BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(112, 8), 0ul);
            BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(120, 8), 0ul);
        }

        private static void WriteFloatLittleEndian(Span<byte> destination, float value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination, math.asuint(value));
        }

        private static void PublishBlackBoxDumpFault(int hResult)
        {
            GlobalTelemetryBus.PublishPerformanceWarning(BlackBoxDumpFaultHash, BlackBoxMagic, hResult);
        }

        private static float Sanitize01(float value, float fallback)
        {
            return math.isfinite(value) ? math.saturate(value) : fallback;
        }

        private static float SanitizeNonNegative(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private static float SanitizeMinimum(float value, float minimum)
        {
            return math.isfinite(value) ? math.max(minimum, value) : minimum;
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float value = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(value) ? value : 1f);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void ValidateNativeLayouts()
        {
            if (UnsafeUtility.SizeOf<VRSomaticBlackBoxEntry>() != 128 ||
                UnsafeUtility.SizeOf<VRSomaticRootSyncInput>() != 80 ||
                UnsafeUtility.SizeOf<VRSomaticRootSyncOutput>() != 32 ||
                UnsafeUtility.SizeOf<HeadCastSample>() != 48 ||
                UnsafeUtility.SizeOf<SomaticComfortStateDTO>() != 32 ||
                UnsafeUtility.SizeOf<VrComfortProfileDTO>() != 64 ||
                UnsafeUtility.SizeOf<VrComfortProfileLookupSlotDTO>() != 16 ||
                UnsafeUtility.SizeOf<SomaticKinematicHistoryDTO>() != 96 ||
                UnsafeUtility.SizeOf<SomaticDerivativeDTO>() != 64 ||
                UnsafeUtility.SizeOf<ComfortTelemetryEntry>() != 64 ||
                UnsafeUtility.SizeOf<SomaticMockSicknessSampleDTO>() != 64 ||
                OffsetOf<SomaticComfortStateDTO>(nameof(SomaticComfortStateDTO.FovTunnelingIntensity)) != 0 ||
                OffsetOf<SomaticComfortStateDTO>(nameof(SomaticComfortStateDTO.HorizonLockBlend)) != 4 ||
                OffsetOf<SomaticComfortStateDTO>(nameof(SomaticComfortStateDTO.FoveatedScaleMultiplier)) != 8 ||
                OffsetOf<SomaticComfortStateDTO>(nameof(SomaticComfortStateDTO.ActiveComfortFlags)) != 12 ||
                OffsetOf<SomaticComfortStateDTO>(nameof(SomaticComfortStateDTO.ReservedParameters)) != 16 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.Pressure01)) != 44 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.LockContentionCount)) != 48 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.StateHash)) != 52 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.Sequence)) != 56 ||
                OffsetOf<ComfortTelemetryEntry>(nameof(ComfortTelemetryEntry.AupHash)) != 60)
            {
                throw new System.InvalidOperationException("VRSomaticProvider native layout contract drift.");
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ValidateHorizonLockLayouts();
#endif
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }

        private static float SanitizeAudioCutoffHz(float value)
        {
            return math.isfinite(value) ? math.clamp(value, 200f, 22000f) : 200f;
        }

        private static bool Approximately(in Vector4 left, in Vector4 right)
        {
            return math.abs(left.x - right.x) <= ShaderPublishEpsilon &&
                   math.abs(left.y - right.y) <= ShaderPublishEpsilon &&
                   math.abs(left.z - right.z) <= ShaderPublishEpsilon &&
                   math.abs(left.w - right.w) <= ShaderPublishEpsilon;
        }

        private static bool IsFiniteFloat3(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static float3 SanitizeFiniteFloat3(float3 value)
        {
            return IsFiniteFloat3(value) ? value : float3.zero;
        }

        private static bool IsFiniteQuaternion(quaternion value)
        {
            float4 q = value.value;
            float lengthSq = math.lengthsq(q);
            return math.all(math.isfinite(q)) &&
                   math.isfinite(lengthSq) &&
                   lengthSq >= QuaternionLengthSqMinimum &&
                   lengthSq <= QuaternionLengthSqMaximum;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static void RebaseHandArray(NativeArray<float3> array, float3 shift)
        {
            if (!array.IsCreated || !math.all(math.isfinite(shift)))
                return;

            for (int i = 0; i < array.Length; i++)
                array[i] -= shift;
        }

        private void CacheSocketRotations()
        {
            _pdaSocketLocalRotation = ResolveEulerRotationNoTrig(pdaChestRotationEuler);
            _flareSocketLocalRotation = ResolveEulerRotationNoTrig(flareToolChestRotationEuler);
        }

        private static Quaternion ResolveEulerRotationNoTrig(Vector3 eulerDegrees)
        {
            if (!IsFiniteVector(eulerDegrees))
                return Quaternion.identity;

            ApproximateSinCosFullNoTrig(eulerDegrees.x * DegreesToRadians * 0.5f, out float sx, out float cx);
            ApproximateSinCosFullNoTrig(eulerDegrees.y * DegreesToRadians * 0.5f, out float sy, out float cy);
            ApproximateSinCosFullNoTrig(eulerDegrees.z * DegreesToRadians * 0.5f, out float sz, out float cz);

            float4 pitch = new float4(sx, 0f, 0f, cx);
            float4 yaw = new float4(0f, sy, 0f, cy);
            float4 roll = new float4(0f, 0f, sz, cz);
            return ToQuaternion(NormalizeQuaternionNoSqrt(MulQuaternionNoSqrt(MulQuaternionNoSqrt(yaw, pitch), roll)));
        }

        private static void ApproximateSinCosFullNoTrig(float radians, out float sin, out float cos)
        {
            float x = radians - (TwoPi * math.round(radians / TwoPi));
            float cosSign = 1f;
            if (x > HalfPi)
            {
                x = Pi - x;
                cosSign = -1f;
            }
            else if (x < -HalfPi)
            {
                x = -Pi - x;
                cosSign = -1f;
            }

            float x2 = x * x;
            sin = x * (1f - (x2 * (0.16666667f - (x2 * 0.008333333f))));
            cos = cosSign * (1f - (x2 * (0.5f - (x2 * 0.041666667f))));
        }

        private static float4 MulQuaternionNoSqrt(float4 lhs, float4 rhs)
        {
            return new float4(
                lhs.w * rhs.x + lhs.x * rhs.w + lhs.y * rhs.z - lhs.z * rhs.y,
                lhs.w * rhs.y - lhs.x * rhs.z + lhs.y * rhs.w + lhs.z * rhs.x,
                lhs.w * rhs.z + lhs.x * rhs.y - lhs.y * rhs.x + lhs.z * rhs.w,
                lhs.w * rhs.w - lhs.x * rhs.x - lhs.y * rhs.y - lhs.z * rhs.z);
        }

        private static float4 NormalizeQuaternionNoSqrt(float4 value)
        {
            float lengthSq = math.dot(value, value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return new float4(0f, 0f, 0f, 1f);

            return value * ApproximateInverseLengthNoSqrt(lengthSq);
        }

        private static float Smoothstep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - (2f * t));
        }

        private static Quaternion ToQuaternion(float4 value)
        {
            return new Quaternion(value.x, value.y, value.z, value.w);
        }

        private static float ResolveCinematicBlendApprox(float sharpness, float deltaTime)
        {
            if (!math.isfinite(deltaTime) || !math.isfinite(sharpness) || deltaTime <= 0f || sharpness <= 0f)
                return 1f;

            float x = math.min(sharpness * deltaTime, 32f);
            return math.saturate(x / (1f + x));
        }

        private static float ApproximateMagnitudeNoSqrt(Vector3 value)
        {
            float3 absValue = math.abs((float3)value);
            float largest = math.cmax(absValue);
            float smallest = math.cmin(absValue);
            float middle = absValue.x + absValue.y + absValue.z - largest - smallest;
            return largest + (middle * 0.375f) + (smallest * 0.125f);
        }

        private static float ApproximateMagnitudeNoSqrt(float3 value)
        {
            float3 absValue = math.abs(value);
            float largest = math.cmax(absValue);
            float smallest = math.cmin(absValue);
            float middle = absValue.x + absValue.y + absValue.z - largest - smallest;
            return largest + (middle * 0.375f) + (smallest * 0.125f);
        }

        private static float3 ResolveAngularVelocityRadiansPerSecond(
            Quaternion previousRotation,
            Quaternion currentRotation,
            float angularDeltaRadians,
            float invDeltaTime)
        {
            if (!math.isfinite(angularDeltaRadians) ||
                angularDeltaRadians <= 0.000001f ||
                !math.isfinite(invDeltaTime) ||
                invDeltaTime <= 0f)
            {
                return float3.zero;
            }

            float4 previous = ((quaternion)previousRotation).value;
            float4 current = ((quaternion)currentRotation).value;
            if (math.dot(previous, current) < 0f)
                current = -current;

            float4 inversePrevious = new float4(-previous.x, -previous.y, -previous.z, previous.w);
            float4 delta = MulQuaternionNoSqrt(current, inversePrevious);
            if (delta.w < 0f)
                delta = -delta;

            float3 deltaVector = new float3(delta.x, delta.y, delta.z);
            if (!IsFiniteFloat3(deltaVector))
                return float3.zero;

            float deltaVectorMagnitude = ApproximateMagnitudeNoSqrt(deltaVector);
            if (deltaVectorMagnitude <= 0.000001f)
                return float3.zero;

            return deltaVector * (angularDeltaRadians * math.rcp(deltaVectorMagnitude) * invDeltaTime);
        }

        private static float ApproximateAngularDeltaRadiansNoAcos(Quaternion previousRotation, Quaternion currentRotation)
        {
            float4 previous = ((quaternion)previousRotation).value;
            float4 current = ((quaternion)currentRotation).value;
            if (math.dot(previous, current) < 0f)
                current = -current;

            float4 absDelta = math.abs(current - previous);
            float maxA = math.max(absDelta.x, absDelta.y);
            float maxB = math.max(absDelta.z, absDelta.w);
            float minA = math.min(absDelta.x, absDelta.y);
            float minB = math.min(absDelta.z, absDelta.w);
            float largest = math.max(maxA, maxB);
            float smallest = math.min(minA, minB);
            float middleSum = absDelta.x + absDelta.y + absDelta.z + absDelta.w - largest - smallest;
            return (largest + (middleSum * 0.33333334f) + (smallest * 0.125f)) * 2f;
        }

        private static Quaternion ApproximateNlerpNoSqrt(Quaternion fromRotation, Quaternion toRotation, float blend01)
        {
            float4 from = ((quaternion)fromRotation).value;
            float4 to = ((quaternion)toRotation).value;
            if (math.dot(from, to) < 0f)
                to = -to;

            float4 blended = math.lerp(from, to, blend01);
            float inverseLengthApprox = ApproximateInverseLengthNoSqrt(math.dot(blended, blended));
            quaternion approximated = blended * inverseLengthApprox;
            return approximated;
        }

        private static Quaternion ResolveTorsoYawFromQuaternionNoTrig(Quaternion headRotation, Quaternion fallbackRotation)
        {
            float4 head = ((quaternion)headRotation).value;
            float lengthSq = (head.y * head.y) + (head.w * head.w);
            if (lengthSq <= 0.000001f || !math.isfinite(lengthSq))
                return fallbackRotation;

            float inverseLengthApprox = ApproximateInverseLengthNoSqrt(lengthSq);
            float yawY = head.y * inverseLengthApprox;
            float yawW = head.w * inverseLengthApprox;
            if (yawW < 0f)
            {
                yawY = -yawY;
                yawW = -yawW;
            }

            return new Quaternion(0f, yawY, 0f, yawW);
        }

        private static Vector3 RotateYawOffsetNoMatrix(Vector3 localOffset, Quaternion yawRotation)
        {
            float yawY = yawRotation.y;
            float yawW = yawRotation.w;
            float sinYaw = 2f * yawY * yawW;
            float cosYaw = 1f - (2f * yawY * yawY);
            return new Vector3(
                (cosYaw * localOffset.x) + (sinYaw * localOffset.z),
                localOffset.y,
                (cosYaw * localOffset.z) - (sinYaw * localOffset.x));
        }

        private static float ApproximateInverseLengthNoSqrt(float lengthSq)
        {
            return math.rcp(0.5f + (0.5f * math.max(lengthSq, 0.000001f)));
        }

        private bool HasHeadCollisionBuffers()
        {
            return _headCollisionSamples.IsCreated;
        }

        private static AbsoluteUniversePosition OffsetAupLocal(in AbsoluteUniversePosition anchorAup, Vector3 runtimeOffset)
        {
            AbsoluteUniversePosition result = anchorAup;
            result.LocalX += runtimeOffset.x;
            result.LocalY += runtimeOffset.y;
            result.LocalZ += runtimeOffset.z;
            NormalizeAupLocalAxis(ref result.GridX, ref result.LocalX);
            NormalizeAupLocalAxis(ref result.GridY, ref result.LocalY);
            NormalizeAupLocalAxis(ref result.GridZ, ref result.LocalZ);
            return result;
        }

        private static bool TryResolveXrCachedHeadAup(Vector3 runtimePosition, out AbsoluteUniversePosition headAup)
        {
            if (HectonXRRuntimeState.TryResolveCachedHeadAupFields(
                    runtimePosition,
                    out long gridX,
                    out long gridY,
                    out long gridZ,
                    out float localX,
                    out float localY,
                    out float localZ))
            {
                headAup = new AbsoluteUniversePosition
                {
                    GridX = gridX,
                    GridY = gridY,
                    GridZ = gridZ,
                    LocalX = localX,
                    LocalY = localY,
                    LocalZ = localZ
                };
                return true;
            }

            headAup = default;
            return false;
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private static void NormalizeAupLocalAxis(ref long grid, ref float local)
        {
            if (local >= 0f && local < AupCellSizeMeters)
                return;

            long gridDelta = (long)math.floor(local / AupCellSizeMeters);
            grid += gridDelta;
            local -= gridDelta * AupCellSizeMeters;
            if (local < 0f)
            {
                local += AupCellSizeMeters;
                grid--;
                return;
            }

            if (local >= AupCellSizeMeters)
            {
                local -= AupCellSizeMeters;
                grid++;
            }
        }

        private IDataVault ResolveDataVault()
        {
            return _dataVault;
        }

        private void CacheDataVaultCold()
        {
            if (_dataVault != null)
                return;

            _dataVault = GlobalRegistry.DataVault;
        }

        private void CachePlayerRuntimeContextCold()
        {
            _playerRuntimeContext = GlobalRegistry.Player;
        }

        private void EnsureBlackBoxBuffer()
        {
            if (_blackBox.IsCreated)
                return;

            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return;

            _blackBox = VaultBufferView<VRSomaticBlackBoxEntry>.Create(
                vault,
                BufferID.ShinobuVRSomaticBlackBox,
                BlackBoxFrameCapacity,
                NativeArrayOptions.ClearMemory);
        }

        private void EnsureNativeBuffers()
        {
            DispatcherJobSwap.TryFinalizeCompleted(ref _headCollisionDisposeHandle);
            if (!_headCollisionDisposeHandle.IsCompleted)
                return;

            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return;

            EnsureBlackBoxBuffer();

            if (!_headCollisionSamples.IsCreated)
            {
                _headCollisionSamples = VaultBufferView<HeadCastSample>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticHeadCollisionSamples,
                    HeadCollisionCommandCount,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_rootSyncInput.IsCreated)
            {
                _rootSyncInput = VaultBufferView<VRSomaticRootSyncInput>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticRootSyncInput,
                    1,
                    NativeArrayOptions.ClearMemory);
                _rootSyncOutput = VaultBufferView<VRSomaticRootSyncOutput>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticRootSyncOutput,
                    1,
                    NativeArrayOptions.ClearMemory);
            }

            if (!HandTargets.IsCreated)
            {
                HandTargets = VaultBufferView<float3>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticHandTargets,
                    HandCount,
                    NativeArrayOptions.ClearMemory);
                HandPhysicalPositions = VaultBufferView<float3>.Create(
                    vault,
                    BufferID.ShinobuVRSomaticHandPhysicalPositions,
                    HandCount,
                    NativeArrayOptions.ClearMemory);
            }

            EnsureSomaticComfortBuffers(vault);
        }

        private void DisposeNativeBuffers()
        {
            DispatcherJobSwap.TryFinalizeCompleted(ref _headCollisionDisposeHandle);
            _headCollisionSamples.Release();
            _rootSyncInput.Release();
            _rootSyncOutput.Release();
            HandTargets.Release();
            HandPhysicalPositions.Release();
            _blackBox.Release();
            _headCollisionSamples = default;
            _rootSyncInput = default;
            _rootSyncOutput = default;
            HandTargets = default;
            HandPhysicalPositions = default;
            _blackBox = default;
            ResetSomaticComfortBuffers();
            _dataVault = null;
            _headCollisionDisposeHandle = default;
            _blackBoxCursor = 0;
            _blackBoxLastRecordedFrame = -1;
            _stateFlags &= ~(StateHeadCollisionReady | StateHandsInitialized | StateRootInitialized | StateQueuedPresentationPoseMask);
        }

        private static VRSomaticRootSyncOutput ResolveRootSyncOutput(in VRSomaticRootSyncInput input)
        {
            quaternion headRotation = SanitizeQuaternion(input.HeadRotation, quaternion.identity);
            quaternion previousRootRotation = SanitizeQuaternion(input.PreviousRootRotation, headRotation);
            float3 worldUp = new float3(0f, 1f, 0f);
            float3 headUp = math.rotate(headRotation, worldUp);
            if (!math.all(math.isfinite(headUp)))
                headUp = worldUp;

            float3 correctionAxis = math.cross(headUp, worldUp);
            float axisLenSq = math.lengthsq(correctionAxis);
            quaternion horizonCorrection = quaternion.identity;
            float kccLock01 = math.saturate(input.KccHorizonLock01);
            if (math.isfinite(axisLenSq) && axisLenSq > 0.000001f)
            {
                float dynamicStart = math.max(0.000001f, HorizonLockStartSinSq * (1f - (0.85f * kccLock01)));
                if (axisLenSq > dynamicStart || kccLock01 > 0.001f)
                {
                    float3 axis = correctionAxis * math.rsqrt(axisLenSq);
                    float correctionRcp = math.rcp(math.max(0.000001f, 1f - dynamicStart));
                    float correction01 = math.saturate((axisLenSq - dynamicStart) * correctionRcp);
                    correction01 = math.max(correction01, kccLock01 * 0.65f);
                    float maxCorrection = HorizonLockMaxCorrectionRadians * (1f + (0.25f * kccLock01));
                    horizonCorrection = FastSmallAngleRotation(axis, maxCorrection * correction01);
                }
            }

            quaternion desiredRootRotation = SanitizeQuaternion(math.mul(horizonCorrection, headRotation), headRotation);
            float blend = ResolveRootSyncBlend(input.RootRotationSharpness, input.DeltaTime);
            quaternion rootRotation = Nlerp(previousRootRotation, desiredRootRotation, blend);
            float speedStart = math.max(0.01f, input.VignetteAngularSpeedStart);
            float speedFull = math.max(speedStart + 0.01f, input.VignetteAngularSpeedFull);
            float speedSpanRcp = math.rcp(speedFull - speedStart);
            float vignette01 = math.saturate((input.HeadAngularSpeed - speedStart) * speedSpanRcp);
            vignette01 *= math.saturate(input.VignetteMaximum);
            vignette01 = math.max(vignette01, math.saturate(input.AccelerationVignette01));

            return new VRSomaticRootSyncOutput
            {
                RootPosition = math.all(math.isfinite(input.HeadPosition)) ? input.HeadPosition : float3.zero,
                RootRotation = rootRotation,
                ComfortVignette01 = vignette01
            };
        }

        private static float3 ResolveHandPhysicalPosition(float3 target, float3 physical, float deltaTime, float springForce)
        {
            if (!math.all(math.isfinite(target)))
                target = physical;
            if (!math.all(math.isfinite(physical)))
                physical = target;

            float3 velocity = (target - physical) * math.max(0f, springForce);
            float3 next = physical + (velocity * math.max(deltaTime, MinimumDeltaTime));
            return math.all(math.isfinite(next)) ? next : target;
        }

        private static float ResolveRootSyncBlend(float sharpness, float deltaTime)
        {
            float x = math.min(math.max(sharpness, 0f) * math.max(deltaTime, MinimumDeltaTime), 32f);
            return math.saturate(x * math.rcp(1f + x));
        }

        private static quaternion Nlerp(quaternion fromRotation, quaternion toRotation, float blend01)
        {
            float4 from = fromRotation.value;
            float4 to = toRotation.value;
            if (math.dot(from, to) < 0f)
                to = -to;

            float4 blended = math.lerp(from, to, math.saturate(blend01));
            return SanitizeQuaternion(new quaternion(blended), toRotation);
        }

        private static quaternion FastSmallAngleRotation(float3 axis, float radians)
        {
            float axisLenSq = math.lengthsq(axis);
            float3 safeAxis = math.isfinite(axisLenSq) && axisLenSq > 0.000001f
                ? axis * math.rsqrt(axisLenSq)
                : new float3(0f, 1f, 0f);
            float half = math.clamp(math.select(0f, radians, math.isfinite(radians)), -0.5f, 0.5f) * 0.5f;
            float halfSq = half * half;
            quaternion result = new quaternion(new float4(safeAxis * half, 1f - (0.5f * halfSq)));
            return SanitizeQuaternion(result, quaternion.identity);
        }

        private static quaternion SanitizeQuaternion(quaternion value, quaternion fallback)
        {
            float4 q = value.value;
            float lengthSq = math.lengthsq(q);
            if (!math.all(math.isfinite(q)) || !math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return fallback;

            return new quaternion(q * math.rsqrt(lengthSq));
        }

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        private struct VRSomaticBlackBoxEntry
        {
            [FieldOffset(0)] public ulong Reserved1;
            [FieldOffset(8)] public float4 HeadRotation;
            [FieldOffset(24)] public float3 HeadPosition;
            [FieldOffset(36)] public float NearCollision01;
            [FieldOffset(40)] public float ComfortVignette01;
            [FieldOffset(44)] public float LeftHandSeparationSq;
            [FieldOffset(48)] public float RightHandSeparationSq;
            [FieldOffset(52)] public float HeadAngularSpeedRadiansPerSecond;
            [FieldOffset(56)] public float KccAngularVelocityRadiansPerSecond;
            [FieldOffset(60)] public float KccAngularAccelerationRadiansPerSecondSq;
            [FieldOffset(64)] public float KccComfortVignette01;
            [FieldOffset(68)] public float KccHorizonLock01;
            [FieldOffset(72)] public int Frame;
            [FieldOffset(76)] public uint StateHash;
            [FieldOffset(80)] public uint AupShiftSequence;
            [FieldOffset(84)] public uint KccVelocitySequence;
            [FieldOffset(88)] public uint KccVelocityFrame;
            [FieldOffset(92)] public uint KccVelocitySourceId;
            [FieldOffset(96)] public uint Reserved0;
            [FieldOffset(100)] public ushort Flags;
            [FieldOffset(102)] public ushort HandGhostMask;
            [FieldOffset(104)] private byte _pad0;
            [FieldOffset(105)] private byte _pad1;
            [FieldOffset(106)] private byte _pad2;
            [FieldOffset(107)] private byte _pad3;
            [FieldOffset(108)] private byte _pad4;
            [FieldOffset(109)] private byte _pad5;
            [FieldOffset(110)] private byte _pad6;
            [FieldOffset(111)] private byte _pad7;
            [FieldOffset(112)] private byte _pad8;
            [FieldOffset(113)] private byte _pad9;
            [FieldOffset(114)] private byte _pad10;
            [FieldOffset(115)] private byte _pad11;
            [FieldOffset(116)] private byte _pad12;
            [FieldOffset(117)] private byte _pad13;
            [FieldOffset(118)] private byte _pad14;
            [FieldOffset(119)] private byte _pad15;
            [FieldOffset(120)] private byte _pad16;
            [FieldOffset(121)] private byte _pad17;
            [FieldOffset(122)] private byte _pad18;
            [FieldOffset(123)] private byte _pad19;
            [FieldOffset(124)] private byte _pad20;
            [FieldOffset(125)] private byte _pad21;
            [FieldOffset(126)] private byte _pad22;
            [FieldOffset(127)] private byte _pad23;
        }

        [StructLayout(LayoutKind.Explicit, Size = 80)]
        private struct VRSomaticRootSyncInput
        {
            [FieldOffset(0)] public quaternion HeadRotation;
            [FieldOffset(16)] public quaternion PreviousRootRotation;
            [FieldOffset(32)] public float3 HeadPosition;
            [FieldOffset(44)] public float DeltaTime;
            [FieldOffset(48)] public float HeadAngularSpeed;
            [FieldOffset(52)] public float RootRotationSharpness;
            [FieldOffset(56)] public float VignetteAngularSpeedStart;
            [FieldOffset(60)] public float VignetteAngularSpeedFull;
            [FieldOffset(64)] public float VignetteMaximum;
            [FieldOffset(68)] public float AccelerationVignette01;
            [FieldOffset(72)] public float KccHorizonLock01;
            [FieldOffset(76)] public uint Reserved0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct VRSomaticRootSyncOutput
        {
            [FieldOffset(0)] public quaternion RootRotation;
            [FieldOffset(16)] public float3 RootPosition;
            [FieldOffset(28)] public float ComfortVignette01;
        }

        [StructLayout(LayoutKind.Explicit, Size = 48)]
        private struct HeadCastSample
        {
            [FieldOffset(0)] public float3 Point;
            [FieldOffset(12)] public float3 Normal;
            [FieldOffset(24)] public float Distance;
            [FieldOffset(28)] public float LocalSide;
            [FieldOffset(32)] public int HasHit;
            [FieldOffset(36)] public uint Reserved0;
            [FieldOffset(40)] private byte _pad0;
            [FieldOffset(41)] private byte _pad1;
            [FieldOffset(42)] private byte _pad2;
            [FieldOffset(43)] private byte _pad3;
            [FieldOffset(44)] private byte _pad4;
            [FieldOffset(45)] private byte _pad5;
            [FieldOffset(46)] private byte _pad6;
            [FieldOffset(47)] private byte _pad7;
        }
    }
}
