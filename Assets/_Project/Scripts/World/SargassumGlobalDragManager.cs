using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.SaveSystem;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Hecton8.World
{
    /// <summary>
    /// Listener contract for queue-backed sargassum drag notifications.
    /// </summary>
    public interface ISargassumGlobalDragEventListener
    {
        /// <summary>Called when a player or vehicle strains against sargassum entanglement.</summary>
        /// <param name="signal">Entanglement strain payload.</param>
        void OnSargassumEntanglementStrain(in SargassumGlobalDragManager.EntanglementStrainSignal signal);

        /// <summary>Called when a large displacement disturbs the sargassum canopy.</summary>
        /// <param name="signal">Massive displacement payload.</param>
        void OnSargassumMassiveDisplacement(in SargassumGlobalDragManager.MassiveDisplacementSignal signal);
    }

    /// <summary>
    /// Builds a coarse world-space density field for floating sargassum and serves zero-allocation drag queries.
    /// Also bakes a density texture used by ocean damping facades and GPU micro-fauna.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-104)]
    public sealed class SargassumGlobalDragManager : MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, ISargassumMassiveDisplacementReceiver, ISargassumDragReadModel, IOriginShiftListener, ISaveable, IGlobalRegistryHotSwapListener
    {
        private static readonly int _GlobalDriftOffsetId = Shader.PropertyToID("_SargassumGlobalDriftOffset");
        private static readonly int _SinkTextureId = Shader.PropertyToID("_SargassumBuoyancySinkRT");
        private static readonly int _SinkWorldRectId = Shader.PropertyToID("_SargassumBuoyancySinkWorldRect");
        private static readonly int _MaxSinkDepthId = Shader.PropertyToID("_SargassumBuoyancySinkDepth");
        private static readonly int _ScavengerMatricesId = Shader.PropertyToID("_HectonScavengerMatrices");

        private const uint NestingHashSeed = 0x7F4A7C15u;
        private const int InitialCellCapacity = 4096;
        private const int MinDensityTextureResolution = 64;
        private const int MaxDensityTextureResolution = 256;
        private const int DefaultDensityTextureResolution = 128;
        private const int MinDynamicTextureRefreshStrideFrames = 2;
        private const int MaxDynamicTextureRefreshStrideFrames = 18;
        private const int MaxDisruptionZoneCapacity = 16;
        private const int MaxEditorValidateDepth = 2;
#if UNITY_EDITOR
        private const string CollapseChunkFallbackPrefabPath = "Assets/_Project/Prefabs/Construction/Final/PFB_SargassumCollapseChunk.prefab";
        private const string FinalPrefabQualityGateAssemblyName = "Hecton8.Editor";
        private const string FinalPrefabQualityGateTypeName = "Hecton8.EditorTools.WorldProceduralFinalPrefabQualityGate";
#endif
        private const float MinDensityThreshold = 0.025f;
        private const int ExternalSiteChunkSizeMeters = 64;
        private const float ExternalSiteQuantizationMeters = 0.5f;
        private const byte CollapseChunkBurstSpawnedFlag = 1 << 0;
        private const int EntanglementStrainEventCapacity = 16;
        private const int MassiveDisplacementEventCapacity = 16;
        private const int EventListenerCapacity = 16;
        private const uint EntanglementStrainOverflowWarningHash = 0x5347454Fu;
        private const uint MassiveDisplacementOverflowWarningHash = 0x53474D4Fu;
        private const uint ListenerRejectedWarningHash = 0x5347524Au;
        private const uint ListenerExceptionWarningHash = 0x53474558u;
        private const uint EntanglementStrainContextHash = 0x53474553u;
        private const uint MassiveDisplacementContextHash = 0x53474D53u;
        private const uint ListenerContextHash = 0x53474C53u;
        private const int DebrisPetrificationTimerCapacity = 128;
        private const int DebrisPetrificationTimerIndexMask = DebrisPetrificationTimerCapacity - 1;
        private const int DebrisPetrificationDisableBudgetPerSlowTick = 16;
        private const SystemID VaultOwnerSystemId = SystemID.WorldSargassum;
        private const BufferID ScavengerMatricesBufferId = BufferID.SargassumGlobalDragScavengerMatrices;
        private const float DebrisPetrificationDelaySeconds = 4f;
        private const float DebrisPetrificationSlowTickSeconds = 0.5f;
        private const int ScavengerBrgMetadataPlaceholderCount = 1;
        private const float ScavengerInvTwoPi = 0.15915494309189535f;
        private static readonly Quaternion _yawPositiveZ = new Quaternion(0f, 0f, 0f, 1f);
        private static readonly Quaternion _yawNegativeZ = new Quaternion(0f, 1f, 0f, 0f);
        private static readonly Quaternion _yawPositiveX = new Quaternion(0f, 0.70710678118f, 0f, 0.70710678118f);
        private static readonly Quaternion _yawNegativeX = new Quaternion(0f, -0.70710678118f, 0f, 0.70710678118f);

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        internal struct SargassumFieldSample
        {
            [FieldOffset(0)] public byte HasInfluence;
            [FieldOffset(1)] private byte _pad0;
            [FieldOffset(2)] private ushort _pad1;
            [FieldOffset(4)] public float SpeedMultiplier;
            [FieldOffset(8)] public float DragMultiplier;
            [FieldOffset(12)] public float Density01;
            [FieldOffset(16)] public Vector3 AnchorWS;
            [FieldOffset(28)] public float Entanglement01;
            [FieldOffset(32)] public float Window01;
            [FieldOffset(36)] public float Occlusion01;
            [FieldOffset(40)] private ulong _pad2;
            [FieldOffset(48)] private ulong _pad3;
            [FieldOffset(56)] private ulong _pad4;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct EntanglementStrainSignal
        {
            [FieldOffset(0)] public int SourceInstanceId;
            [FieldOffset(4)] public Vector3 PositionWS;
            [FieldOffset(16)] public Vector3 AnchorWS;
            [FieldOffset(28)] public float Tension01;
            [FieldOffset(32)] public float EscapeIntent01;
            [FieldOffset(36)] public float Shake01;
            [FieldOffset(40)] private ulong _pad0;
            [FieldOffset(48)] private ulong _pad1;
            [FieldOffset(56)] private ulong _pad2;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        public struct MassiveDisplacementSignal
        {
            [FieldOffset(0)] public Vector3 PositionWS;
            [FieldOffset(12)] public float RadiusWS;
            [FieldOffset(16)] public float Duration;
            [FieldOffset(20)] public float ExtremePanicRadiusWS;
            [FieldOffset(24)] private ulong _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct CellData
        {
            [FieldOffset(0)] public float Density;
            [FieldOffset(4)] public float MinY;
            [FieldOffset(8)] public float MaxY;
            [FieldOffset(12)] private uint _pad0;
        }

        private struct FixedDensityCellMap
        {
            private const byte Empty = 0;
            private const byte Occupied = 1;
            private const byte Tombstone = 2;

            private readonly int _maxCount;
            private readonly int _bucketMask;
            private readonly long[] _keys;
            private readonly CellData[] _values;
            private readonly byte[] _states;
            private int _count;

            public FixedDensityCellMap(int maxCount)
            {
                _maxCount = Math.Max(1, maxCount);
                int bucketCapacity = NextPowerOfTwo(_maxCount * 2);
                _bucketMask = bucketCapacity - 1;
                _keys = new long[bucketCapacity];
                _values = new CellData[bucketCapacity];
                _states = new byte[bucketCapacity];
                _count = 0;
            }

            public int Count => _count;

            public int BucketCapacity => _states != null ? _states.Length : 0;

            public void Clear()
            {
                if (_states == null)
                {
                    _count = 0;
                    return;
                }

                for (int i = 0; i < _states.Length; i++)
                    _states[i] = Empty;

                _count = 0;
            }

            public bool TryGetValue(long key, out CellData value)
            {
                int bucket = FindBucket(key);
                if (bucket >= 0)
                {
                    value = _values[bucket];
                    return true;
                }

                value = default;
                return false;
            }

            public bool TryAccumulate(long key, CellData value)
            {
                int bucket = FindInsertBucket(key, out bool existing);
                if (bucket < 0)
                    return false;

                if (existing)
                {
                    CellData current = _values[bucket];
                    current.Density += value.Density;
                    current.MinY = Mathf.Min(current.MinY, value.MinY);
                    current.MaxY = Mathf.Max(current.MaxY, value.MaxY);
                    _values[bucket] = current;
                    return true;
                }

                if (_count >= _maxCount)
                    return false;

                _keys[bucket] = key;
                _values[bucket] = value;
                _states[bucket] = Occupied;
                _count++;
                return true;
            }

            public bool TryGetOccupiedAtBucket(int bucket, out long key, out CellData value)
            {
                if (_states != null &&
                    (uint)bucket < (uint)_states.Length &&
                    _states[bucket] == Occupied)
                {
                    key = _keys[bucket];
                    value = _values[bucket];
                    return true;
                }

                key = 0L;
                value = default;
                return false;
            }

            private int FindBucket(long key)
            {
                if (_states == null || _states.Length == 0)
                    return -1;

                int start = HashKey(key) & _bucketMask;
                for (int probe = 0; probe < _states.Length; probe++)
                {
                    int bucket = (start + probe) & _bucketMask;
                    byte state = _states[bucket];
                    if (state == Empty)
                        return -1;

                    if (state == Occupied && _keys[bucket] == key)
                        return bucket;
                }

                return -1;
            }

            private int FindInsertBucket(long key, out bool existing)
            {
                existing = false;
                if (_states == null || _states.Length == 0)
                    return -1;

                int start = HashKey(key) & _bucketMask;
                int tombstone = -1;
                for (int probe = 0; probe < _states.Length; probe++)
                {
                    int bucket = (start + probe) & _bucketMask;
                    byte state = _states[bucket];
                    if (state == Occupied)
                    {
                        if (_keys[bucket] == key)
                        {
                            existing = true;
                            return bucket;
                        }
                    }
                    else if (state == Tombstone)
                    {
                        if (tombstone < 0)
                            tombstone = bucket;
                    }
                    else
                    {
                        return tombstone >= 0 ? tombstone : bucket;
                    }
                }

                return tombstone;
            }

            private static int NextPowerOfTwo(int value)
            {
                int result = 1;
                while (result < value && result < (1 << 30))
                    result <<= 1;

                return result;
            }

            private static int HashKey(long key)
            {
                unchecked
                {
                    ulong hash = (ulong)key;
                    hash ^= hash >> 33;
                    hash *= 0xff51afd7ed558ccdUL;
                    hash ^= hash >> 33;
                    hash *= 0xc4ceb9fe1a85ec53UL;
                    hash ^= hash >> 33;
                    return (int)hash;
                }
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        private struct NestedAttachmentState
        {
            [FieldOffset(0)] public Vector3 SampleSpaceAnchorWS;
            [FieldOffset(12)] public Vector3 RenderOffsetWS;
            [FieldOffset(24)] public Vector3 ReleaseVelocityWS;
            [FieldOffset(36)] public Quaternion Rotation;
            [FieldOffset(52)] public float UniformScale;
            [FieldOffset(56)] public float ReleaseLifetime;
            [FieldOffset(60)] public float ReleaseAge;
            [FieldOffset(64)] public int PrototypeIndex;
            [FieldOffset(68)] public byte ReleasedFlag;
            [FieldOffset(69)] private byte _pad0;
            [FieldOffset(70)] private ushort _pad1;
            [FieldOffset(72)] private ulong _pad2;
            [FieldOffset(80)] private ulong _pad3;
            [FieldOffset(88)] private ulong _pad4;
            [FieldOffset(96)] private ulong _pad5;
            [FieldOffset(104)] private ulong _pad6;
            [FieldOffset(112)] private ulong _pad7;
            [FieldOffset(120)] private ulong _pad8;
        }

        private enum DisruptionZoneMode : byte
        {
            CutCollapse = 1,
            MassiveDisplacement = 2
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct DisruptionZoneState
        {
            [FieldOffset(0)] public Vector3 SampleSpaceCenterWS;
            [FieldOffset(12)] public float RadiusWS;
            [FieldOffset(16)] public float Strength01;
            [FieldOffset(20)] public float SinkDepthWS;
            [FieldOffset(24)] public float RampDuration;
            [FieldOffset(28)] public float HoldDuration;
            [FieldOffset(32)] public float FadeDuration;
            [FieldOffset(36)] public float Age;
            [FieldOffset(40)] public byte Mode;
            [FieldOffset(41)] public byte Flags;
            [FieldOffset(42)] private ushort _pad0;
            [FieldOffset(44)] private uint _pad1;
            [FieldOffset(48)] private ulong _pad2;
            [FieldOffset(56)] private ulong _pad3;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct DisruptionSample
        {
            [FieldOffset(0)] public float Suppression01;
            [FieldOffset(4)] public float SinkDepthWS;
            [FieldOffset(8)] private ulong _pad0;
        }

        private struct ScavengerHostState
        {
            public SargassumCollapseChunk Chunk;
            public Vector3 AnchorWS;
            public float SettledTime;
            public float Consumed01;
            public uint Seed;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct ExternalScavengerSiteState
        {
            [FieldOffset(0)] public Vector3 AnchorWS;
            [FieldOffset(12)] public float RadiusWS;
            [FieldOffset(16)] public float RemainingTime;
            [FieldOffset(20)] public uint Seed;
            [FieldOffset(24)] private ulong _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct DebrisTimer
        {
            [FieldOffset(0)] public int Slot;
            [FieldOffset(4)] public float RemainingSeconds;
            [FieldOffset(8)] private ulong _pad0;
        }

        [Serializable]
        private struct NestedAttachmentPrototype
        {
            [Tooltip("Instanced mesh rendered for this nesting archetype.")]
            public Mesh Mesh;
            [Tooltip("Shared asset material used by the instanced render path.")]
            public Material Material;
            [Tooltip("Relative selection weight for deterministic prototype picking.")]
            public int Weight;
            [Tooltip("Uniform scale range applied to this nesting archetype.")]
            public Vector2 UniformScaleRange;
            [Tooltip("Vertical offset range applied above the sampled canopy anchor.")]
            public Vector2 VerticalOffsetRange;
            [Tooltip("Minimum local density required before this archetype may spawn.")]
            public float DensityThreshold;
            [Tooltip("Maximum canopy window openness allowed for this archetype.")]
            public float WindowThreshold;
            [Tooltip("Shadow mode for this archetype.")]
            public ShadowCastingMode ShadowCastingMode;
        }

        private static readonly ListenerSlot[] _listeners = new ListenerSlot[EventListenerCapacity]; // COLD ALLOC: ListenerSlot[16] - active sargassum drag event listeners - owner: SargassumGlobalDragManager
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[EventListenerCapacity]; // COLD ALLOC: ListenerSlot[16] - deferred listener registers during event dispatch - owner: SargassumGlobalDragManager
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[EventListenerCapacity]; // COLD ALLOC: ListenerSlot[16] - deferred listener unregisters during event dispatch - owner: SargassumGlobalDragManager
        private static int _listenerCount;
        private static readonly EntanglementStrainSignal[] _pendingEntanglementStrain = new EntanglementStrainSignal[EntanglementStrainEventCapacity]; // COLD ALLOC: EntanglementStrainSignal[16] - pending sargassum strain event lane - owner: SargassumGlobalDragManager
        private static readonly EntanglementStrainSignal[] _nextFrameEntanglementStrain = new EntanglementStrainSignal[EntanglementStrainEventCapacity]; // COLD ALLOC: EntanglementStrainSignal[16] - next-frame sargassum strain event lane - owner: SargassumGlobalDragManager
        private static readonly MassiveDisplacementSignal[] _pendingMassiveDisplacement = new MassiveDisplacementSignal[MassiveDisplacementEventCapacity]; // COLD ALLOC: MassiveDisplacementSignal[16] - pending sargassum displacement event lane - owner: SargassumGlobalDragManager
        private static readonly MassiveDisplacementSignal[] _nextFrameMassiveDisplacement = new MassiveDisplacementSignal[MassiveDisplacementEventCapacity]; // COLD ALLOC: MassiveDisplacementSignal[16] - next-frame sargassum displacement event lane - owner: SargassumGlobalDragManager
        private static int _pendingEntanglementStrainCount;
        private static int _nextFrameEntanglementStrainCount;
        private static int _pendingMassiveDisplacementCount;
        private static int _nextFrameMassiveDisplacementCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedEntanglementStrainCount;
        private static int _droppedMassiveDisplacementCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastEntanglementOverflowTelemetryFrame = -1;
        private static int _lastMassiveDisplacementOverflowTelemetryFrame = -1;
        private static int _lastListenerRejectedTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static bool _isDispatchingEvents;
        private readonly DebrisTimer[] _debrisPetrificationTimers = new DebrisTimer[DebrisPetrificationTimerCapacity]; // COLD ALLOC: DebrisTimer[128] - slow-tick debris petrification timers - owner: SargassumGlobalDragManager
        // COLD ALLOC: Rigidbody[128] - managed handles for queued debris petrification timers - owner: SargassumGlobalDragManager
        private readonly Rigidbody[] _debrisPetrificationBodies = new Rigidbody[DebrisPetrificationTimerCapacity];
        private int _debrisPetrificationTimerCount;
        private int _debrisPetrificationTimerHead;
        private int _debrisPetrificationTimerTail;
        private int _debrisPetrificationCursor;

        public static int PendingEventCount =>
            _pendingEntanglementStrainCount +
            _nextFrameEntanglementStrainCount +
            _pendingMassiveDisplacementCount +
            _nextFrameMassiveDisplacementCount;

        /// <summary>Number of entanglement-strain payloads rejected because the fixed event lane was full.</summary>
        public static int DroppedEntanglementStrainCount => _droppedEntanglementStrainCount;

        /// <summary>Number of massive-displacement payloads rejected because the fixed event lane was full.</summary>
        public static int DroppedMassiveDisplacementCount => _droppedMassiveDisplacementCount;

        /// <summary>Number of listener registrations rejected because the fixed listener lane was full.</summary>
        public static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;

        /// <summary>Number of listener callback exceptions caught during event dispatch.</summary>
        public static int ListenerExceptionCount => _listenerExceptionCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            DropQueuedEvents();
            for (int i = 0; i < _deferredRegisterCount; i++)
                _deferredRegisterListeners[i].Clear();
            for (int i = 0; i < _deferredUnregisterCount; i++)
                _deferredUnregisterListeners[i].Clear();
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedEntanglementStrainCount = 0;
            _droppedMassiveDisplacementCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastEntanglementOverflowTelemetryFrame = -1;
            _lastMassiveDisplacementOverflowTelemetryFrame = -1;
            _lastListenerRejectedTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _isDispatchingEvents = false;
            for (int i = 0; i < _listenerCount; i++)
                _listeners[i].Clear();
            _listenerCount = 0;
        }

        [Header("── Runtime Wiring ──────────────────")]
        [SerializeField]
        [Tooltip("Primary runtime source for active floating sargassum matrices coming from MapMagic residency.")]
        private HectonMapMagicVegetationBridge mapMagicVegetationBridge;

        [Header("── Spatial Hash ──────────────────")]
        [SerializeField, Min(2f)]
        [Tooltip("XZ cell size used by the coarse world-space density field.")]
        private float cellSize = 6f;

        [SerializeField, Min(0.5f)]
        [Tooltip("Base planar influence radius contributed by one sargassum instance at unit scale.")]
        private float baseInfluenceRadius = 3.2f;

        [SerializeField, Min(0.25f)]
        [Tooltip("Vertical half-extent used to detect that the player body intersects a floating sargassum layer.")]
        private float baseVerticalHalfExtent = 2.15f;

        [SerializeField, Range(0.1f, 4f)]
        [Tooltip("Density normalization applied after sampling nearby cells.")]
        private float densityNormalization = 0.42f;

        [SerializeField, Range(1f, 4f)]
        [Tooltip("How aggressively cell contributions decay with planar distance.")]
        private float distancePower = 1.4f;

        [Header("── Canopy Lighting ──────────────────")]
        [SerializeField]
        [Tooltip("Fallback patch threshold used only if the MapMagic bridge is unavailable. Runtime should stay synced from ActiveFloatingLabyrinthConfig.")]
        private float fallbackPatchThreshold = HectonVegetationConstants.FloatingPatchThreshold;

        [SerializeField]
        [Tooltip("Fallback world-flow direction used only if the MapMagic bridge is unavailable.")]
        private Vector2 canopyFlowDirection = HectonVegetationConstants.FloatingFlowDirection;

        [SerializeField, Range(0.2f, 1f)]
        [Tooltip("Fallback cross-flow anisotropy used only if the MapMagic bridge is unavailable.")]
        private float canopyFlowAnisotropy = HectonVegetationConstants.FloatingFlowAnisotropy;

        [SerializeField, Min(4f)]
        [Tooltip("Fallback primary cell size used only if the MapMagic bridge is unavailable.")]
        private float canopyPrimaryCellSize = HectonVegetationConstants.FloatingPrimaryCellSize;

        [SerializeField, Min(4f)]
        [Tooltip("Fallback secondary cell size used only if the MapMagic bridge is unavailable.")]
        private float canopySecondaryCellSize = HectonVegetationConstants.FloatingSecondaryCellSize;

        [SerializeField, Min(0.25f)]
        [Tooltip("Fallback wall width used only if the MapMagic bridge is unavailable.")]
        private float canopyWallWidth = HectonVegetationConstants.FloatingWallWidth;

        [SerializeField, Min(0f)]
        [Tooltip("Fallback warp amplitude used only if the MapMagic bridge is unavailable.")]
        private float canopyWarpMeters = HectonVegetationConstants.FloatingWarpMeters;

        [SerializeField, Min(0.0001f)]
        [Tooltip("Fallback patch noise scale used only if the MapMagic bridge is unavailable.")]
        private float canopyWarpNoiseScale = HectonVegetationConstants.FloatingPatchNoiseScale;

        [Header("── Density Texture ──────────────────")]
        [SerializeField, Range(64, 256)]
        [Tooltip("Resolution of the baked density texture consumed by ocean damping facades and GPU micro-fauna.")]
        private int densityTextureResolution = DefaultDensityTextureResolution;

        [SerializeField, Min(0f)]
        [Tooltip("Extra world-space padding added around the streamed field bounds when baking the density texture.")]
        private float densityTextureBoundsPadding = 24f;

        [Header("── Global Drift ──────────────────")]
        [SerializeField]
        [Tooltip("World-space drift offset applied by ocean systems. Physics samples subtract it to stay aligned with the visual shader drift.")]
        private Vector3 _globalDriftOffset;

        [Header("── Nesting Attachments ──────────────────")]
        [SerializeField]
        [FormerlySerializedAs("nestingPrototypes")]
        [Tooltip("Authored instanced archetypes used for rare debris or crates tangled into dense canopy walls. Runtime fallback geometry is forbidden.")]
        private NestedAttachmentPrototype[] _authoredNestingPrototypes;

        [SerializeField, Range(0f, 5f)]
        [Tooltip("Chance in percent for one dense canopy node to trap a debris nest during field rebuild. The authored default is 1%.")]
        private float denseNodeNestChancePercent = 1f;

        [SerializeField, Range(4, 128)]
        [Tooltip("Hard cap for active instanced nesting attachments in the current field.")]
        private int maxNestedAttachmentCount = 48;

        [SerializeField, Range(4, 24)]
        [Tooltip("Maximum deterministic rejection attempts per nested attachment spawn.")]
        private int maxNestedAttachmentAttempts = 12;

        [SerializeField, Range(0.1f, 2.5f)]
        [Tooltip("Sampling radius used when validating dense nesting anchors.")]
        private float nestingSampleRadius = 0.95f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum dense-node value required before a debris nest candidate is allowed to trap objects.")]
        private float nestingNodeDensityThreshold = 0.72f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Maximum canopy window openness allowed for debris nests. Lower values keep trapped objects inside the thickest labyrinth knots.")]
        private float nestingNodeWindowThreshold = 0.14f;

        [SerializeField, Range(0.1f, 4f)]
        [Tooltip("Additional cut-query radius used when deciding whether a trapped debris nest has been severed free.")]
        private float nestingCutReleaseRadius = 1.15f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum recent-cut weight required before a trapped debris nest releases and starts falling out of the canopy.")]
        private float nestingCutReleaseThreshold = 0.22f;

        [SerializeField, Range(0.2f, 8f)]
        [Tooltip("Seconds a released debris nest keeps rendering while falling away from the severed canopy knot.")]
        private float nestingReleaseLifetime = 2.8f;

        [SerializeField, Range(0f, 6f)]
        [Tooltip("Horizontal burst speed applied when a debris nest is severed out of the canopy.")]
        private float nestingReleaseHorizontalSpeed = 1.15f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Initial downward speed applied when a debris nest is severed out of the canopy.")]
        private float nestingReleaseDownwardSpeed = 0.85f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Additional downward acceleration applied while a severed debris nest is falling out of the canopy.")]
        private float nestingReleaseGravity = 0.72f;

        [Header("── Destruction & Buoyancy Collapse ──────────────────")]
        [SerializeField, Range(1, MaxDisruptionZoneCapacity)]
        [Tooltip("Hard cap for persistent collapse or leviathan disruption zones applied over the current canopy field.")]
        private int maxDisruptionZoneCount = 8;

        [SerializeField, Range(2f, 18f)]
        [Tooltip("Local query radius used when measuring whether clustered cuts have punctured too much canopy in one knot.")]
        private float buoyancyCollapseQueryRadius = 7.5f;

        [SerializeField, Min(1f)]
        [Tooltip("Weighted recent-cut area required before a dense canopy knot loses buoyancy and begins to sink.")]
        private float buoyancyCollapseAreaThreshold = 42f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Strongest overlapping cut required before a clustered puncture is allowed to trigger a buoyancy collapse zone.")]
        private float buoyancyCollapseCutStrengthThreshold = 0.38f;

        [SerializeField, Range(2f, 18f)]
        [Tooltip("World-space radius of a collapse zone registered after too many cuts puncture one dense cluster.")]
        private float buoyancyCollapseRadius = 6.5f;

        [SerializeField, Range(10f, 20f)]
        [Tooltip("Seconds required for a collapsed canopy knot to reach its full sink offset.")]
        private float buoyancyCollapseSinkDuration = 14f;

        [SerializeField, Range(1f, 24f)]
        [Tooltip("Maximum downward sink depth applied to a collapsed canopy knot before it is treated as locally drowned.")]
        private float buoyancyCollapseSinkDepth = 9.5f;

        [SerializeField]
        [Tooltip("Pooled rigidbody chunk spawned when a catastrophic cut cluster collapses a large canopy knot.")]
        private GameObject collapseChunkPrefab;

        [SerializeField, Range(1f, 4f)]
        [Tooltip("Multiplier applied to the base collapse-area threshold before a collapse escalates into pooled falling chunks.")]
        private float collapseChunkAreaThresholdMultiplier = 1.85f;

        [SerializeField, Range(1, 6)]
        [Tooltip("Maximum chunk count spawned by one catastrophic canopy collapse burst.")]
        private int collapseChunkSpawnCount = 3;

        [SerializeField, Range(0.5f, 8f)]
        [Tooltip("Planar spawn radius used when scattering pooled collapse chunks around the failing canopy knot.")]
        private float collapseChunkSpawnRadius = 2.6f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Initial lateral velocity applied to catastrophic collapse chunks.")]
        private float collapseChunkHorizontalSpeed = 1.9f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("Initial downward velocity applied to catastrophic collapse chunks.")]
        private float collapseChunkDownwardSpeed = 1.25f;

        [SerializeField, Range(0.5f, 24f)]
        [Tooltip("Seconds catastrophic collapse chunks remain active before they are returned to the object pool.")]
        private float collapseChunkLifetime = 18f;

        [SerializeField, Range(0.25f, 3f)]
        [Tooltip("Minimum uniform scale applied to pooled collapse chunks.")]
        private float collapseChunkScaleMin = 0.85f;

        [SerializeField, Range(0.5f, 4f)]
        [Tooltip("Maximum uniform scale applied to pooled collapse chunks.")]
        private float collapseChunkScaleMax = 1.65f;

        [SerializeField, Range(0.5f, 12f)]
        [Tooltip("Impact speed required before a falling collapse chunk is allowed to fracture again on collision.")]
        private float collapseCascadeImpactThreshold = 3.8f;

        [SerializeField, Range(1, 3)]
        [Tooltip("Maximum recursive fragment depth allowed when collapse chunks keep tearing on impact.")]
        private int collapseCascadeMaxDepth = 2;

        [SerializeField, Range(1, 4)]
        [Tooltip("Maximum secondary chunk count spawned by one collapse-chunk impact.")]
        private int collapseCascadeSpawnCount = 2;

        [SerializeField, Range(0.2f, 1f)]
        [Tooltip("Uniform scale multiplier applied per cascade depth so secondary fragments stay smaller than the parent chunk.")]
        private float collapseCascadeScaleMultiplier = 0.62f;

        [SerializeField, Range(0.5f, 2f)]
        [Tooltip("Velocity multiplier applied to secondary cascade fragments.")]
        private float collapseCascadeVelocityMultiplier = 0.9f;

        [SerializeField, Range(0.1f, 2f)]
        [Tooltip("Strength written into the global cut mask when a leviathan or submarine punches through the canopy.")]
        private float massiveDisplacementCutStrength = 1f;

        [SerializeField, Range(0.1f, 4f)]
        [Tooltip("Seconds required for a massive displacement tear to reach full local density suppression.")]
        private float massiveDisplacementRampDuration = 0.65f;

        [SerializeField, Range(0.1f, 8f)]
        [Tooltip("Seconds used to fade a massive displacement tear out after its requested duration expires.")]
        private float massiveDisplacementFadeDuration = 2.4f;

        [SerializeField, Range(50f, 96f)]
        [Tooltip("Minimum flee radius broadcast to GPU boids when a leviathan or submarine rips through the canopy.")]
        private float massiveDisplacementExtremePanicRadius = HectonVegetationConstants.BoidMassiveDisplacementPanicRadius;

        [Header("── Procedural Scavengers ──────────────────")]
        [SerializeField]
        [Tooltip("Authored static mesh rendered for bottom scavengers feeding on fallen collapse chunks. Must include vertex colors R sway, G bio, B AO, A wear.")]
        [FormerlySerializedAs("scavengerMesh")]
        private Mesh _authoredSargassumMesh;

        [SerializeField]
        [Tooltip("Shared authored material used by scavenger BRG rendering. Runtime material clones are forbidden.")]
        [FormerlySerializedAs("scavengerMaterial")]
        private Material _authoredSargassumMaterial;

        [SerializeField, Range(2, 24)]
        [Tooltip("Maximum number of settled collapse chunks tracked as active scavenger hosts.")]
        private int maxScavengerHostCount = 12;

        [SerializeField, Range(4, 32)]
        [Tooltip("Maximum scavenger instance count spawned around one settled collapse chunk.")]
        private int scavengersPerHost = 12;

        [SerializeField, Range(30f, 90f)]
        [Tooltip("Seconds a fallen chunk must hang on the seabed before scavengers begin feeding.")]
        private float scavengerActivationDelay = 30f;

        [SerializeField, Range(4f, 30f)]
        [Tooltip("Seconds required for a full scavenger swarm to consume one collapse chunk once feeding begins.")]
        private float scavengerConsumeDuration = 12f;

        [SerializeField, Range(0.2f, 4f)]
        [Tooltip("Horizontal feeding radius used when scattering scavengers around one fallen chunk.")]
        private float scavengerOrbitRadius = 1.1f;

        [SerializeField, Range(-1f, 1f)]
        [Tooltip("Vertical local offset applied to scavenger swarms relative to the chunk anchor.")]
        private float scavengerVerticalOffset = -0.12f;

        [SerializeField, Range(0f, 0.5f)]
        [Tooltip("Vertical bob amplitude applied to scavenger motion.")]
        private float scavengerBobAmplitude = 0.06f;

        [SerializeField, Range(0.02f, 0.4f)]
        [Tooltip("Uniform scale range minimum for one scavenger instance.")]
        private float scavengerScaleMin = 0.08f;

        [SerializeField, Range(0.04f, 0.6f)]
        [Tooltip("Uniform scale range maximum for one scavenger instance.")]
        private float scavengerScaleMax = 0.16f;

        [SerializeField, Range(1, 16)]
        [Tooltip("Hard cap for temporary corpse or carcass sites that feed the same bottom-scavenger renderer without requiring a collapse chunk host.")]
        private int maxExternalScavengerSiteCount = 6;

        [SerializeField, Range(4f, 90f)]
        [Tooltip("Default lifetime applied to externally registered corpse sites before the scavenger swarm clears them away.")]
        private float externalScavengerSiteDuration = 26f;

        [Header("── Gameplay Response ──────────────────")]
        [SerializeField, Range(0.3f, 1f)]
        [Tooltip("Maximum swim-speed multiplier when the sampled density reaches full saturation.")]
        private float minSpeedMultiplier = 0.58f;

        [SerializeField, Range(1f, 4f)]
        [Tooltip("Maximum drag multiplier when the sampled density reaches full saturation.")]
        private float maxDragMultiplier = 2.35f;

        [SerializeField, Range(0.1f, 1f)]
        [Tooltip("Directional relief applied to extra drag when movement aligns with the canopy flow axis. 0.1 means along-flow travel only keeps 10% of the extra drag, while cross-flow keeps 100%.")]
        private float alongFlowDragScale = 0.1f;

        [SerializeField, Range(0.1f, 1f)]
        [Tooltip("Minimum density required before the sticky field upgrades from drag into an entangling snare.")]
        private float entanglementMinDensity = 0.28f;

        [SerializeField, Min(0.25f)]
        [Tooltip("Player or scooter speed below which the sticky field begins to snare and pull back toward the local mass center.")]
        private float entanglementSpeedThreshold = 1.5f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Maximum normalized entanglement tension resolved by the global field sample.")]
        private float maxEntanglementStrength = 0.92f;

        [Header("── Diagnostics ──────────────────")]
        [SerializeField]
        [Tooltip("Number of active sargassum instances seen in the current source payload.")]
        private int _debugTrackedInstances;

        [SerializeField]
        [Tooltip("Number of occupied density cells after the last rebuild.")]
        private int _debugOccupiedCells;

        [SerializeField]
        [Tooltip("Approximate world-space bounds of the current density field.")]
        private Bounds _debugFieldBounds;

        [SerializeField]
        [Tooltip("World-space density rect encoded as minX, minZ, invSizeX, invSizeZ.")]
        private Vector4 _debugDensityFieldWorldRect;

        [SerializeField]
        [Tooltip("Total active instanced attachments rendered inside the current canopy field.")]
        private int _debugNestedAttachmentCount;

        [SerializeField]
        [Tooltip("Bounds used for nesting attachment draw calls.")]
        private Bounds _debugNestedAttachmentBounds;

        [SerializeField]
        [Tooltip("Count of active trapped debris nests retained in CPU state before prototype batching.")]
        private int _debugNestedStateCount;

        [SerializeField]
        [Tooltip("Active canopy disruption zone count currently influencing density, sinking, and megafauna tears.")]
        private int _debugActiveDisruptionZones;

        [SerializeField]
        [Tooltip("Maximum sink depth currently encoded into the global buoyancy sink texture.")]
        private float _debugMaxSinkDepth;

        [SerializeField]
        [Tooltip("Number of settled collapse chunks currently tracked as scavenger hosts.")]
        private int _debugScavengerHostCount;

        [SerializeField]
        [Tooltip("Total scavenger BRG instances rendered this frame.")]
        private int _debugScavengerInstanceCount;

        [SerializeField]
        [Tooltip("Bounds used for the current scavenger BRG draw call.")]
        private Bounds _debugScavengerBounds;

        // COLD ALLOC: fixed open-address density field keyed by XZ cell hash - owner: SargassumGlobalDragManager
        private FixedDensityCellMap _densityCells = new FixedDensityCellMap(InitialCellCapacity);
        // COLD ALLOC: fixed open-address origin-shift rebin scratch - owner: SargassumGlobalDragManager
        private FixedDensityCellMap _densityCellsShiftScratch = new FixedDensityCellMap(InitialCellCapacity);
        private byte[] _densityTextureRaw;
        private Texture2D _densityFieldTexture;
        private byte[] _sinkTextureRaw;
        private Texture2D _sinkFieldTexture;
        private Matrix4x4[][] _nestedMatricesByPrototype;
        private int[] _nestedMatrixCounts;
        private NestedAttachmentState[] _nestedAttachmentStates;
        private DisruptionZoneState[] _disruptionZones;
        private ScavengerHostState[] _scavengerHosts;
        private ExternalScavengerSiteState[] _externalScavengerSites;
        private Matrix4x4[] _scavengerMatrixStaging;
        private VaultGenerationHandle<Matrix4x4> _scavengerMatricesHandle;
        private GraphicsBuffer _scavengerMatrixBufferA;
        private GraphicsBuffer _scavengerMatrixBufferB;
        private GraphicsBuffer _activeScavengerMatrixBuffer;
        private BatchRendererGroup _scavengerBatchRendererGroup;
        private GraphicsBuffer _scavengerBatchHandleBuffer;
        private BatchID _scavengerBatchId;
        private BatchMeshID _scavengerBatchMeshId;
        private BatchMaterialID _scavengerBatchMaterialId;
        private GraphicsBuffer _registeredScavengerBatchBuffer;
        private Mesh _registeredScavengerMesh;
        private Material _registeredScavengerMaterial;
        private Material _scavengerBrgMaterial;
        private Material _scavengerBrgMaterialSource;
        private Material _boundScavengerMatrixMaterial;
        private GraphicsBuffer _boundScavengerMatrixBuffer;
        private int _nestedPrototypeCapacity;
        private int _activeNestedPrototypeCount;
        private int _activeNestedAttachmentStateCount;
        private int _activeDisruptionZoneCount;
        private int _activeScavengerHostCount;
        private int _activeExternalScavengerSiteCount;
        private int _scavengerInstanceCapacity;
        private int _scavengerMatrixUploadIndex;
        private NestedAttachmentPrototype[] _activeNestingPrototypes;
        private SargassumCutManager _cutManager;
        private IObjectPoolService _objectPoolService;
        private IPhysicsService _physicsService;
        private Bounds _scavengerDrawBounds;
        private Bounds _registeredScavengerDrawBounds;
        private bool _scavengerDrawBoundsRegistered;

        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrameTick;
        private bool _debrisPetrificationDrainRequested;
        private bool _collapseZoneEvaluationRequested;
        private bool _dynamicTextureRefreshRequested;
        private bool _dynamicTextureRefreshIncrementRevision;
        private bool _registeredHotSwap;
        private bool _saveRegistered;
        private bool _runtimeRoutesRetiredAfterOwnershipLoss;
        private ISaveService _saveService;
        private ISaveService _registeredSaveService;
        private int _editorValidateDepth;
        private int _cachedRenderLayer;
        private bool _hasFieldData;
        private float _inverseCellSize;
        private int _fieldRevision;
        private int _resolvedDensityTextureResolution = DefaultDensityTextureResolution;
        private int _nextDynamicTextureRefreshFrame;
        private float _cachedGlobalQualityWeight01 = 1f;
        private IDataVault _dataVault;
        private HectonMapMagicVegetationBridge.FloatingLabyrinthConfig _fallbackLabyrinthConfig;
        private Vector4 _densityFieldWorldRect;
        private bool _serviceRegistered;
        private Vector4 _publishedGlobalDriftOffset;
        private Vector4 _publishedSinkWorldRect;
        private Texture _publishedSinkTexture;
        private float _publishedMaxSinkDepth;
        private bool _shaderGlobalsPublished;
        private Vector3 _pendingGlobalDriftOffset;
        private Vector4 _pendingSinkWorldRect;
        private Texture _pendingSinkTexture;
        private float _pendingMaxSinkDepth;
        private float _pendingNestedRenderDeltaTime;
        private bool _shaderGlobalsDirty;
        private bool _densityTextureUploadDirty;
        private bool _sinkTextureUploadDirty;
        private bool _densityTextureHasUploadedContent;
        private bool _sinkTextureHasUploadedContent;
        private bool _nestedRenderRequested;
        private bool _nestedAttachmentRebuildRequested;
        private bool _visualResourceRepairRequested = true;

        private static SargassumGlobalDragManager s_activeRuntimeInstance;

        /// <summary>
        /// Active owner-published runtime instance.
        /// </summary>
        public static SargassumGlobalDragManager Instance => s_activeRuntimeInstance;

        /// <summary>
        /// Resolve-or-create the sole SargassumGlobalDragManager owner for GlobalRegistry.SargassumDrag.
        /// Script GUID fcf340598bd22a94ab47ed42a25868ee has ZERO live scene/prefab hits; without this
        /// path ISargassumDragReadModel / drag-field consumers stay permanent null.
        /// </summary>
        public static SargassumGlobalDragManager EnsureRuntimeInstance()
        {
            SargassumGlobalDragManager registered = GlobalRegistry.SargassumDrag;
            if (IsSargassumDragRuntimeUsable(registered))
                return registered;

            SargassumGlobalDragManager active = s_activeRuntimeInstance;
            if (IsSargassumDragRuntimeUsable(active))
                return active;

            if (!ReferenceEquals(registered, null))
            {
                GlobalRegistry.UnregisterSargassumDragRuntime(registered);
                if (registered != null)
                    registered._serviceRegistered = false;
            }

            if (!ReferenceEquals(active, null) && active == null)
            {
                s_activeRuntimeInstance = null;
            }
            else if (!ReferenceEquals(active, null) && !IsSargassumDragRuntimeUsable(active))
            {
                if (ReferenceEquals(s_activeRuntimeInstance, active))
                    s_activeRuntimeInstance = null;
                if (active != null)
                    active._serviceRegistered = false;
            }

            if (!Application.isPlaying)
                return null;

            // Player-build construction path: zero authored scene/prefab hits for this owner.
            GameObject runtimeRoot = new GameObject("[SargassumGlobalDragManager]"); // COLD ALLOC
            return runtimeRoot.AddComponent<SargassumGlobalDragManager>();
        }


        public int SavePriority => 45;
        public int LoadPriority => 45;

        public static void Register(ISargassumGlobalDragEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatchingEvents)
            {
                QueueDeferredRegister(listener);
                return;
            }

            RegisterImmediate(listener);
        }

        public static void Unregister(ISargassumGlobalDragEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatchingEvents)
            {
                QueueDeferredUnregister(listener);
                return;
            }

            if (!TryUnregisterImmediate(listener))
                return;

            if (_listenerCount <= 0)
                DropQueuedEvents();
        }

        /// <summary>
        /// True while the manager has at least one active sargassum density cell.
        /// </summary>
        public bool HasFieldData => _hasFieldData;

        /// <summary>
        /// Monotonic revision incremented every time the field and density texture are rebuilt.
        /// </summary>
        public int FieldRevision => _fieldRevision;

        /// <summary>
        /// Baked density texture consumed by ocean damping facades and GPU micro-fauna.
        /// </summary>
        public Texture2D DensityFieldTexture => _densityFieldTexture;

        /// <summary>
        /// Baked buoyancy sink texture consumed by the sargassum canopy shader.
        /// </summary>
        public Texture2D SinkFieldTexture => _sinkFieldTexture;

        /// <summary>
        /// World-space density rect encoded as minX, minZ, invSizeX, invSizeZ in non-drifted sampling space.
        /// </summary>
        public Vector4 DensityFieldWorldRect => _densityFieldWorldRect;

        /// <summary>
        /// Gets or sets the global drift offset applied to both the sargassum shader and drag sampling space.
        /// </summary>
        public Vector3 GlobalDriftOffset
        {
            get => _globalDriftOffset;
            set
            {
                if (_globalDriftOffset == value)
                    return;

                _globalDriftOffset = value;
                PublishShaderGlobals();
            }
        }

        /// <summary>
        /// Broadcasts an active entanglement-strain cue to interested runtime systems.
        /// </summary>
        [Obsolete("Use TryRaiseEntanglementStrain and handle bounded event-lane refusal explicitly.", true)]
        public static void RaiseEntanglementStrain(EntanglementStrainSignal signal)
        {
            TryRaiseEntanglementStrain(signal);
        }

        /// <summary>
        /// Attempts to broadcast an active entanglement-strain cue to interested runtime systems.
        /// </summary>
        /// <param name="signal">Entanglement strain payload.</param>
        /// <returns>True when the event was accepted or no listeners are registered; false when the fixed event lane is full.</returns>
        public static bool TryRaiseEntanglementStrain(EntanglementStrainSignal signal)
        {
            if (!IsFiniteEntanglementStrainSignal(in signal))
                return false;

            if (_listenerCount <= 0)
                return true;

            if (_pendingEntanglementStrainCount + _nextFrameEntanglementStrainCount >= EntanglementStrainEventCapacity)
            {
                ReportEntanglementStrainOverflow();
                return false;
            }

            bool accepted = _isDispatchingEvents
                ? TryEnqueueEntanglementStrain(_nextFrameEntanglementStrain, ref _nextFrameEntanglementStrainCount, in signal)
                : TryEnqueueEntanglementStrain(_pendingEntanglementStrain, ref _pendingEntanglementStrainCount, in signal);
            if (!accepted)
            {
                ReportEntanglementStrainOverflow();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Broadcasts a canopy-clearing massive displacement cue to interested runtime systems.
        /// </summary>
        /// <param name="signal">Displacement payload.</param>
        [Obsolete("Use TryRaiseMassiveDisplacement and handle bounded event-lane refusal explicitly.", true)]
        public static void RaiseMassiveDisplacement(MassiveDisplacementSignal signal)
        {
            TryRaiseMassiveDisplacement(signal);
        }

        /// <summary>
        /// Attempts to broadcast a canopy-clearing massive displacement cue to interested runtime systems.
        /// </summary>
        /// <param name="signal">Displacement payload.</param>
        /// <returns>True when the event was accepted or no listeners are registered; false when the fixed event lane is full.</returns>
        public static bool TryRaiseMassiveDisplacement(MassiveDisplacementSignal signal)
        {
            if (!IsFiniteMassiveDisplacementSignal(in signal))
                return false;

            if (_listenerCount <= 0)
                return true;

            if (_pendingMassiveDisplacementCount + _nextFrameMassiveDisplacementCount >= MassiveDisplacementEventCapacity)
            {
                ReportMassiveDisplacementOverflow();
                return false;
            }

            bool accepted = _isDispatchingEvents
                ? TryEnqueueMassiveDisplacement(_nextFrameMassiveDisplacement, ref _nextFrameMassiveDisplacementCount, in signal)
                : TryEnqueueMassiveDisplacement(_pendingMassiveDisplacement, ref _pendingMassiveDisplacementCount, in signal);
            if (!accepted)
            {
                ReportMassiveDisplacementOverflow();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Flushes queued sargassum signal payloads through the dispatcher event budget.
        /// </summary>
        public static void FlushPendingEvents()
        {
            bool completed = false;
            _isDispatchingEvents = true;
            try
            {
                if (_listenerCount <= 0)
                {
                    DropQueuedEvents();
                    completed = true;
                }
                else
                {
                    completed = FlushEntanglementStrain();
                    if (completed)
                        completed = FlushMassiveDisplacement();
                }
            }
            finally
            {
                _isDispatchingEvents = false;
                ApplyDeferredListenerMutations();
            }

            if (!completed || HasPendingFrontEvents())
                return;

            PromoteNextFrameEvents();
        }

        private static bool TryEnqueueEntanglementStrain(EntanglementStrainSignal[] queue, ref int count, in EntanglementStrainSignal signal)
        {
            if ((uint)count >= (uint)queue.Length)
                return false;

            queue[count] = signal;
            count++;
            return true;
        }

        private static bool TryDequeueEntanglementStrain(EntanglementStrainSignal[] queue, ref int count, out EntanglementStrainSignal signal)
        {
            if (count <= 0)
            {
                signal = default;
                return false;
            }

            signal = queue[0];
            count--;
            for (int i = 0; i < count; i++)
                queue[i] = queue[i + 1];

            queue[count] = default;
            return true;
        }

        private static bool TryEnqueueMassiveDisplacement(MassiveDisplacementSignal[] queue, ref int count, in MassiveDisplacementSignal signal)
        {
            if ((uint)count >= (uint)queue.Length)
                return false;

            queue[count] = signal;
            count++;
            return true;
        }

        private static bool TryDequeueMassiveDisplacement(MassiveDisplacementSignal[] queue, ref int count, out MassiveDisplacementSignal signal)
        {
            if (count <= 0)
            {
                signal = default;
                return false;
            }

            signal = queue[0];
            count--;
            for (int i = 0; i < count; i++)
                queue[i] = queue[i + 1];

            queue[count] = default;
            return true;
        }

        private static bool FlushEntanglementStrain()
        {
            if (_pendingEntanglementStrainCount <= 0)
                return true;

            int scanBudget = _pendingEntanglementStrainCount;
            while (scanBudget-- > 0 && _pendingEntanglementStrainCount > 0)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!TryDequeueEntanglementStrain(_pendingEntanglementStrain, ref _pendingEntanglementStrainCount, out EntanglementStrainSignal signal))
                {
                    _pendingEntanglementStrainCount = 0;
                    return true;
                }

                int count = _listenerCount;
                for (int i = count - 1; i >= 0; i--)
                {
                    ISargassumGlobalDragEventListener listener = _listeners[i].Listener;
                    if (listener == null || IsDeferredUnregisterPending(listener))
                        continue;

                    DispatchEntanglementStrainToListener(listener, in signal);
                }
            }

            return true;
        }

        private static bool FlushMassiveDisplacement()
        {
            if (_pendingMassiveDisplacementCount <= 0)
                return true;

            int scanBudget = _pendingMassiveDisplacementCount;
            while (scanBudget-- > 0 && _pendingMassiveDisplacementCount > 0)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!TryDequeueMassiveDisplacement(_pendingMassiveDisplacement, ref _pendingMassiveDisplacementCount, out MassiveDisplacementSignal signal))
                {
                    _pendingMassiveDisplacementCount = 0;
                    return true;
                }

                int count = _listenerCount;
                for (int i = count - 1; i >= 0; i--)
                {
                    ISargassumGlobalDragEventListener listener = _listeners[i].Listener;
                    if (listener == null || IsDeferredUnregisterPending(listener))
                        continue;

                    DispatchMassiveDisplacementToListener(listener, in signal);
                }
            }

            return true;
        }

        private static bool HasPendingFrontEvents()
        {
            return _pendingEntanglementStrainCount > 0
                || _pendingMassiveDisplacementCount > 0;
        }

        private static void DropQueuedEvents()
        {
            Array.Clear(_pendingEntanglementStrain, 0, _pendingEntanglementStrain.Length);
            Array.Clear(_nextFrameEntanglementStrain, 0, _nextFrameEntanglementStrain.Length);
            Array.Clear(_pendingMassiveDisplacement, 0, _pendingMassiveDisplacement.Length);
            Array.Clear(_nextFrameMassiveDisplacement, 0, _nextFrameMassiveDisplacement.Length);

            _pendingEntanglementStrainCount = 0;
            _nextFrameEntanglementStrainCount = 0;
            _pendingMassiveDisplacementCount = 0;
            _nextFrameMassiveDisplacementCount = 0;
        }

        private static void DispatchEntanglementStrainToListener(ISargassumGlobalDragEventListener listener, in EntanglementStrainSignal signal)
        {
            try
            {
                listener.OnSargassumEntanglementStrain(in signal);
            }
            catch (Exception exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
        }

        private static void DispatchMassiveDisplacementToListener(ISargassumGlobalDragEventListener listener, in MassiveDisplacementSignal signal)
        {
            try
            {
                listener.OnSargassumMassiveDisplacement(in signal);
            }
            catch (Exception exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogListenerDispatchException(Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogException(exception);
#endif
        }

        private static void QueueDeferredRegister(ISargassumGlobalDragEventListener listener)
        {
            if (ContainsImmediate(listener))
            {
                CancelDeferredUnregister(listener);
                return;
            }

            if (IsDeferredRegisterPending(listener))
                return;

            if (_deferredRegisterCount >= EventListenerCapacity)
            {
                ReportListenerRegistrationRejected();
                return;
            }

            _deferredRegisterListeners[_deferredRegisterCount].Listener = listener;
            _deferredRegisterCount++;
        }

        private static void QueueDeferredUnregister(ISargassumGlobalDragEventListener listener)
        {
            if (CancelDeferredRegister(listener))
                return;

            if (!ContainsImmediate(listener) || IsDeferredUnregisterPending(listener))
                return;

            if (_deferredUnregisterCount >= EventListenerCapacity)
            {
                ReportListenerRegistrationRejected();
                return;
            }

            _deferredUnregisterListeners[_deferredUnregisterCount].Listener = listener;
            _deferredUnregisterCount++;
        }

        private static bool CancelDeferredRegister(ISargassumGlobalDragEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (!ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    continue;

                _deferredRegisterCount--;
                _deferredRegisterListeners[i] = _deferredRegisterListeners[_deferredRegisterCount];
                _deferredRegisterListeners[_deferredRegisterCount].Clear();
                return true;
            }

            return false;
        }

        private static void CancelDeferredUnregister(ISargassumGlobalDragEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (!ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    continue;

                _deferredUnregisterCount--;
                _deferredUnregisterListeners[i] = _deferredUnregisterListeners[_deferredUnregisterCount];
                _deferredUnregisterListeners[_deferredUnregisterCount].Clear();
                return;
            }
        }

        private static bool IsDeferredRegisterPending(ISargassumGlobalDragEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(ISargassumGlobalDragEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static void ApplyDeferredListenerMutations()
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                ISargassumGlobalDragEventListener listener = _deferredUnregisterListeners[i].Listener;
                _deferredUnregisterListeners[i].Clear();
                if (listener != null)
                    TryUnregisterImmediate(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                ISargassumGlobalDragEventListener listener = _deferredRegisterListeners[i].Listener;
                _deferredRegisterListeners[i].Clear();
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;

            if (_listenerCount <= 0)
                DropQueuedEvents();
        }

        private static void RegisterImmediate(ISargassumGlobalDragEventListener listener)
        {
            if (ContainsImmediate(listener))
                return;

            if (_listenerCount >= EventListenerCapacity)
            {
                ReportListenerRegistrationRejected();
                return;
            }

            _listeners[_listenerCount].Listener = listener;
            _listenerCount++;
        }

        private static bool TryUnregisterImmediate(ISargassumGlobalDragEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                int lastIndex = _listenerCount - 1;
                _listeners[i] = _listeners[lastIndex];
                _listeners[lastIndex].Clear();
                _listenerCount = lastIndex;
                return true;
            }

            return false;
        }

        private static bool ContainsImmediate(ISargassumGlobalDragEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private struct ListenerSlot
        {
            public ISargassumGlobalDragEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private static void ReportEntanglementStrainOverflow()
        {
            _droppedEntanglementStrainCount++;
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastEntanglementOverflowTelemetryFrame == frame)
                return;

            _lastEntanglementOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                EntanglementStrainOverflowWarningHash,
                EntanglementStrainContextHash,
                Mathf.Max(1, _droppedEntanglementStrainCount));
        }

        private static void ReportMassiveDisplacementOverflow()
        {
            _droppedMassiveDisplacementCount++;
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastMassiveDisplacementOverflowTelemetryFrame == frame)
                return;

            _lastMassiveDisplacementOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                MassiveDisplacementOverflowWarningHash,
                MassiveDisplacementContextHash,
                Mathf.Max(1, _droppedMassiveDisplacementCount));
        }

        private static void ReportListenerRegistrationRejected()
        {
            _droppedListenerRegistrationCount++;
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastListenerRejectedTelemetryFrame == frame)
                return;

            _lastListenerRejectedTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                ListenerRejectedWarningHash,
                ListenerContextHash,
                Mathf.Max(1, _droppedListenerRegistrationCount));
        }

        private static void ReportListenerDispatchException()
        {
            _listenerExceptionCount++;
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastListenerExceptionTelemetryFrame == frame)
                return;

            _lastListenerExceptionTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                ListenerExceptionWarningHash,
                ListenerContextHash,
                Mathf.Max(1, _listenerExceptionCount));
        }

        private static void PromoteNextFrameEvents()
        {
            while (_nextFrameEntanglementStrainCount > 0)
            {
                if (!TryDequeueEntanglementStrain(_nextFrameEntanglementStrain, ref _nextFrameEntanglementStrainCount, out EntanglementStrainSignal signal))
                    break;

                if (!TryEnqueueEntanglementStrain(_pendingEntanglementStrain, ref _pendingEntanglementStrainCount, in signal))
                {
                    ReportEntanglementStrainOverflow();
                    break;
                }
            }

            while (_nextFrameMassiveDisplacementCount > 0)
            {
                if (!TryDequeueMassiveDisplacement(_nextFrameMassiveDisplacement, ref _nextFrameMassiveDisplacementCount, out MassiveDisplacementSignal signal))
                    break;

                if (!TryEnqueueMassiveDisplacement(_pendingMassiveDisplacement, ref _pendingMassiveDisplacementCount, in signal))
                {
                    ReportMassiveDisplacementOverflow();
                    break;
                }
            }
        }

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            TryRegisterService();
            CacheDataVaultCold();
            RefreshRenderLayerCache();
            SanitizeSettings();
            ResolveBridge();
            ResolveActiveNestingPrototypes();
            CreateDensityTexture();
            EnsureDisruptionStorage();
            EnsureScavengerStorage();
            EnsureScavengerRenderResources();
            ClearField();
            ClearDisruptionZones();
            ClearSinkTexture();
            PublishShaderGlobals();
        }

        private void OnEnable()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            RefreshRenderLayerCache();
            if (!Application.isPlaying)
            {
                SanitizeSettings();
                return;
            }

            TryRegisterService();
            if (!_serviceRegistered)
                return;

            ResolveActiveNestingPrototypes();
            RefreshColdRegistryDependencies();
            EnsureDisruptionStorage();
            EnsureScavengerStorage();
            EnsureScavengerRenderResources();
            HectonFloatingOrigin.RegisterListener(this);
            PublishShaderGlobals();
            TryRegister();
            _runtimeRoutesRetiredAfterOwnershipLoss = false;
        }

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            _cutManager = null;
            TryUnregisterService();
            TryUnregister();
            ClearField();
            ClearNestedAttachments();
            ClearDisruptionZones();
            ClearDensityTexture();
            ClearSinkTexture();
            ClearScavengerHosts();
            ReleaseDebrisPetrificationStorage();

            if (!Application.isPlaying)
            {
                ReleaseNestedAttachmentStorage();
                ClearAuthoredNestingPrototypeCache();
                ReleaseDensityTexture();
                ReleaseSinkTexture();
                ReleaseScavengerResources();
            }

            PublishShaderGlobalsImmediate(Vector3.zero, Vector4.zero, Texture2D.blackTexture, 0f);
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregisterService();
            TryUnregister();
            ReleaseNestedAttachmentStorage();
            ClearAuthoredNestingPrototypeCache();
            ReleaseDensityTexture();
            ReleaseSinkTexture();
            ReleaseScavengerResources();
            ReleaseDebrisPetrificationStorage();
            PublishShaderGlobalsImmediate(Vector3.zero, Vector4.zero, Texture2D.blackTexture, 0f);
        }

        /// <summary>
        /// Rebuilds the active sargassum density field from the current MapMagic residency payload.
        /// </summary>
        public void SlowTick()
        {
            _cachedGlobalQualityWeight01 = ResolveSargassumQualityWeight01(_cachedGlobalQualityWeight01);
            RefreshRenderLayerCache();
            ResolveBridge();
            EnsureVisualResourcesForSlowTick();
            RebuildDensityField();
            _debrisPetrificationDrainRequested = true;
            _collapseZoneEvaluationRequested = true;
            QueueDynamicTextureRefresh(incrementRevision: true);
            _nestedAttachmentRebuildRequested = true;
            QueueShaderGlobals();
        }

        public void LateFrameTick()
        {
            float nestedRenderDeltaTime = math.isfinite(_pendingNestedRenderDeltaTime)
                ? math.max(0f, _pendingNestedRenderDeltaTime)
                : 0f;

            if (!HasVisualResourcesReadyForLateFrame())
                _visualResourceRepairRequested = true;

            if (_debrisPetrificationDrainRequested)
            {
                ProcessDebrisPetrificationTimers();
                _debrisPetrificationDrainRequested = false;
            }
            if (_collapseZoneEvaluationRequested)
            {
                EvaluateBuoyancyCollapseZones();
                _collapseZoneEvaluationRequested = false;
            }
            if (_dynamicTextureRefreshRequested)
            {
                if (HasDensityTextureResourcesReady())
                {
                    if (CanRefreshDynamicTexturesThisFrame())
                    {
                        RefreshDynamicTextures(_dynamicTextureRefreshIncrementRevision);
                        _dynamicTextureRefreshRequested = false;
                        _dynamicTextureRefreshIncrementRevision = false;
                        _nextDynamicTextureRefreshFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex + ResolveDynamicTextureRefreshStrideFrames();
                    }
                }
                else
                {
                    _visualResourceRepairRequested = true;
                }
            }
            ApplyDynamicTexturesIfDirty();
            FlushShaderGlobals();
            if (_nestedAttachmentRebuildRequested && HasNestedAttachmentStorageReady())
            {
                RebuildNestedAttachments();
                _nestedAttachmentRebuildRequested = false;
            }
            else if (_nestedAttachmentRebuildRequested)
            {
                _visualResourceRepairRequested = true;
            }

            if (_nestedRenderRequested && HasVisualResourcesReadyForLateFrame())
            {
                if (HasScavengerRenderResourcesReady())
                    UpdateScavengerHosts(nestedRenderDeltaTime);
                else
                    ClearScavengerPresentationState();

                RenderNestedAttachmentsAndScavengers(nestedRenderDeltaTime);
                _nestedRenderRequested = false;
            }
            else if (_nestedRenderRequested)
            {
                _visualResourceRepairRequested = true;
            }
        }

        private void EnsureVisualResourcesForSlowTick()
        {
            if (!_visualResourceRepairRequested && HasVisualResourcesReadyForLateFrame())
                return;

            ResolveActiveNestingPrototypes();
            EnsureNestedAttachmentStorage();
            CreateDensityTexture();
            EnsureScavengerRenderResources();
            _visualResourceRepairRequested = !HasVisualResourcesReadyForLateFrame();
        }

        private bool HasVisualResourcesReadyForLateFrame()
        {
            return HasDensityTextureResourcesReady() &&
                   HasNestedAttachmentStorageReady();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!isActiveAndEnabled ||
                !IsFiniteVector3(shiftOffset) ||
                !math.isfinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.0001f)
            {
                return;
            }

            ApplyRuntimeOffsetToCachedState(-shiftOffset);
        }

        /// <summary>
        /// Draws rare nested attachments tangled into the current dense canopy field.
        /// </summary>
        /// <param name="dt">Frame delta supplied by GameTickManager.</param>
        public void Tick(float dt)
        {
            dt = math.isfinite(dt) ? math.max(0f, dt) : 0f;
            bool texturesChanged = UpdateDisruptionZones(dt);
            if (texturesChanged)
            {
                QueueDynamicTextureRefresh(incrementRevision: false);
                QueueShaderGlobals();
            }

            _pendingNestedRenderDeltaTime = dt;
            _nestedRenderRequested = true;
        }

        private void RenderNestedAttachmentsAndScavengers(float dt)
        {
            if (!_hasFieldData || _activeNestedPrototypeCount <= 0 || _nestedMatricesByPrototype == null || _nestedMatrixCounts == null || _activeNestingPrototypes == null)
            {
                DrawScavengers();
                return;
            }

            UpdateNestedAttachmentBatches(dt);

            for (int prototypeIndex = 0; prototypeIndex < _activeNestedPrototypeCount; prototypeIndex++)
            {
                if ((uint)prototypeIndex >= (uint)_activeNestingPrototypes.Length)
                    break;

                NestedAttachmentPrototype prototype = _activeNestingPrototypes[prototypeIndex];
                if (prototype.Mesh == null || prototype.Material == null)
                    continue;

                int matrixCount = _nestedMatrixCounts[prototypeIndex];
                if (matrixCount <= 0)
                    continue;

                RenderParams renderParams = new RenderParams(prototype.Material)
                {
                    worldBounds = _debugNestedAttachmentBounds,
                    shadowCastingMode = prototype.ShadowCastingMode,
                    receiveShadows = true,
                    layer = _cachedRenderLayer,
                    lightProbeUsage = LightProbeUsage.Off
                };
                UnityEngine.Graphics.RenderMeshInstanced(renderParams, prototype.Mesh, 0, _nestedMatricesByPrototype[prototypeIndex], matrixCount);
            }

            DrawScavengers();
        }

        /// <summary>
        /// Samples sticky drag at the given world position.
        /// </summary>
        /// <param name="positionWS">World-space sample position.</param>
        /// <param name="radius">Approximate body radius used for overlap with the density field.</param>
        /// <param name="speedMultiplier">Resolved speed multiplier when intersecting active sargassum density.</param>
        /// <param name="dragMultiplier">Resolved drag multiplier when intersecting active sargassum density.</param>
        /// <param name="density01">Resolved normalized density in the 0..1 range.</param>
        /// <returns>True when the sample intersects meaningful sargassum density.</returns>
        public bool SampleInfluence(
            Vector3 positionWS,
            float radius,
            out float speedMultiplier,
            out float dragMultiplier,
            out float density01)
        {
            bool hasSample = SampleInfluence(positionWS, radius, Vector3.zero, out speedMultiplier, out dragMultiplier, out density01);
            return hasSample;
        }

        public bool SampleInfluence(
            Vector3 positionWS,
            float radius,
            Vector3 movementVelocityWS,
            out float speedMultiplier,
            out float dragMultiplier,
            out float density01)
        {
            bool hasSample = SampleDetailedInfluence(positionWS, radius, movementVelocityWS, entanglementSpeedThreshold + 1f, out SargassumFieldSample sample);
            speedMultiplier = sample.SpeedMultiplier;
            dragMultiplier = sample.DragMultiplier;
            density01 = sample.Density01;
            return hasSample;
        }

        /// <summary>
        /// Returns the current baked density texture and source world rect.
        /// </summary>
        /// <param name="texture">Current density texture.</param>
        /// <param name="worldRect">Current world rect encoded as minX, minZ, invSizeX, invSizeZ.</param>
        /// <returns>True when a valid density texture exists.</returns>
        public bool TryGetDensityFieldTexture(out Texture2D texture, out Vector4 worldRect)
        {
            texture = _densityFieldTexture;
            worldRect = _densityFieldWorldRect;
            return _hasFieldData &&
                   texture != null &&
                   _densityFieldWorldRect.z > 0f &&
                   _densityFieldWorldRect.w > 0f;
        }

        /// <summary>
        /// Returns the current buoyancy sink texture and source world rect.
        /// </summary>
        /// <param name="texture">Current sink texture.</param>
        /// <param name="worldRect">Current world rect encoded as minX, minZ, invSizeX, invSizeZ.</param>
        /// <returns>True when a valid sink texture exists.</returns>
        public bool TryGetSinkFieldTexture(out Texture2D texture, out Vector4 worldRect)
        {
            texture = _sinkFieldTexture;
            worldRect = _densityFieldWorldRect;
            return _hasFieldData &&
                   texture != null &&
                   _densityFieldWorldRect.z > 0f &&
                   _densityFieldWorldRect.w > 0f;
        }

        /// <summary>
        /// Registers a leviathan-scale or submarine-scale canopy displacement.
        /// </summary>
        /// <param name="position">World-space center of the tear.</param>
        /// <param name="radius">World-space canopy tear radius.</param>
        /// <param name="duration">Lifetime of the tear cue in seconds.</param>
        public void RegisterMassiveDisplacement(Vector3 position, float radius, float duration)
        {
            if (!IsFiniteVector3(position) ||
                !math.isfinite(radius) ||
                !math.isfinite(duration))
            {
                return;
            }

            float clampedRadius = Mathf.Max(1f, radius);
            float clampedDuration = Mathf.Max(0.25f, duration);
            float extremePanicRadius = Mathf.Max(massiveDisplacementExtremePanicRadius, clampedRadius * 3f);
            if (!math.isfinite(clampedRadius) ||
                !math.isfinite(clampedDuration) ||
                !math.isfinite(extremePanicRadius))
            {
                return;
            }

            RegisterOrReinforceDisruptionZone(
                position - _globalDriftOffset,
                clampedRadius,
                1f,
                0f,
                massiveDisplacementRampDuration,
                clampedDuration,
                massiveDisplacementFadeDuration,
                DisruptionZoneMode.MassiveDisplacement);

            SargassumCutManager cutManager = _cutManager;
            if (cutManager != null)
                cutManager.RegisterExternalCut(position, clampedRadius, massiveDisplacementCutStrength, Vector3.up, 1.15f);

            TryRaiseMassiveDisplacement(new MassiveDisplacementSignal
            {
                PositionWS = position,
                RadiusWS = clampedRadius,
                Duration = clampedDuration,
                ExtremePanicRadiusWS = extremePanicRadius
            });

            QueueDynamicTextureRefresh(incrementRevision: false);
            QueueShaderGlobals();
        }

        void ISargassumMassiveDisplacementReceiver.RegisterMassiveDisplacement(Vector3 position, float radius, float duration)
        {
            RegisterMassiveDisplacement(position, radius, duration);
        }

        internal bool SampleDetailedInfluence(
            Vector3 positionWS,
            float radius,
            float currentSpeed,
            out SargassumFieldSample sample)
        {
            return SampleDetailedInfluence(positionWS, radius, Vector3.zero, currentSpeed, out sample);
        }

        internal bool SampleDetailedInfluence(
            Vector3 positionWS,
            float radius,
            Vector3 movementVelocityWS,
            float currentSpeed,
            out SargassumFieldSample sample)
        {
            sample = default;
            sample.SpeedMultiplier = 1f;
            sample.DragMultiplier = 1f;
            sample.Window01 = 1f;

            if (!IsFiniteVector3(positionWS) ||
                !math.isfinite(radius) ||
                !IsFiniteVector3(movementVelocityWS) ||
                !math.isfinite(currentSpeed))
            {
                return false;
            }

            sample.AnchorWS = positionWS;

            if (!_hasFieldData || _densityCells.Count == 0)
                return false;

            Vector3 sampledPositionWS = positionWS - _globalDriftOffset;
            float effectiveRadius = Mathf.Max(0.1f, radius);
            int minCellX = Mathf.FloorToInt((sampledPositionWS.x - effectiveRadius) * _inverseCellSize);
            int maxCellX = Mathf.FloorToInt((sampledPositionWS.x + effectiveRadius) * _inverseCellSize);
            int minCellZ = Mathf.FloorToInt((sampledPositionWS.z - effectiveRadius) * _inverseCellSize);
            int maxCellZ = Mathf.FloorToInt((sampledPositionWS.z + effectiveRadius) * _inverseCellSize);
            float accumulatedDensity = 0f;
            float weightedCenterX = 0f;
            float weightedCenterY = 0f;
            float weightedCenterZ = 0f;
            float accumulatedWeight = 0f;
            float strongestContribution = 0f;
            Vector3 strongestCenterWS = sampledPositionWS;

            for (int cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
            {
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                {
                    long key = PackCellKey(cellX, cellZ);
                    if (!_densityCells.TryGetValue(key, out CellData cell))
                        continue;

                    if (sampledPositionWS.y + effectiveRadius < cell.MinY || sampledPositionWS.y - effectiveRadius > cell.MaxY)
                        continue;

                    float cellCenterX = (cellX + 0.5f) * cellSize;
                    float cellCenterZ = (cellZ + 0.5f) * cellSize;
                    float planarDeltaX = sampledPositionWS.x - cellCenterX;
                    float planarDeltaZ = sampledPositionWS.z - cellCenterZ;
                    float planarDistanceSq = (planarDeltaX * planarDeltaX) + (planarDeltaZ * planarDeltaZ);
                    float influenceRadius = Mathf.Max(cellSize + effectiveRadius, 0.001f);
                    float influenceRadiusSq = Mathf.Max(influenceRadius * influenceRadius, 0.000001f);
                    float planarWeight = 1f - Mathf.Clamp01(planarDistanceSq / influenceRadiusSq);
                    if (planarWeight <= 0f)
                        continue;

                    float weightedContribution = cell.Density * FastDensityFalloff01(planarWeight, Mathf.Max(1f, distancePower));
                    accumulatedDensity += weightedContribution;
                    accumulatedWeight += weightedContribution;
                    weightedCenterX += cellCenterX * weightedContribution;
                    weightedCenterY += ((cell.MinY + cell.MaxY) * 0.5f) * weightedContribution;
                    weightedCenterZ += cellCenterZ * weightedContribution;

                    if (weightedContribution > strongestContribution)
                    {
                        strongestContribution = weightedContribution;
                        strongestCenterWS.x = cellCenterX;
                        strongestCenterWS.y = (cell.MinY + cell.MaxY) * 0.5f;
                        strongestCenterWS.z = cellCenterZ;
                    }
                }
            }

            DisruptionSample disruption = SampleDisruptionNoDrift(sampledPositionWS);
            sample.Density01 = Mathf.Clamp01(accumulatedDensity * densityNormalization);
            if (disruption.Suppression01 > 0f)
                sample.Density01 *= 1f - disruption.Suppression01;

            if (sample.Density01 < MinDensityThreshold)
            {
                sample.Density01 = 0f;
                return false;
            }

            Vector3 centroidWS = accumulatedWeight > 0.0001f
                ? new Vector3(
                    weightedCenterX / accumulatedWeight,
                    weightedCenterY / accumulatedWeight,
                    weightedCenterZ / accumulatedWeight)
                : strongestCenterWS;
            sample.AnchorWS = centroidWS + (strongestCenterWS - centroidWS) * 0.65f + _globalDriftOffset;
            sample.AnchorWS.y -= disruption.SinkDepthWS;
            sample.SpeedMultiplier = 1f + (minSpeedMultiplier - 1f) * sample.Density01;
            float isotropicDragMultiplier = 1f + (maxDragMultiplier - 1f) * sample.Density01;
            float directionalDragScale = ResolveDirectionalDragScale(movementVelocityWS);
            sample.DragMultiplier = 1f + (isotropicDragMultiplier - 1f) * directionalDragScale;
            sample.Window01 = EvaluateCanopyWindow01(sampledPositionWS);
            sample.Occlusion01 = Mathf.Clamp01(sample.Density01 * (1f - sample.Window01 * 0.78f));
            sample.Entanglement01 = ResolveEntanglement01(sample.Density01, currentSpeed);
            sample.HasInfluence = 1;
            return true;
        }

        private void ResolveBridge()
        {
            if (mapMagicVegetationBridge == null)
                _nestedAttachmentRebuildRequested = true;
        }

        private void ApplyRuntimeOffsetToCachedState(Vector3 runtimeOffset)
        {
            ShiftDensityCellsRuntimeOffset(runtimeOffset);
            ShiftBoundsCenter(ref _debugFieldBounds, runtimeOffset);
            ShiftBoundsCenter(ref _debugNestedAttachmentBounds, runtimeOffset);
            ShiftBoundsCenter(ref _scavengerDrawBounds, runtimeOffset);
            ShiftBoundsCenter(ref _debugScavengerBounds, runtimeOffset);
            ShiftWorldRectXY(ref _densityFieldWorldRect, runtimeOffset);
            ShiftWorldRectXY(ref _debugDensityFieldWorldRect, runtimeOffset);

            if (_disruptionZones != null)
            {
                for (int i = 0; i < _activeDisruptionZoneCount; i++)
                {
                    DisruptionZoneState zone = _disruptionZones[i];
                    zone.SampleSpaceCenterWS += runtimeOffset;
                    _disruptionZones[i] = zone;
                }
            }

            if (_nestedAttachmentStates != null)
            {
                for (int i = 0; i < _activeNestedAttachmentStateCount; i++)
                {
                    NestedAttachmentState state = _nestedAttachmentStates[i];
                    state.SampleSpaceAnchorWS += runtimeOffset;
                    _nestedAttachmentStates[i] = state;
                }
            }

            if (_scavengerHosts != null)
            {
                for (int i = 0; i < _activeScavengerHostCount; i++)
                {
                    ScavengerHostState host = _scavengerHosts[i];
                    host.AnchorWS += runtimeOffset;
                    _scavengerHosts[i] = host;
                }
            }

            if (_externalScavengerSites != null)
            {
                for (int i = 0; i < _externalScavengerSites.Length; i++)
                {
                    ExternalScavengerSiteState site = _externalScavengerSites[i];
                    site.AnchorWS += runtimeOffset;
                    _externalScavengerSites[i] = site;
                }
            }

            PublishShaderGlobals();
        }

        private void ShiftDensityCellsRuntimeOffset(Vector3 runtimeOffset)
        {
            if (_densityCells.Count <= 0)
                return;

            _densityCellsShiftScratch.Clear();
            for (int bucket = 0; bucket < _densityCells.BucketCapacity; bucket++)
            {
                if (!_densityCells.TryGetOccupiedAtBucket(bucket, out long key, out CellData cell))
                    continue;

                cell.MinY += runtimeOffset.y;
                cell.MaxY += runtimeOffset.y;

                int cellX = (int)(key >> 32);
                int cellZ = (int)key;
                float shiftedCenterX = ((cellX + 0.5f) * cellSize) + runtimeOffset.x;
                float shiftedCenterZ = ((cellZ + 0.5f) * cellSize) + runtimeOffset.z;
                int shiftedCellX = Mathf.FloorToInt(shiftedCenterX * _inverseCellSize);
                int shiftedCellZ = Mathf.FloorToInt(shiftedCenterZ * _inverseCellSize);
                long shiftedKey = PackCellKey(shiftedCellX, shiftedCellZ);

                _densityCellsShiftScratch.TryAccumulate(shiftedKey, cell);
            }

            FixedDensityCellMap shifted = _densityCellsShiftScratch;
            _densityCellsShiftScratch = _densityCells;
            _densityCells = shifted;
            _densityCellsShiftScratch.Clear();

            _debugOccupiedCells = _densityCells.Count;
            _hasFieldData = _densityCells.Count > 0;
        }

        private static void ShiftBoundsCenter(ref Bounds bounds, Vector3 runtimeOffset)
        {
            if (bounds.size.sqrMagnitude <= 0.0001f)
                return;

            bounds.center += runtimeOffset;
        }

        private static void ShiftWorldRectXY(ref Vector4 worldRect, Vector3 runtimeOffset)
        {
            if (worldRect.z <= 0f || worldRect.w <= 0f)
                return;

            worldRect.x += runtimeOffset.x;
            worldRect.y += runtimeOffset.z;
        }

        private void RebuildDensityField()
        {
            if (mapMagicVegetationBridge == null)
            {
                ClearField();
                return;
            }

            if (!mapMagicVegetationBridge.TryGetActiveSurfaceNativePayload(
                    out NativeArray<Matrix4x4> matrices,
                    out _,
                    out NativeArray<int> types,
                    out int activeCount) ||
                activeCount <= 0)
            {
                ClearField();
                return;
            }

            bool boundsInitialized = false;
            Vector3 boundsMin = default;
            Vector3 boundsMax = default;
            int trackedInstances = 0;
            double3 universeOffset = mapMagicVegetationBridge.TotalUniverseOffsetDouble;
            AbsoluteUniversePosition runtimeOriginAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!runtimeOriginAup.IsFinite())
            {
                ClearField();
                return;
            }

            ClearField();

            for (int i = 0; i < activeCount; i++)
            {
                if ((HectonVegetationInstanceType)types[i] != HectonVegetationInstanceType.Sargassum)
                    continue;

                Matrix4x4 matrix = matrices[i];
                double3 originDouble = new double3(matrix.m03, matrix.m13, matrix.m23) + universeOffset;
                AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromAbsolutePosition(originDouble);
                float3 originRuntime = AUPMath.ResolveCameraRelative(in originAup, in runtimeOriginAup);
                if (!math.isfinite(originRuntime.x) ||
                    !math.isfinite(originRuntime.y) ||
                    !math.isfinite(originRuntime.z))
                {
                    continue;
                }

                Vector3 origin = new Vector3(originRuntime.x, originRuntime.y, originRuntime.z);
                float scale = ExtractUniformScale(matrix);
                float influenceRadius = Mathf.Max(baseInfluenceRadius * scale, cellSize * 0.35f);
                float verticalHalfExtent = Mathf.Max(baseVerticalHalfExtent * scale, 0.5f);

                AccumulateDensityCells(origin, scale, cellSize, baseInfluenceRadius, baseVerticalHalfExtent, distancePower);
                EncapsulateExtents(
                    origin,
                    new Vector3(influenceRadius, verticalHalfExtent, influenceRadius),
                    ref boundsMin,
                    ref boundsMax,
                    ref boundsInitialized);

                trackedInstances++;
            }

            if (trackedInstances <= 0 || _densityCells.Count <= 0)
            {
                ClearField();
                return;
            }

            _hasFieldData = true;
            _debugTrackedInstances = trackedInstances;
            _debugOccupiedCells = _densityCells.Count;
            _debugFieldBounds = boundsInitialized ? BuildBoundsFromMinMax(boundsMin, boundsMax) : default;
        }

        private void AccumulateDensityCells(
            Vector3 origin,
            float scale,
            float cellSize,
            float baseInfluenceRadius,
            float baseVerticalHalfExtent,
            float distancePower)
        {
            float influenceRadius = Mathf.Max(baseInfluenceRadius * scale, cellSize * 0.35f);
            float verticalHalfExtent = Mathf.Max(baseVerticalHalfExtent * scale, 0.5f);
            float minY = origin.y - verticalHalfExtent;
            float maxY = origin.y + verticalHalfExtent;
            int minCellX = Mathf.FloorToInt((origin.x - influenceRadius) * _inverseCellSize);
            int maxCellX = Mathf.FloorToInt((origin.x + influenceRadius) * _inverseCellSize);
            int minCellZ = Mathf.FloorToInt((origin.z - influenceRadius) * _inverseCellSize);
            int maxCellZ = Mathf.FloorToInt((origin.z + influenceRadius) * _inverseCellSize);
            float safeDistancePower = Mathf.Max(1f, distancePower);
            float influenceRadiusSq = Mathf.Max(influenceRadius * influenceRadius, 0.000001f);

            for (int cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
            {
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                {
                    float cellCenterX = (cellX + 0.5f) * cellSize;
                    float cellCenterZ = (cellZ + 0.5f) * cellSize;
                    float deltaX = origin.x - cellCenterX;
                    float deltaZ = origin.z - cellCenterZ;
                    float planarDistanceSq = (deltaX * deltaX) + (deltaZ * deltaZ);
                    if (planarDistanceSq > influenceRadiusSq)
                        continue;

                    float normalized = 1f - Mathf.Clamp01(planarDistanceSq / influenceRadiusSq);
                    float density = FastDensityFalloff01(normalized, safeDistancePower);
                    if (density <= 0.0001f)
                        continue;

                    long key = PackCellKey(cellX, cellZ);
                    _densityCells.TryAccumulate(
                        key,
                        new CellData
                        {
                            Density = density,
                            MinY = minY,
                            MaxY = maxY
                        });
                }
            }
        }

        private void ClearField()
        {
            _densityCells.Clear();
            _hasFieldData = false;
            _debugTrackedInstances = 0;
            _debugOccupiedCells = 0;
            _debugFieldBounds = default;
            _densityFieldWorldRect = Vector4.zero;
            _debugDensityFieldWorldRect = Vector4.zero;
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            return _dataVault;
        }

        private bool EnsureScavengerMatrixCapacity(int requiredCount)
        {
            int capacity = Mathf.Max(1, requiredCount);
            if (_scavengerMatrixStaging == null || _scavengerMatrixStaging.Length < capacity)
                _scavengerMatrixStaging = new Matrix4x4[capacity];

            return EnsureVaultBuffer(
                ref _scavengerMatricesHandle,
                ScavengerMatricesBufferId,
                capacity,
                NativeArrayOptions.UninitializedMemory);
        }

        private bool EnsureVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            uint expectedBufferId = unchecked((uint)(int)bufferId);
            if (IsVaultHandleCreated(in handle) &&
                handle.BufferID == expectedBufferId &&
                !vault.IsCompactionFenceActive &&
                vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly existing) &&
                existing.IsCreated &&
                existing.Length >= requiredLength &&
                !vault.IsCompactionFenceActive)
            {
                return true;
            }

            if (IsVaultHandleCreated(in handle))
                vault.ReleaseBuffer(in handle);

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                VaultOwnerSystemId,
                options);

            return !vault.IsCompactionFenceActive &&
                   IsVaultHandleCreated(in handle) &&
                   handle.BufferID == expectedBufferId &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly resolved) &&
                   resolved.IsCreated &&
                   resolved.Length >= requiredLength &&
                   !vault.IsCompactionFenceActive;
        }

        private void ReleaseVaultBuffer<T>(ref VaultGenerationHandle<T> handle) where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault != null && IsVaultHandleCreated(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private void ClearNestedAttachments()
        {
            _activeNestedPrototypeCount = 0;
            _activeNestedAttachmentStateCount = 0;
            _debugNestedAttachmentCount = 0;
            _debugNestedAttachmentBounds = default;
            _debugNestedStateCount = 0;

            if (_nestedMatrixCounts == null)
                return;

            for (int i = 0; i < _nestedMatrixCounts.Length; i++)
                _nestedMatrixCounts[i] = 0;
        }

        private void EnsureDisruptionStorage()
        {
            int requiredCapacity = Mathf.Clamp(maxDisruptionZoneCount, 1, MaxDisruptionZoneCapacity);
            if (_disruptionZones != null && _disruptionZones.Length == requiredCapacity)
                return;

            // COLD ALLOC: DisruptionZoneState[capacity] - persistent canopy destruction zones - owner: SargassumGlobalDragManager
            _disruptionZones = new DisruptionZoneState[requiredCapacity];
            _activeDisruptionZoneCount = 0;
            _debugActiveDisruptionZones = 0;
        }

        private void ClearDisruptionZones()
        {
            _activeDisruptionZoneCount = 0;
            _debugActiveDisruptionZones = 0;
            _debugMaxSinkDepth = 0f;

            if (_disruptionZones == null)
                return;

            Array.Clear(_disruptionZones, 0, _disruptionZones.Length);
        }

        private bool UpdateDisruptionZones(float dt)
        {
            if (_disruptionZones == null || _activeDisruptionZoneCount <= 0)
                return false;

            bool changed = false;
            float deltaTime = math.isfinite(dt) ? math.max(0f, dt) : 0f;
            int index = 0;
            while (index < _activeDisruptionZoneCount)
            {
                DisruptionZoneState zone = _disruptionZones[index];
                if (!IsFiniteDisruptionZone(in zone))
                {
                    int lastIndex = _activeDisruptionZoneCount - 1;
                    _disruptionZones[index] = _disruptionZones[lastIndex];
                    _disruptionZones[lastIndex] = default;
                    _activeDisruptionZoneCount = lastIndex;
                    changed = true;
                    continue;
                }

                float previousStrength01 = EvaluateDisruptionZone01(zone);
                zone.Age += deltaTime;

                float lifeTime = zone.Mode == (byte)DisruptionZoneMode.CutCollapse
                    ? float.MaxValue
                    : zone.HoldDuration + zone.FadeDuration;

                if (zone.Age >= lifeTime)
                {
                    int lastIndex = _activeDisruptionZoneCount - 1;
                    _disruptionZones[index] = _disruptionZones[lastIndex];
                    _disruptionZones[lastIndex] = default;
                    _activeDisruptionZoneCount = lastIndex;
                    changed = true;
                    continue;
                }

                float currentStrength01 = EvaluateDisruptionZone01(zone);
                if (Mathf.Abs(currentStrength01 - previousStrength01) > 0.0005f)
                    changed = true;

                _disruptionZones[index] = zone;
                index++;
            }

            _debugActiveDisruptionZones = _activeDisruptionZoneCount;
            return changed;
        }

        private void EvaluateBuoyancyCollapseZones()
        {
            SargassumCutManager cutManager = _cutManager;
            if (cutManager == null || _densityCells.Count == 0)
                return;

            for (int bucket = 0; bucket < _densityCells.BucketCapacity; bucket++)
            {
                if (!_densityCells.TryGetOccupiedAtBucket(bucket, out long cellKey, out CellData cell))
                    continue;

                float normalizedDensity = Mathf.Clamp01(cell.Density * densityNormalization);
                if (normalizedDensity < nestingNodeDensityThreshold)
                    continue;

                int cellX = (int)(cellKey >> 32);
                int cellZ = (int)cellKey;
                Vector3 cellCenterWS = new Vector3(
                    (cellX + 0.5f) * cellSize + _globalDriftOffset.x,
                    (cell.MinY + cell.MaxY) * 0.5f + _globalDriftOffset.y,
                    (cellZ + 0.5f) * cellSize + _globalDriftOffset.z);

                if (!cutManager.SampleRecentCutArea(cellCenterWS, buoyancyCollapseQueryRadius, out float accumulatedCutAreaWS, out float strongestCut01))
                    continue;

                if (accumulatedCutAreaWS < buoyancyCollapseAreaThreshold || strongestCut01 < buoyancyCollapseCutStrengthThreshold)
                    continue;

                float collapseStrength = Mathf.Clamp01(accumulatedCutAreaWS / Mathf.Max(buoyancyCollapseAreaThreshold, 0.001f));
                float sinkDepth = LerpClamped(buoyancyCollapseSinkDepth * 0.45f, buoyancyCollapseSinkDepth, collapseStrength);
                int zoneIndex = RegisterOrReinforceDisruptionZone(
                    cellCenterWS - _globalDriftOffset,
                    buoyancyCollapseRadius,
                    collapseStrength,
                    sinkDepth,
                    buoyancyCollapseSinkDuration,
                    0f,
                    0f,
                    DisruptionZoneMode.CutCollapse);
                TrySpawnCollapseChunks(zoneIndex, cellCenterWS, collapseStrength, accumulatedCutAreaWS);
            }
        }

        private int RegisterOrReinforceDisruptionZone(
            Vector3 sampleSpaceCenterWS,
            float radiusWS,
            float strength01,
            float sinkDepthWS,
            float rampDuration,
            float holdDuration,
            float fadeDuration,
            DisruptionZoneMode mode)
        {
            if (!IsFiniteVector3(sampleSpaceCenterWS) ||
                !math.isfinite(radiusWS) ||
                !math.isfinite(strength01) ||
                !math.isfinite(sinkDepthWS) ||
                !math.isfinite(rampDuration) ||
                !math.isfinite(holdDuration) ||
                !math.isfinite(fadeDuration))
            {
                return -1;
            }

            EnsureDisruptionStorage();

            float clampedRadius = Mathf.Max(cellSize * 0.5f, radiusWS);
            float clampedStrength = Mathf.Clamp01(strength01);
            float clampedRamp = Mathf.Max(0.01f, rampDuration);
            float clampedHold = Mathf.Max(0f, holdDuration);
            float clampedFade = Mathf.Max(0f, fadeDuration);
            if (!math.isfinite(clampedRadius) ||
                !math.isfinite(clampedStrength) ||
                !math.isfinite(clampedRamp) ||
                !math.isfinite(clampedHold) ||
                !math.isfinite(clampedFade))
            {
                return -1;
            }

            int zoneIndex = FindMatchingDisruptionZoneIndex(sampleSpaceCenterWS, clampedRadius, mode);

            if (zoneIndex < 0)
            {
                if (_activeDisruptionZoneCount >= (_disruptionZones != null ? _disruptionZones.Length : 0))
                    return -1;

                zoneIndex = _activeDisruptionZoneCount++;
            }

            DisruptionZoneState zone = _disruptionZones[zoneIndex];
            bool isExistingZone = zone.Mode != 0;
            if (zone.Mode == 0)
                zone.SampleSpaceCenterWS = sampleSpaceCenterWS;
            else
                zone.SampleSpaceCenterWS = LerpVector3Clamped(zone.SampleSpaceCenterWS, sampleSpaceCenterWS, 0.35f);

            zone.RadiusWS = Mathf.Max(zone.RadiusWS, clampedRadius);
            zone.Strength01 = Mathf.Max(zone.Strength01, clampedStrength);
            zone.SinkDepthWS = Mathf.Max(zone.SinkDepthWS, Mathf.Max(0f, sinkDepthWS));
            zone.RampDuration = Mathf.Min(zone.RampDuration > 0f ? zone.RampDuration : clampedRamp, clampedRamp);
            zone.HoldDuration = Mathf.Max(zone.HoldDuration, clampedHold);
            zone.FadeDuration = Mathf.Max(zone.FadeDuration, clampedFade);
            zone.Mode = (byte)mode;
            if (!isExistingZone || mode == DisruptionZoneMode.MassiveDisplacement)
                zone.Age = 0f;
            _disruptionZones[zoneIndex] = zone;
            _debugActiveDisruptionZones = _activeDisruptionZoneCount;
            return zoneIndex;
        }

        private int FindMatchingDisruptionZoneIndex(Vector3 sampleSpaceCenterWS, float radiusWS, DisruptionZoneMode mode)
        {
            if (_disruptionZones == null)
                return -1;

            float mergeDistance = Mathf.Max(cellSize, radiusWS * 0.85f);
            float mergeDistanceSq = mergeDistance * mergeDistance;
            for (int i = 0; i < _activeDisruptionZoneCount; i++)
            {
                if (_disruptionZones[i].Mode != (byte)mode)
                    continue;

                Vector3 delta = _disruptionZones[i].SampleSpaceCenterWS - sampleSpaceCenterWS;
                delta.y = 0f;
                if (delta.sqrMagnitude <= mergeDistanceSq)
                    return i;
            }

            return -1;
        }

        private static float EvaluateDisruptionZone01(DisruptionZoneState zone)
        {
            float ramp = Mathf.Clamp01(zone.Age / Mathf.Max(zone.RampDuration, 0.01f));
            if (zone.Mode == (byte)DisruptionZoneMode.CutCollapse)
                return zone.Strength01 * ramp;

            float fade = zone.Age <= zone.HoldDuration
                ? 1f
                : 1f - Mathf.Clamp01((zone.Age - zone.HoldDuration) / Mathf.Max(zone.FadeDuration, 0.01f));
            return zone.Strength01 * ramp * fade;
        }

        private void TrySpawnCollapseChunks(int zoneIndex, Vector3 zoneCenterWS, float collapseStrength, float accumulatedCutAreaWS)
        {
            if (zoneIndex < 0 ||
                collapseChunkPrefab == null ||
                _disruptionZones == null ||
                zoneIndex >= _activeDisruptionZoneCount ||
                _activeDisruptionZoneCount <= 0)
            {
                return;
            }

            float catastrophicAreaThreshold = buoyancyCollapseAreaThreshold * Mathf.Max(1f, collapseChunkAreaThresholdMultiplier);
            if (accumulatedCutAreaWS < catastrophicAreaThreshold)
                return;

            if (!TryResolveCachedObjectPool(out IObjectPoolService poolManager))
                return;

            DisruptionZoneState zone = _disruptionZones[zoneIndex];
            if ((zone.Flags & CollapseChunkBurstSpawnedFlag) != 0)
                return;

            int spawnCount = Mathf.Clamp(
                Mathf.RoundToInt(LerpClamped(1f, collapseChunkSpawnCount, Mathf.Clamp01(collapseStrength))),
                1,
                collapseChunkSpawnCount);

            for (int chunkIndex = 0; chunkIndex < spawnCount; chunkIndex++)
            {
                float radialT = HashToFloat01((uint)zoneIndex, (uint)chunkIndex, 0x13198A2Eu);
                float radius = collapseChunkSpawnRadius * radialT;
                Vector3 lateralDirection = ResolveHashPlanarDirection((uint)zoneIndex, (uint)chunkIndex, 0x85A308D3u, 0xA4093822u, Vector3.right);
                Vector3 spawnOffset = lateralDirection * radius;
                Vector3 spawnPosition = zoneCenterWS + spawnOffset;
                spawnPosition.y += LerpClamped(0.25f, 1.1f, HashToFloat01((uint)zoneIndex, (uint)chunkIndex, 0x03707344u));

                Vector3 linearVelocity = new Vector3(
                    lateralDirection.x * collapseChunkHorizontalSpeed,
                    -collapseChunkDownwardSpeed,
                    lateralDirection.z * collapseChunkHorizontalSpeed);
                Vector3 angularVelocity = new Vector3(
                    LerpClamped(-2.8f, 2.8f, HashToFloat01((uint)zoneIndex, (uint)chunkIndex, 0xA4093822u)),
                    LerpClamped(-1.9f, 1.9f, HashToFloat01((uint)zoneIndex, (uint)chunkIndex, 0x299F31D0u)),
                    LerpClamped(-2.4f, 2.4f, HashToFloat01((uint)zoneIndex, (uint)chunkIndex, 0x082EFA98u)));
                float uniformScale = LerpClamped(
                    collapseChunkScaleMin,
                    collapseChunkScaleMax,
                    HashToFloat01((uint)zoneIndex, (uint)chunkIndex, 0xEC4E6C89u));

                GameObject chunkInstance = poolManager.Spawn(
                    collapseChunkPrefab,
                    spawnPosition,
                    Quaternion.Euler(
                        LerpClamped(-18f, 18f, HashToFloat01((uint)zoneIndex, (uint)chunkIndex, 0x452821E6u)),
                        LerpClamped(0f, 360f, HashToFloat01((uint)zoneIndex, (uint)chunkIndex, 0x38D01377u)),
                        LerpClamped(-18f, 18f, HashToFloat01((uint)zoneIndex, (uint)chunkIndex, 0xBE5466CFu))));
                if (chunkInstance == null)
                    continue;

                if (poolManager.TryGetPooledComponent(chunkInstance, out SargassumCollapseChunk collapseChunk))
                {
                    collapseChunk.ActivateChunk(linearVelocity, angularVelocity, uniformScale, collapseChunkLifetime, 0);
                    if (poolManager.TryGetPooledRootRigidbody(chunkInstance, out Rigidbody chunkRigidbody))
                        ScheduleDebrisPetrification(chunkRigidbody);
                }
                else
                {
                    if (poolManager.TryGetPooledRootRigidbody(chunkInstance, out Rigidbody chunkRigidbody))
                    {
                        chunkRigidbody.detectCollisions = true;
                        chunkRigidbody.isKinematic = false;
                        chunkRigidbody.WakeUp();
                        WriteSafeVelocities(chunkRigidbody, linearVelocity, angularVelocity);
                        ScheduleDebrisPetrification(chunkRigidbody);
                    }

                    poolManager.Despawn(chunkInstance, collapseChunkLifetime);
                }
            }

            zone.Flags |= CollapseChunkBurstSpawnedFlag;
            _disruptionZones[zoneIndex] = zone;
        }

        internal void RegisterCollapseChunkImpact(Vector3 impactPointWS, Vector3 impactNormalWS, float impactSpeedSq, int fragmentDepth)
        {
            float thresholdSq = collapseCascadeImpactThreshold * collapseCascadeImpactThreshold;
            if (collapseChunkPrefab == null ||
                fragmentDepth > collapseCascadeMaxDepth ||
                impactSpeedSq < thresholdSq)
            {
                return;
            }

            if (!TryResolveCachedObjectPool(out IObjectPoolService poolManager))
                return;

            float severity01 = Mathf.Clamp01((impactSpeedSq - thresholdSq) / Mathf.Max(thresholdSq, 0.001f));
            Vector3 sampleSpaceImpact = impactPointWS - _globalDriftOffset;
            float radiusScale = FastPositivePowInt(collapseCascadeScaleMultiplier, Mathf.Max(0, fragmentDepth));
            RegisterOrReinforceDisruptionZone(
                sampleSpaceImpact,
                Mathf.Max(cellSize * 0.35f, buoyancyCollapseRadius * radiusScale),
                LerpClamped(0.2f, 0.55f, severity01),
                LerpClamped(buoyancyCollapseSinkDepth * 0.18f, buoyancyCollapseSinkDepth * 0.42f, severity01),
                LerpClamped(buoyancyCollapseSinkDuration * 0.35f, buoyancyCollapseSinkDuration * 0.65f, severity01),
                0f,
                0f,
                DisruptionZoneMode.CutCollapse);

            Vector3 safeNormal = NormalizeVector3Fast(impactNormalWS, Vector3.up);
            Vector3 tangent = Vector3.Cross(safeNormal, Vector3.up);
            if (tangent.sqrMagnitude <= 0.0001f)
                tangent = Vector3.Cross(safeNormal, Vector3.right);
            tangent = NormalizeVector3Fast(tangent, Vector3.right);
            Vector3 bitangent = NormalizeVector3Fast(Vector3.Cross(safeNormal, tangent), Vector3.forward);
            int spawnCount = Mathf.Clamp(
                Mathf.RoundToInt(LerpClamped(1f, collapseCascadeSpawnCount, severity01)),
                1,
                collapseCascadeSpawnCount);

            for (int chunkIndex = 0; chunkIndex < spawnCount; chunkIndex++)
            {
                float radialT = HashToFloat01((uint)fragmentDepth, (uint)(chunkIndex + 5), 0x5D588B65u);
                Vector3 lateralDirection = ResolveHashTangentDirection(
                    (uint)fragmentDepth,
                    (uint)(chunkIndex + 1),
                    0x6C8E9CF5u,
                    0xD1310BA6u,
                    tangent,
                    bitangent);
                Vector3 spawnPosition = impactPointWS + safeNormal * 0.18f + lateralDirection * (collapseChunkSpawnRadius * 0.35f * radialT);
                Vector3 linearVelocity =
                    safeNormal * (collapseChunkHorizontalSpeed * collapseCascadeVelocityMultiplier * LerpClamped(0.45f, 0.95f, severity01)) +
                    lateralDirection * (collapseChunkHorizontalSpeed * collapseCascadeVelocityMultiplier * LerpClamped(0.35f, 0.9f, severity01)) +
                    Vector3.down * (collapseChunkDownwardSpeed * LerpClamped(0.55f, 1.1f, severity01));
                Vector3 angularVelocity = new Vector3(
                    LerpClamped(-3.4f, 3.4f, HashToFloat01((uint)fragmentDepth, (uint)(chunkIndex + 9), 0x8CB92BA7u)),
                    LerpClamped(-2.8f, 2.8f, HashToFloat01((uint)fragmentDepth, (uint)(chunkIndex + 13), 0x4CF5AD43u)),
                    LerpClamped(-3.1f, 3.1f, HashToFloat01((uint)fragmentDepth, (uint)(chunkIndex + 17), 0x9D12A0F1u)));
                float parentScaleMultiplier = FastPositivePowInt(collapseCascadeScaleMultiplier, Mathf.Max(1, fragmentDepth));
                float uniformScale = LerpClamped(collapseChunkScaleMin, collapseChunkScaleMax, HashToFloat01((uint)fragmentDepth, (uint)(chunkIndex + 21), 0x3D8E1129u)) * parentScaleMultiplier;

                GameObject chunkInstance = poolManager.Spawn(
                    collapseChunkPrefab,
                    spawnPosition,
                    ResolveCardinalYawRotation(lateralDirection));
                if (chunkInstance == null)
                    continue;

                if (poolManager.TryGetPooledComponent(chunkInstance, out SargassumCollapseChunk collapseChunk))
                {
                    collapseChunk.ActivateChunk(linearVelocity, angularVelocity, uniformScale, collapseChunkLifetime * 0.7f, fragmentDepth);
                    if (poolManager.TryGetPooledRootRigidbody(chunkInstance, out Rigidbody chunkRigidbody))
                        ScheduleDebrisPetrification(chunkRigidbody);
                }
                else
                {
                    chunkInstance.transform.localScale = chunkInstance.transform.localScale * uniformScale;
                    if (poolManager.TryGetPooledRootRigidbody(chunkInstance, out Rigidbody chunkRigidbody))
                    {
                        chunkRigidbody.detectCollisions = true;
                        chunkRigidbody.isKinematic = false;
                        chunkRigidbody.WakeUp();
                        WriteSafeVelocities(chunkRigidbody, linearVelocity, angularVelocity);
                        ScheduleDebrisPetrification(chunkRigidbody);
                    }

                    poolManager.Despawn(chunkInstance, collapseChunkLifetime * 0.7f);
                }
            }
        }

        private DisruptionSample SampleDisruptionNoDrift(Vector3 sampledPositionWS)
        {
            DisruptionSample sample = default;
            if (!IsFiniteVector3(sampledPositionWS) ||
                _disruptionZones == null ||
                _activeDisruptionZoneCount <= 0)
            {
                return sample;
            }

            Vector2 sampleXZ = new Vector2(sampledPositionWS.x, sampledPositionWS.z);
            for (int i = 0; i < _activeDisruptionZoneCount; i++)
            {
                DisruptionZoneState zone = _disruptionZones[i];
                if (!IsFiniteDisruptionZone(in zone))
                    continue;

                float zone01 = EvaluateDisruptionZone01(zone);
                if (zone01 <= 0f || zone.RadiusWS <= 0f)
                    continue;

                Vector2 deltaXZ = sampleXZ - new Vector2(zone.SampleSpaceCenterWS.x, zone.SampleSpaceCenterWS.z);
                float distanceSq = deltaXZ.sqrMagnitude;
                float radiusSq = Mathf.Max(zone.RadiusWS * zone.RadiusWS, 0.000001f);
                if (distanceSq >= radiusSq)
                    continue;

                float invRadiusSq = math.rcp(math.max(radiusSq, 0.000001f));
                float radial01 = 1f - math.saturate(distanceSq * invRadiusSq);
                float influence01 = zone01 * radial01 * radial01;
                if (influence01 > sample.Suppression01)
                    sample.Suppression01 = influence01;

                float sinkDepthWS = zone.SinkDepthWS * influence01;
                if (sinkDepthWS > sample.SinkDepthWS)
                    sample.SinkDepthWS = sinkDepthWS;
            }

            return sample;
        }

        private void PublishShaderGlobals()
        {
            QueueShaderGlobals(_globalDriftOffset, _densityFieldWorldRect, _sinkFieldTexture != null ? _sinkFieldTexture : Texture2D.blackTexture, _debugMaxSinkDepth);
        }

        private void PublishEmptyShaderGlobals()
        {
            QueueShaderGlobals(Vector3.zero, Vector4.zero, Texture2D.blackTexture, 0f);
        }

        private void QueueShaderGlobals()
        {
            QueueShaderGlobals(_globalDriftOffset, _densityFieldWorldRect, _sinkFieldTexture != null ? _sinkFieldTexture : Texture2D.blackTexture, _debugMaxSinkDepth);
        }

        private void QueueShaderGlobals(Vector3 driftOffset, Vector4 sinkWorldRect, Texture sinkTexture, float maxSinkDepth)
        {
            _pendingGlobalDriftOffset = driftOffset;
            _pendingSinkWorldRect = sinkWorldRect;
            _pendingSinkTexture = sinkTexture != null ? sinkTexture : Texture2D.blackTexture;
            _pendingMaxSinkDepth = maxSinkDepth;
            _shaderGlobalsDirty = true;
        }

        private void FlushShaderGlobals()
        {
            if (!_shaderGlobalsDirty)
                return;

            PublishShaderGlobalsImmediate(_pendingGlobalDriftOffset, _pendingSinkWorldRect, _pendingSinkTexture, _pendingMaxSinkDepth);
            _shaderGlobalsDirty = false;
        }

        private void PublishShaderGlobalsImmediate(Vector3 driftOffset, Vector4 sinkWorldRect, Texture sinkTexture, float maxSinkDepth)
        {
            Vector4 driftVector = new Vector4(driftOffset.x, driftOffset.y, driftOffset.z, 0f);
            Texture resolvedSinkTexture = sinkTexture != null ? sinkTexture : Texture2D.blackTexture;

            if (!_shaderGlobalsPublished || _publishedGlobalDriftOffset != driftVector)
            {
                Shader.SetGlobalVector(_GlobalDriftOffsetId, driftVector);
                _publishedGlobalDriftOffset = driftVector;
            }

            if (!_shaderGlobalsPublished || _publishedSinkTexture != resolvedSinkTexture)
            {
                Shader.SetGlobalTexture(_SinkTextureId, resolvedSinkTexture);
                _publishedSinkTexture = resolvedSinkTexture;
            }

            if (!_shaderGlobalsPublished || _publishedSinkWorldRect != sinkWorldRect)
            {
                Shader.SetGlobalVector(_SinkWorldRectId, sinkWorldRect);
                _publishedSinkWorldRect = sinkWorldRect;
            }

            if (!_shaderGlobalsPublished || _publishedMaxSinkDepth != maxSinkDepth)
            {
                Shader.SetGlobalFloat(_MaxSinkDepthId, maxSinkDepth);
                _publishedMaxSinkDepth = maxSinkDepth;
            }

            _shaderGlobalsPublished = true;
        }

        private void ResolveActiveNestingPrototypes()
        {
            _activeNestingPrototypes = _authoredNestingPrototypes != null && _authoredNestingPrototypes.Length > 0
                ? _authoredNestingPrototypes
                : null;
        }

        private void ClearAuthoredNestingPrototypeCache()
        {
            _activeNestingPrototypes = null;
        }

        private void RebuildNestedAttachments()
        {
            if (!HasNestedAttachmentStorageReady())
            {
                ClearNestedAttachments();
                return;
            }

            ClearNestedAttachments();

            if (!_hasFieldData ||
                _activeNestingPrototypes == null ||
                _activeNestingPrototypes.Length == 0 ||
                _nestedMatricesByPrototype == null ||
                _nestedMatrixCounts == null ||
                _debugFieldBounds.size.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float denseNodeChance = Mathf.Clamp01(denseNodeNestChancePercent * 0.01f);
            if (denseNodeChance <= 0f)
                return;

            for (int bucket = 0; bucket < _densityCells.BucketCapacity; bucket++)
            {
                if (_activeNestedAttachmentStateCount >= maxNestedAttachmentCount)
                    break;

                if (!_densityCells.TryGetOccupiedAtBucket(bucket, out long cellKey, out CellData cell))
                    continue;

                float normalizedDensity = Mathf.Clamp01(cell.Density * densityNormalization);
                if (normalizedDensity < nestingNodeDensityThreshold)
                    continue;

                int cellX = (int)(cellKey >> 32);
                int cellZ = (int)cellKey;
                float chance = HashToFloat01((uint)cellX, (uint)cellZ, 0x6A09E667u);
                if (chance > denseNodeChance)
                    continue;

                Vector3 cellCenterWS = new Vector3(
                    (cellX + 0.5f) * cellSize + _globalDriftOffset.x,
                    (cell.MinY + cell.MaxY) * 0.5f + _globalDriftOffset.y,
                    (cellZ + 0.5f) * cellSize + _globalDriftOffset.z);
                if (!SampleDetailedInfluence(cellCenterWS, nestingSampleRadius, 0f, out SargassumFieldSample fieldSample))
                    continue;

                if (fieldSample.Window01 > nestingNodeWindowThreshold)
                    continue;

                int prototypeIndex = ResolveNestedPrototypeIndex(fieldSample, cellX, cellZ);
                if (prototypeIndex < 0)
                    continue;

                NestedAttachmentPrototype prototype = _activeNestingPrototypes[prototypeIndex];
                float minScale = prototype.UniformScaleRange.x > 0f ? prototype.UniformScaleRange.x : 0.72f;
                float maxScale = prototype.UniformScaleRange.y > minScale ? prototype.UniformScaleRange.y : minScale * 1.18f;
                float uniformScale = LerpClamped(
                    Mathf.Max(0.1f, minScale),
                    Mathf.Max(minScale, maxScale),
                    HashToFloat01((uint)cellX, (uint)cellZ, 0x91E10DA5u));
                float verticalOffset = LerpClamped(
                    prototype.VerticalOffsetRange.x,
                    prototype.VerticalOffsetRange.y,
                    HashToFloat01((uint)cellX, (uint)cellZ, 0x52A3F15Du));
                float yaw = HashToFloat01((uint)cellX, (uint)cellZ, 0xD23F4195u) * 360f;
                float pitch = LerpClamped(-10f, 10f, HashToFloat01((uint)cellX, (uint)cellZ, 0x3AF21D8Bu));
                float roll = LerpClamped(-14f, 14f, HashToFloat01((uint)cellX, (uint)cellZ, 0xF13A0BCDu));
                Vector3 sampleSpaceAnchorWS = fieldSample.AnchorWS - _globalDriftOffset;
                sampleSpaceAnchorWS.y = Mathf.Clamp(cellCenterWS.y + verticalOffset - _globalDriftOffset.y, cell.MinY, cell.MaxY);

                _nestedAttachmentStates[_activeNestedAttachmentStateCount] = new NestedAttachmentState
                {
                    SampleSpaceAnchorWS = sampleSpaceAnchorWS,
                    RenderOffsetWS = Vector3.zero,
                    ReleaseVelocityWS = Vector3.zero,
                    Rotation = Quaternion.Euler(pitch, yaw, roll),
                    UniformScale = uniformScale,
                    ReleaseLifetime = nestingReleaseLifetime,
                    ReleaseAge = 0f,
                    PrototypeIndex = prototypeIndex,
                    ReleasedFlag = 0
                };
                _activeNestedAttachmentStateCount++;
            }

            _activeNestedPrototypeCount = _activeNestingPrototypes.Length;
            _debugNestedStateCount = _activeNestedAttachmentStateCount;
            UpdateNestedAttachmentBatches(0f);
        }

        private void EnsureNestedAttachmentStorage()
        {
            ResolveActiveNestingPrototypes();
            int prototypeCount = _activeNestingPrototypes != null ? _activeNestingPrototypes.Length : 0;
            int requiredCapacity = Mathf.Clamp(maxNestedAttachmentCount, 4, 128);
            if (_nestedMatricesByPrototype != null &&
                _nestedMatrixCounts != null &&
                _nestedAttachmentStates != null &&
                _nestedMatricesByPrototype.Length == prototypeCount &&
                _nestedPrototypeCapacity == requiredCapacity)
            {
                return;
            }

            _nestedPrototypeCapacity = requiredCapacity;
            // COLD ALLOC: Matrix4x4[prototypeCount][capacity] - persistent instanced nesting matrices by archetype - owner: SargassumGlobalDragManager
            _nestedMatricesByPrototype = new Matrix4x4[prototypeCount][];
            // COLD ALLOC: int[prototypeCount] - per-archetype instanced nesting counts - owner: SargassumGlobalDragManager
            _nestedMatrixCounts = new int[prototypeCount];
            for (int i = 0; i < prototypeCount; i++)
            {
                // COLD ALLOC: Matrix4x4[capacity] - instanced nesting batch storage for one archetype - owner: SargassumGlobalDragManager
                _nestedMatricesByPrototype[i] = new Matrix4x4[requiredCapacity];
            }
            // COLD ALLOC: NestedAttachmentState[capacity] - persistent trapped-debris state for cut-release simulation - owner: SargassumGlobalDragManager
            _nestedAttachmentStates = new NestedAttachmentState[requiredCapacity];
        }

        private bool HasNestedAttachmentStorageReady()
        {
            int prototypeCount = _activeNestingPrototypes != null ? _activeNestingPrototypes.Length : 0;
            int requiredCapacity = Mathf.Clamp(maxNestedAttachmentCount, 4, 128);
            if (_nestedMatricesByPrototype == null ||
                _nestedMatrixCounts == null ||
                _nestedAttachmentStates == null ||
                _nestedMatricesByPrototype.Length != prototypeCount ||
                _nestedMatrixCounts.Length != prototypeCount ||
                _nestedPrototypeCapacity != requiredCapacity ||
                _nestedAttachmentStates.Length < requiredCapacity)
            {
                return false;
            }

            for (int i = 0; i < prototypeCount; i++)
            {
                if (_nestedMatricesByPrototype[i] == null ||
                    _nestedMatricesByPrototype[i].Length < requiredCapacity)
                {
                    return false;
                }
            }

            return true;
        }

        private void ReleaseNestedAttachmentStorage()
        {
            _nestedMatricesByPrototype = null;
            _nestedMatrixCounts = null;
            _nestedAttachmentStates = null;
            _nestedPrototypeCapacity = 0;
            _activeNestedPrototypeCount = 0;
            _activeNestedAttachmentStateCount = 0;
        }

        internal bool RegisterSettledCollapseChunk(SargassumCollapseChunk chunk)
        {
            if (chunk == null)
                return false;
            if (!HasAuthoredScavengerRenderAssets())
                return false;

            Vector3 anchorWS = chunk.GetScavengerAnchorWS();
            if (!IsFiniteVector3(anchorWS))
                return false;

            EnsureScavengerStorage();

            for (int i = 0; i < _activeScavengerHostCount; i++)
            {
                if (_scavengerHosts[i].Chunk == chunk)
                    return true;
            }

            if (_activeScavengerHostCount >= _scavengerHosts.Length)
                return false;

            int slot = _activeScavengerHostCount;
            _scavengerHosts[slot] = new ScavengerHostState
            {
                Chunk = chunk,
                AnchorWS = anchorWS,
                SettledTime = 0f,
                Consumed01 = 0f,
                Seed = unchecked((uint)EntityId.ToULong(chunk.GetEntityId())) ^ NestingHashSeed
            };
            _activeScavengerHostCount++;
            return true;
        }

        internal void UnregisterSettledCollapseChunk(SargassumCollapseChunk chunk)
        {
            if (chunk == null || _scavengerHosts == null)
                return;

            for (int i = 0; i < _activeScavengerHostCount; i++)
            {
                if (_scavengerHosts[i].Chunk != chunk)
                    continue;

                int lastIndex = _activeScavengerHostCount - 1;
                if (i < lastIndex)
                    _scavengerHosts[i] = _scavengerHosts[lastIndex];

                _scavengerHosts[lastIndex] = default;
                _activeScavengerHostCount = lastIndex;
                return;
            }
        }

        internal void RegisterExternalScavengerSite(Vector3 anchorWS, float radiusWS, float duration)
        {
            if (!HasAuthoredScavengerRenderAssets())
                return;

            if (!IsFiniteVector3(anchorWS) || !math.isfinite(radiusWS))
            {
                return;
            }

            EnsureScavengerStorage();
            if (_externalScavengerSites == null || _externalScavengerSites.Length == 0)
                return;

            float clampedRadius = Mathf.Clamp(radiusWS, 0.2f, scavengerOrbitRadius * 3f);
            float clampedDuration = duration > 0f ? duration : externalScavengerSiteDuration;
            if (!math.isfinite(clampedRadius) || !math.isfinite(clampedDuration))
                return;

            int targetIndex = -1;
            float weakestRemaining = float.MaxValue;

            for (int i = 0; i < _externalScavengerSites.Length; i++)
            {
                ExternalScavengerSiteState site = _externalScavengerSites[i];
                if (site.RemainingTime <= 0f)
                {
                    targetIndex = i;
                    break;
                }

                float mergeRadius = Mathf.Max(site.RadiusWS, clampedRadius);
                if ((site.AnchorWS - anchorWS).sqrMagnitude <= mergeRadius * mergeRadius)
                {
                    targetIndex = i;
                    break;
                }

                if (site.RemainingTime < weakestRemaining)
                {
                    weakestRemaining = site.RemainingTime;
                    targetIndex = i;
                }
            }

            if (targetIndex < 0)
                targetIndex = 0;

            _externalScavengerSites[targetIndex] = new ExternalScavengerSiteState
            {
                AnchorWS = anchorWS,
                RadiusWS = clampedRadius,
                RemainingTime = clampedDuration,
                Seed = BuildDeterministicAnchorSeed(anchorWS, _globalDriftOffset, (uint)targetIndex * 0x45D9F3Bu)
            };
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            if (!HasAuthoredScavengerRenderAssets() ||
                _externalScavengerSites == null ||
                _externalScavengerSites.Length == 0)
            {
                data.externalScavengerSites = null;
                return;
            }

            int activeCount = 0;
            for (int i = 0; i < _externalScavengerSites.Length; i++)
            {
                if (_externalScavengerSites[i].RemainingTime > 0f)
                    activeCount++;
            }

            if (activeCount <= 0)
            {
                data.externalScavengerSites = null;
                return;
            }

            // COLD ALLOC: ExternalScavengerSiteDTO[activeCount] - quantized corpse/carcass site persistence payload - owner: SargassumGlobalDragManager
            ExternalScavengerSiteDTO[] snapshot = new ExternalScavengerSiteDTO[activeCount];
            int writeIndex = 0;
            for (int i = 0; i < _externalScavengerSites.Length; i++)
            {
                ExternalScavengerSiteState site = _externalScavengerSites[i];
                if (site.RemainingTime <= 0f)
                    continue;

                if (TryQuantizeExternalSite(site, out ExternalScavengerSiteDTO dto))
                    snapshot[writeIndex++] = dto;
            }

            data.externalScavengerSites = writeIndex > 0 ? snapshot : null;
        }

        public void LoadFromSaveData(SaveData data)
        {
            if (!HasAuthoredScavengerRenderAssets())
            {
                ClearScavengerHosts();
                return;
            }

            EnsureScavengerStorage();
            if (_externalScavengerSites == null)
                return;

            Array.Clear(_externalScavengerSites, 0, _externalScavengerSites.Length);
            _activeExternalScavengerSiteCount = 0;

            ExternalScavengerSiteDTO[] snapshot = data != null ? data.externalScavengerSites : null;
            if (snapshot == null || snapshot.Length == 0)
                return;

            int writeIndex = 0;
            int restoreCount = Math.Min(snapshot.Length, _externalScavengerSites.Length);
            for (int i = 0; i < restoreCount; i++)
            {
                ExternalScavengerSiteDTO dto = snapshot[i];
                if (!dto.IsValid())
                    continue;

                _externalScavengerSites[writeIndex++] = DequantizeExternalSite(dto);
            }

            _activeExternalScavengerSiteCount = writeIndex;
        }

        private static bool TryQuantizeExternalSite(ExternalScavengerSiteState site, out ExternalScavengerSiteDTO dto)
        {
            dto = default;
            if (!TryResolveRuntimeAup(site.AnchorWS, out AbsoluteUniversePosition anchorAup))
                return false;

            int3 chunkId = AbsoluteUniversePosition.ResolveChunkId(in anchorAup, ExternalSiteChunkSizeMeters);
            double3 absolutePosition = anchorAup.ToAbsoluteDouble3();
            double3 chunkCenter = new double3(
                (chunkId.x * (double)ExternalSiteChunkSizeMeters) + (ExternalSiteChunkSizeMeters * 0.5d),
                (chunkId.y * (double)ExternalSiteChunkSizeMeters) + (ExternalSiteChunkSizeMeters * 0.5d),
                (chunkId.z * (double)ExternalSiteChunkSizeMeters) + (ExternalSiteChunkSizeMeters * 0.5d));

            dto = new ExternalScavengerSiteDTO
            {
                chunkX = chunkId.x,
                chunkY = chunkId.y,
                chunkZ = chunkId.z,
                offsetX = QuantizeChunkOffset(absolutePosition.x - chunkCenter.x),
                offsetY = QuantizeChunkOffset(absolutePosition.y - chunkCenter.y),
                offsetZ = QuantizeChunkOffset(absolutePosition.z - chunkCenter.z),
                quantizedRadius = (byte)math.clamp(math.round(site.RadiusWS / ExternalSiteQuantizationMeters), 1f, byte.MaxValue),
                remainingTime = math.max(0f, site.RemainingTime),
                seed = site.Seed
            };
            return dto.IsValid();
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private static ExternalScavengerSiteState DequantizeExternalSite(ExternalScavengerSiteDTO dto)
        {
            double3 chunkCenter = new double3(
                (dto.chunkX * (double)ExternalSiteChunkSizeMeters) + (ExternalSiteChunkSizeMeters * 0.5d),
                (dto.chunkY * (double)ExternalSiteChunkSizeMeters) + (ExternalSiteChunkSizeMeters * 0.5d),
                (dto.chunkZ * (double)ExternalSiteChunkSizeMeters) + (ExternalSiteChunkSizeMeters * 0.5d));

            double3 absolutePosition = new double3(
                chunkCenter.x + (dto.offsetX * ExternalSiteQuantizationMeters),
                chunkCenter.y + (dto.offsetY * ExternalSiteQuantizationMeters),
                chunkCenter.z + (dto.offsetZ * ExternalSiteQuantizationMeters));
            AbsoluteUniversePosition runtimeOriginAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            AbsoluteUniversePosition siteAup = AbsoluteUniversePosition.FromAbsolutePosition(absolutePosition);
            float3 runtimePosition = AUPMath.ResolveCameraRelative(in siteAup, in runtimeOriginAup);

            return new ExternalScavengerSiteState
            {
                AnchorWS = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z),
                RadiusWS = math.max(ExternalSiteQuantizationMeters, dto.quantizedRadius * ExternalSiteQuantizationMeters),
                RemainingTime = math.max(0f, dto.remainingTime),
                Seed = dto.seed
            };
        }

        private static sbyte QuantizeChunkOffset(double offsetMeters)
        {
            float quantized = math.round((float)(offsetMeters / ExternalSiteQuantizationMeters));
            return (sbyte)math.clamp(quantized, -127f, 127f);
        }

        private void EnsureScavengerStorage()
        {
            int hostCapacity = Mathf.Clamp(maxScavengerHostCount, 2, 24);
            int externalSiteCapacity = Mathf.Clamp(maxExternalScavengerSiteCount, 1, 16);
            int instanceCapacity = Mathf.Max(1, hostCapacity * Mathf.Clamp(scavengersPerHost, 4, 32));
            if (_scavengerHosts != null &&
                _externalScavengerSites != null &&
                _scavengerHosts.Length == hostCapacity &&
                _externalScavengerSites.Length == externalSiteCapacity &&
                _scavengerInstanceCapacity == instanceCapacity)
            {
                EnsureScavengerMatrixCapacity(instanceCapacity);
                return;
            }

            ReleaseScavengerBuffers();
            _activeScavengerHostCount = 0;
            _activeExternalScavengerSiteCount = 0;
            // COLD ALLOC: ScavengerHostState[hostCapacity] - tracked settled collapse chunks feeding the scavenger BRG renderer - owner: SargassumGlobalDragManager
            _scavengerHosts = new ScavengerHostState[hostCapacity];
            // COLD ALLOC: ExternalScavengerSiteState[externalSiteCapacity] - corpse/carcass scavenger targets without live chunk owners - owner: SargassumGlobalDragManager
            _externalScavengerSites = new ExternalScavengerSiteState[externalSiteCapacity];
            _scavengerInstanceCapacity = instanceCapacity;
            EnsureScavengerMatrixCapacity(instanceCapacity);
        }

        private void EnsureScavengerRenderResources()
        {
            if (!HasAuthoredScavengerRenderAssets())
            {
                ReleaseScavengerResources();
                return;
            }

            Mesh activeMesh = _authoredSargassumMesh;
            Material activeMaterial = PrepareScavengerBrgMaterialCold();
            if (activeMesh == null || activeMaterial == null)
                return;

            EnsureScavengerStorage();

            if (_scavengerMatrixBufferA == null || _scavengerMatrixBufferA.count < Mathf.Max(1, _scavengerInstanceCapacity) ||
                _scavengerMatrixBufferB == null || _scavengerMatrixBufferB.count < Mathf.Max(1, _scavengerInstanceCapacity))
            {
                ReleaseGraphicsBuffer(ref _scavengerMatrixBufferA);
                ReleaseGraphicsBuffer(ref _scavengerMatrixBufferB);
                int capacity = Mathf.Max(1, _scavengerInstanceCapacity);
                _scavengerMatrixBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Matrix4x4>(capacity); // COLD ALLOC: GraphicsBuffer[instanceCapacity] - scavenger BRG matrices A - owner: SargassumGlobalDragManager
                _scavengerMatrixBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Matrix4x4>(capacity); // COLD ALLOC: GraphicsBuffer[instanceCapacity] - scavenger BRG matrices B - owner: SargassumGlobalDragManager
                _activeScavengerMatrixBuffer = _scavengerMatrixBufferA;
                _scavengerMatrixUploadIndex = 0;
                _registeredScavengerBatchBuffer = null;
                _boundScavengerMatrixBuffer = null;
            }

            if (_scavengerBatchRendererGroup == null)
            {
                _scavengerBatchRendererGroup = new BatchRendererGroup(new BatchRendererGroupCreateInfo
                {
                    cullingCallback = OnPerformScavengerCulling,
                    userContext = IntPtr.Zero
                });

                if (!TryRegisterScavengerBatchCold())
                {
                    _scavengerBatchRendererGroup.Dispose();
                    _scavengerBatchRendererGroup = null;
                    return;
                }
            }

            SyncScavengerBatchRegistration(activeMesh, activeMaterial);
        }

        private bool TryRegisterScavengerBatchCold()
        {
            if (_scavengerBatchRendererGroup == null)
                return false;

            _scavengerBatchHandleBuffer = HectonBatchRendererGroupUtility.CreateBatchHandleBuffer(); // COLD ALLOC: GraphicsBuffer[1] - BRG registration handle buffer for scavenger scatter - owner: SargassumGlobalDragManager
            NativeArray<MetadataValue> batchMetadata = H8Memory.Allocate<MetadataValue>(
                ScavengerBrgMetadataPlaceholderCount,
                VaultOwnerSystemId,
                Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            try
            {
                if (!batchMetadata.IsCreated || batchMetadata.Length < ScavengerBrgMetadataPlaceholderCount)
                {
                    ReleaseScavengerBatchHandleBuffer();
                    return false;
                }

                _scavengerBatchId = _scavengerBatchRendererGroup.AddBatch(batchMetadata, _scavengerBatchHandleBuffer.bufferHandle);
            }
            finally
            {
                if (batchMetadata.IsCreated)
                    H8Memory.Release(ref batchMetadata, VaultOwnerSystemId);
            }

            if (!_scavengerBatchId.Equals(default))
                return true;

            ReleaseScavengerBatchHandleBuffer();
            return false;
        }

        private bool HasScavengerRenderResourcesReady()
        {
            if (!HasAuthoredScavengerRenderAssets())
                return false;

            int requiredCapacity = Mathf.Max(1, _scavengerInstanceCapacity);
            if (_scavengerHosts == null ||
                _externalScavengerSites == null ||
                _scavengerMatrixBufferA == null ||
                _scavengerMatrixBufferB == null ||
                _scavengerMatrixBufferA.count < requiredCapacity ||
                _scavengerMatrixBufferB.count < requiredCapacity ||
                _scavengerBatchRendererGroup == null ||
                _scavengerBatchHandleBuffer == null)
            {
                return false;
            }

            Mesh activeMesh = _authoredSargassumMesh;
            Material sourceMaterial = _authoredSargassumMaterial;
            return activeMesh != null &&
                   sourceMaterial != null &&
                   _scavengerBrgMaterial != null &&
                   _scavengerBrgMaterialSource == sourceMaterial &&
                   IsScavengerBatchRegistrationReady(activeMesh, _scavengerBrgMaterial);
        }

        private bool HasAuthoredScavengerRenderAssets()
        {
            return _authoredSargassumMesh != null &&
                   _authoredSargassumMaterial != null;
        }

        private void ReleaseScavengerBuffers()
        {
            ReleaseGraphicsBuffer(ref _scavengerMatrixBufferA);
            ReleaseGraphicsBuffer(ref _scavengerMatrixBufferB);
            _activeScavengerMatrixBuffer = null;
            _registeredScavengerBatchBuffer = null;
            _boundScavengerMatrixBuffer = null;
            _boundScavengerMatrixMaterial = null;
            _scavengerMatrixUploadIndex = 0;
            _scavengerMatrixStaging = null;
            ReleaseVaultBuffer(ref _scavengerMatricesHandle);
        }

        private void ReleaseScavengerResources()
        {
            ClearScavengerHosts();
            ReleaseScavengerBuffers();
            _scavengerHosts = null;
            _externalScavengerSites = null;
            _scavengerInstanceCapacity = 0;
            _activeScavengerHostCount = 0;
            _activeExternalScavengerSiteCount = 0;

            if (_scavengerBatchRendererGroup != null)
            {
                if (!_scavengerBatchId.Equals(default))
                    _scavengerBatchRendererGroup.RemoveBatch(_scavengerBatchId);
                if (!_scavengerBatchMeshId.Equals(default))
                    _scavengerBatchRendererGroup.UnregisterMesh(_scavengerBatchMeshId);
                if (!_scavengerBatchMaterialId.Equals(default))
                    _scavengerBatchRendererGroup.UnregisterMaterial(_scavengerBatchMaterialId);
                _scavengerBatchRendererGroup.Dispose();
                _scavengerBatchRendererGroup = null;
                _scavengerBatchId = default;
                _scavengerBatchMeshId = default;
                _scavengerBatchMaterialId = default;
                _registeredScavengerBatchBuffer = null;
                _registeredScavengerMesh = null;
                _registeredScavengerMaterial = null;
                _registeredScavengerDrawBounds = default;
                _scavengerDrawBoundsRegistered = false;
            }

            ReleaseScavengerBatchHandleBuffer();

            ReleaseScavengerBrgMaterial();
            ClearScavengerPresentationState();
        }

        private void ReleaseScavengerBatchHandleBuffer()
        {
            if (_scavengerBatchHandleBuffer == null)
                return;

            _scavengerBatchHandleBuffer.Release();
            _scavengerBatchHandleBuffer = null;
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private void ClearScavengerHosts()
        {
            if (_scavengerHosts != null)
            {
                for (int i = 0; i < _activeScavengerHostCount; i++)
                {
                    SargassumCollapseChunk chunk = _scavengerHosts[i].Chunk;
                    if (chunk != null)
                        chunk.ClearScavengerHostRegistration();
                }

                Array.Clear(_scavengerHosts, 0, _scavengerHosts.Length);
            }

            if (_externalScavengerSites != null)
                Array.Clear(_externalScavengerSites, 0, _externalScavengerSites.Length);

            _activeScavengerHostCount = 0;
            _activeExternalScavengerSiteCount = 0;
            ClearScavengerPresentationState();
        }

        private void ClearScavengerPresentationState()
        {
            _debugScavengerHostCount = 0;
            _debugScavengerInstanceCount = 0;
            _debugScavengerBounds = default;
            _scavengerDrawBounds = default;
            _registeredScavengerDrawBounds = default;
            _scavengerDrawBoundsRegistered = false;
        }

        private void UpdateScavengerHosts(float dt)
        {
            if (_scavengerHosts == null || _activeScavengerHostCount <= 0)
            {
                _debugScavengerHostCount = 0;
                _debugScavengerInstanceCount = 0;
                _debugScavengerBounds = default;
                UploadScavengerInstances(0);
                return;
            }

            if (!HasScavengerRenderResourcesReady())
            {
                _debugScavengerHostCount = 0;
                _debugScavengerInstanceCount = 0;
                _debugScavengerBounds = default;
                UploadScavengerInstances(0);
                return;
            }

            int writeHostIndex = 0;
            int matrixCount = 0;
            bool boundsInitialized = false;
            Vector3 boundsMin = default;
            Vector3 boundsMax = default;
            float safeDeltaTime = math.isfinite(dt) ? math.max(0f, dt) : 0f;
            float consumeDurationInv = 1f / math.max(0.1f, scavengerConsumeDuration);
            float externalSiteDurationInv = 1f / math.max(0.1f, externalScavengerSiteDuration);
            int maxScavengersPerHost = ResolveActiveScavengersPerHost();
            Matrix4x4[] scavengerMatrixStaging = _scavengerMatrixStaging;
            if (scavengerMatrixStaging == null || scavengerMatrixStaging.Length < _scavengerInstanceCapacity)
            {
                _debugScavengerHostCount = 0;
                _debugScavengerInstanceCount = 0;
                _debugScavengerBounds = default;
                UploadScavengerInstances(0);
                return;
            }

            for (int readHostIndex = 0; readHostIndex < _activeScavengerHostCount; readHostIndex++)
            {
                ScavengerHostState host = _scavengerHosts[readHostIndex];
                if (host.Chunk == null || !host.Chunk.CanHostScavengers)
                    continue;

                host.AnchorWS = host.Chunk.GetScavengerAnchorWS();
                host.SettledTime += safeDeltaTime;
                if (host.SettledTime >= scavengerActivationDelay)
                {
                    float consumeDelta01 = safeDeltaTime * consumeDurationInv;
                    host.Chunk.ApplyScavengerConsumptionDelta(consumeDelta01);
                    if (host.Chunk == null || !host.Chunk.CanHostScavengers)
                        continue;

                    host.Consumed01 = math.saturate(host.Consumed01 + consumeDelta01);
                    int instanceCount = math.clamp(
                        RoundToIntPositive(LerpClamped(maxScavengersPerHost, 2f, host.Consumed01)),
                        1,
                        maxScavengersPerHost);
                    float invInstanceCount = 1f / instanceCount;
                    float activeTime = host.SettledTime - scavengerActivationDelay;
                    float consumeBias = 1f - host.Consumed01 * 0.35f;
                    float bobPhaseBase = activeTime * 4.2f * ScavengerInvTwoPi;

                    for (int instanceIndex = 0; instanceIndex < instanceCount && matrixCount < _scavengerInstanceCapacity; instanceIndex++)
                    {
                        float phase01 = (instanceIndex + 0.5f) * invInstanceCount;
                        float radialHash = HashToFloat01(host.Seed, (uint)(instanceIndex + 1), 0xA53C91E5u);
                        float scaleHash = HashToFloat01(host.Seed, (uint)(instanceIndex + 7), 0x9E3779B9u);
                        float bobHash = HashToFloat01(host.Seed, (uint)(instanceIndex + 13), 0x7F4A7C15u);
                        float orbitPhase01 = phase01 + activeTime * LerpClamped(0.55f, 1.1f, radialHash) * ScavengerInvTwoPi;
                        float radius = scavengerOrbitRadius * LerpClamped(0.35f, 1f, radialHash) * consumeBias;
                        Vector3 orbitOffset = ResolveDiamondOrbitOffset(orbitPhase01, radius);
                        float bob = TriangleWaveSigned01(bobPhaseBase + bobHash) * scavengerBobAmplitude;
                        Vector3 positionWS = host.AnchorWS + orbitOffset + new Vector3(0f, scavengerVerticalOffset + bob, 0f);

                        float scale = LerpClamped(scavengerScaleMin, scavengerScaleMax, scaleHash);
                        Matrix4x4 matrix = BuildScavengerCardinalMatrix(positionWS, orbitPhase01, scale);
                        if (matrixCount < scavengerMatrixStaging.Length)
                            scavengerMatrixStaging[matrixCount] = matrix;
                        matrixCount++;

                        EncapsulateRadius(positionWS, math.max(0.1f, scale * 1.4f), ref boundsMin, ref boundsMax, ref boundsInitialized);
                    }
                }

                _scavengerHosts[writeHostIndex] = host;
                writeHostIndex++;
            }

            int writeExternalSiteIndex = 0;
            if (_externalScavengerSites != null)
            {
                for (int readExternalSiteIndex = 0; readExternalSiteIndex < _externalScavengerSites.Length; readExternalSiteIndex++)
                {
                    ExternalScavengerSiteState site = _externalScavengerSites[readExternalSiteIndex];
                    if (site.RemainingTime <= 0f)
                        continue;

                    site.RemainingTime = math.max(0f, site.RemainingTime - safeDeltaTime);
                    if (site.RemainingTime <= 0f)
                        continue;

                    int instanceCount = math.clamp(maxScavengersPerHost / 2, 1, maxScavengersPerHost);
                    float invInstanceCount = 1f / instanceCount;
                    float life01 = 1f - site.RemainingTime * externalSiteDurationInv;
                    float activeTime = (externalScavengerSiteDuration - site.RemainingTime) * 0.85f;
                    float bobPhaseBase = activeTime * 3.4f * ScavengerInvTwoPi;
                    for (int instanceIndex = 0; instanceIndex < instanceCount && matrixCount < _scavengerInstanceCapacity; instanceIndex++)
                    {
                        float phase01 = (instanceIndex + 0.5f) * invInstanceCount;
                        float radialHash = HashToFloat01(site.Seed, (uint)(instanceIndex + 3), 0x6D2B79F5u);
                        float scaleHash = HashToFloat01(site.Seed, (uint)(instanceIndex + 11), 0xA53C91E5u);
                        float bobHash = HashToFloat01(site.Seed, (uint)(instanceIndex + 19), 0x9E3779B9u);
                        float orbitPhase01 = phase01 + activeTime * LerpClamped(0.35f, 0.9f, radialHash) * ScavengerInvTwoPi;
                        float radius = site.RadiusWS * LerpClamped(0.2f, 1f, radialHash) * LerpClamped(1f, 0.55f, life01);
                        Vector3 orbitOffset = ResolveDiamondOrbitOffset(orbitPhase01, radius);
                        float bob = TriangleWaveSigned01(bobPhaseBase + bobHash) * scavengerBobAmplitude;
                        Vector3 positionWS = site.AnchorWS + orbitOffset + new Vector3(0f, scavengerVerticalOffset + bob, 0f);

                        float scale = LerpClamped(scavengerScaleMin, scavengerScaleMax, scaleHash) * LerpClamped(0.7f, 1f, 1f - life01);
                        Matrix4x4 matrix = BuildScavengerCardinalMatrix(positionWS, orbitPhase01, scale);
                        if (matrixCount < scavengerMatrixStaging.Length)
                            scavengerMatrixStaging[matrixCount] = matrix;
                        matrixCount++;

                        EncapsulateRadius(positionWS, math.max(0.1f, scale * 1.4f), ref boundsMin, ref boundsMax, ref boundsInitialized);
                    }

                    _externalScavengerSites[writeExternalSiteIndex] = site;
                    writeExternalSiteIndex++;
                }
            }

            for (int clearIndex = writeHostIndex; clearIndex < _activeScavengerHostCount; clearIndex++)
                _scavengerHosts[clearIndex] = default;

            if (_externalScavengerSites != null)
            {
                for (int clearIndex = writeExternalSiteIndex; clearIndex < _externalScavengerSites.Length; clearIndex++)
                    _externalScavengerSites[clearIndex] = default;
            }

            _activeScavengerHostCount = writeHostIndex;
            _activeExternalScavengerSiteCount = writeExternalSiteIndex;

            if (!TryPublishScavengerMatrices(scavengerMatrixStaging, matrixCount))
            {
                _debugScavengerHostCount = 0;
                _debugScavengerInstanceCount = 0;
                _debugScavengerBounds = default;
                _scavengerDrawBounds = default;
                UploadScavengerInstances(0);
                return;
            }

            _debugScavengerHostCount = writeHostIndex + writeExternalSiteIndex;
            _debugScavengerInstanceCount = matrixCount;
            _scavengerDrawBounds = boundsInitialized ? BuildBoundsFromMinMax(boundsMin, boundsMax) : default;
            _debugScavengerBounds = _scavengerDrawBounds;
            UploadScavengerInstances(matrixCount);
        }

        private int ResolveActiveScavengersPerHost()
        {
            int authoredMax = math.max(1, scavengersPerHost);
            float quality01 = Smooth01(_cachedGlobalQualityWeight01);
            float minimum = math.max(1f, authoredMax * 0.35f);
            return math.clamp(Mathf.RoundToInt(math.lerp(minimum, authoredMax, quality01)), 1, authoredMax);
        }

        private bool TryPublishScavengerMatrices(Matrix4x4[] stagedMatrices, int matrixCount)
        {
            if (matrixCount <= 0)
                return true;

            if (stagedMatrices == null || stagedMatrices.Length < matrixCount)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsVaultHandleCreated(in _scavengerMatricesHandle) ||
                !vault.TryAcquireWriteLock(in _scavengerMatricesHandle, VaultOwnerSystemId, out NativeArray<Matrix4x4> scavengerMatrices))
            {
                return false;
            }

            try
            {
                if (!scavengerMatrices.IsCreated || scavengerMatrices.Length < matrixCount)
                    return false;

                for (int i = 0; i < matrixCount; i++)
                    scavengerMatrices[i] = stagedMatrices[i];

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _scavengerMatricesHandle, VaultOwnerSystemId);
            }
        }

        private bool TryReadScavengerMatrices(out NativeArray<Matrix4x4>.ReadOnly scavengerMatrices)
        {
            scavengerMatrices = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsVaultHandleCreated(in _scavengerMatricesHandle) &&
                   vault.TryReadOnlyHandle(in _scavengerMatricesHandle, out scavengerMatrices) &&
                   scavengerMatrices.IsCreated &&
                   scavengerMatrices.Length >= _scavengerInstanceCapacity;
        }

        private void UploadScavengerInstances(int instanceCount)
        {
            if (instanceCount > 0)
            {
                if (!TryReadScavengerMatrices(out NativeArray<Matrix4x4>.ReadOnly scavengerMatrices))
                    return;

                GraphicsBuffer writeBuffer = ResolveScavengerMatrixWriteBuffer();
                if (writeBuffer == null)
                    return;

                GraphicsBufferUploadUtility.UploadNativeArray(writeBuffer, scavengerMatrices, instanceCount);
                _activeScavengerMatrixBuffer = writeBuffer;
                _scavengerMatrixUploadIndex ^= 1;
            }
        }

        private GraphicsBuffer ResolveScavengerMatrixWriteBuffer()
        {
            GraphicsBuffer preferred = (_scavengerMatrixUploadIndex & 1) == 0
                ? _scavengerMatrixBufferB
                : _scavengerMatrixBufferA;
            if (preferred != null && preferred.IsValid())
                return preferred;

            return _scavengerMatrixBufferA != null && _scavengerMatrixBufferA.IsValid()
                ? _scavengerMatrixBufferA
                : _scavengerMatrixBufferB;
        }

        private void DrawScavengers()
        {
            if (_debugScavengerInstanceCount <= 0 || _activeScavengerMatrixBuffer == null || _scavengerBatchRendererGroup == null)
                return;

            Mesh activeMesh = _authoredSargassumMesh;
            Material activeMaterial = GetPreparedScavengerBrgMaterial();
            if (activeMesh == null || activeMaterial == null)
                return;
            if (!IsScavengerBatchRegistrationReady(activeMesh, activeMaterial))
                return;

            if (!ReferenceEquals(_boundScavengerMatrixMaterial, activeMaterial) ||
                !ReferenceEquals(_boundScavengerMatrixBuffer, _activeScavengerMatrixBuffer))
            {
                activeMaterial.SetBuffer(_ScavengerMatricesId, _activeScavengerMatrixBuffer);
                _boundScavengerMatrixMaterial = activeMaterial;
                _boundScavengerMatrixBuffer = _activeScavengerMatrixBuffer;
            }

            SyncScavengerBatchBuffer(_activeScavengerMatrixBuffer);
            if (!_scavengerDrawBoundsRegistered ||
                _registeredScavengerDrawBounds.center != _scavengerDrawBounds.center ||
                _registeredScavengerDrawBounds.size != _scavengerDrawBounds.size)
            {
                _scavengerBatchRendererGroup.SetGlobalBounds(_scavengerDrawBounds);
                _registeredScavengerDrawBounds = _scavengerDrawBounds;
                _scavengerDrawBoundsRegistered = true;
            }
        }

        private Material PrepareScavengerBrgMaterialCold()
        {
            Material sourceMaterial = _authoredSargassumMaterial;
            if (sourceMaterial == null)
                return null;

            if (_scavengerBrgMaterial != null && _scavengerBrgMaterialSource == sourceMaterial)
                return _scavengerBrgMaterial;

            ReleaseScavengerBrgMaterial();
            _scavengerBrgMaterial = sourceMaterial;
            _scavengerBrgMaterialSource = sourceMaterial;
            return _scavengerBrgMaterial;
        }

        private Material GetPreparedScavengerBrgMaterial()
        {
            Material sourceMaterial = _authoredSargassumMaterial;
            if (sourceMaterial == null ||
                _scavengerBrgMaterial == null ||
                _scavengerBrgMaterialSource != sourceMaterial)
            {
                return null;
            }

            return _scavengerBrgMaterial;
        }

        private bool IsScavengerBatchRegistrationReady(Mesh activeMesh, Material activeMaterial)
        {
            return _scavengerBatchRendererGroup != null &&
                   _registeredScavengerMesh == activeMesh &&
                   _registeredScavengerMaterial == activeMaterial &&
                   !_scavengerBatchMeshId.Equals(default) &&
                   !_scavengerBatchMaterialId.Equals(default);
        }

        private void ReleaseScavengerBrgMaterial()
        {
            _scavengerBrgMaterial = null;
            _scavengerBrgMaterialSource = null;
        }

        private static void DestroyOwnedRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(target);
                return;
            }
#endif
            Destroy(target);
        }

        private void SyncScavengerBatchRegistration(Mesh activeMesh, Material activeMaterial)
        {
            if (_scavengerBatchRendererGroup == null)
                return;

            if (_registeredScavengerMesh != activeMesh)
            {
                if (!_scavengerBatchMeshId.Equals(default))
                    _scavengerBatchRendererGroup.UnregisterMesh(_scavengerBatchMeshId);

                _scavengerBatchMeshId = activeMesh != null ? _scavengerBatchRendererGroup.RegisterMesh(activeMesh) : default;
                _registeredScavengerMesh = activeMesh;
            }

            if (_registeredScavengerMaterial != activeMaterial)
            {
                if (!_scavengerBatchMaterialId.Equals(default))
                    _scavengerBatchRendererGroup.UnregisterMaterial(_scavengerBatchMaterialId);

                _scavengerBatchMaterialId = activeMaterial != null ? _scavengerBatchRendererGroup.RegisterMaterial(activeMaterial) : default;
                _registeredScavengerMaterial = activeMaterial;
            }
        }

        private void SyncScavengerBatchBuffer(GraphicsBuffer matrixBuffer)
        {
            if (_scavengerBatchRendererGroup == null || _scavengerBatchId.Equals(default) || matrixBuffer == null)
                return;

            if (ReferenceEquals(_registeredScavengerBatchBuffer, matrixBuffer))
                return;

            _scavengerBatchRendererGroup.SetBatchBuffer(_scavengerBatchId, matrixBuffer.bufferHandle);
            _registeredScavengerBatchBuffer = matrixBuffer;
        }

        private JobHandle OnPerformScavengerCulling(
            BatchRendererGroup rendererGroup,
            BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput,
            IntPtr userContext)
        {
            int instanceCount = _debugScavengerInstanceCount;
            if (instanceCount <= 0 ||
                _scavengerBatchId.Equals(default) ||
                _scavengerBatchMeshId.Equals(default) ||
                _scavengerBatchMaterialId.Equals(default))
            {
                HectonBatchRendererGroupUtility.WriteDirectDrawOutput(
                    cullingOutput,
                    HectonBatchRendererGroupUtility.AllocateDirectDrawOutput(0, 0, 0));
                return default;
            }

            if (!HectonBatchRendererGroupUtility.IsBoundsVisible(cullingContext.cullingPlanes, _scavengerDrawBounds))
            {
                HectonBatchRendererGroupUtility.WriteDirectDrawOutput(
                    cullingOutput,
                    HectonBatchRendererGroupUtility.AllocateDirectDrawOutput(0, 0, 0));
                return default;
            }

            HectonBatchRendererGroupUtility.WriteAllVisibleSingleDrawOutput(
                cullingOutput,
                instanceCount,
                _scavengerBatchId,
                _scavengerBatchMeshId,
                _scavengerBatchMaterialId,
                _cachedRenderLayer,
                subMeshIndex: 0,
                ShadowCastingMode.Off,
                receiveShadows: false,
                MotionVectorGenerationMode.Camera);
            return default;
        }

        private void RefreshRenderLayerCache()
        {
            _cachedRenderLayer = gameObject.layer;
        }

        private int ResolveNestedPrototypeIndex(SargassumFieldSample fieldSample, int sampleX, int sampleZ)
        {
            if (_activeNestingPrototypes == null || _activeNestingPrototypes.Length == 0)
                return -1;

            int totalWeight = 0;
            for (int i = 0; i < _activeNestingPrototypes.Length; i++)
            {
                NestedAttachmentPrototype prototype = _activeNestingPrototypes[i];
                if (prototype.Mesh == null || prototype.Material == null)
                    continue;

                float densityThreshold = prototype.DensityThreshold > 0f ? Mathf.Clamp01(prototype.DensityThreshold) : 0.56f;
                float windowThreshold = prototype.WindowThreshold > 0f ? Mathf.Clamp01(prototype.WindowThreshold) : 0.18f;
                if (fieldSample.Density01 < densityThreshold)
                    continue;

                if (fieldSample.Window01 > windowThreshold)
                    continue;

                totalWeight += Mathf.Max(1, prototype.Weight);
            }

            if (totalWeight <= 0)
                return -1;

            float pick = HashToFloat01((uint)sampleX, (uint)sampleZ, 0x64A8B875u) * totalWeight;
            float cursor = 0f;
            for (int i = 0; i < _activeNestingPrototypes.Length; i++)
            {
                NestedAttachmentPrototype prototype = _activeNestingPrototypes[i];
                if (prototype.Mesh == null || prototype.Material == null)
                    continue;

                float densityThreshold = prototype.DensityThreshold > 0f ? Mathf.Clamp01(prototype.DensityThreshold) : 0.56f;
                float windowThreshold = prototype.WindowThreshold > 0f ? Mathf.Clamp01(prototype.WindowThreshold) : 0.18f;
                if (fieldSample.Density01 < densityThreshold)
                    continue;

                if (fieldSample.Window01 > windowThreshold)
                    continue;

                cursor += Mathf.Max(1, prototype.Weight);
                if (pick <= cursor)
                    return i;
            }

            return -1;
        }

        private void UpdateNestedAttachmentBatches(float dt)
        {
            if (_nestedAttachmentStates == null || _nestedMatrixCounts == null || _nestedMatricesByPrototype == null)
                return;

            for (int i = 0; i < _nestedMatrixCounts.Length; i++)
                _nestedMatrixCounts[i] = 0;

            SargassumCutManager cutManager = _cutManager;
            bool boundsInitialized = false;
            Vector3 boundsMin = default;
            Vector3 boundsMax = default;
            float safeDeltaTime = math.isfinite(dt) ? math.max(0f, dt) : 0f;
            int totalVisible = 0;

            for (int stateIndex = 0; stateIndex < _activeNestedAttachmentStateCount; stateIndex++)
            {
                NestedAttachmentState state = _nestedAttachmentStates[stateIndex];
                if ((uint)state.PrototypeIndex >= (uint)_activeNestingPrototypes.Length)
                    continue;

                DisruptionSample disruption = SampleDisruptionNoDrift(state.SampleSpaceAnchorWS);
                Vector3 worldPosition = state.SampleSpaceAnchorWS + _globalDriftOffset + state.RenderOffsetWS;
                bool released = state.ReleasedFlag != 0;
                if (!released && disruption.SinkDepthWS > 0f)
                    worldPosition.y -= disruption.SinkDepthWS;
                if (!released &&
                    cutManager != null &&
                    cutManager.SampleRecentCut01(worldPosition, nestingCutReleaseRadius, out float cut01) &&
                    cut01 >= nestingCutReleaseThreshold)
                {
                    Vector3 releaseDirection = new Vector3(
                        LerpClamped(-1f, 1f, HashToFloat01((uint)stateIndex, 0xB5C0FBCFu, 0x51ED270Bu)),
                        0f,
                        LerpClamped(-1f, 1f, HashToFloat01((uint)stateIndex, 0x9E3779B9u, 0xA54FF53Au)));
                    releaseDirection = NormalizeVector3Fast(releaseDirection, Vector3.right);
                    released = true;
                    state.ReleasedFlag = 1;
                    state.ReleaseAge = 0f;
                    state.ReleaseVelocityWS = new Vector3(
                        releaseDirection.x * nestingReleaseHorizontalSpeed,
                        -nestingReleaseDownwardSpeed,
                        releaseDirection.z * nestingReleaseHorizontalSpeed);
                }

                if (released)
                {
                    state.ReleaseAge += safeDeltaTime;
                    if (state.ReleaseAge >= state.ReleaseLifetime)
                    {
                        _nestedAttachmentStates[stateIndex] = state;
                        continue;
                    }

                    state.ReleaseVelocityWS.y -= nestingReleaseGravity * safeDeltaTime;
                    state.RenderOffsetWS += state.ReleaseVelocityWS * safeDeltaTime;
                    worldPosition = state.SampleSpaceAnchorWS + _globalDriftOffset + state.RenderOffsetWS;
                }

                int prototypeIndex = state.PrototypeIndex;
                int matrixCount = _nestedMatrixCounts[prototypeIndex];
                if (matrixCount >= _nestedMatricesByPrototype[prototypeIndex].Length)
                {
                    _nestedAttachmentStates[stateIndex] = state;
                    continue;
                }

                Matrix4x4 matrix = BuildUniformMatrix(worldPosition, state.Rotation, state.UniformScale);
                _nestedMatricesByPrototype[prototypeIndex][matrixCount] = matrix;
                _nestedMatrixCounts[prototypeIndex] = matrixCount + 1;
                totalVisible++;

                EncapsulateRadius(worldPosition, math.max(0.5f, state.UniformScale * 1.3f), ref boundsMin, ref boundsMax, ref boundsInitialized);

                _nestedAttachmentStates[stateIndex] = state;
            }

            _debugNestedAttachmentCount = totalVisible;
            _debugNestedAttachmentBounds = boundsInitialized ? BuildBoundsFromMinMax(boundsMin, boundsMax) : default;
        }

        private void OnValidate()
        {
            RefreshRenderLayerCache();
            _editorValidateDepth++;
            if (_editorValidateDepth > MaxEditorValidateDepth)
            {
                _editorValidateDepth--;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("SargassumGlobalDragManager editor validation watchdog tripped.", this);
#endif
                return;
            }

            try
            {
#if UNITY_EDITOR
                ValidateCollapseChunkPrefabAssignment();
#endif
                SanitizeSettings();
                if (!Application.isPlaying)
                {
                    _activeNestingPrototypes = _authoredNestingPrototypes != null && _authoredNestingPrototypes.Length > 0
                        ? _authoredNestingPrototypes
                        : null;
                    _activeNestedPrototypeCount = 0;
                    _activeNestedAttachmentStateCount = 0;
                    _activeScavengerHostCount = 0;
                    _activeExternalScavengerSiteCount = 0;
                    _debugNestedAttachmentCount = 0;
                    _debugNestedStateCount = 0;
                    _debugActiveDisruptionZones = 0;
                    return;
                }

                ResolveActiveNestingPrototypes();
                EnsureNestedAttachmentStorage();
                EnsureDisruptionStorage();
                EnsureScavengerStorage();
                CreateDensityTexture();
                ClearNestedAttachments();
                ClearScavengerHosts();
                ClearDensityTexture();
                ClearSinkTexture();
                PublishShaderGlobals();
            }
            finally
            {
                _editorValidateDepth--;
            }
        }

