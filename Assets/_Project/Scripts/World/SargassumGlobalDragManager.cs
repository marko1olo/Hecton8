using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.SaveSystem;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

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
    public sealed class SargassumGlobalDragManager : MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, ISargassumMassiveDisplacementReceiver, IOriginShiftListener, ISaveable, IGlobalRegistryHotSwapListener
    {
        private static readonly int _GlobalDriftOffsetId = Shader.PropertyToID("_SargassumGlobalDriftOffset");
        private static readonly int _SinkTextureId = Shader.PropertyToID("_SargassumBuoyancySinkRT");
        private static readonly int _SinkWorldRectId = Shader.PropertyToID("_SargassumBuoyancySinkWorldRect");
        private static readonly int _MaxSinkDepthId = Shader.PropertyToID("_SargassumBuoyancySinkDepth");
        private static readonly int _ScavengerMatricesId = Shader.PropertyToID("_HectonScavengerMatrices");

        private const uint NestingHashSeed = 0x7F4A7C15u;
        private const int InitialCellCapacity = 4096;
        private const int DefaultDensityTextureResolution = 128;
        private const int MaxDisruptionZoneCapacity = 16;
        private const int MaxEditorValidateDepth = 2;
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
        private const int DebrisPetrificationDisableBudgetPerSlowTick = 16;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptDensityBuildAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptRenderStagingAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptSceneTimerAllocator = Allocator.Persistent;
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

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct DensitySourceData
        {
            [FieldOffset(0)] public float3 OriginWS;
            [FieldOffset(12)] public float Scale;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct DensityContributionData
        {
            [FieldOffset(0)] public float Density;
            [FieldOffset(4)] public float MinY;
            [FieldOffset(8)] public float MaxY;
            [FieldOffset(12)] private uint _pad0;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildDensityContributionJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<DensitySourceData> Sources;
            [NoAlias] public NativeParallelMultiHashMap<long, DensityContributionData>.ParallelWriter Contributions;
            public float CellSize;
            public float InverseCellSize;
            public float BaseInfluenceRadius;
            public float BaseVerticalHalfExtent;
            public float DistancePower;

            public void Execute(int index)
            {
                DensitySourceData source = Sources[index];
                float influenceRadius = math.max(BaseInfluenceRadius * source.Scale, CellSize * 0.35f);
                float verticalHalfExtent = math.max(BaseVerticalHalfExtent * source.Scale, 0.5f);
                float minY = source.OriginWS.y - verticalHalfExtent;
                float maxY = source.OriginWS.y + verticalHalfExtent;

                int minCellX = (int)math.floor((source.OriginWS.x - influenceRadius) * InverseCellSize);
                int maxCellX = (int)math.floor((source.OriginWS.x + influenceRadius) * InverseCellSize);
                int minCellZ = (int)math.floor((source.OriginWS.z - influenceRadius) * InverseCellSize);
                int maxCellZ = (int)math.floor((source.OriginWS.z + influenceRadius) * InverseCellSize);
                float safeDistancePower = math.max(1f, DistancePower);
                float influenceRadiusSq = math.max(influenceRadius * influenceRadius, 0.000001f);

                for (int cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
                {
                    for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                    {
                        float cellCenterX = (cellX + 0.5f) * CellSize;
                        float cellCenterZ = (cellZ + 0.5f) * CellSize;
                        float2 planarDelta = new float2(source.OriginWS.x - cellCenterX, source.OriginWS.z - cellCenterZ);
                        float planarDistanceSq = math.lengthsq(planarDelta);
                        if (planarDistanceSq > influenceRadiusSq)
                            continue;

                        float normalized = 1f - math.saturate(planarDistanceSq / influenceRadiusSq);
                        float density = FastDensityFalloff01(normalized, safeDistancePower);
                        if (density <= 0.0001f)
                            continue;

                        long key = ((long)cellX << 32) ^ (uint)cellZ;
                        Contributions.Add(key, new DensityContributionData
                        {
                            Density = density,
                            MinY = minY,
                            MaxY = maxY
                        });
                    }
                }
            }
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
        private static NativeQueue<EntanglementStrainSignal> _pendingEntanglementStrain;
        private static NativeQueue<EntanglementStrainSignal> _nextFrameEntanglementStrain;
        private static NativeQueue<MassiveDisplacementSignal> _pendingMassiveDisplacement;
        private static NativeQueue<MassiveDisplacementSignal> _nextFrameMassiveDisplacement;
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
        private NativeQueue<DebrisTimer> _debrisPetrificationTimers;
        // COLD ALLOC: Rigidbody[128] - managed handles for queued debris petrification timers - owner: SargassumGlobalDragManager
        private readonly Rigidbody[] _debrisPetrificationBodies = new Rigidbody[DebrisPetrificationTimerCapacity];
        private int _debrisPetrificationTimerCount;
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
            if (_pendingEntanglementStrain.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SargassumGlobalDragManager), nameof(_pendingEntanglementStrain));
                _pendingEntanglementStrain.Dispose();
                _pendingEntanglementStrain = default;
            }

            if (_nextFrameEntanglementStrain.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SargassumGlobalDragManager), nameof(_nextFrameEntanglementStrain));
                _nextFrameEntanglementStrain.Dispose();
                _nextFrameEntanglementStrain = default;
            }

            if (_pendingMassiveDisplacement.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SargassumGlobalDragManager), nameof(_pendingMassiveDisplacement));
                _pendingMassiveDisplacement.Dispose();
                _pendingMassiveDisplacement = default;
            }

            if (_nextFrameMassiveDisplacement.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SargassumGlobalDragManager), nameof(_nextFrameMassiveDisplacement));
                _nextFrameMassiveDisplacement.Dispose();
                _nextFrameMassiveDisplacement = default;
            }

            _pendingEntanglementStrainCount = 0;
            _nextFrameEntanglementStrainCount = 0;
            _pendingMassiveDisplacementCount = 0;
            _nextFrameMassiveDisplacementCount = 0;
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
        [Tooltip("Instanced archetypes used for rare debris or crates tangled into dense canopy walls. Future ecosystems can swap these without changing the renderer path.")]
        private NestedAttachmentPrototype[] nestingPrototypes;

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
        [Tooltip("Optional authored mesh rendered for bottom scavengers feeding on fallen collapse chunks.")]
        private Mesh scavengerMesh;

        [SerializeField]
        [Tooltip("Optional authored material used for scavenger rendering. If missing, a first-party fallback material is created from the scavenger shader.")]
        private Material scavengerMaterial;

        [SerializeField]
        [Tooltip("First-party fallback shader used to build the hidden scavenger material when the authored material is missing.")]
        private Shader scavengerFallbackShader;

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

        // COLD ALLOC: Dictionary<long, CellData>[4096] - coarse sargassum density field keyed by XZ cell hash - owner: SargassumGlobalDragManager
        private readonly Dictionary<long, CellData> _densityCells = new Dictionary<long, CellData>(InitialCellCapacity);
        // COLD ALLOC: Dictionary<long, CellData>[4096] - transient origin-shift rebin scratch used to preserve canopy sampling until the next Burst rebuild - owner: SargassumGlobalDragManager
        private readonly Dictionary<long, CellData> _densityCellsShiftScratch = new Dictionary<long, CellData>(InitialCellCapacity);
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
        private NativeArray<Matrix4x4> _scavengerMatricesNative;
        private GraphicsBuffer _scavengerMatrixBuffer;
        private BatchRendererGroup _scavengerBatchRendererGroup;
        private NativeArray<MetadataValue> _scavengerBatchMetadata;
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
        private bool _ownsScavengerBrgMaterial;
        private int _nestedPrototypeCapacity;
        private int _activeNestedPrototypeCount;
        private int _activeNestedAttachmentStateCount;
        private int _activeDisruptionZoneCount;
        private int _activeScavengerHostCount;
        private int _activeExternalScavengerSiteCount;
        private int _scavengerInstanceCapacity;
        private NestedAttachmentPrototype[] _activeNestingPrototypes;
        private NestedAttachmentPrototype[] _fallbackNestingPrototypes;
        private Mesh _fallbackAttachmentMesh;
        private Material _fallbackCrateMaterial;
        private Material _fallbackScrapMaterial;
        private Mesh _fallbackScavengerMesh;
        private Material _fallbackScavengerMaterial;
        private Shader _fallbackLitShader;
        private SargassumCutManager _cutManager;
        private Bounds _scavengerDrawBounds;
        private Bounds _registeredScavengerDrawBounds;
        private bool _scavengerDrawBoundsRegistered;

        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrameTick;
        private bool _registeredHotSwap;
        private bool _saveRegistered;
        private int _editorValidateDepth;
        private int _cachedRenderLayer;
        private bool _hasFieldData;
        private float _inverseCellSize;
        private int _fieldRevision;
        private HectonMapMagicVegetationBridge.FloatingLabyrinthConfig _fallbackLabyrinthConfig;
        private Vector4 _densityFieldWorldRect;
        private NativeArray<DensitySourceData> _densityBuildSources;
        private NativeParallelMultiHashMap<long, DensityContributionData> _densityContributions;
        private JobHandle _densityBuildHandle;
        private bool _serviceRegistered;
        private bool _densityBuildScheduled;
        private int _pendingDensityTrackedInstances;
        private Bounds _pendingDensityFieldBounds;
        private bool _pendingDensityFieldBoundsInitialized;
        private Vector4 _publishedGlobalDriftOffset;
        private Vector4 _publishedSinkWorldRect;
        private Texture _publishedSinkTexture;
        private float _publishedMaxSinkDepth;
        private bool _shaderGlobalsPublished;

        /// <summary>
        /// Active registry-owned instance.
        /// </summary>
        public static SargassumGlobalDragManager Instance => GlobalRegistry.SargassumDrag;
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
        public static void RaiseEntanglementStrain(EntanglementStrainSignal signal)
        {
            if (_listenerCount <= 0)
                return;

            EnsureEventQueues();
            if (_pendingEntanglementStrainCount + _nextFrameEntanglementStrainCount >= EntanglementStrainEventCapacity)
            {
                ReportEntanglementStrainOverflow();
                return;
            }

            if (_isDispatchingEvents)
            {
                _nextFrameEntanglementStrain.Enqueue(signal);
                _nextFrameEntanglementStrainCount++;
            }
            else
            {
                _pendingEntanglementStrain.Enqueue(signal);
                _pendingEntanglementStrainCount++;
            }
        }

        /// <summary>
        /// Broadcasts a canopy-clearing massive displacement cue to interested runtime systems.
        /// </summary>
        /// <param name="signal">Displacement payload.</param>
        public static void RaiseMassiveDisplacement(MassiveDisplacementSignal signal)
        {
            if (_listenerCount <= 0)
                return;

            EnsureEventQueues();
            if (_pendingMassiveDisplacementCount + _nextFrameMassiveDisplacementCount >= MassiveDisplacementEventCapacity)
            {
                ReportMassiveDisplacementOverflow();
                return;
            }

            if (_isDispatchingEvents)
            {
                _nextFrameMassiveDisplacement.Enqueue(signal);
                _nextFrameMassiveDisplacementCount++;
            }
            else
            {
                _pendingMassiveDisplacement.Enqueue(signal);
                _pendingMassiveDisplacementCount++;
            }
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

        private static void EnsureEventQueues()
        {
            if (!_pendingEntanglementStrain.IsCreated)
            {
                _pendingEntanglementStrain = new NativeQueue<EntanglementStrainSignal>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<EntanglementStrainSignal>[16] - sargassum strain event lane flushed by SystemDispatcher - owner: SargassumGlobalDragManager
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEntanglementStrain,
                    EntanglementStrainEventCapacity,
                    nameof(SargassumGlobalDragManager),
                    nameof(_pendingEntanglementStrain),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEntanglementStrain, EntanglementStrainEventCapacity);
            }

            if (!_nextFrameEntanglementStrain.IsCreated)
            {
                _nextFrameEntanglementStrain = new NativeQueue<EntanglementStrainSignal>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<EntanglementStrainSignal>[16] - next-frame sargassum strain lane - owner: SargassumGlobalDragManager
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEntanglementStrain,
                    EntanglementStrainEventCapacity,
                    nameof(SargassumGlobalDragManager),
                    nameof(_nextFrameEntanglementStrain),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameEntanglementStrain, EntanglementStrainEventCapacity);
            }

            if (!_pendingMassiveDisplacement.IsCreated)
            {
                _pendingMassiveDisplacement = new NativeQueue<MassiveDisplacementSignal>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<MassiveDisplacementSignal>[16] - sargassum displacement event lane flushed by SystemDispatcher - owner: SargassumGlobalDragManager
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingMassiveDisplacement,
                    MassiveDisplacementEventCapacity,
                    nameof(SargassumGlobalDragManager),
                    nameof(_pendingMassiveDisplacement),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingMassiveDisplacement, MassiveDisplacementEventCapacity);
            }

            if (!_nextFrameMassiveDisplacement.IsCreated)
            {
                _nextFrameMassiveDisplacement = new NativeQueue<MassiveDisplacementSignal>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<MassiveDisplacementSignal>[16] - next-frame sargassum displacement lane - owner: SargassumGlobalDragManager
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameMassiveDisplacement,
                    MassiveDisplacementEventCapacity,
                    nameof(SargassumGlobalDragManager),
                    nameof(_nextFrameMassiveDisplacement),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameMassiveDisplacement, MassiveDisplacementEventCapacity);
            }
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

        private static bool FlushEntanglementStrain()
        {
            if (!_pendingEntanglementStrain.IsCreated)
                return true;

            int scanBudget = _pendingEntanglementStrainCount > 0
                ? _pendingEntanglementStrainCount
                : EntanglementStrainEventCapacity;
            while (scanBudget-- > 0 && !_pendingEntanglementStrain.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingEntanglementStrain.TryDequeue(out EntanglementStrainSignal signal))
                    return true;

                if (_pendingEntanglementStrainCount > 0)
                    _pendingEntanglementStrainCount--;

                int count = _listenerCount;
                for (int i = count - 1; i >= 0; i--)
                {
                    ISargassumGlobalDragEventListener listener = _listeners[i].Listener;
                    if (listener == null || IsDeferredUnregisterPending(listener))
                        continue;

                    DispatchEntanglementStrainToListener(listener, in signal);
                }
            }

            if (_pendingEntanglementStrain.IsEmpty())
                _pendingEntanglementStrainCount = 0;

            return true;
        }

        private static bool FlushMassiveDisplacement()
        {
            if (!_pendingMassiveDisplacement.IsCreated)
                return true;

            int scanBudget = _pendingMassiveDisplacementCount > 0
                ? _pendingMassiveDisplacementCount
                : MassiveDisplacementEventCapacity;
            while (scanBudget-- > 0 && !_pendingMassiveDisplacement.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingMassiveDisplacement.TryDequeue(out MassiveDisplacementSignal signal))
                    return true;

                if (_pendingMassiveDisplacementCount > 0)
                    _pendingMassiveDisplacementCount--;

                int count = _listenerCount;
                for (int i = count - 1; i >= 0; i--)
                {
                    ISargassumGlobalDragEventListener listener = _listeners[i].Listener;
                    if (listener == null || IsDeferredUnregisterPending(listener))
                        continue;

                    DispatchMassiveDisplacementToListener(listener, in signal);
                }
            }

            if (_pendingMassiveDisplacement.IsEmpty())
                _pendingMassiveDisplacementCount = 0;

            return true;
        }

        private static bool HasPendingFrontEvents()
        {
            return (_pendingEntanglementStrain.IsCreated && !_pendingEntanglementStrain.IsEmpty())
                || (_pendingMassiveDisplacement.IsCreated && !_pendingMassiveDisplacement.IsEmpty());
        }

        private static void DropQueuedEvents()
        {
            if (_pendingEntanglementStrain.IsCreated)
            {
                while (_pendingEntanglementStrain.TryDequeue(out _))
                {
                }
            }

            if (_nextFrameEntanglementStrain.IsCreated)
            {
                while (_nextFrameEntanglementStrain.TryDequeue(out _))
                {
                }
            }

            if (_pendingMassiveDisplacement.IsCreated)
            {
                while (_pendingMassiveDisplacement.TryDequeue(out _))
                {
                }
            }

            if (_nextFrameMassiveDisplacement.IsCreated)
            {
                while (_nextFrameMassiveDisplacement.TryDequeue(out _))
                {
                }
            }

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
            Debug.LogException(exception);
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
            int frame = Time.frameCount;
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
            int frame = Time.frameCount;
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
            int frame = Time.frameCount;
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
            int frame = Time.frameCount;
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
            if (_nextFrameEntanglementStrain.IsCreated)
            {
                while (_nextFrameEntanglementStrainCount > 0 && _nextFrameEntanglementStrain.TryDequeue(out EntanglementStrainSignal signal))
                {
                    _nextFrameEntanglementStrainCount--;
                    _pendingEntanglementStrain.Enqueue(signal);
                    _pendingEntanglementStrainCount++;
                }
            }

            if (_nextFrameMassiveDisplacement.IsCreated)
            {
                while (_nextFrameMassiveDisplacementCount > 0 && _nextFrameMassiveDisplacement.TryDequeue(out MassiveDisplacementSignal signal))
                {
                    _nextFrameMassiveDisplacementCount--;
                    _pendingMassiveDisplacement.Enqueue(signal);
                    _pendingMassiveDisplacementCount++;
                }
            }
        }

        private void Awake()
        {
            SargassumGlobalDragManager registered = GlobalRegistry.SargassumDrag;
            if (registered != null && registered != this)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[SargassumGlobalDragManager] Duplicate instance detected. Destroying the newer component.", this);
#endif
                Destroy(this);
                return;
            }

            TryRegisterService();
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
            RefreshRenderLayerCache();
            if (!Application.isPlaying)
            {
                SanitizeSettings();
                return;
            }

            ResolveActiveNestingPrototypes();
            RefreshColdRegistryDependencies();
            EnsureDisruptionStorage();
            EnsureScavengerStorage();
            EnsureScavengerRenderResources();
            HectonFloatingOrigin.RegisterListener(this);
            PublishShaderGlobals();
            TryRegisterService();
            TryRegister();
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
            ReleaseDensityBuildStorage();
            ReleaseDebrisPetrificationStorage();

            if (!Application.isPlaying)
            {
                ReleaseNestedAttachmentStorage();
                ReleaseFallbackNestingResources();
                ReleaseDensityTexture();
                ReleaseSinkTexture();
                ReleaseScavengerResources();
            }

            PublishEmptyShaderGlobals();
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregisterService();
            TryUnregister();
            ReleaseNestedAttachmentStorage();
            ReleaseFallbackNestingResources();
            ReleaseDensityTexture();
            ReleaseSinkTexture();
            ReleaseScavengerResources();
            ReleaseDensityBuildStorage();
            ReleaseDebrisPetrificationStorage();
            PublishEmptyShaderGlobals();
        }

        /// <summary>
        /// Rebuilds the active sargassum density field from the current MapMagic residency payload.
        /// </summary>
        public void SlowTick()
        {
            RefreshRenderLayerCache();
            ProcessDebrisPetrificationTimers();
            ResolveBridge();
            RebuildDensityField();
            EvaluateBuoyancyCollapseZones();
            RefreshDynamicTextures(incrementRevision: true);
            RebuildNestedAttachments();
            PublishShaderGlobals();
        }

        /// <summary>
        /// Recovers completed density build jobs in the dispatcher-owned late-frame swap window.
        /// </summary>
        public void LateFrameTick()
        {
            CompleteDensityFieldBuildIfReady();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled || shiftData.ShiftOffset.sqrMagnitude <= 0.0001f)
                return;

            ApplyRuntimeOffsetToCachedState(-shiftData.ShiftOffset);
        }

        /// <summary>
        /// Draws rare nested attachments tangled into the current dense canopy field.
        /// </summary>
        /// <param name="dt">Frame delta supplied by GameTickManager.</param>
        public void Tick(float dt)
        {
            bool texturesChanged = UpdateDisruptionZones(dt);
            if (texturesChanged)
            {
                RefreshDynamicTextures(incrementRevision: false);
                PublishShaderGlobals();
            }

            UpdateScavengerHosts(dt);

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
            float clampedRadius = Mathf.Max(1f, radius);
            float clampedDuration = Mathf.Max(0.25f, duration);
            float extremePanicRadius = Mathf.Max(massiveDisplacementExtremePanicRadius, clampedRadius * 3f);

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

            RaiseMassiveDisplacement(new MassiveDisplacementSignal
            {
                PositionWS = position,
                RadiusWS = clampedRadius,
                Duration = clampedDuration,
                ExtremePanicRadiusWS = extremePanicRadius
            });

            RefreshDynamicTextures(incrementRevision: false);
            PublishShaderGlobals();
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
            sample.AnchorWS = positionWS;
            sample.Window01 = 1f;

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
            if (mapMagicVegetationBridge != null)
                return;

            mapMagicVegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
        }

        private void ApplyRuntimeOffsetToCachedState(Vector3 runtimeOffset)
        {
            ShiftDensityCellsRuntimeOffset(runtimeOffset);
            ShiftBoundsCenter(ref _debugFieldBounds, runtimeOffset);
            ShiftBoundsCenter(ref _pendingDensityFieldBounds, runtimeOffset);
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
            Dictionary<long, CellData>.Enumerator enumerator = _densityCells.GetEnumerator();
            while (enumerator.MoveNext())
            {
                long key = enumerator.Current.Key;
                CellData cell = enumerator.Current.Value;
                cell.MinY += runtimeOffset.y;
                cell.MaxY += runtimeOffset.y;

                int cellX = (int)(key >> 32);
                int cellZ = (int)key;
                float shiftedCenterX = ((cellX + 0.5f) * cellSize) + runtimeOffset.x;
                float shiftedCenterZ = ((cellZ + 0.5f) * cellSize) + runtimeOffset.z;
                int shiftedCellX = Mathf.FloorToInt(shiftedCenterX * _inverseCellSize);
                int shiftedCellZ = Mathf.FloorToInt(shiftedCenterZ * _inverseCellSize);
                long shiftedKey = PackCellKey(shiftedCellX, shiftedCellZ);

                if (_densityCellsShiftScratch.TryGetValue(shiftedKey, out CellData existing))
                {
                    existing.Density += cell.Density;
                    existing.MinY = Mathf.Min(existing.MinY, cell.MinY);
                    existing.MaxY = Mathf.Max(existing.MaxY, cell.MaxY);
                    _densityCellsShiftScratch[shiftedKey] = existing;
                }
                else
                {
                    _densityCellsShiftScratch.Add(shiftedKey, cell);
                }
            }

            _densityCells.Clear();
            Dictionary<long, CellData>.Enumerator shiftedEnumerator = _densityCellsShiftScratch.GetEnumerator();
            while (shiftedEnumerator.MoveNext())
                _densityCells.Add(shiftedEnumerator.Current.Key, shiftedEnumerator.Current.Value);

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
            if (_densityBuildScheduled)
                return;

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
            EnsureDensityBuildSourceCapacity(activeCount);

            int sourceCount = 0;
            int estimatedContributionCount = 0;

            for (int i = 0; i < activeCount; i++)
            {
                if ((HectonVegetationInstanceType)types[i] != HectonVegetationInstanceType.Sargassum)
                    continue;

                Matrix4x4 matrix = matrices[i];
                double3 originDouble = new double3(matrix.m03, matrix.m13, matrix.m23) + universeOffset;
                Vector3 origin = new Vector3((float)originDouble.x, (float)originDouble.y, (float)originDouble.z);
                float scale = ExtractUniformScale(matrix);
                float influenceRadius = Mathf.Max(baseInfluenceRadius * scale, cellSize * 0.35f);
                float verticalHalfExtent = Mathf.Max(baseVerticalHalfExtent * scale, 0.5f);

                _densityBuildSources[sourceCount++] = new DensitySourceData
                {
                    OriginWS = origin,
                    Scale = scale
                };

                int minCellX = Mathf.FloorToInt((origin.x - influenceRadius) * _inverseCellSize);
                int maxCellX = Mathf.FloorToInt((origin.x + influenceRadius) * _inverseCellSize);
                int minCellZ = Mathf.FloorToInt((origin.z - influenceRadius) * _inverseCellSize);
                int maxCellZ = Mathf.FloorToInt((origin.z + influenceRadius) * _inverseCellSize);
                estimatedContributionCount += Mathf.Max(1, (maxCellX - minCellX + 1) * (maxCellZ - minCellZ + 1));
                EncapsulateExtents(
                    origin,
                    new Vector3(influenceRadius, verticalHalfExtent, influenceRadius),
                    ref boundsMin,
                    ref boundsMax,
                    ref boundsInitialized);

                trackedInstances++;
            }

            if (sourceCount <= 0)
            {
                ClearField();
                return;
            }

            EnsureDensityContributionCapacity(estimatedContributionCount);
            _densityContributions.Clear();

            BuildDensityContributionJob buildJob = new BuildDensityContributionJob
            {
                Sources = _densityBuildSources.GetSubArray(0, sourceCount),
                Contributions = _densityContributions.AsParallelWriter(),
                CellSize = cellSize,
                InverseCellSize = _inverseCellSize,
                BaseInfluenceRadius = baseInfluenceRadius,
                BaseVerticalHalfExtent = baseVerticalHalfExtent,
                DistancePower = distancePower
            };

            _densityBuildHandle = buildJob.Schedule(sourceCount, math.max(1, math.min(32, sourceCount / 4)));
            _densityBuildScheduled = true;
            _pendingDensityTrackedInstances = trackedInstances;
            _pendingDensityFieldBounds = boundsInitialized ? BuildBoundsFromMinMax(boundsMin, boundsMax) : default;
            _pendingDensityFieldBoundsInitialized = boundsInitialized;
        }

        private void CompleteDensityFieldBuildIfReady()
        {
            if (!_densityBuildScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _densityBuildHandle, forceComplete: false))
                return;

            _densityBuildScheduled = false;
            ApplyDensityFieldBuildResults();
        }

        private void ApplyDensityFieldBuildResults()
        {
            ClearField();

            var contributionEnumerator = _densityContributions.GetEnumerator();
            while (contributionEnumerator.MoveNext())
            {
                long key = contributionEnumerator.Current.Key;
                DensityContributionData contribution = contributionEnumerator.Current.Value;
                if (_densityCells.TryGetValue(key, out CellData existing))
                {
                    existing.Density += contribution.Density;
                    existing.MinY = Mathf.Min(existing.MinY, contribution.MinY);
                    existing.MaxY = Mathf.Max(existing.MaxY, contribution.MaxY);
                    _densityCells[key] = existing;
                }
                else
                {
                    _densityCells.Add(
                        key,
                        new CellData
                        {
                            Density = contribution.Density,
                            MinY = contribution.MinY,
                            MaxY = contribution.MaxY
                        });
                }
            }

            _hasFieldData = _densityCells.Count > 0;
            _debugTrackedInstances = _pendingDensityTrackedInstances;
            _debugOccupiedCells = _densityCells.Count;
            _debugFieldBounds = _pendingDensityFieldBoundsInitialized ? _pendingDensityFieldBounds : default;
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

        private void EnsureDensityBuildSourceCapacity(int requiredCount)
        {
            int safeCount = Mathf.Max(1, Mathf.NextPowerOfTwo(requiredCount));
            if (_densityBuildSources.IsCreated && _densityBuildSources.Length >= safeCount)
                return;

            if (_densityBuildSources.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_densityBuildSources);
                _densityBuildSources.Dispose();
            }

            // COLD ALLOC: NativeArray<DensitySourceData>[capacity] - transient Burst source staging for canopy density rebuilds - owner: SargassumGlobalDragManager
            _densityBuildSources = new NativeArray<DensitySourceData>(safeCount, DataVaultExemptDensityBuildAllocator, NativeArrayOptions.UninitializedMemory);
            NativeMemorySentinel.RegisterNativeArray(
                _densityBuildSources,
                nameof(SargassumGlobalDragManager),
                nameof(_densityBuildSources),
                NativeAllocationLifetime.Scene);
        }

        private void EnsureDensityContributionCapacity(int requiredCount)
        {
            int safeCount = Mathf.Max(1, Mathf.NextPowerOfTwo(requiredCount));
            if (_densityContributions.IsCreated && _densityContributions.Capacity >= safeCount)
                return;

            if (_densityContributions.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelMultiHashMap(
                    nameof(SargassumGlobalDragManager),
                    nameof(_densityContributions));
                _densityContributions.Dispose();
            }

            // COLD ALLOC: NativeParallelMultiHashMap<long,DensityContributionData>[capacity] - Burst-built cell contribution fanout before dictionary compaction - owner: SargassumGlobalDragManager
            _densityContributions = new NativeParallelMultiHashMap<long, DensityContributionData>(safeCount, DataVaultExemptDensityBuildAllocator);
            NativeMemorySentinel.RegisterNativeParallelMultiHashMap(
                _densityContributions,
                nameof(SargassumGlobalDragManager),
                nameof(_densityContributions),
                NativeAllocationLifetime.Scene);
        }

        private void ReleaseDensityBuildStorage()
        {
            JobHandle dependency = _densityBuildScheduled ? _densityBuildHandle : default;
            if (_densityBuildSources.IsCreated)
                NativeMemorySentinel.UnregisterNativeArray(_densityBuildSources);

            if (_densityContributions.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelMultiHashMap(
                    nameof(SargassumGlobalDragManager),
                    nameof(_densityContributions));
            }

            DisposeNativeArray(ref _densityBuildSources, dependency);
            DisposeNativeParallelMultiHashMap(ref _densityContributions, dependency);
            _densityBuildHandle = default;
            _densityBuildScheduled = false;
            _pendingDensityTrackedInstances = 0;
            _pendingDensityFieldBounds = default;
            _pendingDensityFieldBoundsInitialized = false;
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            if (dependency.IsCompleted)
                array.Dispose();
            else
                array.Dispose(dependency);

            array = default;
        }

        private static void DisposeNativeParallelMultiHashMap<TKey, TValue>(
            ref NativeParallelMultiHashMap<TKey, TValue> hashMap,
            JobHandle dependency)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            if (!hashMap.IsCreated)
                return;

            if (dependency.IsCompleted)
                hashMap.Dispose();
            else
                hashMap.Dispose(dependency);

            hashMap = default;
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
            float deltaTime = math.max(0f, dt);
            int index = 0;
            while (index < _activeDisruptionZoneCount)
            {
                DisruptionZoneState zone = _disruptionZones[index];
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

            Dictionary<long, CellData>.Enumerator enumerator = _densityCells.GetEnumerator();
            while (enumerator.MoveNext())
            {
                long cellKey = enumerator.Current.Key;
                CellData cell = enumerator.Current.Value;
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
            EnsureDisruptionStorage();

            float clampedRadius = Mathf.Max(cellSize * 0.5f, radiusWS);
            float clampedStrength = Mathf.Clamp01(strength01);
            float clampedRamp = Mathf.Max(0.01f, rampDuration);
            float clampedHold = Mathf.Max(0f, holdDuration);
            float clampedFade = Mathf.Max(0f, fadeDuration);
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

            ObjectPoolManager poolManager = GlobalRegistry.ObjectPool;
            if (poolManager == null)
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

                if (chunkInstance.TryGetComponent(out SargassumCollapseChunk collapseChunk))
                {
                    collapseChunk.ActivateChunk(linearVelocity, angularVelocity, uniformScale, collapseChunkLifetime, 0);
                    if (chunkInstance.TryGetComponent(out Rigidbody chunkRigidbody))
                        ScheduleDebrisPetrification(chunkRigidbody);
                }
                else
                {
                    chunkInstance.transform.localScale = chunkInstance.transform.localScale * uniformScale;
                    if (chunkInstance.TryGetComponent(out Rigidbody chunkRigidbody))
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

            ObjectPoolManager poolManager = GlobalRegistry.ObjectPool;
            if (poolManager == null)
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

                if (chunkInstance.TryGetComponent(out SargassumCollapseChunk collapseChunk))
                {
                    collapseChunk.ActivateChunk(linearVelocity, angularVelocity, uniformScale, collapseChunkLifetime * 0.7f, fragmentDepth);
                    if (chunkInstance.TryGetComponent(out Rigidbody chunkRigidbody))
                        ScheduleDebrisPetrification(chunkRigidbody);
                }
                else
                {
                    chunkInstance.transform.localScale = chunkInstance.transform.localScale * uniformScale;
                    if (chunkInstance.TryGetComponent(out Rigidbody chunkRigidbody))
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
            if (_disruptionZones == null || _activeDisruptionZoneCount <= 0)
                return sample;

            Vector2 sampleXZ = new Vector2(sampledPositionWS.x, sampledPositionWS.z);
            for (int i = 0; i < _activeDisruptionZoneCount; i++)
            {
                DisruptionZoneState zone = _disruptionZones[i];
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
            PublishShaderGlobals(_globalDriftOffset, _densityFieldWorldRect, _sinkFieldTexture != null ? _sinkFieldTexture : Texture2D.blackTexture, _debugMaxSinkDepth);
        }

        private void PublishEmptyShaderGlobals()
        {
            PublishShaderGlobals(Vector3.zero, Vector4.zero, Texture2D.blackTexture, 0f);
        }

        private void PublishShaderGlobals(Vector3 driftOffset, Vector4 sinkWorldRect, Texture sinkTexture, float maxSinkDepth)
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
            if (nestingPrototypes != null && nestingPrototypes.Length > 0)
            {
                _activeNestingPrototypes = nestingPrototypes;
                return;
            }

            EnsureFallbackNestingResources();
            _activeNestingPrototypes = _fallbackNestingPrototypes;
        }

        private void EnsureFallbackNestingResources()
        {
            if (_fallbackAttachmentMesh == null)
                _fallbackAttachmentMesh = BuildFallbackAttachmentMesh();

            if (_fallbackCrateMaterial == null)
            {
                Shader litShader = ResolveFallbackLitShader();
                if (litShader != null)
                {
                    _fallbackCrateMaterial = new Material(litShader)
                    {
                        name = "__SargassumNestCrateFallback",
                        hideFlags = HideFlags.HideAndDontSave
                    }; // COLD ALLOC: Material[1] - runtime crate fallback for canopy nesting - owner: SargassumGlobalDragManager
                    _fallbackCrateMaterial.enableInstancing = true;
                    _fallbackCrateMaterial.SetColor("_BaseColor", new Color(0.42f, 0.29f, 0.17f, 1f));
                    _fallbackCrateMaterial.SetColor("_Color", new Color(0.42f, 0.29f, 0.17f, 1f));
                }
            }

            if (_fallbackScrapMaterial == null)
            {
                Shader litShader = ResolveFallbackLitShader();
                if (litShader != null)
                {
                    _fallbackScrapMaterial = new Material(litShader)
                    {
                        name = "__SargassumNestScrapFallback",
                        hideFlags = HideFlags.HideAndDontSave
                    }; // COLD ALLOC: Material[1] - runtime scrap fallback for canopy nesting - owner: SargassumGlobalDragManager
                    _fallbackScrapMaterial.enableInstancing = true;
                    _fallbackScrapMaterial.SetColor("_BaseColor", new Color(0.28f, 0.34f, 0.38f, 1f));
                    _fallbackScrapMaterial.SetColor("_Color", new Color(0.28f, 0.34f, 0.38f, 1f));
                }
            }

            if (_fallbackNestingPrototypes != null)
                return;

            _fallbackNestingPrototypes = new[]
            {
                new NestedAttachmentPrototype
                {
                    Mesh = _fallbackAttachmentMesh,
                    Material = _fallbackCrateMaterial,
                    Weight = 1,
                    UniformScaleRange = new Vector2(0.72f, 1.08f),
                    VerticalOffsetRange = new Vector2(-0.15f, 0.25f),
                    DensityThreshold = 0.62f,
                    WindowThreshold = 0.18f,
                    ShadowCastingMode = ShadowCastingMode.Off
                },
                new NestedAttachmentPrototype
                {
                    Mesh = _fallbackAttachmentMesh,
                    Material = _fallbackScrapMaterial,
                    Weight = 2,
                    UniformScaleRange = new Vector2(0.54f, 0.92f),
                    VerticalOffsetRange = new Vector2(-0.28f, 0.18f),
                    DensityThreshold = 0.56f,
                    WindowThreshold = 0.22f,
                    ShadowCastingMode = ShadowCastingMode.Off
                }
            }; // COLD ALLOC: NestedAttachmentPrototype[2] - runtime fallback nesting archetypes - owner: SargassumGlobalDragManager
        }

        private void ReleaseFallbackNestingResources()
        {
            _activeNestingPrototypes = null;
            _fallbackNestingPrototypes = null;

            if (_fallbackCrateMaterial != null)
            {
                DestroyOwnedRuntimeObject(_fallbackCrateMaterial);
                _fallbackCrateMaterial = null;
            }

            if (_fallbackScrapMaterial != null)
            {
                DestroyOwnedRuntimeObject(_fallbackScrapMaterial);
                _fallbackScrapMaterial = null;
            }

            if (_fallbackAttachmentMesh != null)
            {
                DestroyOwnedRuntimeObject(_fallbackAttachmentMesh);
                _fallbackAttachmentMesh = null;
            }
        }

        private void RebuildNestedAttachments()
        {
            EnsureNestedAttachmentStorage();
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

            Dictionary<long, CellData>.Enumerator enumerator = _densityCells.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (_activeNestedAttachmentStateCount >= maxNestedAttachmentCount)
                    break;

                long cellKey = enumerator.Current.Key;
                CellData cell = enumerator.Current.Value;
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
                AnchorWS = chunk.GetScavengerAnchorWS(),
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

                _scavengerHosts[i].Chunk = null;
                return;
            }
        }

        internal void RegisterExternalScavengerSite(Vector3 anchorWS, float radiusWS, float duration)
        {
            EnsureScavengerStorage();
            if (_externalScavengerSites == null || _externalScavengerSites.Length == 0)
                return;

            float clampedRadius = Mathf.Clamp(radiusWS, 0.2f, scavengerOrbitRadius * 3f);
            float clampedDuration = duration > 0f ? duration : externalScavengerSiteDuration;
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

            if (_externalScavengerSites == null || _externalScavengerSites.Length == 0)
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

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
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
            float3 runtimePosition = AbsoluteUniversePosition.FromAbsolutePosition(absolutePosition).ToRuntimeFloat3();

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
                _scavengerMatricesNative.IsCreated &&
                _scavengerMatricesNative.Length == instanceCapacity &&
                _scavengerHosts.Length == hostCapacity &&
                _externalScavengerSites.Length == externalSiteCapacity &&
                _scavengerInstanceCapacity == instanceCapacity)
            {
                return;
            }

            ReleaseScavengerBuffers();
            _activeScavengerHostCount = 0;
            _activeExternalScavengerSiteCount = 0;
            // COLD ALLOC: ScavengerHostState[hostCapacity] - tracked settled collapse chunks feeding the scavenger BRG renderer - owner: SargassumGlobalDragManager
            _scavengerHosts = new ScavengerHostState[hostCapacity];
            // COLD ALLOC: ExternalScavengerSiteState[externalSiteCapacity] - corpse/carcass scavenger targets without live chunk owners - owner: SargassumGlobalDragManager
            _externalScavengerSites = new ExternalScavengerSiteState[externalSiteCapacity];
            _scavengerMatricesNative = new NativeArray<Matrix4x4>(instanceCapacity, DataVaultExemptRenderStagingAllocator, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Matrix4x4>[instanceCapacity] - Burst-readable scavenger BRG culling payload mirror - owner: SargassumGlobalDragManager
            NativeMemorySentinel.RegisterNativeArray(_scavengerMatricesNative, nameof(SargassumGlobalDragManager), nameof(_scavengerMatricesNative), NativeAllocationLifetime.Session);
            _scavengerInstanceCapacity = instanceCapacity;
        }

        private void EnsureScavengerRenderResources()
        {
            EnsureScavengerStorage();

            if (scavengerMesh == null && _fallbackScavengerMesh == null)
                _fallbackScavengerMesh = BuildFallbackScavengerMesh();

            if (scavengerMaterial == null && _fallbackScavengerMaterial == null)
            {
                Shader shader = scavengerFallbackShader;
                if (shader != null)
                {
                    _fallbackScavengerMaterial = new Material(shader)
                    {
                        name = "__CollapseScavengerIndirectFallback",
                        hideFlags = HideFlags.HideAndDontSave,
                        enableInstancing = true
                    }; // COLD ALLOC: Material[1] - scavenger fallback material bound to first-party shader - owner: SargassumGlobalDragManager
                }
            }

            if (_scavengerMatrixBuffer == null)
                _scavengerMatrixBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Matrix4x4>(Mathf.Max(1, _scavengerInstanceCapacity)); // COLD ALLOC: GraphicsBuffer[instanceCapacity] - scavenger BRG matrices - owner: SargassumGlobalDragManager

            if (_scavengerBatchRendererGroup == null)
            {
                _scavengerBatchRendererGroup = new BatchRendererGroup(new BatchRendererGroupCreateInfo
                {
                    cullingCallback = OnPerformScavengerCulling,
                    userContext = IntPtr.Zero
                });

                _scavengerBatchMetadata = new NativeArray<MetadataValue>(ScavengerBrgMetadataPlaceholderCount, DataVaultExemptRenderStagingAllocator); // COLD ALLOC: NativeArray<MetadataValue>[1] - BRG metadata placeholder for scavenger scatter - owner: SargassumGlobalDragManager
                NativeMemorySentinel.RegisterNativeArray(_scavengerBatchMetadata, nameof(SargassumGlobalDragManager), nameof(_scavengerBatchMetadata), NativeAllocationLifetime.Session);
                _scavengerBatchHandleBuffer = HectonBatchRendererGroupUtility.CreateBatchHandleBuffer(); // COLD ALLOC: GraphicsBuffer[1] - BRG registration handle buffer for scavenger scatter - owner: SargassumGlobalDragManager
                _scavengerBatchId = _scavengerBatchRendererGroup.AddBatch(_scavengerBatchMetadata, _scavengerBatchHandleBuffer.bufferHandle);
            }
        }

        private void ReleaseScavengerBuffers()
        {
            if (_scavengerMatrixBuffer != null)
            {
                _scavengerMatrixBuffer.Release();
                _scavengerMatrixBuffer = null;
                _registeredScavengerBatchBuffer = null;
                _boundScavengerMatrixBuffer = null;
                _boundScavengerMatrixMaterial = null;
            }

            if (_scavengerMatricesNative.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_scavengerMatricesNative);
                _scavengerMatricesNative.Dispose();
                _scavengerMatricesNative = default;
            }
        }

        private void ReleaseScavengerResources()
        {
            ReleaseScavengerBuffers();
            _scavengerHosts = null;
            _externalScavengerSites = null;
            _scavengerInstanceCapacity = 0;
            _activeScavengerHostCount = 0;
            _activeExternalScavengerSiteCount = 0;

            if (_fallbackScavengerMaterial != null)
            {
                DestroyOwnedRuntimeObject(_fallbackScavengerMaterial);
                _fallbackScavengerMaterial = null;
            }

            if (_fallbackScavengerMesh != null)
            {
                DestroyOwnedRuntimeObject(_fallbackScavengerMesh);
                _fallbackScavengerMesh = null;
            }

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

            if (_scavengerBatchHandleBuffer != null)
            {
                _scavengerBatchHandleBuffer.Release();
                _scavengerBatchHandleBuffer = null;
            }

            if (_scavengerBatchMetadata.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_scavengerBatchMetadata);
                _scavengerBatchMetadata.Dispose();
            }

            ReleaseScavengerBrgMaterial();
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

            EnsureScavengerRenderResources();

            int writeHostIndex = 0;
            int matrixCount = 0;
            bool boundsInitialized = false;
            Vector3 boundsMin = default;
            Vector3 boundsMax = default;
            float safeDeltaTime = math.max(0f, dt);
            float consumeDurationInv = 1f / math.max(0.1f, scavengerConsumeDuration);
            float externalSiteDurationInv = 1f / math.max(0.1f, externalScavengerSiteDuration);
            int maxScavengersPerHost = math.max(1, scavengersPerHost);

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
                        if (_scavengerMatricesNative.IsCreated && matrixCount < _scavengerMatricesNative.Length)
                            _scavengerMatricesNative[matrixCount] = matrix;
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
                        if (_scavengerMatricesNative.IsCreated && matrixCount < _scavengerMatricesNative.Length)
                            _scavengerMatricesNative[matrixCount] = matrix;
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
            _debugScavengerHostCount = writeHostIndex + writeExternalSiteIndex;
            _debugScavengerInstanceCount = matrixCount;
            _scavengerDrawBounds = boundsInitialized ? BuildBoundsFromMinMax(boundsMin, boundsMax) : default;
            _debugScavengerBounds = _scavengerDrawBounds;
            UploadScavengerInstances(matrixCount);
        }

        private void UploadScavengerInstances(int instanceCount)
        {
            if (_scavengerMatrixBuffer == null || !_scavengerMatricesNative.IsCreated)
                return;
            if (instanceCount > 0)
                GraphicsBufferUploadUtility.UploadNativeArray(_scavengerMatrixBuffer, _scavengerMatricesNative, instanceCount);
        }

        private void DrawScavengers()
        {
            if (_debugScavengerInstanceCount <= 0 || _scavengerMatrixBuffer == null || _scavengerBatchRendererGroup == null)
                return;

            Mesh activeMesh = scavengerMesh != null ? scavengerMesh : _fallbackScavengerMesh;
            Material activeMaterial = EnsureScavengerBrgMaterial();
            if (activeMesh == null || activeMaterial == null)
                return;

            if (!ReferenceEquals(_boundScavengerMatrixMaterial, activeMaterial) ||
                !ReferenceEquals(_boundScavengerMatrixBuffer, _scavengerMatrixBuffer))
            {
                activeMaterial.SetBuffer(_ScavengerMatricesId, _scavengerMatrixBuffer);
                _boundScavengerMatrixMaterial = activeMaterial;
                _boundScavengerMatrixBuffer = _scavengerMatrixBuffer;
            }

            SyncScavengerBatchRegistration(activeMesh, activeMaterial);
            SyncScavengerBatchBuffer(_scavengerMatrixBuffer);
            if (!_scavengerDrawBoundsRegistered ||
                _registeredScavengerDrawBounds.center != _scavengerDrawBounds.center ||
                _registeredScavengerDrawBounds.size != _scavengerDrawBounds.size)
            {
                _scavengerBatchRendererGroup.SetGlobalBounds(_scavengerDrawBounds);
                _registeredScavengerDrawBounds = _scavengerDrawBounds;
                _scavengerDrawBoundsRegistered = true;
            }
        }

        private Material EnsureScavengerBrgMaterial()
        {
            Material sourceMaterial = scavengerMaterial != null ? scavengerMaterial : _fallbackScavengerMaterial;
            if (sourceMaterial == null)
                return null;

            if (_scavengerBrgMaterial != null && _scavengerBrgMaterialSource == sourceMaterial)
                return _scavengerBrgMaterial;

            ReleaseScavengerBrgMaterial();
            _scavengerBrgMaterial = new Material(sourceMaterial)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "__CollapseScavengerBrgMaterial",
                enableInstancing = true
            }; // COLD ALLOC: Material[1] - BRG-local scavenger material clone for per-renderer buffer binding - owner: SargassumGlobalDragManager
            _scavengerBrgMaterialSource = sourceMaterial;
            _ownsScavengerBrgMaterial = true;
            return _scavengerBrgMaterial;
        }

        private void ReleaseScavengerBrgMaterial()
        {
            if (!_ownsScavengerBrgMaterial || _scavengerBrgMaterial == null)
            {
                _scavengerBrgMaterial = null;
                _scavengerBrgMaterialSource = null;
                _ownsScavengerBrgMaterial = false;
                return;
            }

            DestroyOwnedRuntimeObject(_scavengerBrgMaterial);
            _scavengerBrgMaterial = null;
            _scavengerBrgMaterialSource = null;
            _ownsScavengerBrgMaterial = false;
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

        private Mesh BuildFallbackScavengerMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "__CollapseScavengerFallback",
                hideFlags = HideFlags.HideAndDontSave
            }; // COLD ALLOC: Mesh[1] - first-party fallback scavenger mesh - owner: SargassumGlobalDragManager

            Vector3[] vertices =
            {
                new Vector3(0f, 0f, 0.28f),
                new Vector3(0f, 0f, -0.28f),
                new Vector3(-0.16f, 0f, 0f),
                new Vector3(0.16f, 0f, 0f),
                new Vector3(0f, 0.08f, 0f),
                new Vector3(0f, -0.05f, 0f)
            }; // COLD ALLOC: Vector3[6] - fallback scavenger octahedron vertices - owner: SargassumGlobalDragManager

            int[] triangles =
            {
                0, 4, 3,
                0, 3, 5,
                0, 5, 2,
                0, 2, 4,
                1, 3, 4,
                1, 5, 3,
                1, 2, 5,
                1, 4, 2
            }; // COLD ALLOC: int[24] - fallback scavenger octahedron triangles - owner: SargassumGlobalDragManager

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
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
            float safeDeltaTime = math.max(0f, dt);
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
                Debug.LogError("SargassumGlobalDragManager editor validation watchdog tripped.", this);
