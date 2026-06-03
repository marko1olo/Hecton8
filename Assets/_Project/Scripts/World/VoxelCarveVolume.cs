#if UNITY_2021_3_OR_NEWER
using System;
using System.Runtime.InteropServices;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    public enum WreckageVoxelCarveInstruction : byte
    {
        None = 0,
        FlattenAndBury = 1
    }

    [Serializable]
    [StructLayout(LayoutKind.Explicit, Size = DescriptorStrideBytes)]
    public struct WreckageVoxelCarveDescriptor
    {
        public const int DescriptorStrideBytes = 56;

        [FieldOffset(0)]
        public Vector3 LocalCenter;
        [FieldOffset(12)]
        public Vector3 HalfExtents;
        [FieldOffset(24)]
        public Quaternion LocalRotation;
        [FieldOffset(40)]
        public float BurialDepthMeters;
        [FieldOffset(44)]
        public uint StableHash;
        [FieldOffset(48)]
        public byte OperationType;
        [FieldOffset(49)]
        public byte ShapeType;
        [FieldOffset(50)]
        public byte Instruction;
        [FieldOffset(51)]
        public byte Reserved0;
        [FieldOffset(52)]
        public uint Reserved1;
    }

    [DisallowMultipleComponent]
    public sealed class VoxelCarveVolume : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const float MinimumExtentMeters = 0.05f;
        private const float MaximumBurialDepthMeters = 16f;
        private const float MinimumBlendStrengthMeters = 0.05f;
        private const float MaximumBlendStrengthMeters = 4f;
        private const byte DefaultSpawnResolveFrameBudget = 8;

        [SerializeField] private Vector3 localCenter;
        [SerializeField] private Vector3 halfExtents = new Vector3(0.5f, 0.5f, 0.5f);
        [SerializeField] private Quaternion localRotation = Quaternion.identity;
        [SerializeField] private float burialDepthMeters = 1f;
        [SerializeField] private float blendStrengthMeters = 0.35f;
        [SerializeField] private byte operationType;
        [SerializeField] private byte shapeType = 1;
        [SerializeField] private WreckageVoxelCarveInstruction instruction = WreckageVoxelCarveInstruction.FlattenAndBury;
        [SerializeField] private uint stableHash;
        [SerializeField] private bool queueCarveOnEnable = true;
        [SerializeField] private byte spawnResolveFrameBudget = DefaultSpawnResolveFrameBudget;
        [SerializeField] private HectonVoxelVolume explicitVoxelVolume;
        [SerializeField] private VoxelDeltaProcessor explicitDeltaProcessor;
        private HectonVoxelVolume _cachedVoxelVolume;
        private VoxelDeltaProcessor _cachedDeltaProcessor;
        private byte _spawnCarvePending;
        private byte _spawnCarveAttemptsRemaining;
        private byte _registeredLateFrameTick;
        private byte _registeredHotSwapListener;

        public Vector3 LocalCenter => localCenter;
        public Vector3 HalfExtents => halfExtents;
        public Quaternion LocalRotation => localRotation;
        public float BurialDepthMeters => burialDepthMeters;
        public float BlendStrengthMeters => blendStrengthMeters;
        public byte OperationType => operationType;
        public byte ShapeType => shapeType;
        public WreckageVoxelCarveInstruction Instruction => instruction;
        public uint StableHash => stableHash;
        public bool QueueCarveOnEnable => queueCarveOnEnable;

        public static bool ValidateDescriptorLayout()
        {
            int byteCount = UnsafeUtility.SizeOf<WreckageVoxelCarveDescriptor>();
            return byteCount == WreckageVoxelCarveDescriptor.DescriptorStrideBytes &&
                   (byteCount & 7) == 0;
        }

        public bool TryReadDescriptor(out WreckageVoxelCarveDescriptor descriptor)
        {
            descriptor = default;
            if (!IsFinite(localCenter) ||
                !IsFinite(halfExtents) ||
                !IsFinite(localRotation) ||
                !math.isfinite(burialDepthMeters) ||
                halfExtents.x < MinimumExtentMeters ||
                halfExtents.y < MinimumExtentMeters ||
                halfExtents.z < MinimumExtentMeters)
            {
                return false;
            }

            descriptor = new WreckageVoxelCarveDescriptor
            {
                LocalCenter = localCenter,
                HalfExtents = halfExtents,
                LocalRotation = localRotation,
                BurialDepthMeters = burialDepthMeters,
                OperationType = operationType,
                ShapeType = shapeType,
                Instruction = (byte)instruction,
                Reserved0 = 0,
                StableHash = stableHash,
                Reserved1 = 0u
            };
            return stableHash != 0u && instruction != WreckageVoxelCarveInstruction.None;
        }

        public Bounds ReadLocalAabb()
        {
            Vector3 safeExtents = SanitizeExtents(halfExtents);
            return new Bounds(localCenter, safeExtents * 2f);
        }