#if UNITY_EDITOR
        private void ValidateCollapseChunkPrefabAssignment()
        {
            if (collapseChunkPrefab != null)
            {
                if (!UsesUnityBuiltInPrimitiveMeshViaEditorGate(collapseChunkPrefab))
                    return;

                string assignedPath = UnityEditor.AssetDatabase.GetAssetPath(collapseChunkPrefab);
                collapseChunkPrefab = null;
                Hecton8.Core.H8Debug.LogError(
                    $"Sargassum collapse chunk prefab rejected: '{assignedPath}' uses Unity built-in primitive mesh. Real non-primitive collapse chunk art is required.",
                    this);
            }

            if (!AssetPathUsesUnityBuiltInPrimitiveMeshViaEditorGate(CollapseChunkFallbackPrefabPath))
            {
                collapseChunkPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(CollapseChunkFallbackPrefabPath);
                if (collapseChunkPrefab != null)
                    return;
            }

            Hecton8.Core.H8Debug.LogError(
                $"Sargassum collapse chunk fallback not assigned: '{CollapseChunkFallbackPrefabPath}' is missing or uses Unity built-in primitive mesh. Real non-primitive collapse chunk art is required.",
                this);
        }

        private bool UsesUnityBuiltInPrimitiveMeshViaEditorGate(GameObject prefab)
        {
            return InvokePrimitiveGateBool(nameof(UsesUnityBuiltInPrimitiveMeshViaEditorGate), "UsesUnityBuiltInPrimitiveMesh", prefab);
        }

        private bool AssetPathUsesUnityBuiltInPrimitiveMeshViaEditorGate(string assetPath)
        {
            return InvokePrimitiveGateBool(nameof(AssetPathUsesUnityBuiltInPrimitiveMeshViaEditorGate), "AssetPathUsesUnityBuiltInPrimitiveMesh", assetPath);
        }

        private bool InvokePrimitiveGateBool(string ownerMethod, string gateMethod, object argument)
        {
            System.Reflection.Assembly gateAssembly;
            try
            {
                gateAssembly = System.Reflection.Assembly.Load(FinalPrefabQualityGateAssemblyName);
            }
            catch (Exception exception)
            {
                Hecton8.Core.H8Debug.LogError(
                    $"Sargassum collapse chunk validation failed closed: cannot load {FinalPrefabQualityGateAssemblyName}.{FinalPrefabQualityGateTypeName} for {ownerMethod}. {exception.GetType().Name}: {exception.Message}",
                    this);
                return true;
            }

            Type gateType = gateAssembly.GetType(FinalPrefabQualityGateTypeName);
            System.Reflection.MethodInfo method = gateType?.GetMethod(
                gateMethod,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (method == null)
            {
                Hecton8.Core.H8Debug.LogError(
                    $"Sargassum collapse chunk validation failed closed: missing {FinalPrefabQualityGateTypeName}.{gateMethod}.",
                    this);
                return true;
            }

            try
            {
                object result = method.Invoke(null, new[] { argument });
                return result is bool usesPrimitive && usesPrimitive;
            }
            catch (Exception exception)
            {
                Hecton8.Core.H8Debug.LogError(
                    $"Sargassum collapse chunk validation failed closed: {FinalPrefabQualityGateTypeName}.{gateMethod} threw during {ownerMethod}. {exception.GetType().Name}: {exception.Message}",
                    this);
                return true;
            }
        }
#endif

        private void TryRegister()
        {
            if (Application.isPlaying && !_serviceRegistered)
                return;

            TryRegisterSaveOwner();

            if (!Application.isPlaying)
                return;

            if (!_registeredHotSwap)
                _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            }

            if (!_registeredSlowTick)
            {
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            }

            if (!_registeredLateFrameTick)
            {
                _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            }
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            GlobalRegistry.RegisterSargassumDragRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.SargassumDrag, this);
            if (_serviceRegistered)
                s_activeRuntimeInstance = this;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;
            _serviceRegistered = false;

            if (ReferenceEquals(GlobalRegistry.SargassumDrag, this))
                GlobalRegistry.UnregisterSargassumDragRuntime(this);
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            SargassumGlobalDragManager registered = GlobalRegistry.SargassumDrag;
            if (!ReferenceEquals(registered, null) && !ReferenceEquals(registered, this))
            {
                if (IsSargassumDragRuntimeUsable(registered))
                {
                    s_activeRuntimeInstance = registered;
                    Destroy(this);
                    return true;
                }

                if (ReferenceEquals(s_activeRuntimeInstance, registered))
                    s_activeRuntimeInstance = null;
                GlobalRegistry.UnregisterSargassumDragRuntime(registered);
            }

            SargassumGlobalDragManager active = s_activeRuntimeInstance;
            if (ReferenceEquals(active, null) || ReferenceEquals(active, this))
                return false;

            if (IsSargassumDragRuntimeUsable(active))
            {
                GlobalRegistry.RegisterSargassumDragRuntime(active);
                s_activeRuntimeInstance = active;
                Destroy(this);
                return true;
            }

            if (ReferenceEquals(s_activeRuntimeInstance, active))
                s_activeRuntimeInstance = null;
            GlobalRegistry.UnregisterSargassumDragRuntime(active);

            return false;
        }

        private static bool IsSargassumDragRuntimeUsable(SargassumGlobalDragManager manager)
        {
            return manager != null && manager._serviceRegistered && manager.isActiveAndEnabled;
        }

        private void ReconcileRuntimeOwnerFromRegistryReplacement(object previousService, object currentService)
        {
            if (currentService is SargassumGlobalDragManager currentRuntime)
            {
                s_activeRuntimeInstance = currentRuntime;
                bool ownsRuntime = ReferenceEquals(currentRuntime, this);
                _serviceRegistered = ownsRuntime;
                if (ownsRuntime)
                {
                    if (_runtimeRoutesRetiredAfterOwnershipLoss)
                        RestoreRuntimeRoutesAfterOwnershipGain();
                    return;
                }

                if (ReferenceEquals(previousService, this))
                    RetireRuntimeRoutesAfterOwnershipLoss();
                return;
            }

            if (ReferenceEquals(previousService, this))
            {
                _serviceRegistered = false;
                if (ReferenceEquals(s_activeRuntimeInstance, this))
                    s_activeRuntimeInstance = null;
                RetireRuntimeRoutesAfterOwnershipLoss();
            }
        }

        private void RetireRuntimeRoutesAfterOwnershipLoss()
        {
            if (_runtimeRoutesRetiredAfterOwnershipLoss)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _cutManager = null;
            TryUnregisterSaveOwner();
            _saveService = null;

            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTick = false;
            }

            _runtimeRoutesRetiredAfterOwnershipLoss = true;
        }

        private void RestoreRuntimeRoutesAfterOwnershipGain()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
                return;

            RefreshColdRegistryDependencies();
            HectonFloatingOrigin.RegisterListener(this);
            TryRegister();
            _runtimeRoutesRetiredAfterOwnershipLoss = false;
        }

        private void TryUnregister()
        {
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTick = false;
            }

            TryUnregisterSaveOwner();
            _saveService = null;

            if (_registeredHotSwap)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.SargassumDragRuntime:
                    ReconcileRuntimeOwnerFromRegistryReplacement(previousService, currentService);
                    break;
                case GlobalRegistryServiceSlot.SargassumCutRuntime:
                    _cutManager = currentService as SargassumCutManager;
                    WorldRuntimeReferenceUtility.TryResolveSargassumCutManager(ref _cutManager);
                    break;
                case GlobalRegistryServiceSlot.Save:
                    TryUnregisterSaveOwner();
                    _saveService = currentService as ISaveService;

                    if (Application.isPlaying && isActiveAndEnabled && _serviceRegistered)
                        TryRegisterSaveOwner();
                    break;
                case GlobalRegistryServiceSlot.ObjectPool:
                    CacheObjectPoolService(currentService as ObjectPoolManager);
                    break;
                case GlobalRegistryServiceSlot.Physics:
                    _physicsService = currentService as IPhysicsService;
                    break;
                case GlobalRegistryServiceSlot.MapMagicVegetationRuntime:
                    mapMagicVegetationBridge = currentService as HectonMapMagicVegetationBridge;
                    WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref mapMagicVegetationBridge);
                    _nestedAttachmentRebuildRequested = true;
                    _dynamicTextureRefreshRequested = true;
                    _dynamicTextureRefreshIncrementRevision = true;
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    ReleaseVaultBuffer(ref _scavengerMatricesHandle);
                    _dataVault = currentService as IDataVault;
                    break;
            }
        }

        private void RefreshColdRegistryDependencies()
        {
            _dataVault = GlobalRegistry.DataVault;
            WorldRuntimeReferenceUtility.TryResolveSargassumCutManager(ref _cutManager);
            CacheObjectPoolService(null);
            _physicsService = GlobalRegistry.Physics;
            WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref mapMagicVegetationBridge);
            _saveService = GlobalRegistry.Save;
        }

        private void CacheObjectPoolService(ObjectPoolManager candidate)
        {
            ObjectPoolManager pool = candidate;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(pool) ||
                ObjectPoolManager.TryResolveActiveRuntime(ref pool))
            {
                _objectPoolService = pool;
                return;
            }

            _objectPoolService = null;
        }

        private bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = _objectPoolService as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = cached;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                _objectPoolService = resolved;
                pool = resolved;
                return true;
            }

            _objectPoolService = null;
            pool = null;
            return false;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private void TryRegisterSaveOwner()
        {
            if (!Application.isPlaying ||
                _saveRegistered ||
                !_serviceRegistered ||
                !ReferenceEquals(s_activeRuntimeInstance, this))
            {
                return;
            }

            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _saveService = saveService;
            }

            if (!IsSaveServiceUsable(saveService))
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
            _saveRegistered = true;
        }

        private void TryUnregisterSaveOwner()
        {
            if (!_saveRegistered && _registeredSaveService == null)
                return;

            ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredSaveService = null;
            _saveRegistered = false;
        }

        private static float ExtractUniformScale(Matrix4x4 matrix)
        {
            Vector3 axisX = new Vector3(matrix.m00, matrix.m10, matrix.m20);
            Vector3 axisY = new Vector3(matrix.m01, matrix.m11, matrix.m21);
            Vector3 axisZ = new Vector3(matrix.m02, matrix.m12, matrix.m22);
            return Mathf.Max(0.1f, Mathf.Max(ApproximateMagnitude(axisX), Mathf.Max(ApproximateMagnitude(axisY), ApproximateMagnitude(axisZ))));
        }

        private static long PackCellKey(int cellX, int cellZ)
        {
            return ((long)cellX << 32) ^ (uint)cellZ;
        }

        private void SanitizeSettings()
        {
            cellSize = Mathf.Max(2f, cellSize);
            _inverseCellSize = 1f / cellSize;
            densityTextureResolution = Mathf.Clamp(densityTextureResolution, 64, 256);
            densityTextureBoundsPadding = Mathf.Max(0f, densityTextureBoundsPadding);
            maxNestedAttachmentCount = Mathf.Clamp(maxNestedAttachmentCount, 4, 128);
            maxNestedAttachmentAttempts = Mathf.Clamp(maxNestedAttachmentAttempts, 4, 24);
            denseNodeNestChancePercent = Mathf.Clamp(denseNodeNestChancePercent, 0f, 5f);
            nestingSampleRadius = Mathf.Clamp(nestingSampleRadius, 0.1f, 2.5f);
            nestingNodeDensityThreshold = Mathf.Clamp01(nestingNodeDensityThreshold);
            nestingNodeWindowThreshold = Mathf.Clamp01(nestingNodeWindowThreshold);
            nestingCutReleaseRadius = Mathf.Clamp(nestingCutReleaseRadius, 0.1f, 4f);
            nestingCutReleaseThreshold = Mathf.Clamp01(nestingCutReleaseThreshold);
            nestingReleaseLifetime = Mathf.Clamp(nestingReleaseLifetime, 0.2f, 8f);
            nestingReleaseHorizontalSpeed = Mathf.Clamp(nestingReleaseHorizontalSpeed, 0f, 6f);
            nestingReleaseDownwardSpeed = Mathf.Clamp(nestingReleaseDownwardSpeed, 0f, 4f);
            nestingReleaseGravity = Mathf.Clamp(nestingReleaseGravity, 0f, 4f);
            maxDisruptionZoneCount = Mathf.Clamp(maxDisruptionZoneCount, 1, MaxDisruptionZoneCapacity);
            buoyancyCollapseQueryRadius = Mathf.Clamp(buoyancyCollapseQueryRadius, 2f, 18f);
            buoyancyCollapseAreaThreshold = Mathf.Max(1f, buoyancyCollapseAreaThreshold);
            buoyancyCollapseCutStrengthThreshold = Mathf.Clamp01(buoyancyCollapseCutStrengthThreshold);
            buoyancyCollapseRadius = Mathf.Clamp(buoyancyCollapseRadius, 2f, 18f);
            buoyancyCollapseSinkDuration = Mathf.Clamp(buoyancyCollapseSinkDuration, 10f, 20f);
            buoyancyCollapseSinkDepth = Mathf.Clamp(buoyancyCollapseSinkDepth, 1f, 24f);
            collapseChunkAreaThresholdMultiplier = Mathf.Clamp(collapseChunkAreaThresholdMultiplier, 1f, 4f);
            collapseChunkSpawnCount = Mathf.Clamp(collapseChunkSpawnCount, 1, 6);
            collapseChunkSpawnRadius = Mathf.Clamp(collapseChunkSpawnRadius, 0.5f, 8f);
            collapseChunkHorizontalSpeed = Mathf.Clamp(collapseChunkHorizontalSpeed, 0f, 8f);
            collapseChunkDownwardSpeed = Mathf.Clamp(collapseChunkDownwardSpeed, 0f, 8f);
            collapseChunkLifetime = Mathf.Clamp(collapseChunkLifetime, 0.5f, 24f);
            collapseChunkScaleMin = Mathf.Clamp(collapseChunkScaleMin, 0.25f, 3f);
            collapseChunkScaleMax = Mathf.Max(collapseChunkScaleMin, Mathf.Clamp(collapseChunkScaleMax, 0.5f, 4f));
            collapseCascadeImpactThreshold = Mathf.Clamp(collapseCascadeImpactThreshold, 0.5f, 12f);
            collapseCascadeMaxDepth = Mathf.Clamp(collapseCascadeMaxDepth, 1, 3);
            collapseCascadeSpawnCount = Mathf.Clamp(collapseCascadeSpawnCount, 1, 4);
            collapseCascadeScaleMultiplier = Mathf.Clamp(collapseCascadeScaleMultiplier, 0.2f, 1f);
            collapseCascadeVelocityMultiplier = Mathf.Clamp(collapseCascadeVelocityMultiplier, 0.5f, 2f);
            massiveDisplacementCutStrength = Mathf.Clamp(massiveDisplacementCutStrength, 0.1f, 2f);
            massiveDisplacementRampDuration = Mathf.Clamp(massiveDisplacementRampDuration, 0.1f, 4f);
            massiveDisplacementFadeDuration = Mathf.Clamp(massiveDisplacementFadeDuration, 0.1f, 8f);
            massiveDisplacementExtremePanicRadius = Mathf.Clamp(massiveDisplacementExtremePanicRadius, 50f, 96f);
            maxScavengerHostCount = Mathf.Clamp(maxScavengerHostCount, 2, 24);
            scavengersPerHost = Mathf.Clamp(scavengersPerHost, 4, 32);
            scavengerActivationDelay = Mathf.Clamp(scavengerActivationDelay, 30f, 90f);
            scavengerConsumeDuration = Mathf.Clamp(scavengerConsumeDuration, 4f, 30f);
            scavengerOrbitRadius = Mathf.Clamp(scavengerOrbitRadius, 0.2f, 4f);
            scavengerVerticalOffset = Mathf.Clamp(scavengerVerticalOffset, -1f, 1f);
            scavengerBobAmplitude = Mathf.Clamp(scavengerBobAmplitude, 0f, 0.5f);
            scavengerScaleMin = Mathf.Clamp(scavengerScaleMin, 0.02f, 0.4f);
            scavengerScaleMax = Mathf.Max(scavengerScaleMin, Mathf.Clamp(scavengerScaleMax, 0.04f, 0.6f));
            maxExternalScavengerSiteCount = Mathf.Clamp(maxExternalScavengerSiteCount, 1, 16);
            externalScavengerSiteDuration = Mathf.Clamp(externalScavengerSiteDuration, 4f, 90f);
            fallbackPatchThreshold = Mathf.Clamp01(fallbackPatchThreshold);
            canopyFlowAnisotropy = Mathf.Clamp(canopyFlowAnisotropy, 0.2f, 1f);
            canopyPrimaryCellSize = Mathf.Max(4f, canopyPrimaryCellSize);
            canopySecondaryCellSize = Mathf.Max(4f, canopySecondaryCellSize);
            canopyWallWidth = Mathf.Max(0.25f, canopyWallWidth);
            canopyWarpMeters = Mathf.Max(0f, canopyWarpMeters);
            canopyWarpNoiseScale = Mathf.Max(0.0001f, canopyWarpNoiseScale);
            entanglementMinDensity = Mathf.Clamp01(entanglementMinDensity);
            entanglementSpeedThreshold = Mathf.Max(0.25f, entanglementSpeedThreshold);
            maxEntanglementStrength = Mathf.Clamp01(maxEntanglementStrength);
            alongFlowDragScale = Mathf.Clamp(alongFlowDragScale, 0.1f, 1f);
            _fallbackLabyrinthConfig = new HectonMapMagicVegetationBridge.FloatingLabyrinthConfig(
                fallbackPatchThreshold,
                canopyWarpNoiseScale,
                canopyPrimaryCellSize,
                canopySecondaryCellSize,
                canopyWallWidth,
                canopyWarpMeters,
                NormalizeFlowDirection(canopyFlowDirection),
                canopyFlowAnisotropy);
        }

        private float ResolveDirectionalDragScale(Vector3 movementVelocityWS)
        {
            Vector2 movementXZ = new Vector2(movementVelocityWS.x, movementVelocityWS.z);
            float movementMagnitudeSq = movementXZ.sqrMagnitude;
            if (movementMagnitudeSq <= 0.0001f)
                return 1f;

            HectonMapMagicVegetationBridge.FloatingLabyrinthConfig config = ResolveLabyrinthConfig();
            Vector2 flowDirection = NormalizeFlowDirection(config.FlowDirection);
            if (flowDirection.sqrMagnitude <= 0.0001f)
                return 1f;

            movementXZ *= math.rsqrt(movementMagnitudeSq);
            float alongFlow01 = Mathf.Abs(Vector2.Dot(movementXZ, flowDirection));
            float crossFlow01 = 1f - alongFlow01;
            float anisotropy = Mathf.Clamp01(config.FlowAnisotropy);
            float directionalBlend = 1f + (crossFlow01 - 1f) * anisotropy;
            return alongFlowDragScale + (1f - alongFlowDragScale) * directionalBlend;
        }

        private float ResolveEntanglement01(float density01, float currentSpeed)
        {
            float densityGate = Mathf.InverseLerp(entanglementMinDensity, 1f, density01);
            if (densityGate <= 0f)
                return 0f;

            float speedGate = 1f - Mathf.Clamp01(currentSpeed / Mathf.Max(0.01f, entanglementSpeedThreshold));
            if (speedGate <= 0f)
                return 0f;

            return densityGate * speedGate * maxEntanglementStrength;
        }

        private float EvaluateCanopyWindow01(Vector3 sampledPositionWS)
        {
            float occupancy = EvaluateCanopyOccupancy(sampledPositionWS.x, sampledPositionWS.z);
            return Mathf.Clamp01(1f - occupancy);
        }

        private float EvaluateCanopyOccupancy(float worldX, float worldZ)
        {
            HectonMapMagicVegetationBridge.FloatingLabyrinthConfig config = ResolveLabyrinthConfig();
            HectonMapMagicVegetationBridge.EvaluateFloatingLabyrinth(
                worldX,
                worldZ,
                HectonVegetationConstants.FloatingLabyrinthSamplingSeed,
                config,
                out float occupancy);
            return Mathf.Clamp01(occupancy);
        }

        private HectonMapMagicVegetationBridge.FloatingLabyrinthConfig ResolveLabyrinthConfig()
        {
            if (mapMagicVegetationBridge != null)
                return mapMagicVegetationBridge.ActiveFloatingLabyrinthConfig;

            return _fallbackLabyrinthConfig;
        }

        private static Vector2 NormalizeFlowDirection(Vector2 direction)
        {
            float magnitudeSq = direction.sqrMagnitude;
            if (magnitudeSq <= 0.0001f)
                return Vector2.right;

            return direction * math.rsqrt(magnitudeSq);
        }

        private static Vector3 NormalizeVector3Fast(Vector3 vector, Vector3 fallback)
        {
            float magnitudeSq = vector.sqrMagnitude;
            return magnitudeSq > 0.0001f ? vector * math.rsqrt(magnitudeSq) : fallback;
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static bool IsFiniteMassiveDisplacementSignal(in MassiveDisplacementSignal signal)
        {
            return IsFiniteVector3(signal.PositionWS) &&
                   math.isfinite(signal.RadiusWS) &&
                   math.isfinite(signal.Duration) &&
                   math.isfinite(signal.ExtremePanicRadiusWS) &&
                   signal.RadiusWS > 0.001f &&
                   signal.Duration > 0.001f &&
                   signal.ExtremePanicRadiusWS > 0.001f;
        }

        private static bool IsFiniteEntanglementStrainSignal(in EntanglementStrainSignal signal)
        {
            return IsFiniteVector3(signal.PositionWS) &&
                   IsFiniteVector3(signal.AnchorWS) &&
                   math.isfinite(signal.Tension01) &&
                   math.isfinite(signal.EscapeIntent01) &&
                   math.isfinite(signal.Shake01) &&
                   signal.Tension01 >= 0f &&
                   signal.EscapeIntent01 >= 0f &&
                   signal.Shake01 >= 0f;
        }

        private static bool IsFiniteDisruptionZone(in DisruptionZoneState zone)
        {
            return (zone.Mode == (byte)DisruptionZoneMode.CutCollapse ||
                    zone.Mode == (byte)DisruptionZoneMode.MassiveDisplacement) &&
                   IsFiniteVector3(zone.SampleSpaceCenterWS) &&
                   math.isfinite(zone.RadiusWS) &&
                   math.isfinite(zone.Strength01) &&
                   math.isfinite(zone.SinkDepthWS) &&
                   math.isfinite(zone.RampDuration) &&
                   math.isfinite(zone.HoldDuration) &&
                   math.isfinite(zone.FadeDuration) &&
                   math.isfinite(zone.Age) &&
                   zone.RadiusWS > 0f &&
                   zone.Strength01 >= 0f &&
                   zone.SinkDepthWS >= 0f &&
                   zone.RampDuration > 0f &&
                   zone.HoldDuration >= 0f &&
                   zone.FadeDuration >= 0f;
        }

        private static void EncapsulateRadius(Vector3 center, float radius, ref Vector3 boundsMin, ref Vector3 boundsMax, ref bool boundsInitialized)
        {
            Vector3 extents = new Vector3(radius, radius, radius);
            EncapsulateExtents(center, extents, ref boundsMin, ref boundsMax, ref boundsInitialized);
        }

        private static void EncapsulateExtents(Vector3 center, Vector3 extents, ref Vector3 boundsMin, ref Vector3 boundsMax, ref bool boundsInitialized)
        {
            Vector3 localMin = center - extents;
            Vector3 localMax = center + extents;
            if (!boundsInitialized)
            {
                boundsMin = localMin;
                boundsMax = localMax;
                boundsInitialized = true;
                return;
            }

            boundsMin = new Vector3(
                math.min(boundsMin.x, localMin.x),
                math.min(boundsMin.y, localMin.y),
                math.min(boundsMin.z, localMin.z));
            boundsMax = new Vector3(
                math.max(boundsMax.x, localMax.x),
                math.max(boundsMax.y, localMax.y),
                math.max(boundsMax.z, localMax.z));
        }

        private static Bounds BuildBoundsFromMinMax(Vector3 boundsMin, Vector3 boundsMax)
        {
            Vector3 size = boundsMax - boundsMin;
            return new Bounds(boundsMin + size * 0.5f, size);
        }

        private static int RoundToIntPositive(float value)
        {
            if (!math.isfinite(value) || value <= 0f)
                return 0;

            return value >= int.MaxValue ? int.MaxValue : (int)(value + 0.5f);
        }

        private static Vector3 ResolveHashPlanarDirection(uint index, uint iteration, uint saltX, uint saltZ, Vector3 fallback)
        {
            Vector3 direction = new Vector3(
                HashToSignedUnit(index, iteration, saltX),
                0f,
                HashToSignedUnit(index, iteration, saltZ));
            return NormalizeVector3Fast(direction, fallback);
        }

        private static Vector3 ResolveHashTangentDirection(
            uint index,
            uint iteration,
            uint saltX,
            uint saltZ,
            Vector3 tangent,
            Vector3 bitangent)
        {
            Vector3 direction =
                tangent * HashToSignedUnit(index, iteration, saltX) +
                bitangent * HashToSignedUnit(index, iteration, saltZ);
            return NormalizeVector3Fast(direction, tangent);
        }

        private static Vector3 ResolveDiamondOrbitOffset(float phase01, float radius)
        {
            float phase = Frac01(phase01) * 4f;
            if (phase < 1f)
                return new Vector3(1f - phase, 0f, phase) * radius;

            if (phase < 2f)
            {
                float local = phase - 1f;
                return new Vector3(-local, 0f, 1f - local) * radius;
            }

            if (phase < 3f)
            {
                float local = phase - 2f;
                return new Vector3(local - 1f, 0f, -local) * radius;
            }

            float finalLocal = phase - 3f;
            return new Vector3(finalLocal, 0f, finalLocal - 1f) * radius;
        }

        private static Vector3 ResolveDiamondOrbitForward(float phase01)
        {
            float phase = Frac01(phase01) * 4f;
            if (phase < 1f)
                return new Vector3(-1f, 0f, 1f);

            if (phase < 2f)
                return new Vector3(-1f, 0f, -1f);

            if (phase < 3f)
                return new Vector3(1f, 0f, -1f);

            return new Vector3(1f, 0f, 1f);
        }

        private static Quaternion ResolveCardinalYawRotation(Vector3 forward)
        {
            if (math.abs(forward.x) > math.abs(forward.z))
                return forward.x >= 0f ? _yawPositiveX : _yawNegativeX;

            return forward.z >= 0f ? _yawPositiveZ : _yawNegativeZ;
        }

        private static Matrix4x4 BuildScavengerCardinalMatrix(Vector3 positionWS, float orbitPhase01, float uniformScale)
        {
            float phase = Frac01(orbitPhase01) * 4f;
            float forwardSign = phase < 1f || phase >= 3f ? 1f : -1f;
            float signedScale = uniformScale * forwardSign;

            Matrix4x4 matrix = default;
            matrix.m00 = signedScale;
            matrix.m11 = uniformScale;
            matrix.m22 = signedScale;
            matrix.m03 = positionWS.x;
            matrix.m13 = positionWS.y;
            matrix.m23 = positionWS.z;
            matrix.m33 = 1f;
            return matrix;
        }

        private static Matrix4x4 BuildUniformMatrix(Vector3 positionWS, Quaternion rotation, float uniformScale)
        {
            float x2 = rotation.x + rotation.x;
            float y2 = rotation.y + rotation.y;
            float z2 = rotation.z + rotation.z;
            float xx = rotation.x * x2;
            float yy = rotation.y * y2;
            float zz = rotation.z * z2;
            float xy = rotation.x * y2;
            float xz = rotation.x * z2;
            float yz = rotation.y * z2;
            float wx = rotation.w * x2;
            float wy = rotation.w * y2;
            float wz = rotation.w * z2;

            Matrix4x4 matrix = default;
            matrix.m00 = (1f - (yy + zz)) * uniformScale;
            matrix.m01 = (xy - wz) * uniformScale;
            matrix.m02 = (xz + wy) * uniformScale;
            matrix.m03 = positionWS.x;
            matrix.m10 = (xy + wz) * uniformScale;
            matrix.m11 = (1f - (xx + zz)) * uniformScale;
            matrix.m12 = (yz - wx) * uniformScale;
            matrix.m13 = positionWS.y;
            matrix.m20 = (xz - wy) * uniformScale;
            matrix.m21 = (yz + wx) * uniformScale;
            matrix.m22 = (1f - (xx + yy)) * uniformScale;
            matrix.m23 = positionWS.z;
            matrix.m33 = 1f;
            return matrix;
        }

        private static float TriangleWaveSigned01(float phase01)
        {
            float cycle = Frac01(phase01);
            return (1f - math.abs(cycle * 2f - 1f)) * 2f - 1f;
        }

        private static float Frac01(float value)
        {
            return value - math.floor(value);
        }

        private static float HashToSignedUnit(uint index, uint iteration, uint salt)
        {
            return HashToFloat01(index, iteration, salt) * 2f - 1f;
        }

        private static float LerpClamped(float from, float to, float t)
        {
            return from + ((to - from) * math.saturate(t));
        }

        private static Vector3 LerpVector3Clamped(Vector3 from, Vector3 to, float t)
        {
            float clampedT = math.saturate(t);
            return from + ((to - from) * clampedT);
        }

        private static float FastDensityFalloff01(float weight01, float distancePower)
        {
            float x = math.saturate(weight01);
            float x2 = x * x;
            float x4 = x2 * x2;
            float blend12 = math.saturate(distancePower - 1f);
            float blend24 = math.saturate((distancePower - 2f) * 0.5f);
            return math.lerp(math.lerp(x, x2, blend12), x4, blend24);
        }

        private static float FastPositivePowInt(float baseValue, int exponent)
        {
            float safeBase = math.max(0f, baseValue);
            int safeExponent = math.max(0, exponent);
            float result = 1f;
            for (int i = 0; i < safeExponent; i++)
                result *= safeBase;

            return result;
        }

        private static float ApproximateMagnitude(Vector3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float max = math.max(ax, math.max(ay, az));
            float min = math.min(ax, math.min(ay, az));
            float mid = ax + ay + az - max - min;
            return max + mid * 0.375f + min * 0.125f;
        }

        private void CreateDensityTexture()
        {
            _cachedGlobalQualityWeight01 = ResolveSargassumQualityWeight01(_cachedGlobalQualityWeight01);
            int resolution = ResolveDensityTextureResolution();
            int texelCount = resolution * resolution;

            if (_densityTextureRaw == null || _densityTextureRaw.Length != texelCount)
            {
                // COLD ALLOC: byte[resolution*resolution] - CPU density texture staging buffer - owner: SargassumGlobalDragManager
                _densityTextureRaw = new byte[texelCount];
            }

            if (_sinkTextureRaw == null || _sinkTextureRaw.Length != texelCount)
            {
                // COLD ALLOC: byte[resolution*resolution] - CPU buoyancy sink texture staging buffer - owner: SargassumGlobalDragManager
                _sinkTextureRaw = new byte[texelCount];
            }

            if (_densityFieldTexture == null ||
                _densityFieldTexture.width != resolution ||
                _densityFieldTexture.height != resolution)
            {
                ReleaseDensityTexture();
                _densityFieldTexture = new Texture2D(resolution, resolution, TextureFormat.R8, false, true)
                {
                    name = "__SargassumDensityField",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                }; // COLD ALLOC: Texture2D[1] - baked sargassum density map - owner: SargassumGlobalDragManager
                _densityTextureHasUploadedContent = true;
            }

            if (_sinkFieldTexture == null ||
                _sinkFieldTexture.width != resolution ||
                _sinkFieldTexture.height != resolution)
            {
                ReleaseSinkTexture();
                _sinkFieldTexture = new Texture2D(resolution, resolution, TextureFormat.R8, false, true)
                {
                    name = "__SargassumBuoyancySink",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                }; // COLD ALLOC: Texture2D[1] - baked sargassum buoyancy sink map - owner: SargassumGlobalDragManager
                _sinkTextureHasUploadedContent = true;
            }
        }

        private void ReleaseDensityTexture()
        {
            if (_densityFieldTexture == null)
                return;

            DestroyOwnedRuntimeObject(_densityFieldTexture);
            _densityFieldTexture = null;
            _densityTextureUploadDirty = false;
            _densityTextureHasUploadedContent = false;
        }

        private void ReleaseSinkTexture()
        {
            if (_sinkFieldTexture == null)
                return;

            DestroyOwnedRuntimeObject(_sinkFieldTexture);
            _sinkFieldTexture = null;
            _sinkTextureUploadDirty = false;
            _sinkTextureHasUploadedContent = false;
        }

        private void ClearDensityTexture()
        {
            if (_densityFieldTexture == null || _densityTextureRaw == null)
                return;
            if (!_densityTextureHasUploadedContent)
                return;

            Array.Clear(_densityTextureRaw, 0, _densityTextureRaw.Length);
            _densityFieldTexture.LoadRawTextureData(_densityTextureRaw);
            _densityTextureUploadDirty = true;
            _densityTextureHasUploadedContent = false;
        }

        private void ClearSinkTexture()
        {
            if (_sinkFieldTexture == null || _sinkTextureRaw == null)
                return;
            if (!_sinkTextureHasUploadedContent)
                return;

            Array.Clear(_sinkTextureRaw, 0, _sinkTextureRaw.Length);
            _sinkFieldTexture.LoadRawTextureData(_sinkTextureRaw);
            _sinkTextureUploadDirty = true;
            _sinkTextureHasUploadedContent = false;
        }

        private void RefreshDynamicTextures(bool incrementRevision)
        {
            if (!HasDensityTextureResourcesReady())
            {
                return;
            }

            if (incrementRevision)
                _fieldRevision++;

            if (!_hasFieldData || _debugFieldBounds.size.sqrMagnitude <= 0.0001f)
            {
                _densityFieldWorldRect = Vector4.zero;
                _debugDensityFieldWorldRect = Vector4.zero;
                ClearDensityTexture();
                ClearSinkTexture();
                _debugMaxSinkDepth = 0f;
                return;
            }

            Vector3 boundsCenter = _debugFieldBounds.center;
            float halfExtent = Mathf.Max(_debugFieldBounds.extents.x, _debugFieldBounds.extents.z) + densityTextureBoundsPadding;
            float worldSize = Mathf.Max(halfExtent * 2f, cellSize * 2f);
            float minX = boundsCenter.x - halfExtent;
            float minZ = boundsCenter.z - halfExtent;
            float texelWorldSize = worldSize / _densityFieldTexture.width;
            float sampleRadius = Mathf.Max(texelWorldSize * 0.65f, cellSize * 0.35f);
            float sampleY = Mathf.Clamp(boundsCenter.y, _debugFieldBounds.min.y, _debugFieldBounds.max.y);

            _densityFieldWorldRect = new Vector4(
                minX,
                minZ,
                1f / Mathf.Max(worldSize, 0.001f),
                1f / Mathf.Max(worldSize, 0.001f));
            _debugDensityFieldWorldRect = _densityFieldWorldRect;

            int resolution = _densityFieldTexture.width;
            float maxSinkDepthWS = ResolveMaximumSinkDepthWS();
            for (int y = 0; y < resolution; y++)
            {
                float worldZ = minZ + (y + 0.5f) * texelWorldSize;
                int rowStart = y * resolution;

                for (int x = 0; x < resolution; x++)
                {
                    float worldX = minX + (x + 0.5f) * texelWorldSize;
                    Vector3 sampledPositionWS = new Vector3(worldX, sampleY, worldZ);
                    float density01 = SampleDensity01NoDrift(sampledPositionWS, sampleRadius);
                    float occupancy01 = EvaluateCanopyOccupancy(worldX, worldZ);
                    DisruptionSample disruption = SampleDisruptionNoDrift(sampledPositionWS);
                    float encodedDensity01 = Mathf.Max(density01, occupancy01) * (1f - disruption.Suppression01);
                    _densityTextureRaw[rowStart + x] = (byte)Mathf.Clamp(Mathf.RoundToInt(encodedDensity01 * 255f), 0, 255);
                    float encodedSink01 = maxSinkDepthWS > 0f ? Mathf.Clamp01(disruption.SinkDepthWS / maxSinkDepthWS) : 0f;
                    _sinkTextureRaw[rowStart + x] = (byte)Mathf.Clamp(Mathf.RoundToInt(encodedSink01 * 255f), 0, 255);
                }
            }

            _densityFieldTexture.LoadRawTextureData(_densityTextureRaw);
            _sinkFieldTexture.LoadRawTextureData(_sinkTextureRaw);
            _densityTextureUploadDirty = true;
            _sinkTextureUploadDirty = true;
            _densityTextureHasUploadedContent = true;
            _sinkTextureHasUploadedContent = true;
            _debugMaxSinkDepth = maxSinkDepthWS;
        }

        private bool HasDensityTextureResourcesReady()
        {
            int resolution = ResolveDensityTextureResolution();
            int texelCount = resolution * resolution;
            return _densityFieldTexture != null &&
                   _densityFieldTexture.width == resolution &&
                   _densityFieldTexture.height == resolution &&
                   _densityTextureRaw != null &&
                   _densityTextureRaw.Length == texelCount &&
                   _sinkFieldTexture != null &&
                   _sinkFieldTexture.width == resolution &&
                   _sinkFieldTexture.height == resolution &&
                   _sinkTextureRaw != null &&
                   _sinkTextureRaw.Length == texelCount;
        }

        private int ResolveDensityTextureResolution()
        {
            int authoredMax = Mathf.Clamp(densityTextureResolution, MinDensityTextureResolution, MaxDensityTextureResolution);
            float quality01 = Smooth01(_cachedGlobalQualityWeight01);
            int continuous = Mathf.RoundToInt(math.lerp(MinDensityTextureResolution, authoredMax, quality01));
            int aligned = math.max(MinDensityTextureResolution, ((continuous + 15) / 16) * 16);
            _resolvedDensityTextureResolution = math.min(aligned, authoredMax);
            return _resolvedDensityTextureResolution;
        }

        private int ResolveDynamicTextureRefreshStrideFrames()
        {
            float quality01 = Smooth01(_cachedGlobalQualityWeight01);
            float resolved = math.lerp(MaxDynamicTextureRefreshStrideFrames, MinDynamicTextureRefreshStrideFrames, quality01);
            return math.max(1, Mathf.RoundToInt(resolved));
        }

        private bool CanRefreshDynamicTexturesThisFrame()
        {
            int frameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            return frameIndex >= _nextDynamicTextureRefreshFrame;
        }

        private static float ResolveSargassumQualityWeight01(float fallback01)
        {
            float fallback = math.saturate(math.isfinite(fallback01) ? fallback01 : 1f);
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(fallback, quality, math.isfinite(quality)));
        }

        private static float Smooth01(float value)
        {
            float saturated = math.saturate(math.isfinite(value) ? value : 1f);
            return saturated * saturated * (3f - 2f * saturated);
        }

        private void QueueDynamicTextureRefresh(bool incrementRevision)
        {
            _dynamicTextureRefreshRequested = true;
            _dynamicTextureRefreshIncrementRevision |= incrementRevision;
        }

        private void ApplyDynamicTexturesIfDirty()
        {
            if (!_densityTextureUploadDirty && !_sinkTextureUploadDirty)
                return;

            if (_densityTextureUploadDirty && _densityFieldTexture != null)
                _densityFieldTexture.Apply(false, false);

            if (_sinkTextureUploadDirty && _sinkFieldTexture != null)
                _sinkFieldTexture.Apply(false, false);

            _densityTextureUploadDirty = false;
            _sinkTextureUploadDirty = false;
        }

        private float SampleDensity01NoDrift(Vector3 sampledPositionWS, float radius)
        {
            if (!_hasFieldData || _densityCells.Count == 0)
                return 0f;

            float effectiveRadius = Mathf.Max(0.1f, radius);
            int minCellX = Mathf.FloorToInt((sampledPositionWS.x - effectiveRadius) * _inverseCellSize);
            int maxCellX = Mathf.FloorToInt((sampledPositionWS.x + effectiveRadius) * _inverseCellSize);
            int minCellZ = Mathf.FloorToInt((sampledPositionWS.z - effectiveRadius) * _inverseCellSize);
            int maxCellZ = Mathf.FloorToInt((sampledPositionWS.z + effectiveRadius) * _inverseCellSize);
            float accumulatedDensity = 0f;

            for (int cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
            {
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                {
                    long key = PackCellKey(cellX, cellZ);
                    if (!_densityCells.TryGetValue(key, out CellData cell))
                        continue;

                    if (sampledPositionWS.y + effectiveRadius < cell.MinY || sampledPositionWS.y - effectiveRadius > cell.MaxY)
                        continue;

                    float cellCenterX = (cellX + 0.5f) * cellSize;
                    float cellCenterZ = (cellZ + 0.5f) * cellSize;
                    float planarDeltaX = sampledPositionWS.x - cellCenterX;
                    float planarDeltaZ = sampledPositionWS.z - cellCenterZ;
                    float planarDistanceSq = (planarDeltaX * planarDeltaX) + (planarDeltaZ * planarDeltaZ);
                    float influenceRadius = Mathf.Max(cellSize + effectiveRadius, 0.001f);
                    float influenceRadiusSq = Mathf.Max(influenceRadius * influenceRadius, 0.000001f);
                    float planarWeight = 1f - Mathf.Clamp01(planarDistanceSq / influenceRadiusSq);
                    if (planarWeight <= 0f)
                        continue;

                    accumulatedDensity += cell.Density * FastDensityFalloff01(planarWeight, Mathf.Max(1f, distancePower));
                }
            }

            return Mathf.Clamp01(accumulatedDensity * densityNormalization);
        }

        private float ResolveMaximumSinkDepthWS()
        {
            if (_disruptionZones == null || _activeDisruptionZoneCount <= 0)
                return 0f;

            float maxSinkDepthWS = 0f;
            for (int i = 0; i < _activeDisruptionZoneCount; i++)
            {
                DisruptionZoneState zone = _disruptionZones[i];
                if (!IsFiniteDisruptionZone(in zone))
                    continue;

                if (zone.SinkDepthWS > maxSinkDepthWS)
                    maxSinkDepthWS = zone.SinkDepthWS;
            }

            return maxSinkDepthWS;
        }

        private void WriteSafeVelocities(Rigidbody body, Vector3 linearVelocity, Vector3 angularVelocity)
        {
            if (body == null)
                return;

            IPhysicsService physicsService = _physicsService;
            physicsService?.QueueLinearVelocitySet(
                body,
                HectonPlayerMotor.SafeVelocity(linearVelocity, body.linearVelocity));
            physicsService?.QueueAngularVelocitySet(
                body,
                HectonPlayerMotor.SafeVelocity(angularVelocity, body.angularVelocity));
        }

        private void ScheduleDebrisPetrification(Rigidbody body)
        {
            if (body == null)
                return;

            if (!EnsureDebrisPetrificationStorage())
            {
                ApplyDebrisPetrification(body);
                return;
            }

            if (IsDebrisPetrificationQueued(body))
                return;

            if (_debrisPetrificationTimerCount >= DebrisPetrificationTimerCapacity)
            {
                ApplyDebrisPetrification(body);
                return;
            }

            int slot = FindDebrisPetrificationSlot();
            if (slot < 0)
            {
                ApplyDebrisPetrification(body);
                return;
            }

            _debrisPetrificationBodies[slot] = body;
            DebrisTimer timer = default;
            timer.Slot = slot;
            timer.RemainingSeconds = DebrisPetrificationDelaySeconds;
            if (!TryEnqueueDebrisPetrificationTimer(in timer))
            {
                _debrisPetrificationBodies[slot] = null;
                ApplyDebrisPetrification(body);
            }
        }

        private bool EnsureDebrisPetrificationStorage()
        {
            return _debrisPetrificationTimers != null && _debrisPetrificationBodies != null;
        }

        private bool TryEnqueueDebrisPetrificationTimer(in DebrisTimer timer)
        {
            if ((uint)_debrisPetrificationTimerCount >= (uint)_debrisPetrificationTimers.Length)
                return false;

            _debrisPetrificationTimers[_debrisPetrificationTimerTail] = timer;
            _debrisPetrificationTimerTail = (_debrisPetrificationTimerTail + 1) & DebrisPetrificationTimerIndexMask;
            _debrisPetrificationTimerCount++;
            return true;
        }

        private bool TryDequeueDebrisPetrificationTimer(out DebrisTimer timer)
        {
            if (_debrisPetrificationTimerCount <= 0)
            {
                timer = default;
                return false;
            }

            timer = _debrisPetrificationTimers[_debrisPetrificationTimerHead];
            _debrisPetrificationTimers[_debrisPetrificationTimerHead] = default;
            _debrisPetrificationTimerHead = (_debrisPetrificationTimerHead + 1) & DebrisPetrificationTimerIndexMask;
            _debrisPetrificationTimerCount--;
            return true;
        }

        private bool IsDebrisPetrificationQueued(Rigidbody body)
        {
            for (int i = 0; i < _debrisPetrificationBodies.Length; i++)
            {
                if (_debrisPetrificationBodies[i] == body)
                    return true;
            }

            return false;
        }

        private int FindDebrisPetrificationSlot()
        {
            for (int i = 0; i < DebrisPetrificationTimerCapacity; i++)
            {
                int slot = (_debrisPetrificationCursor + i) & DebrisPetrificationTimerIndexMask;
                if (_debrisPetrificationBodies[slot] != null)
                    continue;

                _debrisPetrificationCursor = (slot + 1) & DebrisPetrificationTimerIndexMask;
                return slot;
            }

            return -1;
        }

        private void ProcessDebrisPetrificationTimers()
        {
            if (_debrisPetrificationTimerCount <= 0)
                return;

            int scanBudget = _debrisPetrificationTimerCount;
            int disableBudget = DebrisPetrificationDisableBudgetPerSlowTick;
            while (scanBudget-- > 0 && _debrisPetrificationTimerCount > 0)
            {
                if (!TryDequeueDebrisPetrificationTimer(out DebrisTimer timer))
                {
                    _debrisPetrificationTimerCount = 0;
                    return;
                }

                timer.RemainingSeconds -= DebrisPetrificationSlowTickSeconds;
                if (timer.RemainingSeconds > 0f || disableBudget <= 0)
                {
                    if (!TryEnqueueDebrisPetrificationTimer(in timer) &&
                        (uint)timer.Slot < (uint)_debrisPetrificationBodies.Length)
                    {
                        ApplyDebrisPetrification(_debrisPetrificationBodies[timer.Slot]);
                        _debrisPetrificationBodies[timer.Slot] = null;
                    }
                    continue;
                }

                if ((uint)timer.Slot < (uint)_debrisPetrificationBodies.Length)
                {
                    ApplyDebrisPetrification(_debrisPetrificationBodies[timer.Slot]);
                    _debrisPetrificationBodies[timer.Slot] = null;
                    disableBudget--;
                }
            }
        }

        private void ApplyDebrisPetrification(Rigidbody body)
        {
            if (body == null)
                return;

            IPhysicsService physicsService = _physicsService;
            physicsService?.QueueLinearVelocitySet(body, Vector3.zero, wake: false);
            physicsService?.QueueAngularVelocitySet(body, Vector3.zero, wake: false);
            body.isKinematic = true;
            body.detectCollisions = false;
            body.Sleep();
        }

        private void ReleaseDebrisPetrificationStorage()
        {
            Array.Clear(_debrisPetrificationTimers, 0, _debrisPetrificationTimers.Length);
            Array.Clear(_debrisPetrificationBodies, 0, _debrisPetrificationBodies.Length);
            _debrisPetrificationTimerCount = 0;
            _debrisPetrificationTimerHead = 0;
            _debrisPetrificationTimerTail = 0;
            _debrisPetrificationCursor = 0;
        }

        private static uint BuildDeterministicAnchorSeed(Vector3 anchorWS, Vector3 driftOffset, uint salt)
        {
            Vector3 sampleSpaceAnchorWS = anchorWS - driftOffset;
            int quantizedX = Mathf.RoundToInt(sampleSpaceAnchorWS.x * 100f);
            int quantizedY = Mathf.RoundToInt(sampleSpaceAnchorWS.y * 100f);
            int quantizedZ = Mathf.RoundToInt(sampleSpaceAnchorWS.z * 100f);
            uint hash = salt ^ NestingHashSeed;
            hash = MixSeed(hash, unchecked((uint)quantizedX));
            hash = MixSeed(hash, unchecked((uint)quantizedY));
            hash = MixSeed(hash, unchecked((uint)quantizedZ));
            return hash != 0u ? hash : NestingHashSeed;
        }

        private static uint MixSeed(uint hash, uint value)
        {
            return hash ^ (value + 0x9E3779B9u + (hash << 6) + (hash >> 2));
        }

        private static float HashToFloat01(uint index, uint iteration, uint salt)
        {
            uint value = index * 374761393u + iteration * 668265263u + salt + NestingHashSeed;
            value = (value ^ (value >> 13)) * 1274126177u;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777215f;
        }
    }
}