#endif
                return;
            }

            try
            {
#if UNITY_EDITOR
                if (collapseChunkPrefab == null)
                    collapseChunkPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Construction/Final/PFB_SargassumCollapseChunk.prefab");
                if (scavengerFallbackShader == null)
                    scavengerFallbackShader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Art/Shaders/Hecton_CollapseScavengerIndirect.shader");
#endif
                SanitizeSettings();
                if (!Application.isPlaying)
                {
                    _activeNestingPrototypes = nestingPrototypes != null && nestingPrototypes.Length > 0
                        ? nestingPrototypes
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

        private Shader ResolveFallbackLitShader()
        {
            if (_fallbackLitShader != null)
                return _fallbackLitShader;

            RenderPipelineAsset renderPipeline = GraphicsSettings.currentRenderPipeline ?? GraphicsSettings.defaultRenderPipeline;
            Material defaultMaterial = renderPipeline != null ? renderPipeline.defaultMaterial : null;
            _fallbackLitShader = defaultMaterial != null ? defaultMaterial.shader : null;
            return _fallbackLitShader;
        }

        private void TryRegister()
        {
            if (Application.isPlaying && !_saveRegistered)
            {
                SaveManager saveRuntime = Hecton8.Core.GlobalRegistry.SaveRuntime;
                if (saveRuntime != null)
                {
                    saveRuntime.Register(this);
                    _saveRegistered = true;
                }
            }

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

            SargassumGlobalDragManager registered = GlobalRegistry.SargassumDrag;
            if (registered != null && registered != this)
                return;

            GlobalRegistry.RegisterSargassumDragRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.SargassumDrag, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterSargassumDragRuntime(this);
            _serviceRegistered = false;
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

            if (_saveRegistered)
            {
                Hecton8.Core.GlobalRegistry.SaveRuntime?.Unregister(this);
                _saveRegistered = false;
            }

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
            if (serviceSlot == GlobalRegistryServiceSlot.SargassumCutRuntime)
                _cutManager = currentService as SargassumCutManager;
        }

        private void RefreshColdRegistryDependencies()
        {
            _cutManager = GlobalRegistry.SargassumCut;
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
            int resolution = Mathf.Clamp(densityTextureResolution, 64, 256);
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
            }
        }

        private void ReleaseDensityTexture()
        {
            if (_densityFieldTexture == null)
                return;

            DestroyOwnedRuntimeObject(_densityFieldTexture);
            _densityFieldTexture = null;
        }

        private void ReleaseSinkTexture()
        {
            if (_sinkFieldTexture == null)
                return;

            DestroyOwnedRuntimeObject(_sinkFieldTexture);
            _sinkFieldTexture = null;
        }

        private void ClearDensityTexture()
        {
            if (_densityFieldTexture == null || _densityTextureRaw == null)
                return;

            Array.Clear(_densityTextureRaw, 0, _densityTextureRaw.Length);
            _densityFieldTexture.LoadRawTextureData(_densityTextureRaw);
            _densityFieldTexture.Apply(false, false);
        }

        private void ClearSinkTexture()
        {
            if (_sinkFieldTexture == null || _sinkTextureRaw == null)
                return;

            Array.Clear(_sinkTextureRaw, 0, _sinkTextureRaw.Length);
            _sinkFieldTexture.LoadRawTextureData(_sinkTextureRaw);
            _sinkFieldTexture.Apply(false, false);
        }

        private void RefreshDynamicTextures(bool incrementRevision)
        {
            CreateDensityTexture();
            if (_densityFieldTexture == null ||
                _densityTextureRaw == null ||
                _sinkFieldTexture == null ||
                _sinkTextureRaw == null)
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
            _densityFieldTexture.Apply(false, false);
            _sinkFieldTexture.LoadRawTextureData(_sinkTextureRaw);
            _sinkFieldTexture.Apply(false, false);
            _debugMaxSinkDepth = maxSinkDepthWS;
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
                if (_disruptionZones[i].SinkDepthWS > maxSinkDepthWS)
                    maxSinkDepthWS = _disruptionZones[i].SinkDepthWS;
            }

            return maxSinkDepthWS;
        }

        private static Mesh BuildFallbackAttachmentMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "__SargassumNestFallbackMesh"
            }; // COLD ALLOC: Mesh[1] - runtime fallback instanced nesting geometry - owner: SargassumGlobalDragManager

            Vector3[] vertices =
            {
                new Vector3(-0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f,  0.5f), new Vector3( 0.5f,  0.5f,  0.5f), new Vector3(-0.5f,  0.5f,  0.5f),
                new Vector3( 0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f,  0.5f, -0.5f), new Vector3( 0.5f,  0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f,  0.5f), new Vector3(-0.5f,  0.5f,  0.5f), new Vector3(-0.5f,  0.5f, -0.5f),
                new Vector3( 0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f, -0.5f), new Vector3( 0.5f,  0.5f, -0.5f), new Vector3( 0.5f,  0.5f,  0.5f),
                new Vector3(-0.5f,  0.5f,  0.5f), new Vector3( 0.5f,  0.5f,  0.5f), new Vector3( 0.5f,  0.5f, -0.5f), new Vector3(-0.5f,  0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3( 0.5f, -0.5f, -0.5f), new Vector3( 0.5f, -0.5f,  0.5f), new Vector3(-0.5f, -0.5f,  0.5f)
            };

            int[] triangles =
            {
                0, 1, 2, 0, 2, 3,
                4, 5, 6, 4, 6, 7,
                8, 9,10, 8,10,11,
                12,13,14, 12,14,15,
                16,17,18, 16,18,19,
                20,21,22, 20,22,23
            };

            Vector3[] normals =
            {
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
                Vector3.back, Vector3.back, Vector3.back, Vector3.back,
                Vector3.left, Vector3.left, Vector3.left, Vector3.left,
                Vector3.right, Vector3.right, Vector3.right, Vector3.right,
                Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                Vector3.down, Vector3.down, Vector3.down, Vector3.down
            };

            Vector2[] uv =
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
            };

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            return mesh;
        }

        private static void WriteSafeVelocities(Rigidbody body, Vector3 linearVelocity, Vector3 angularVelocity)
        {
            if (body == null)
                return;

            body.linearVelocity = HectonPlayerMotor.SafeVelocity(linearVelocity, body.linearVelocity);
            body.angularVelocity = HectonPlayerMotor.SafeVelocity(angularVelocity, body.angularVelocity);
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
            _debrisPetrificationTimers.Enqueue(new DebrisTimer
            {
                Slot = slot,
                RemainingSeconds = DebrisPetrificationDelaySeconds
            });
            _debrisPetrificationTimerCount++;
        }

        private bool EnsureDebrisPetrificationStorage()
        {
            if (_debrisPetrificationTimers.IsCreated)
                return true;

            _debrisPetrificationTimers = new NativeQueue<DebrisTimer>(DataVaultExemptSceneTimerAllocator); // COLD ALLOC: NativeQueue<DebrisTimer>[128] - slow-tick debris petrification timers - owner: SargassumGlobalDragManager
            NativeMemorySentinel.RegisterNativeQueue(
                _debrisPetrificationTimers,
                DebrisPetrificationTimerCapacity,
                nameof(SargassumGlobalDragManager),
                nameof(_debrisPetrificationTimers),
                NativeAllocationLifetime.Scene);
            PrewarmQueue(ref _debrisPetrificationTimers, DebrisPetrificationTimerCapacity);
            return _debrisPetrificationTimers.IsCreated;
        }

        private int FindDebrisPetrificationSlot()
        {
            for (int i = 0; i < DebrisPetrificationTimerCapacity; i++)
            {
                int slot = (_debrisPetrificationCursor + i) % DebrisPetrificationTimerCapacity;
                if (_debrisPetrificationBodies[slot] != null)
                    continue;

                _debrisPetrificationCursor = (slot + 1) % DebrisPetrificationTimerCapacity;
                return slot;
            }

            return -1;
        }

        private void ProcessDebrisPetrificationTimers()
        {
            if (!_debrisPetrificationTimers.IsCreated || _debrisPetrificationTimerCount <= 0)
                return;

            int scanBudget = _debrisPetrificationTimerCount;
            int disableBudget = DebrisPetrificationDisableBudgetPerSlowTick;
            while (scanBudget-- > 0 && _debrisPetrificationTimerCount > 0 && !_debrisPetrificationTimers.IsEmpty())
            {
                if (!_debrisPetrificationTimers.TryDequeue(out DebrisTimer timer))
                    return;

                _debrisPetrificationTimerCount--;
                timer.RemainingSeconds -= DebrisPetrificationSlowTickSeconds;
                if (timer.RemainingSeconds > 0f || disableBudget <= 0)
                {
                    _debrisPetrificationTimers.Enqueue(timer);
                    _debrisPetrificationTimerCount++;
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

        private static void ApplyDebrisPetrification(Rigidbody body)
        {
            if (body == null)
                return;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.detectCollisions = false;
            body.Sleep();
        }

        private void ReleaseDebrisPetrificationStorage()
        {
            if (_debrisPetrificationTimers.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SargassumGlobalDragManager), nameof(_debrisPetrificationTimers));
                _debrisPetrificationTimers.Dispose();
                _debrisPetrificationTimers = default;
            }

            Array.Clear(_debrisPetrificationBodies, 0, _debrisPetrificationBodies.Length);
            _debrisPetrificationTimerCount = 0;
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