#if UNITY_EDITOR
        public void SetEditorBakeData(
            Vector3 authoredLocalCenter,
            Vector3 authoredHalfExtents,
            Quaternion authoredLocalRotation,
            float authoredBurialDepthMeters,
            WreckageVoxelCarveInstruction authoredInstruction,
            byte authoredOperationType,
            byte authoredShapeType,
            uint authoredStableHash)
        {
            localCenter = IsFinite(authoredLocalCenter) ? authoredLocalCenter : Vector3.zero;
            halfExtents = SanitizeExtents(authoredHalfExtents);
            localRotation = IsFinite(authoredLocalRotation) ? NormalizeSafe(authoredLocalRotation) : Quaternion.identity;
            burialDepthMeters = SanitizeBurialDepth(authoredBurialDepthMeters);
            instruction = authoredInstruction;
            operationType = authoredOperationType;
            shapeType = authoredShapeType;
            stableHash = authoredStableHash;
        }
#endif

        private void OnEnable()
        {
            if (!Application.isPlaying || !queueCarveOnEnable)
                return;

            TryRegisterHotSwapListener();
            TryPrimeRuntimeBridge(transform.TransformPoint(localCenter));
            _spawnCarvePending = 1;
            _spawnCarveAttemptsRemaining = spawnResolveFrameBudget == 0 ? (byte)1 : spawnResolveFrameBudget;
            TryRegisterLateFrameTickable();
        }

        private void Start()
        {
            if (_spawnCarvePending == 0)
                return;

            TryPrimeRuntimeBridge(transform.TransformPoint(localCenter));
            if (_registeredLateFrameTick == 0)
                TryRegisterLateFrameTickable();
        }

        private void OnDisable()
        {
            _spawnCarvePending = 0;
            _spawnCarveAttemptsRemaining = 0;
            _cachedVoxelVolume = null;
            _cachedDeltaProcessor = null;
            TryUnregisterLateFrameTickable();
            TryUnregisterHotSwapListener();
        }

        public void LateFrameTick()
        {
            if (_spawnCarvePending == 0)
            {
                TryUnregisterLateFrameTickable();
                return;
            }

            if (TryQueueSpawnCarve())
            {
                _spawnCarvePending = 0;
                _spawnCarveAttemptsRemaining = 0;
                TryUnregisterLateFrameTickable();
                return;
            }

            if (_spawnCarveAttemptsRemaining > 0)
                _spawnCarveAttemptsRemaining--;

            if (_spawnCarveAttemptsRemaining == 0)
            {
                _spawnCarvePending = 0;
                TryUnregisterLateFrameTickable();
            }
        }

        private void OnValidate()
        {
            halfExtents = SanitizeExtents(halfExtents);
            localRotation = IsFinite(localRotation) ? NormalizeSafe(localRotation) : Quaternion.identity;
            burialDepthMeters = SanitizeBurialDepth(burialDepthMeters);
            blendStrengthMeters = SanitizeBlendStrength(blendStrengthMeters);
            spawnResolveFrameBudget = spawnResolveFrameBudget == 0 ? (byte)1 : spawnResolveFrameBudget;
        }

        private bool TryQueueSpawnCarve()
        {
            if (!TryReadDescriptor(out WreckageVoxelCarveDescriptor descriptor))
                return true;

            Vector3 worldCenter = transform.TransformPoint(descriptor.LocalCenter);
            if (!IsFinite(worldCenter) ||
                !TryResolveAupFromRuntimeOrigin(worldCenter, out AbsoluteUniversePosition centerAup) ||
                !TryReadCachedVoxelBridge(out VoxelDeltaProcessor deltaProcessor, out HectonVoxelVolume voxelVolume))
            {
                return false;
            }

            double3 absoluteCenter = centerAup.ToAbsoluteDouble3();
            float3 absoluteHalfExtents = ResolveWorldAabbHalfExtents(in descriptor);
            float carveDepthMeters = math.max(descriptor.BurialDepthMeters, absoluteHalfExtents.y);
            VoxelCarveEvent carveEvent = new VoxelCarveEvent
            {
                AbsoluteHitPoint = new float3((float)absoluteCenter.x, (float)absoluteCenter.y, (float)absoluteCenter.z),
                AbsoluteSegmentEnd = new float3((float)absoluteCenter.x, (float)(absoluteCenter.y - carveDepthMeters), (float)absoluteCenter.z),
                AbsoluteHalfExtents = absoluteHalfExtents,
                AbsoluteImpulseDirection = new float3(0f, -1f, 0f),
                AbsoluteHitPointDouble = absoluteCenter,
                AbsoluteSegmentEndDouble = new double3(absoluteCenter.x, absoluteCenter.y - carveDepthMeters, absoluteCenter.z),
                RadiusMeters = math.cmax(absoluteHalfExtents),
                BlendStrengthMeters = SanitizeBlendStrength(blendStrengthMeters),
                Operation = descriptor.OperationType,
                Shape = descriptor.ShapeType,
                MaterialId = 0,
                SourceFlags = descriptor.Instruction
            };

            return deltaProcessor.TryQueueCarveEvent(voxelVolume, in carveEvent);
        }

        public void SetRuntimeBridge(HectonVoxelVolume voxelVolume, VoxelDeltaProcessor deltaProcessor)
        {
            explicitVoxelVolume = voxelVolume;
            explicitDeltaProcessor = deltaProcessor;
            _cachedVoxelVolume = voxelVolume;
            _cachedDeltaProcessor = deltaProcessor;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (_spawnCarvePending == 0 || currentService == null || !isActiveAndEnabled)
                return;

            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.VoxelEngineRuntime:
                    TryPrimeRuntimeBridge(transform.TransformPoint(localCenter));
                    return;
                case GlobalRegistryServiceSlot.Dispatcher:
                    _registeredLateFrameTick = 0;
                    TryRegisterLateFrameTickable();
                    return;
            }
        }

        public bool TryPrimeRuntimeBridge(Vector3 worldCenter)
        {
            return CacheVoxelBridgeCold(worldCenter);
        }

        private bool TryReadCachedVoxelBridge(
            out VoxelDeltaProcessor deltaProcessor,
            out HectonVoxelVolume voxelVolume)
        {
            deltaProcessor = explicitDeltaProcessor;
            if (deltaProcessor == null)
                deltaProcessor = _cachedDeltaProcessor;

            voxelVolume = IsVoxelVolumeReady(explicitVoxelVolume) ? explicitVoxelVolume : _cachedVoxelVolume;
            return deltaProcessor != null && IsVoxelVolumeReady(voxelVolume);
        }

        private bool CacheVoxelBridgeCold(Vector3 worldCenter)
        {
            _cachedDeltaProcessor = explicitDeltaProcessor;
            _cachedVoxelVolume = explicitVoxelVolume;

            HectonVoxelEngine voxelEngine = GlobalRegistry.VoxelEngine;
            if (_cachedDeltaProcessor == null && voxelEngine != null)
                _cachedDeltaProcessor = voxelEngine.DeltaProcessor;

            if (IsVoxelVolumeReady(_cachedVoxelVolume))
                return _cachedDeltaProcessor != null;

            if (voxelEngine == null ||
                !IsFinite(worldCenter) ||
                !voxelEngine.TryGetNearestActiveVolume(worldCenter, out HectonVoxelVolume nearestVolume) ||
                !IsVoxelVolumeReady(nearestVolume))
            {
                return false;
            }

            _cachedVoxelVolume = nearestVolume;
            if (_cachedDeltaProcessor == null)
                _cachedDeltaProcessor = voxelEngine.DeltaProcessor;

            return _cachedDeltaProcessor != null;
        }

        private static bool IsVoxelVolumeReady(HectonVoxelVolume volume)
        {
            return volume != null && volume.HasRuntimeData && volume.BakeState == VoxelBakeState.Complete;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = AbsoluteUniversePosition.Invalid();
            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!AbsoluteUniversePosition.IsFinite(in originAup))
                return false;

            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return AbsoluteUniversePosition.IsFinite(in aup);
        }

        private float3 ResolveWorldAabbHalfExtents(in WreckageVoxelCarveDescriptor descriptor)
        {
            Vector3 safeScale = AbsVector(transform.lossyScale);
            Vector3 scaledHalfExtents = new Vector3(
                descriptor.HalfExtents.x * safeScale.x,
                descriptor.HalfExtents.y * safeScale.y,
                descriptor.HalfExtents.z * safeScale.z);
            Quaternion worldRotation = NormalizeSafe(transform.rotation * descriptor.LocalRotation);
            Vector3 axisX = worldRotation * Vector3.right;
            Vector3 axisY = worldRotation * Vector3.up;
            Vector3 axisZ = worldRotation * Vector3.forward;
            Vector3 envelope =
                AbsVector(axisX) * scaledHalfExtents.x +
                AbsVector(axisY) * scaledHalfExtents.y +
                AbsVector(axisZ) * scaledHalfExtents.z;
            return new float3(
                math.max(MinimumExtentMeters, envelope.x),
                math.max(MinimumExtentMeters, envelope.y),
                math.max(MinimumExtentMeters, envelope.z));
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrameTick != 0 || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment) ? (byte)1 : (byte)0;
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (_registeredLateFrameTick == 0)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrameTick = 0;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener != 0 || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this) ? (byte)1 : (byte)0;
        }

        private void TryUnregisterHotSwapListener()
        {
            if (_registeredHotSwapListener == 0)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = 0;
        }

        private static Vector3 SanitizeExtents(Vector3 value)
        {
            return new Vector3(
                Mathf.Max(MinimumExtentMeters, IsFinite(value.x) ? Mathf.Abs(value.x) : MinimumExtentMeters),
                Mathf.Max(MinimumExtentMeters, IsFinite(value.y) ? Mathf.Abs(value.y) : MinimumExtentMeters),
                Mathf.Max(MinimumExtentMeters, IsFinite(value.z) ? Mathf.Abs(value.z) : MinimumExtentMeters));
        }

        private static float SanitizeBurialDepth(float value)
        {
            if (!math.isfinite(value))
                return 0f;

            return Mathf.Clamp(value, 0f, MaximumBurialDepthMeters);
        }

        private static float SanitizeBlendStrength(float value)
        {
            if (!math.isfinite(value))
                return MinimumBlendStrengthMeters;

            return Mathf.Clamp(value, MinimumBlendStrengthMeters, MaximumBlendStrengthMeters);
        }

        private static Quaternion NormalizeSafe(Quaternion value)
        {
            float lengthSq =
                value.x * value.x +
                value.y * value.y +
                value.z * value.z +
                value.w * value.w;
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return Quaternion.identity;

            float inv = 1f / Mathf.Sqrt(lengthSq);
            return new Quaternion(value.x * inv, value.y * inv, value.z * inv, value.w * inv);
        }

        private static bool IsFinite(float value)
        {
            return math.isfinite(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static bool IsFinite(Quaternion value)
        {
            return math.all(math.isfinite(new float4(value.x, value.y, value.z, value.w)));
        }

        private static Vector3 AbsVector(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }
}
#endif
