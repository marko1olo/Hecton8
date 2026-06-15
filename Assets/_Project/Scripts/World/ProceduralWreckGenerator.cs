
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.SaveSystem;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using ScanEntryKind = Hecton8.Gameplay.ScanEntryKind;
using ScanEvents = Hecton8.Gameplay.ScanEvents;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.World
{
    /// <summary>
    /// Runtime contract for deterministic procedural geometry generators.
    /// </summary>
    public interface IProceduralGenerator : IDisposable
    {
        /// <summary>
        /// Builds a deterministic wreck payload for the requested chunk-space anchor.
        /// </summary>
        /// <param name="chunkAup">Chunk anchor in absolute-universe chunk space.</param>
        /// <param name="seed">Stable seed used to resolve WFC collapse order and module selection.</param>
        /// <returns>Generated wreck payload.</returns>
        WreckageData Generate(int3 chunkAup, uint seed);
    }

    /// <summary>
    /// Declares the merged render tier for a procedural wreck module.
    /// </summary>
    public enum WreckLodTier : byte
    {
        Essential = 0,
        Detail = 1,
        Clutter = 2
    }

    /// <summary>
    /// Runtime state for the asynchronous wreck navigation bake.
    /// </summary>
    public enum WreckNavigationState : byte
    {
        None = 0,
        Baking = 1,
        Ready = 2,
        Failed = 3
    }

    public enum WreckIntegrityState : byte
    {
        Open = 0,
        Sealed = 1,
        Ruptured = 2
    }

    [Flags]
    internal enum WreckDebrisFlags : byte
    {
        None = 0,
        Pickable = 1 << 0,
        ActivePickup = 1 << 1,
        Harvested = 1 << 2,
        DotOnly = 1 << 3
    }

    internal enum WreckSolveTermination : byte
    {
        Completed = 0,
        CollapseWatchdogTriggered = 1,
        PropagationWatchdogTriggered = 2,
        ContradictionBudgetExceeded = 3,
        SelectionFailed = 4,
        PropagationQueueOverflow = 5,
        DataVaultWriteFailed = 6,
        DataVaultReadFailed = 7
    }

    /// <summary>
    /// Serializable authoring record for a single WFC wreck module.
    /// </summary>
    [Serializable]
    public struct ProceduralWreckModuleDefinition
    {
        [Tooltip("North face socket mask.")]
        public ushort NorthSocket;

        [Tooltip("East face socket mask.")]
        public ushort EastSocket;

        [Tooltip("South face socket mask.")]
        public ushort SouthSocket;

        [Tooltip("West face socket mask.")]
        public ushort WestSocket;

        [Tooltip("Top face socket mask.")]
        public ushort TopSocket;

        [Tooltip("Bottom face socket mask.")]
        public ushort BottomSocket;

        [Tooltip("Structural mesh used by the merged render pass. Mesh layout must expose tightly packed Vector3 position, Vector3 normal, and Vector2 UV streams for direct job access.")]
        public Mesh StructuralMesh;

        [Tooltip("Local axis-aligned bounds used for deterministic proxy hull generation and authored occupancy checks.")]
        public Bounds LocalBounds;

        [Tooltip("Render priority used to split the merged result into Essential/Detail/Clutter tiers.")]
        public WreckLodTier DrawCallPriority;

        [Tooltip("True when this module writes geometry into the merged structural mesh.")]
        public bool EmitsGeometry;

        [Tooltip("True when this module writes a lightweight nav proxy hull derived from LocalBounds.")]
        public bool EmitsNavProxy;

        [Tooltip("True when this module acts as the universal contradiction fallback. Slot 0 is forced to behave this way.")]
        public bool UniversalConnector;

        [Tooltip("Integrity state used by gameplay routing. Sealed modules require a laser/plasma-cut interaction signal before opening.")]
        public WreckIntegrityState IntegrityState;

        [Tooltip("True when this sealed module accepts only the Laser Cutter / plasma-cut capability mask.")]
        public bool RequiresLaserCutter;

        [Tooltip("Optional loot table slot resolved against the native wreck loot SOA.")]
        [Range(0, 15)]
        public byte LootTableIndex;

        [Tooltip("Seeded chance, in permille, that this module contributes a lore fragment hash.")]
        [Range(0, 1000)]
        public ushort LoreFragmentChancePermille;
    }

    /// <summary>
    /// Runtime output for a generated procedural wreck.
    /// </summary>
    [Serializable]
    public struct WreckageData
    {
        /// <summary>Single merged mesh containing all structural modules across all LOD tiers.</summary>
        public Mesh CombinedMesh;
        /// <summary>Filtered merged mesh containing only Essential-tier modules for near-field rendering.</summary>
        public Mesh EssentialMesh;
        /// <summary>Filtered merged mesh containing only Detail-tier modules for mid-range rendering.</summary>
        public Mesh DetailMesh;
        /// <summary>Filtered merged mesh containing only Clutter-tier modules for distant fill.</summary>
        public Mesh ClutterMesh;
        /// <summary>Lightweight axis-aligned proxy mesh used for collision.</summary>
        public Mesh ProxyMesh;
        /// <summary>Reserved navigation payload. Always null; predator navigation uses local steering/SDF queries.</summary>
        public UnityEngine.Object Navigation;
        /// <summary>Reserved async operation. Always null; runtime navigation baking is disabled.</summary>
        public AsyncOperation NavigationBuild;
        /// <summary>Reserved navigation handle. Always null while runtime navigation baking is disabled.</summary>
        [NonSerialized] public WreckNavigationHandle NavigationHandle;
        /// <summary>Current state of the disabled navigation bake path.</summary>
        public WreckNavigationState NavigationState;
        /// <summary>Axis-aligned world-space bounds enclosing the entire generated wreck.</summary>
        public Bounds WorldBounds;
        /// <summary>Camera-relative origin used to place the wreck in the runtime scene.</summary>
        public Vector3 RuntimeOrigin;
        /// <summary>Deterministic seed used by the WFC kernel for this generation pass.</summary>
        public uint GenerationSeed;
    }

    /// <summary>
    /// Mutable disabled navigation handle kept only for serialized compatibility.
    /// </summary>
    public sealed class WreckNavigationHandle : IDisposable
    {
        private readonly ProceduralWreckGenerator _owner;
        private readonly Action<AsyncOperation> _completedCallback;

        /// <summary>Reserved data payload. Always null while runtime navigation baking is disabled.</summary>
        public UnityEngine.Object Data { get; }
        /// <summary>World-space bounds enclosing the navigation proxy geometry.</summary>
        public Bounds WorldBounds { get; }
        /// <summary>Reserved async operation. Always null while runtime navigation baking is disabled.</summary>
        public AsyncOperation BuildOperation { get; private set; }
        /// <summary>Current lifecycle state of the navigation bake.</summary>
        public WreckNavigationState State { get; private set; }

        internal WreckNavigationHandle(ProceduralWreckGenerator owner, UnityEngine.Object data, Bounds worldBounds)
        {
            _owner = owner;
            _completedCallback = OnBuildCompleted;
            Data = data;
            WorldBounds = worldBounds;
            State = WreckNavigationState.Baking;
        }

        internal void Bind(AsyncOperation operation)
        {
            BuildOperation = operation;
            if (BuildOperation == null)
            {
                State = WreckNavigationState.Failed;
                return;
            }

            BuildOperation.completed += _completedCallback;
        }

        internal void MarkFailed()
        {
            State = WreckNavigationState.Failed;
        }

        /// <summary>
        /// Cancels any in-progress disabled bake callback and releases the handle.
        /// </summary>
        public void Dispose()
        {
            if (BuildOperation != null)
            {
                BuildOperation.completed -= _completedCallback;
                BuildOperation = null;
            }

            _owner?.ReleaseNavigationHandle(this);
            State = WreckNavigationState.None;
        }

        private void OnBuildCompleted(AsyncOperation operation)
        {
            if (BuildOperation != null)
                BuildOperation.completed -= _completedCallback;

            BuildOperation = null;
            _owner?.HandleNavigationBakeCompleted(this);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct WreckGridCell
    {
        [FieldOffset(0)] public float Entropy;
        [FieldOffset(4)] public ushort PossibleModuleMask;
        [FieldOffset(6)] public byte CollapsedModuleId;
        [FieldOffset(7)] public byte SocketConstraints;
        [FieldOffset(8)] private byte _pad0;
        [FieldOffset(9)] private byte _pad1;
        [FieldOffset(10)] private byte _pad2;
        [FieldOffset(11)] private byte _pad3;
        [FieldOffset(12)] private byte _pad4;
        [FieldOffset(13)] private byte _pad5;
        [FieldOffset(14)] private byte _pad6;
        [FieldOffset(15)] private byte _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct WreckModuleRuntimeDefinition
    {
        [FieldOffset(0)] public float3 BoundsCenter;
        [FieldOffset(12)] public float3 BoundsSize;
        [FieldOffset(24)] public ushort NorthSocket;
        [FieldOffset(26)] public ushort EastSocket;
        [FieldOffset(28)] public ushort SouthSocket;
        [FieldOffset(30)] public ushort WestSocket;
        [FieldOffset(32)] public ushort TopSocket;
        [FieldOffset(34)] public ushort BottomSocket;
        [FieldOffset(36)] public ushort LoreFragmentChancePermille;
        [FieldOffset(38)] private byte _pad0;
        [FieldOffset(39)] private byte _pad1;
        [FieldOffset(40)] public byte DrawCallPriority;
        [FieldOffset(41)] public byte EmitsGeometry;
        [FieldOffset(42)] public byte EmitsNavProxy;
        [FieldOffset(43)] public byte UniversalConnector;
        [FieldOffset(44)] public byte IntegrityState;
        [FieldOffset(45)] public byte RequiresLaserCutter;
        [FieldOffset(46)] public byte LootTableIndex;
        [FieldOffset(47)] private byte _pad2;
        [FieldOffset(48)] private byte _pad3;
        [FieldOffset(49)] private byte _pad4;
        [FieldOffset(50)] private byte _pad5;
        [FieldOffset(51)] private byte _pad6;
        [FieldOffset(52)] private byte _pad7;
        [FieldOffset(53)] private byte _pad8;
        [FieldOffset(54)] private byte _pad9;
        [FieldOffset(55)] private byte _pad10;
        [FieldOffset(56)] private byte _pad11;
        [FieldOffset(57)] private byte _pad12;
        [FieldOffset(58)] private byte _pad13;
        [FieldOffset(59)] private byte _pad14;
        [FieldOffset(60)] private byte _pad15;
        [FieldOffset(61)] private byte _pad16;
        [FieldOffset(62)] private byte _pad17;
        [FieldOffset(63)] private byte _pad18;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct WreckModulePlacement
    {
        [FieldOffset(0)] public quaternion Rotation;
        [FieldOffset(16)] public float3 Position;
        [FieldOffset(28)] public float3 BoundsCenter;
        [FieldOffset(40)] public float3 BoundsSize;
        [FieldOffset(52)] public int MortonIndex;
        [FieldOffset(56)] public byte ModuleId;
        [FieldOffset(57)] public byte DrawPriority;
        [FieldOffset(58)] public byte IntegrityState;
        [FieldOffset(59)] public byte ModuleFlags;
        [FieldOffset(60)] private byte _pad0;
        [FieldOffset(61)] private byte _pad1;
        [FieldOffset(62)] private byte _pad2;
        [FieldOffset(63)] private byte _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct WreckMergedVertex
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public float2 UV;
        [FieldOffset(32)] public uint Color;
        [FieldOffset(36)] private byte _pad0;
        [FieldOffset(37)] private byte _pad1;
        [FieldOffset(38)] private byte _pad2;
        [FieldOffset(39)] private byte _pad3;
        [FieldOffset(40)] private byte _pad4;
        [FieldOffset(41)] private byte _pad5;
        [FieldOffset(42)] private byte _pad6;
        [FieldOffset(43)] private byte _pad7;
        [FieldOffset(44)] private byte _pad8;
        [FieldOffset(45)] private byte _pad9;
        [FieldOffset(46)] private byte _pad10;
        [FieldOffset(47)] private byte _pad11;
        [FieldOffset(48)] private byte _pad12;
        [FieldOffset(49)] private byte _pad13;
        [FieldOffset(50)] private byte _pad14;
        [FieldOffset(51)] private byte _pad15;
        [FieldOffset(52)] private byte _pad16;
        [FieldOffset(53)] private byte _pad17;
        [FieldOffset(54)] private byte _pad18;
        [FieldOffset(55)] private byte _pad19;
        [FieldOffset(56)] private byte _pad20;
        [FieldOffset(57)] private byte _pad21;
        [FieldOffset(58)] private byte _pad22;
        [FieldOffset(59)] private byte _pad23;
        [FieldOffset(60)] private byte _pad24;
        [FieldOffset(61)] private byte _pad25;
        [FieldOffset(62)] private byte _pad26;
        [FieldOffset(63)] private byte _pad27;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct WreckLootRecord
    {
        [FieldOffset(0)] public int ItemHashId;
        [FieldOffset(4)] public uint StableDropHash;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public ushort MinQuantity;
        [FieldOffset(14)] public ushort MaxQuantity;
        [FieldOffset(16)] private byte _pad0;
        [FieldOffset(17)] private byte _pad1;
        [FieldOffset(18)] private byte _pad2;
        [FieldOffset(19)] private byte _pad3;
        [FieldOffset(20)] private byte _pad4;
        [FieldOffset(21)] private byte _pad5;
        [FieldOffset(22)] private byte _pad6;
        [FieldOffset(23)] private byte _pad7;
        [FieldOffset(24)] private byte _pad8;
        [FieldOffset(25)] private byte _pad9;
        [FieldOffset(26)] private byte _pad10;
        [FieldOffset(27)] private byte _pad11;
        [FieldOffset(28)] private byte _pad12;
        [FieldOffset(29)] private byte _pad13;
        [FieldOffset(30)] private byte _pad14;
        [FieldOffset(31)] private byte _pad15;
        [FieldOffset(32)] private byte _pad16;
        [FieldOffset(33)] private byte _pad17;
        [FieldOffset(34)] private byte _pad18;
        [FieldOffset(35)] private byte _pad19;
        [FieldOffset(36)] private byte _pad20;
        [FieldOffset(37)] private byte _pad21;
        [FieldOffset(38)] private byte _pad22;
        [FieldOffset(39)] private byte _pad23;
        [FieldOffset(40)] private byte _pad24;
        [FieldOffset(41)] private byte _pad25;
        [FieldOffset(42)] private byte _pad26;
        [FieldOffset(43)] private byte _pad27;
        [FieldOffset(44)] private byte _pad28;
        [FieldOffset(45)] private byte _pad29;
        [FieldOffset(46)] private byte _pad30;
        [FieldOffset(47)] private byte _pad31;
        [FieldOffset(48)] private byte _pad32;
        [FieldOffset(49)] private byte _pad33;
        [FieldOffset(50)] private byte _pad34;
        [FieldOffset(51)] private byte _pad35;
        [FieldOffset(52)] private byte _pad36;
        [FieldOffset(53)] private byte _pad37;
        [FieldOffset(54)] private byte _pad38;
        [FieldOffset(55)] private byte _pad39;
        [FieldOffset(56)] private byte _pad40;
        [FieldOffset(57)] private byte _pad41;
        [FieldOffset(58)] private byte _pad42;
        [FieldOffset(59)] private byte _pad43;
        [FieldOffset(60)] private byte _pad44;
        [FieldOffset(61)] private byte _pad45;
        [FieldOffset(62)] private byte _pad46;
        [FieldOffset(63)] private byte _pad47;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct WreckDebrisRecord
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float InitialY;
        [FieldOffset(16)] public float TerrainY;
        [FieldOffset(20)] public int SpatialHashKey;
        [FieldOffset(24)] public int ClusterIndex;
        [FieldOffset(28)] public int ItemHashId;
        [FieldOffset(32)] public uint StableId;
        [FieldOffset(36)] public float SinkMetersPerSlowTick;
        [FieldOffset(40)] public float PickupRadiusSq;
        [FieldOffset(44)] public ushort Quantity;
        [FieldOffset(46)] public byte Flags;
        [FieldOffset(47)] public byte LootTableIndex;
        [FieldOffset(48)] private byte _pad0;
        [FieldOffset(49)] private byte _pad1;
        [FieldOffset(50)] private byte _pad2;
        [FieldOffset(51)] private byte _pad3;
        [FieldOffset(52)] private byte _pad4;
        [FieldOffset(53)] private byte _pad5;
        [FieldOffset(54)] private byte _pad6;
        [FieldOffset(55)] private byte _pad7;
        [FieldOffset(56)] private byte _pad8;
        [FieldOffset(57)] private byte _pad9;
        [FieldOffset(58)] private byte _pad10;
        [FieldOffset(59)] private byte _pad11;
        [FieldOffset(60)] private byte _pad12;
        [FieldOffset(61)] private byte _pad13;
        [FieldOffset(62)] private byte _pad14;
        [FieldOffset(63)] private byte _pad15;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct WreckDebrisCluster
    {
        [FieldOffset(0)] public float3 Center;
        [FieldOffset(12)] public float3 Extents;
        [FieldOffset(24)] public int ClusterKey;
        [FieldOffset(28)] public int DebrisCount;
        [FieldOffset(32)] public byte Visible;
        [FieldOffset(33)] private byte _pad0;
        [FieldOffset(34)] private byte _pad1;
        [FieldOffset(35)] private byte _pad2;
        [FieldOffset(36)] private byte _pad3;
        [FieldOffset(37)] private byte _pad4;
        [FieldOffset(38)] private byte _pad5;
        [FieldOffset(39)] private byte _pad6;
        [FieldOffset(40)] private byte _pad7;
        [FieldOffset(41)] private byte _pad8;
        [FieldOffset(42)] private byte _pad9;
        [FieldOffset(43)] private byte _pad10;
        [FieldOffset(44)] private byte _pad11;
        [FieldOffset(45)] private byte _pad12;
        [FieldOffset(46)] private byte _pad13;
        [FieldOffset(47)] private byte _pad14;
        [FieldOffset(48)] private byte _pad15;
        [FieldOffset(49)] private byte _pad16;
        [FieldOffset(50)] private byte _pad17;
        [FieldOffset(51)] private byte _pad18;
        [FieldOffset(52)] private byte _pad19;
        [FieldOffset(53)] private byte _pad20;
        [FieldOffset(54)] private byte _pad21;
        [FieldOffset(55)] private byte _pad22;
        [FieldOffset(56)] private byte _pad23;
        [FieldOffset(57)] private byte _pad24;
        [FieldOffset(58)] private byte _pad25;
        [FieldOffset(59)] private byte _pad26;
        [FieldOffset(60)] private byte _pad27;
        [FieldOffset(61)] private byte _pad28;
        [FieldOffset(62)] private byte _pad29;
        [FieldOffset(63)] private byte _pad30;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct WreckArtifactRecord
    {
        [FieldOffset(0)] public uint EntryHash;
        [FieldOffset(4)] public float3 Position;
        [FieldOffset(16)] public int ModuleIndex;
        [FieldOffset(20)] public uint StableId;
        [FieldOffset(24)] public float DiscoveryRadiusSq;
        [FieldOffset(28)] public ushort ChancePermille;
        [FieldOffset(30)] public byte State;
        [FieldOffset(31)] public byte ModuleId;
        [FieldOffset(32)] private byte _pad0;
        [FieldOffset(33)] private byte _pad1;
        [FieldOffset(34)] private byte _pad2;
        [FieldOffset(35)] private byte _pad3;
        [FieldOffset(36)] private byte _pad4;
        [FieldOffset(37)] private byte _pad5;
        [FieldOffset(38)] private byte _pad6;
        [FieldOffset(39)] private byte _pad7;
        [FieldOffset(40)] private byte _pad8;
        [FieldOffset(41)] private byte _pad9;
        [FieldOffset(42)] private byte _pad10;
        [FieldOffset(43)] private byte _pad11;
        [FieldOffset(44)] private byte _pad12;
        [FieldOffset(45)] private byte _pad13;
        [FieldOffset(46)] private byte _pad14;
        [FieldOffset(47)] private byte _pad15;
        [FieldOffset(48)] private byte _pad16;
        [FieldOffset(49)] private byte _pad17;
        [FieldOffset(50)] private byte _pad18;
        [FieldOffset(51)] private byte _pad19;
        [FieldOffset(52)] private byte _pad20;
        [FieldOffset(53)] private byte _pad21;
        [FieldOffset(54)] private byte _pad22;
        [FieldOffset(55)] private byte _pad23;
        [FieldOffset(56)] private byte _pad24;
        [FieldOffset(57)] private byte _pad25;
        [FieldOffset(58)] private byte _pad26;
        [FieldOffset(59)] private byte _pad27;
        [FieldOffset(60)] private byte _pad28;
        [FieldOffset(61)] private byte _pad29;
        [FieldOffset(62)] private byte _pad30;
        [FieldOffset(63)] private byte _pad31;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct WreckScorchDecalRecord
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public float Radius;
        [FieldOffset(28)] public float Intensity;
        [FieldOffset(32)] public uint StableId;
        [FieldOffset(36)] public byte ModuleId;
        [FieldOffset(37)] private byte _pad0;
        [FieldOffset(38)] private byte _pad1;
        [FieldOffset(39)] private byte _pad2;
        [FieldOffset(40)] private byte _pad3;
        [FieldOffset(41)] private byte _pad4;
        [FieldOffset(42)] private byte _pad5;
        [FieldOffset(43)] private byte _pad6;
        [FieldOffset(44)] private byte _pad7;
        [FieldOffset(45)] private byte _pad8;
        [FieldOffset(46)] private byte _pad9;
        [FieldOffset(47)] private byte _pad10;
        [FieldOffset(48)] private byte _pad11;
        [FieldOffset(49)] private byte _pad12;
        [FieldOffset(50)] private byte _pad13;
        [FieldOffset(51)] private byte _pad14;
        [FieldOffset(52)] private byte _pad15;
        [FieldOffset(53)] private byte _pad16;
        [FieldOffset(54)] private byte _pad17;
        [FieldOffset(55)] private byte _pad18;
        [FieldOffset(56)] private byte _pad19;
        [FieldOffset(57)] private byte _pad20;
        [FieldOffset(58)] private byte _pad21;
        [FieldOffset(59)] private byte _pad22;
        [FieldOffset(60)] private byte _pad23;
        [FieldOffset(61)] private byte _pad24;
        [FieldOffset(62)] private byte _pad25;
        [FieldOffset(63)] private byte _pad26;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct WreckBurialCutRecord
    {
        [FieldOffset(0)] public double3 AbsoluteCenter;
        [FieldOffset(24)] public float3 HalfExtents;
        [FieldOffset(36)] public float BlendStrength;
        [FieldOffset(40)] public uint StableId;
        [FieldOffset(44)] public byte MaterialId;
        [FieldOffset(45)] public byte Applied;
        [FieldOffset(46)] private byte _pad0;
        [FieldOffset(47)] private byte _pad1;
        [FieldOffset(48)] private byte _pad2;
        [FieldOffset(49)] private byte _pad3;
        [FieldOffset(50)] private byte _pad4;
        [FieldOffset(51)] private byte _pad5;
        [FieldOffset(52)] private byte _pad6;
        [FieldOffset(53)] private byte _pad7;
        [FieldOffset(54)] private byte _pad8;
        [FieldOffset(55)] private byte _pad9;
        [FieldOffset(56)] private byte _pad10;
        [FieldOffset(57)] private byte _pad11;
        [FieldOffset(58)] private byte _pad12;
        [FieldOffset(59)] private byte _pad13;
        [FieldOffset(60)] private byte _pad14;
        [FieldOffset(61)] private byte _pad15;
        [FieldOffset(62)] private byte _pad16;
        [FieldOffset(63)] private byte _pad17;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct WreckTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint EventHash;
        [FieldOffset(8)] public uint Seed;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public float3 Position;
        [FieldOffset(28)] public int DebrisCount;
        [FieldOffset(32)] public int ArtifactCount;
        [FieldOffset(36)] public int SealedCount;
        [FieldOffset(40)] public int RupturedCount;
        [FieldOffset(44)] public float Value0;
        [FieldOffset(48)] public float Value1;
        [FieldOffset(52)] private byte _pad0;
        [FieldOffset(53)] private byte _pad1;
        [FieldOffset(54)] private byte _pad2;
        [FieldOffset(55)] private byte _pad3;
        [FieldOffset(56)] private byte _pad4;
        [FieldOffset(57)] private byte _pad5;
        [FieldOffset(58)] private byte _pad6;
        [FieldOffset(59)] private byte _pad7;
        [FieldOffset(60)] private byte _pad8;
        [FieldOffset(61)] private byte _pad9;
        [FieldOffset(62)] private byte _pad10;
        [FieldOffset(63)] private byte _pad11;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct XorShift32State
    {
        public uint State;

        public XorShift32State(uint seed)
        {
            State = seed == 0u ? 0x6E624EB7u : seed;
        }

        public uint NextUInt()
        {
            uint x = State;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            State = x;
            return x;
        }

        public float NextFloat01()
        {
            return NextUInt() * 2.3283064365386963e-10f;
        }
    }

#if UNITY_EDITOR
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct CombineMeshDataJob : IJob
    {
        public const uint AttributeFlagNormals = 1u << 0;
        public const uint AttributeFlagUvs = 1u << 1;
        public const uint AttributeFlagColors = 1u << 2;

        [ReadOnly] public Mesh.MeshData SourceMeshData;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<WreckMergedVertex> DestinationVertices;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<uint> DestinationIndices;
        public int VertexOffset;
        public int IndexOffset;
        public int PositionStream;
        public int NormalStream;
        public int UvStream;
        public int ColorStream;
        public int PositionOffset;
        public int PositionStride;
        public int NormalOffset;
        public int NormalStride;
        public int UvOffset;
        public int UvStride;
        public int ColorOffset;
        public int ColorStride;
        public int SubMeshCount;
        public uint AttributeFlags;
        public float4x4 LocalToWreck;
        public quaternion Rotation;

        public void Execute()
        {
            bool hasNormals = (AttributeFlags & AttributeFlagNormals) != 0u;
            bool hasUvs = (AttributeFlags & AttributeFlagUvs) != 0u;
            bool hasColors = (AttributeFlags & AttributeFlagColors) != 0u;
            byte* positionBase = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(SourceMeshData.GetVertexData<byte>(PositionStream)) + PositionOffset;
            byte* normalBase = hasNormals
                ? (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(SourceMeshData.GetVertexData<byte>(NormalStream)) + NormalOffset
                : null;
            byte* uvBase = hasUvs
                ? (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(SourceMeshData.GetVertexData<byte>(UvStream)) + UvOffset
                : null;
            byte* colorBase = hasColors
                ? (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(SourceMeshData.GetVertexData<byte>(ColorStream)) + ColorOffset
                : null;

            int vertexCount = SourceMeshData.vertexCount;
            for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
            {
                Vector3 sourcePosition = UnsafeUtility.ReadArrayElementWithStride<Vector3>(positionBase, vertexIndex, PositionStride);
                float3 transformedPosition = math.transform(LocalToWreck, new float3(sourcePosition.x, sourcePosition.y, sourcePosition.z));
                if (!math.all(math.isfinite(transformedPosition)))
                    transformedPosition = float3.zero;

                float3 transformedNormal = new float3(0f, 1f, 0f);
                if (hasNormals)
                {
                    Vector3 sourceNormal = UnsafeUtility.ReadArrayElementWithStride<Vector3>(normalBase, vertexIndex, NormalStride);
                    float3 rotatedNormal = math.rotate(Rotation, new float3(sourceNormal.x, sourceNormal.y, sourceNormal.z));
                    float normalLengthSq = math.lengthsq(rotatedNormal);
                    transformedNormal = math.select(
                        transformedNormal,
                        rotatedNormal * math.rsqrt(math.max(normalLengthSq, 0.000001f)),
                        math.isfinite(normalLengthSq) && normalLengthSq > 0.000001f);
                }

                float2 uv = float2.zero;
                if (hasUvs)
                {
                    Vector2 sourceUv = UnsafeUtility.ReadArrayElementWithStride<Vector2>(uvBase, vertexIndex, UvStride);
                    uv = new float2(sourceUv.x, sourceUv.y);
                }

                uint packedColor = hasColors
                    ? PackColor(UnsafeUtility.ReadArrayElementWithStride<Color32>(colorBase, vertexIndex, ColorStride))
                    : ResolveProceduralRustAlgaeColor(transformedPosition, transformedNormal, vertexIndex);

                DestinationVertices[VertexOffset + vertexIndex] = new WreckMergedVertex
                {
                    Position = transformedPosition,
                    Normal = transformedNormal,
                    UV = uv,
                    Color = packedColor
                };
            }

            int writeIndex = IndexOffset;
            if (SourceMeshData.indexFormat == IndexFormat.UInt32)
            {
                NativeArray<uint> sourceIndices = SourceMeshData.GetIndexData<uint>();
                for (int subMeshIndex = 0; subMeshIndex < SubMeshCount; subMeshIndex++)
                {
                    SubMeshDescriptor descriptor = SourceMeshData.GetSubMesh(subMeshIndex);
                    int descriptorEnd = descriptor.indexStart + descriptor.indexCount;
                    for (int sourceIndex = descriptor.indexStart; sourceIndex < descriptorEnd; sourceIndex++)
                        DestinationIndices[writeIndex++] = sourceIndices[sourceIndex] + (uint)VertexOffset;
                }
            }
            else
            {
                NativeArray<ushort> sourceIndices = SourceMeshData.GetIndexData<ushort>();
                for (int subMeshIndex = 0; subMeshIndex < SubMeshCount; subMeshIndex++)
                {
                    SubMeshDescriptor descriptor = SourceMeshData.GetSubMesh(subMeshIndex);
                    int descriptorEnd = descriptor.indexStart + descriptor.indexCount;
                    for (int sourceIndex = descriptor.indexStart; sourceIndex < descriptorEnd; sourceIndex++)
                        DestinationIndices[writeIndex++] = (uint)(sourceIndices[sourceIndex] + VertexOffset);
                }
            }
        }

        private static uint ResolveProceduralRustAlgaeColor(float3 position, float3 normal, int vertexIndex)
        {
            uint hash = Mix(math.asuint(position.x) ^ (math.asuint(position.y) * 747796405u) ^ (math.asuint(position.z) * 2891336453u) ^ (uint)vertexIndex);
            float rustNoise = (hash & 0xFFu) * (1f / 255f);
            float algaeNoise = ((hash >> 8) & 0xFFu) * (1f / 255f);
            float verticalRust = 1f - math.abs(normal.y);
            float upFacingAlgae = math.max(0f, normal.y);
            byte rust = QuantizeByte(0.18f + verticalRust * 0.35f + rustNoise * 0.47f);
            byte algae = QuantizeByte(upFacingAlgae * 0.45f + algaeNoise * 0.25f);
            return PackColor(rust, algae, 0, 255);
        }

        private static uint PackColor(Color32 color)
        {
            return PackColor(color.r, color.g, color.b, color.a);
        }

        private static uint PackColor(byte r, byte g, byte b, byte a)
        {
            return (uint)r | ((uint)g << 8) | ((uint)b << 16) | ((uint)a << 24);
        }

        private static byte QuantizeByte(float value)
        {
            return (byte)math.clamp((int)(math.saturate(value) * 255f + 0.5f), 0, 255);
        }

        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return value;
        }
    }
#endif

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct BuildWreckRenderPayloadJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<WreckModulePlacement> Placements;
        [NoAlias] public NativeArray<Matrix4x4> WorldMatrices;
        [NoAlias] public NativeArray<byte> ModuleIds;
        [NoAlias] public NativeArray<float> Ages;
        public AbsoluteUniversePosition CenterAup;
        public float3 RuntimeOrigin;
        public float ScatterRadiusMeters;
        public float ScatterVerticalMeters;
        public int ScatterYawEnabled;
        public uint Seed;

        public void Execute(int index)
        {
            WreckModulePlacement placement = Placements[index];
            ExecuteOne(
                in placement,
                index,
                in CenterAup,
                RuntimeOrigin,
                ScatterRadiusMeters,
                ScatterVerticalMeters,
                ScatterYawEnabled,
                Seed,
                out Matrix4x4 matrix,
                out byte moduleId,
                out float age);
            WorldMatrices[index] = matrix;
            ModuleIds[index] = moduleId;
            Ages[index] = age;
        }

        public static void ExecuteOne(
            in WreckModulePlacement placement,
            int index,
            in AbsoluteUniversePosition centerAup,
            float3 runtimeOrigin,
            float scatterRadiusMeters,
            float scatterVerticalMeters,
            int scatterYawEnabled,
            uint seed,
            out Matrix4x4 matrix,
            out byte moduleId,
            out float age)
        {
            quaternion rotation = math.all(math.isfinite(placement.Rotation.value))
                ? placement.Rotation
                : quaternion.identity;
            uint hash = ComputeInstanceHash(in centerAup, seed, placement.MortonIndex, index, placement.ModuleId);
            float3 scatterOffset = ResolveScatterOffset(hash, scatterRadiusMeters, scatterVerticalMeters);
            quaternion scatterRotation = ResolveScatterRotation(hash, scatterYawEnabled);
            float3 worldPosition = placement.Position + runtimeOrigin + scatterOffset;
            if (!math.all(math.isfinite(worldPosition)))
                worldPosition = runtimeOrigin;

            rotation = math.mul(scatterRotation, rotation);
            float4x4 localToWorld = float4x4.TRS(worldPosition, rotation, new float3(1f));
            matrix = ToMatrix4x4(localToWorld);
            moduleId = placement.ModuleId;
            age = ComputeAge01(hash);
        }

        private static float3 ResolveScatterOffset(uint hash, float radiusMeters, float verticalMeters)
        {
            float safeRadius = math.max(0f, radiusMeters);
            float safeVertical = math.max(0f, verticalMeters);
            float safeExtent = safeRadius * 0.7f;
            hash = Mix(hash ^ 0x6D2B79F5u);
            float horizontalX = (HashToUnitFloat(hash) * 2f - 1f) * safeExtent;
            hash = Mix(hash ^ 0x9E3779B9u);
            float horizontalZ = (HashToUnitFloat(hash) * 2f - 1f) * safeExtent;
            hash = Mix(hash ^ 0xBB67AE85u);
            float vertical = (HashToUnitFloat(hash) * 2f - 1f) * safeVertical;
            return new float3(horizontalX, vertical, horizontalZ);
        }

        private static quaternion ResolveScatterRotation(uint hash, int yawEnabled)
        {
            if (yawEnabled == 0)
                return quaternion.identity;

            hash = Mix(hash ^ 0x3C6EF372u);
            return ResolveCardinalYaw(hash);
        }

        private static quaternion ResolveCardinalYaw(uint hash)
        {
            const float HalfTurnSin = 0.70710677f;
            switch (hash & 3u)
            {
                case 0u:
                    return quaternion.identity;
                case 1u:
                    return new quaternion(0f, HalfTurnSin, 0f, HalfTurnSin);
                case 2u:
                    return new quaternion(0f, 1f, 0f, 0f);
                default:
                    return new quaternion(0f, -HalfTurnSin, 0f, HalfTurnSin);
            }
        }

        private static uint ComputeInstanceHash(
            in AbsoluteUniversePosition centerAup,
            uint seed,
            int mortonIndex,
            int instanceIndex,
            byte moduleId)
        {
            uint hash = seed ^ 0xA511E9B3u;
            hash = Mix(hash ^ unchecked((uint)centerAup.GridX));
            hash = Mix(hash ^ unchecked((uint)centerAup.GridY));
            hash = Mix(hash ^ unchecked((uint)centerAup.GridZ));
            hash = Mix(hash ^ math.asuint(centerAup.LocalX));
            hash = Mix(hash ^ math.asuint(centerAup.LocalY));
            hash = Mix(hash ^ math.asuint(centerAup.LocalZ));
            hash = Mix(hash ^ unchecked((uint)mortonIndex * 747796405u));
            hash = Mix(hash ^ unchecked((uint)instanceIndex * 2891336453u));
            hash = Mix(hash ^ unchecked((uint)moduleId * 1597334677u));
            return hash;
        }

        private static Matrix4x4 ToMatrix4x4(float4x4 source)
        {
            Matrix4x4 matrix = default;
            matrix.m00 = source.c0.x;
            matrix.m10 = source.c0.y;
            matrix.m20 = source.c0.z;
            matrix.m30 = source.c0.w;
            matrix.m01 = source.c1.x;
            matrix.m11 = source.c1.y;
            matrix.m21 = source.c1.z;
            matrix.m31 = source.c1.w;
            matrix.m02 = source.c2.x;
            matrix.m12 = source.c2.y;
            matrix.m22 = source.c2.z;
            matrix.m32 = source.c2.w;
            matrix.m03 = source.c3.x;
            matrix.m13 = source.c3.y;
            matrix.m23 = source.c3.z;
            matrix.m33 = source.c3.w;
            return matrix;
        }

        private static float ComputeAge01(uint hash)
        {
            hash = Mix(hash ^ 0xC2B2AE35u);
            return math.saturate(0.18f + ((hash & 0x00FFFFFFu) * (1f / 16777215f)) * 0.82f);
        }

        private static float HashToUnitFloat(uint hash)
        {
            return (hash & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return value;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct BuildWreckScatterMatricesJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<Matrix4x4> WorldMatrices;
        [NoAlias] public NativeArray<byte> ModuleIds;
        [NoAlias] public NativeArray<float> Ages;
        public AbsoluteUniversePosition CenterAup;
        public float3 RuntimeOrigin;
        public int ModuleCount;
        public float ScatterRadiusMeters;
        public float ScatterVerticalMeters;
        public int ScatterYawEnabled;
        public float MinScale;
        public float MaxScale;
        public uint Seed;

        public void Execute(int index)
        {
            ExecuteOne(
                index,
                in CenterAup,
                RuntimeOrigin,
                ModuleCount,
                ScatterRadiusMeters,
                ScatterVerticalMeters,
                ScatterYawEnabled,
                MinScale,
                MaxScale,
                Seed,
                out Matrix4x4 matrix,
                out byte moduleId,
                out float age);
            WorldMatrices[index] = matrix;
            ModuleIds[index] = moduleId;
            Ages[index] = age;
        }

        public static void ExecuteOne(
            int index,
            in AbsoluteUniversePosition centerAup,
            float3 runtimeOrigin,
            int moduleCount,
            float scatterRadiusMeters,
            float scatterVerticalMeters,
            int scatterYawEnabled,
            float minScale,
            float maxScale,
            uint seed,
            out Matrix4x4 matrix,
            out byte moduleId,
            out float age)
        {
            int safeModuleCount = math.max(1, math.min(moduleCount, 16));
            uint hash = ComputeInstanceHash(in centerAup, seed, index);
            float3 offset = ResolveScatterOffset(hash, scatterRadiusMeters, scatterVerticalMeters);
            quaternion rotation = ResolveScatterRotation(hash, scatterYawEnabled);
            float scale = ResolveScale(hash, minScale, maxScale);
            float3 worldPosition = runtimeOrigin + offset;
            if (!math.all(math.isfinite(worldPosition)))
                worldPosition = runtimeOrigin;

            float4x4 localToWorld = float4x4.TRS(worldPosition, rotation, new float3(scale));
            matrix = ToMatrix4x4(localToWorld);
            moduleId = (byte)(Mix(hash ^ 0xA24BAED5u) % (uint)safeModuleCount);
            age = ComputeAge01(hash);
        }

        private static float3 ResolveScatterOffset(uint hash, float radiusMeters, float verticalMeters)
        {
            float safeRadius = math.max(0f, radiusMeters);
            float safeVertical = math.max(0f, verticalMeters);
            float safeExtent = safeRadius * 0.7f;
            hash = Mix(hash ^ 0x6D2B79F5u);
            float horizontalX = (HashToUnitFloat(hash) * 2f - 1f) * safeExtent;
            hash = Mix(hash ^ 0x9E3779B9u);
            float horizontalZ = (HashToUnitFloat(hash) * 2f - 1f) * safeExtent;
            hash = Mix(hash ^ 0xBB67AE85u);
            float vertical = (HashToUnitFloat(hash) * 2f - 1f) * safeVertical;
            return new float3(horizontalX, vertical, horizontalZ);
        }

        private static quaternion ResolveScatterRotation(uint hash, int yawEnabled)
        {
            if (yawEnabled == 0)
                return quaternion.identity;

            hash = Mix(hash ^ 0x3C6EF372u);
            return ResolveCardinalYaw(hash);
        }

        private static quaternion ResolveCardinalYaw(uint hash)
        {
            const float HalfTurnSin = 0.70710677f;
            switch (hash & 3u)
            {
                case 0u:
                    return quaternion.identity;
                case 1u:
                    return new quaternion(0f, HalfTurnSin, 0f, HalfTurnSin);
                case 2u:
                    return new quaternion(0f, 1f, 0f, 0f);
                default:
                    return new quaternion(0f, -HalfTurnSin, 0f, HalfTurnSin);
            }
        }

        private static float ResolveScale(uint hash, float minScale, float maxScale)
        {
            float safeMin = math.max(0.05f, minScale);
            float safeMax = math.max(safeMin, maxScale);
            hash = Mix(hash ^ 0xC2B2AE35u);
            float bandStep = (safeMax - safeMin) * 0.33333334f;
            return safeMin + bandStep * (hash & 3u);
        }

        private static uint ComputeInstanceHash(
            in AbsoluteUniversePosition centerAup,
            uint seed,
            int instanceIndex)
        {
            uint hash = seed ^ 0xA511E9B3u;
            hash = Mix(hash ^ unchecked((uint)centerAup.GridX));
            hash = Mix(hash ^ unchecked((uint)centerAup.GridY));
            hash = Mix(hash ^ unchecked((uint)centerAup.GridZ));
            hash = Mix(hash ^ math.asuint(centerAup.LocalX));
            hash = Mix(hash ^ math.asuint(centerAup.LocalY));
            hash = Mix(hash ^ math.asuint(centerAup.LocalZ));
            hash = Mix(hash ^ unchecked((uint)instanceIndex * 2891336453u));
            return hash;
        }

        private static Matrix4x4 ToMatrix4x4(float4x4 source)
        {
            Matrix4x4 matrix = default;
            matrix.m00 = source.c0.x;
            matrix.m10 = source.c0.y;
            matrix.m20 = source.c0.z;
            matrix.m30 = source.c0.w;
            matrix.m01 = source.c1.x;
            matrix.m11 = source.c1.y;
            matrix.m21 = source.c1.z;
            matrix.m31 = source.c1.w;
            matrix.m02 = source.c2.x;
            matrix.m12 = source.c2.y;
            matrix.m22 = source.c2.z;
            matrix.m32 = source.c2.w;
            matrix.m03 = source.c3.x;
            matrix.m13 = source.c3.y;
            matrix.m23 = source.c3.z;
            matrix.m33 = source.c3.w;
            return matrix;
        }

        private static float ComputeAge01(uint hash)
        {
            hash = Mix(hash ^ 0xC2B2AE35u);
            return math.saturate(0.18f + ((hash & 0x00FFFFFFu) * (1f / 16777215f)) * 0.82f);
        }

        private static float HashToUnitFloat(uint hash)
        {
            return (hash & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return value;
        }
    }

    [Serializable]
    internal struct WreckSiteVoronoiGateParameters
    {
        public double AupCellSizeMeters;
        public double PlateCellSizeMeters;
        public float RidgeWidthMeters;
        public float JunctionWidthMeters;
        public float PlateUniformity;
        public float DomainWarpMeters;
        public float DomainWarpFrequency;
        public float TrenchWidthMeters;
        public float IslandCenterRadiusMeters;
        public float IslandJunctionThreshold;
        public uint Seed;
    }

    /// <summary>
    /// Deterministic wave-function-collapse wreck generator operating in absolute-universe space.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralWreckGenerator : MonoBehaviour, IProceduralGenerator, IUpdatable, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int MaxModuleDefinitions = 16;
        private const byte UncollapsedModuleId = byte.MaxValue;
        private const byte DirectionNorth = 0;
        private const byte DirectionEast = 1;
        private const byte DirectionSouth = 2;
        private const byte DirectionWest = 3;
        private const byte DirectionTop = 4;
        private const byte DirectionBottom = 5;
        private const int AsyncGenerationStageYieldThresholdFrames = 1;
        private const int AsyncMeshBuildSliceCheckInterval = 4;
        private const int AsyncMeshBuildYieldWatchdogFrames = 600;
        private const double AsyncGenerationStageMainThreadBudgetSeconds = 0.0015d;
        private const double AsyncMeshBuildMainThreadBudgetSeconds = 0.0045d;
        private const int MaxEditorPreviewCellBudget = 256;
        private const int MaxScalabilityPlacementCap = 250;
        private const int MaxDebrisRecords = 10000;
        private const int MinQualityDebrisRecords = 512;
        private const int MaxDebrisClusters = 512;
        private const int MaxLootRecords = 16;
        private const int MaxWreckBlackBoxFrames = 300;
        private const int MinArtifactDiscoveryScanSlice = 24;
        private const int MaxArtifactDiscoveryScanSlice = 192;
        private const float DebrisActivationDistanceMeters = 5f;
        private const float DebrisActivationDistanceSq = DebrisActivationDistanceMeters * DebrisActivationDistanceMeters;
        private const float DebrisSpatialCellSizeMeters = 5f;
        private const float DebrisSpatialCellSizeMetersInv = 1f / DebrisSpatialCellSizeMeters;
        private const float DebrisClusterSizeMeters = 50f;
        private const float DebrisClusterSizeMetersInv = 1f / DebrisClusterSizeMeters;
        private const float ArtifactDiscoveryDistanceSq = 36f;
        private const uint LoreFragmentSalt = 0x4C465247u; // LFRG
        private const uint DebrisFieldSalt = 0x44534252u; // DSBR
        private const uint ScorchDecalSalt = 0x53434852u; // SCHR
        private const uint WreckTelemetryGenerationHash = 0x5747454Eu; // WGEN
        private const uint WreckTelemetryInteractionHash = 0x57494E54u; // WINT
        private const uint WreckTelemetryNanHash = 0x574E414Eu; // WNAN
        private const uint WreckTelemetryFailureHash = 0x57464149u; // WFAI
        private const uint FallbackSectionSalt = 0xA511E9B3u;
        private const float WreckSolveTelemetryThresholdMs = 0.2f;
        private const int MaxPendingLootSpawns = 8;
        private const uint WreckSolveBudgetWarningHash = 0x57534C56u; // WSLV
        private const uint WreckBrgContextHash = 0x57425247u; // WBRG
        private const uint WreckBlackBoxDumpMagic = 0x57313731u; // W171
        private const int WreckBlackBoxEntrySizeBytes = 64;
        private const float WreckMinimumGenerationQuality01 = 0.0f;
        private const float WreckMaximumGenerationQuality01 = 1.0f;
#if UNITY_EDITOR
        private const string GeneratedMergedMeshName = "ProceduralWreckGenerator_Merged";
#endif
        private const string BlackBoxDumpPath = "Docs/AgentLogs/Dump_1717_WreckGenerator.bin";
        private const SystemID WreckVaultOwner = SystemID.WorldStreaming;
        private static ulong WreckVaultMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private const BufferID WreckGeneratorGridBufferId = BufferID.WreckGeneratorGrid;
        private const BufferID WreckGeneratorPropagationQueueBufferId = BufferID.WreckGeneratorPropagationQueue;
        private const BufferID WreckGeneratorAllPlacementsBufferId = BufferID.WreckGeneratorAllPlacements;
        private const BufferID WreckGeneratorFilteredPlacementsBufferId = BufferID.WreckGeneratorFilteredPlacements;
        private const BufferID WreckGeneratorRuntimeDefinitionsBufferId = BufferID.WreckGeneratorRuntimeDefinitions;
        private const BufferID WreckGeneratorLootRecordsBufferId = BufferID.WreckGeneratorLootRecords;
        private const BufferID WreckGeneratorDebrisRecordsBufferId = BufferID.WreckGeneratorDebrisRecords;
        private const BufferID WreckGeneratorDebrisSpatialKeysBufferId = BufferID.WreckGeneratorDebrisSpatialKeys;
        private const BufferID WreckGeneratorDebrisClustersBufferId = BufferID.WreckGeneratorDebrisClusters;
        private const BufferID WreckGeneratorArtifactRecordsBufferId = BufferID.WreckGeneratorArtifactRecords;
        private const BufferID WreckGeneratorScorchDecalRecordsBufferId = BufferID.WreckGeneratorScorchDecalRecords;
        private const BufferID WreckGeneratorBurialCutRecordsBufferId = BufferID.WreckGeneratorBurialCutRecords;
        private const BufferID WreckGeneratorTelemetryEntriesBufferId = BufferID.WreckGeneratorTelemetryEntries;
        private const BufferID WreckGeneratorRenderWorldMatricesBufferId = BufferID.WreckGeneratorRenderWorldMatrices;
        private const BufferID WreckGeneratorRenderModuleIdsBufferId = BufferID.WreckGeneratorRenderModuleIds;
        private const BufferID WreckGeneratorRenderAgesBufferId = BufferID.WreckGeneratorRenderAges;
        private static readonly int _wreckEmergencyFlickerId = Shader.PropertyToID("_HectonWreckEmergencyFlicker");
        private static readonly int _wreckEmergencyPhaseId = Shader.PropertyToID("_HectonWreckEmergencyPhase");
        private static readonly WreckSiteVoronoiGateParameters DefaultWreckSiteGateParameters =
            new WreckSiteVoronoiGateParameters
            {
                AupCellSizeMeters = AbsoluteUniversePosition.CellSizeMeters,
                PlateCellSizeMeters = 4200.0,
                RidgeWidthMeters = 1450f,
                JunctionWidthMeters = 2800f,
                PlateUniformity = 0.78f,
                DomainWarpMeters = 1450f,
                DomainWarpFrequency = 0.00011f,
                TrenchWidthMeters = 780f,
                IslandCenterRadiusMeters = 2600f,
                IslandJunctionThreshold = 0.58f,
                Seed = HectonSandboxAbyssalShelfMath.CombineWorldSeed(880031u, 0)
            };

        private struct PendingWreckLootSpawn
        {
            public GameObject Prefab;
            public Hecton8.Items.ItemData ItemData;
            public Vector3 Position;
            public Quaternion Rotation;
            public int Quantity;
            public int DebrisRecordIndex;
        }

        private struct VaultArrayBuffer<T> where T : struct
        {
            private IDataVault _vault;
            private IDataVault _writeLockVault;
            private VaultGenerationHandle<T> _handle;
            private int _length;

            public bool IsCreated => _vault != null && _handle.BufferID != 0u && _length > 0;
            public int Length => _length;

            public void Bind(IDataVault vault, VaultGenerationHandle<T> handle, int length)
            {
                _vault = vault;
                _writeLockVault = null;
                _handle = handle;
                _length = math.max(0, length);
            }

            public bool TryResolve(out NativeArray<T> buffer)
            {
                buffer = default;
                return _vault != null &&
                       _handle.BufferID != 0u &&
                       !_vault.IsCompactionFenceActive &&
                       _vault.TryResolveHandle(in _handle, out buffer) &&
                       buffer.IsCreated &&
                       !_vault.IsCompactionFenceActive &&
                       buffer.Length >= _length;
            }

            public bool TryResolveReadOnly(out NativeArray<T>.ReadOnly buffer)
            {
                buffer = default;
                return _vault != null &&
                       _handle.BufferID != 0u &&
                       !_vault.IsCompactionFenceActive &&
                       _vault.TryReadOnlyHandle(in _handle, out buffer) &&
                       buffer.IsCreated &&
                       !_vault.IsCompactionFenceActive &&
                       buffer.Length >= _length;
            }

            public bool TrySet(int index, in T value)
            {
                IDataVault vault = _vault;
                if ((uint)index >= (uint)_length ||
                    vault == null ||
                    _handle.BufferID == 0u ||
                    vault.IsCompactionFenceActive ||
                    !vault.TryAcquireWriteLock(in _handle, WreckVaultOwner, out NativeArray<T> buffer))
                {
                    return false;
                }

                try
                {
                    if (!buffer.IsCreated ||
                        buffer.Length < _length ||
                        vault.IsCompactionFenceActive)
                    {
                        return false;
                    }

                    buffer[index] = value;
                    return true;
                }
                finally
                {
                    vault.ReleaseWriteLock(in _handle, WreckVaultOwner);
                }
            }

            public bool TryLockForWrite(out NativeArray<T> buffer)
            {
                buffer = default;
                IDataVault vault = _vault;
                if (vault == null ||
                    _writeLockVault != null ||
                    _handle.BufferID == 0u ||
                    vault.IsCompactionFenceActive ||
                    !vault.TryAcquireWriteLock(in _handle, WreckVaultOwner, out buffer))
                {
                    return false;
                }

                bool ownershipTransferred = false;
                try
                {
                    if (!buffer.IsCreated ||
                        buffer.Length < _length ||
                        vault.IsCompactionFenceActive)
                    {
                        buffer = default;
                        return false;
                    }

                    _writeLockVault = vault;
                    ownershipTransferred = true;
                    return true;
                }
                finally
                {
                    if (!ownershipTransferred)
                        vault.ReleaseWriteLock(in _handle, WreckVaultOwner);
                }
            }

            public void ReleaseWriteLock()
            {
                IDataVault vault = _writeLockVault;
                _writeLockVault = null;
                if (vault != null && _handle.BufferID != 0u)
                    vault.ReleaseWriteLock(in _handle, WreckVaultOwner);
            }

            public bool TryGet(int index, out T value)
            {
                value = default;
                if ((uint)index >= (uint)_length || !TryResolveReadOnly(out NativeArray<T>.ReadOnly buffer))
                    return false;

                value = buffer[index];
                return true;
            }

            public void Dispose()
            {
                ReleaseWriteLock();
                if (_vault != null && _handle.BufferID != 0u)
                    _vault.ReleaseBuffer(in _handle);

                this = default;
            }
        }

        private struct VaultListBuffer<T> where T : struct
        {
            private VaultArrayBuffer<T> _storage;
            private int _length;

            public bool IsCreated => _storage.IsCreated;
            public int Length => _length;
            public int Capacity => _storage.Length;

            public void Bind(IDataVault vault, VaultGenerationHandle<T> handle, int capacity)
            {
                _storage.Bind(vault, handle, capacity);
                _length = 0;
            }

            public void Clear()
            {
                _length = 0;
            }

            public bool TryAddNoResize(in T item)
            {
                if ((uint)_length >= (uint)_storage.Length)
                    return false;

                if (_storage.TrySet(_length, in item))
                {
                    _length++;
                    return true;
                }

                return false;
            }

            public bool TrySet(int index, in T value)
            {
                if ((uint)index >= (uint)_length)
                    return false;

                return _storage.TrySet(index, in value);
            }

            public bool TryResolve(out NativeArray<T> buffer)
            {
                return _storage.TryResolve(out buffer) &&
                       buffer.IsCreated &&
                       buffer.Length >= _length;
            }

            public bool TryResolveReadOnly(out NativeArray<T>.ReadOnly buffer)
            {
                return _storage.TryResolveReadOnly(out buffer) &&
                       buffer.IsCreated &&
                       buffer.Length >= _length;
            }

            public bool TryGet(int index, out T value)
            {
                value = default;
                if ((uint)index >= (uint)_length)
                    return false;

                return _storage.TryGet(index, out value);
            }

            public void Dispose()
            {
                _storage.Dispose();
                _length = 0;
            }
        }

        private struct VaultInt3QueueBuffer
        {
            private VaultArrayBuffer<int3> _storage;
            private int _head;
            private int _tail;
            private int _count;

            public bool IsCreated => _storage.IsCreated;
            public int Count => _count;
            public int Capacity => _storage.Length;

            public void Bind(IDataVault vault, VaultGenerationHandle<int3> handle, int capacity)
            {
                _storage.Bind(vault, handle, capacity);
                Clear();
            }

            public void Clear()
            {
                _head = 0;
                _tail = 0;
                _count = 0;
            }

            public bool TryEnqueue(in int3 value)
            {
                int capacity = _storage.Length;
                if (capacity <= 0 || _count >= capacity)
                    return false;

                if (!_storage.TrySet(_tail, in value))
                    return false;

                _tail = (_tail + 1) % capacity;
                _count++;
                return true;
            }

            public bool TryDequeue(out int3 value)
            {
                value = default;
                int capacity = _storage.Length;
                if (capacity <= 0 || _count <= 0)
                    return false;

                if (!_storage.TryGet(_head, out value))
                    return false;

                _head = (_head + 1) % capacity;
                _count--;
                return true;
            }

            public void Dispose()
            {
                _storage.Dispose();
                Clear();
            }
        }

        [Header("WFC")]
        [SerializeField, Range(4, 32)]
        [Tooltip("Power-of-two grid resolution used by the wreck WFC kernel. Stored in Morton order for coherent neighbor traversal.")]
        private int gridResolution = 16;

        [SerializeField, Min(1f)]
        [Tooltip("Edge length for each WFC cell in meters.")]
        private float cellSizeMeters = 6f;

        [SerializeField, Min(1)]
        [Tooltip("Hard ceiling for generated structural placements. Clamped to the wreck mandate cap so Vault storage cannot exceed the active quality envelope.")]
        private int maxPlacements = MaxScalabilityPlacementCap;

        [SerializeField]
        [Tooltip("Additional deterministic salt mixed into the AUP hash to separate generation revisions.")]
        private uint worldGenerationVersionSalt = 1u;

        [Header("Terrain Snap")]
        [SerializeField]
        [Tooltip("If enabled, generated wreck anchors perform one MapMagicBridge AUP height sample and snap to terrain during world generation.")]
        private bool snapToMapMagicTerrainHeight = true;

        [SerializeField, Min(0f)]
        [Tooltip("Small vertical lift applied after MapMagic terrain snapping to keep proxy bounds out of terrain z-fighting.")]
        private float terrainSnapVerticalOffsetMeters = 0.05f;

        [Header("Safety")]
        [SerializeField, Min(1)]
        [Tooltip("Maximum collapse-loop iterations allowed per grid cell before the solver force-resolves remaining cells to the universal connector.")]
        private int collapseWatchdogPerCell = 4;

        [SerializeField, Min(1)]
        [Tooltip("Maximum propagation dequeue operations allowed per grid cell before the solver force-resolves remaining cells to the universal connector.")]
        private int propagationWatchdogPerCell = MaxModuleDefinitions * 6;

        [SerializeField, Min(0)]
        [Tooltip("Maximum contradictions tolerated before the solver abandons the current solve and force-resolves remaining cells to the universal connector.")]
        private int maxContradictionsBeforeFallback = 256;

        [Header("Navigation")]
        [SerializeField]
        [Tooltip("Disabled. Wreck predators use local steering/SDF queries, not baked navigation.")]
        private bool buildAsyncNavigationBake = false;

        [SerializeField, Min(0.1f)]
        [Tooltip("Legacy diver radius retained for serialized data only. Runtime navigation baking is disabled.")]
        private float navAgentRadius = 0.3f;

        [SerializeField, Min(0.5f)]
        [Tooltip("Legacy agent height retained for serialized data only. Runtime navigation baking is disabled.")]
        private float navAgentHeight = 1.8f;

        [SerializeField, Range(0f, 60f)]
        [Tooltip("Legacy climb slope retained for serialized data only. Runtime navigation baking is disabled.")]
        private float navAgentSlope = 45f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Legacy climb ledge retained for serialized data only. Runtime navigation baking is disabled.")]
        private float navAgentClimb = 0.4f;

        [Header("Modules")]
        [SerializeField]
        [Tooltip("Authoring definitions for up to 16 WFC modules. Element 0 is reserved as the contradiction-safe universal connector.")]
        private ProceduralWreckModuleDefinition[] moduleDefinitions = Array.Empty<ProceduralWreckModuleDefinition>();

        [SerializeField]
        [Tooltip("Optional BRG render owner that receives the module matrix payload after WFC collapse. When assigned, merged render meshes are skipped.")]
        private WreckMaterialRegistry wreckMaterialRegistry;

        [Header("BRG Scatter")]
        [SerializeField, Min(0f)]
        [Tooltip("Maximum deterministic horizontal scatter offset applied inside the Burst BRG matrix payload job.")]
        private float brgScatterRadiusMeters = 18f;

        [SerializeField, Min(0f)]
        [Tooltip("Maximum deterministic vertical scatter offset applied inside the Burst BRG matrix payload job.")]
        private float brgScatterVerticalMeters = 2f;

        [SerializeField, Range(0f, 180f)]
        [Tooltip("Maximum deterministic yaw offset applied inside the Burst BRG matrix payload job.")]
        private float brgScatterYawDegrees = 35f;

        [SerializeField, Range(50, 200)]
        [Tooltip("Minimum fragment instance count for the BRG-only wreck scatter path.")]
        private int brgMinFragmentCount = 50;

        [SerializeField, Range(50, 200)]
        [Tooltip("Maximum fragment instance count for the BRG-only wreck scatter path.")]
        private int brgMaxFragmentCount = 160;

        [SerializeField, Min(0.05f)]
        [Tooltip("Minimum deterministic scale applied by the Burst BRG scatter job.")]
        private float brgFragmentMinScale = 0.65f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Maximum deterministic scale applied by the Burst BRG scatter job.")]
        private float brgFragmentMaxScale = 1.35f;

        [Header("Voronoi Wreck Sites")]
        [SerializeField]
        [Tooltip("Reject generation unless the anchor lies on a sandbox abyssal-shelf Voronoi junction.")]
        private bool requireVoronoiWreckSite = true;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum Voronoi junction mask required for a wreck site.")]
        private float wreckSiteJunctionThreshold = 0.58f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum ridge mask required at the same site so wrecks sit on plate-intersection structure, not broad flats.")]
        private float wreckSiteRidgeThreshold = 0.42f;

        [SerializeField]
        [Tooltip("Sandbox abyssal shelf Voronoi parameters used for the wreck-site gate. Evaluated by HectonSandboxAbyssalShelfJobs ridge math.")]
        private WreckSiteVoronoiGateParameters wreckSiteGateParameters;

        [Header("Interactable Loot")]
        [SerializeField]
        [Tooltip("Catalog used to resolve ItemTemplate hash IDs to pooled world prefabs.")]
        private ItemCatalog itemCatalog;

        [SerializeField]
        [Tooltip("ItemTemplate hash IDs allowed inside generated wrecks. Each hash must exist in ItemTemplateRegistry and resolve to a loaded world prefab.")]
        private uint[] wreckLootItemHashes = Array.Empty<uint>();

        [SerializeField, Range(0, 8)]
        [Tooltip("Minimum pooled interactable pickups spawned inside the wreck radius.")]
        private int minLootCount = 3;

        [SerializeField, Range(0, 8)]
        [Tooltip("Maximum pooled interactable pickups spawned inside the wreck radius.")]
        private int maxLootCount = 5;

        [SerializeField, Min(0.5f)]
        [Tooltip("XZ scatter radius for the small interactable loot set.")]
        private float lootSpawnRadiusMeters = 18f;

        [Header("Wreck Gameplay Data")]
        [SerializeField]
        [Tooltip("Optional voxel volume that receives subtractive box cuts for partially buried wreck interiors.")]
        private HectonVoxelVolume wreckVoxelCutVolume;

        [SerializeField, Range(0, MaxDebrisRecords)]
        [Tooltip("Upper bound for deterministic pickable scrap records. Quality tier clamps this at generation time.")]
        private int maxDebrisRecords = MaxDebrisRecords;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Fraction of structural module bounds treated as buried and carved from voxel terrain.")]
        private float buriedWreckCutFraction = 0.32f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Half-height used for cheap SDF box cuts inside buried wreck interiors.")]
        private float wreckInteriorCutHalfHeight = 2.5f;

        [Header("Collision Proxy")]
        [SerializeField]
        [Tooltip("Prewarmed pooled prefab with exactly one BoxCollider trigger covering the wreck center.")]
        private GameObject wreckCollisionProxyPrefab;

        [SerializeField]
        [Tooltip("Legacy nav proxy mesh used only by the non-BRG fallback path.")]
        private Mesh wreckCollisionProxyMesh;

        [Header("Editor Preview")]
        [SerializeField]
        [Tooltip("True when editor gizmos should draw a bounded local footprint preview. Uses serialized dimensions only and never executes WFC.")]
        private bool drawEditorPreviewGizmos;

        [SerializeField, Range(0, MaxEditorPreviewCellBudget)]
        [Tooltip("Maximum cells drawn by OnDrawGizmos. Prevents editor-time scene repaints and script refresh from traversing the full WFC volume.")]
        private int maxEditorPreviewCells = 32;

        [Header("Diagnostics")]
        [SerializeField] private uint _debugLastSeed;
        [SerializeField] private int _debugLastPlacementCount;
        [SerializeField] private int _debugLastCellCount;
        [SerializeField] private bool _debugLastCombinedBoundsValid;
        [SerializeField] private bool _debugLastWorldBoundsValid;
        [SerializeField] private bool _debugLastProxyBoundsValid;
        [SerializeField] private WreckNavigationState _debugLastNavigationState;
        [SerializeField] private int _debugLastCollapseIterations;
        [SerializeField] private int _debugLastPropagationIterations;
        [SerializeField] private int _debugLastContradictionCount;
        [SerializeField] private int _debugLastForcedFallbackCellCount;
        [SerializeField] private WreckSolveTermination _debugLastSolveTermination;
        private VaultArrayBuffer<WreckGridCell> _grid;
        private VaultInt3QueueBuffer _propagationQueue;
        private int _propagationQueueCount;
        private int _propagationQueueCapacity;
        private int _propagationQueueDroppedCount;
        private VaultListBuffer<WreckModulePlacement> _allPlacements;
        private VaultListBuffer<WreckModulePlacement> _filteredPlacements;
        private VaultArrayBuffer<WreckModuleRuntimeDefinition> _runtimeDefinitions;
        private VaultArrayBuffer<WreckLootRecord> _lootRecords;
        private VaultArrayBuffer<WreckDebrisRecord> _debrisRecords;
        private VaultArrayBuffer<int> _debrisSpatialKeys;
        private VaultArrayBuffer<WreckDebrisCluster> _debrisClusters;
        private VaultArrayBuffer<WreckArtifactRecord> _artifactRecords;
        private VaultArrayBuffer<WreckScorchDecalRecord> _scorchDecalRecords;
        private VaultArrayBuffer<WreckBurialCutRecord> _burialCutRecords;
        private VaultArrayBuffer<WreckTelemetryEntry> _telemetryEntries;
        private VaultArrayBuffer<Matrix4x4> _renderWorldMatrices;
        private VaultArrayBuffer<byte> _renderModuleIds;
        private VaultArrayBuffer<float> _renderAges;
        private Matrix4x4[] _renderWorldMatrixSnapshot;
        private byte[] _renderModuleIdSnapshot;
        private float[] _renderAgeSnapshot;
        private List<WreckNavigationHandle> _activeNavigationHandles;
        private Mesh.MeshDataArray[] _readOnlyMeshSnapshots;
        private readonly PendingWreckLootSpawn[] _pendingLootSpawns = new PendingWreckLootSpawn[MaxPendingLootSpawns]; // COLD ALLOC: PendingWreckLootSpawn[MaxPendingLootSpawns] - one-per-frame wreck loot spawn queue - owner: ProceduralWreckGenerator
        private readonly BoxCollider[] _navGridBoxColliderScratch = new BoxCollider[1]; // COLD ALLOC: BoxCollider[1] - navgrid obstacle registration scratch - owner: ProceduralWreckGenerator
        private readonly WreckModuleRuntimeDefinition[] _runtimeDefinitionBuildCache = new WreckModuleRuntimeDefinition[MaxModuleDefinitions]; // COLD ALLOC: WreckModuleRuntimeDefinition[MaxModuleDefinitions] - unmanaged DTO staging cache copied into Vault under one lock - owner: ProceduralWreckGenerator
        private readonly WreckLootRecord[] _lootRecordBuildCache = new WreckLootRecord[MaxLootRecords]; // COLD ALLOC: WreckLootRecord[MaxLootRecords] - unmanaged DTO staging cache copied into Vault under one lock - owner: ProceduralWreckGenerator
        private readonly WreckArtifactRecord[] _artifactRecordBuildCache = new WreckArtifactRecord[MaxScalabilityPlacementCap]; // COLD ALLOC: WreckArtifactRecord[MaxScalabilityPlacementCap] - built outside Vault lock, copied under one lock - owner: ProceduralWreckGenerator
        private readonly WreckScorchDecalRecord[] _scorchDecalBuildCache = new WreckScorchDecalRecord[MaxScalabilityPlacementCap]; // COLD ALLOC: WreckScorchDecalRecord[MaxScalabilityPlacementCap] - built outside Vault lock, copied under one lock - owner: ProceduralWreckGenerator
        private readonly WreckBurialCutRecord[] _burialCutBuildCache = new WreckBurialCutRecord[MaxScalabilityPlacementCap]; // COLD ALLOC: WreckBurialCutRecord[MaxScalabilityPlacementCap] - built outside Vault lock, copied under one lock - owner: ProceduralWreckGenerator
        private readonly WreckDebrisRecord[] _debrisRecordBuildCache = new WreckDebrisRecord[MaxDebrisRecords]; // COLD ALLOC: WreckDebrisRecord[MaxDebrisRecords] - debris field staging outside Vault lock - owner: ProceduralWreckGenerator
        private readonly int[] _debrisSpatialKeyBuildCache = new int[MaxDebrisRecords]; // COLD ALLOC: int[MaxDebrisRecords] - debris spatial key staging outside Vault lock - owner: ProceduralWreckGenerator
        private readonly WreckDebrisCluster[] _debrisClusterBuildCache = new WreckDebrisCluster[MaxDebrisClusters]; // COLD ALLOC: WreckDebrisCluster[MaxDebrisClusters] - cluster staging outside Vault lock - owner: ProceduralWreckGenerator
        private readonly int[] _debrisGravityIndexSnapshot = new int[MaxDebrisRecords]; // COLD ALLOC: int[MaxDebrisRecords] - debris gravity write indices staged before Vault write lock - owner: ProceduralWreckGenerator
        private readonly float[] _debrisGravityYSnapshot = new float[MaxDebrisRecords]; // COLD ALLOC: float[MaxDebrisRecords] - debris gravity y values staged before Vault write lock - owner: ProceduralWreckGenerator
        private readonly Hecton8.Items.ItemData[] _lootItemDataCache = new Hecton8.Items.ItemData[MaxLootRecords]; // COLD ALLOC: Hecton8.Items.ItemData[MaxLootRecords] - managed item ref cache resolved outside SlowTick - owner: ProceduralWreckGenerator
        private readonly GameObject[] _lootPrefabCache = new GameObject[MaxLootRecords]; // COLD ALLOC: GameObject[MaxLootRecords] - managed prefab ref cache resolved outside SlowTick - owner: ProceduralWreckGenerator
        private static readonly CapsuleCollider[] s_EmptyCapsuleColliders = Array.Empty<CapsuleCollider>(); // COLD ALLOC: CapsuleCollider[0] - shared empty collider result - owner: ProceduralWreckGenerator
        private GameObject _activeCollisionProxy;
        private Collider _activeCollisionCollider;
        private IObjectPoolService _objectPool;
        private ITickDispatcher _dispatcher;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private HectonVoxelEngine _voxelEngine;
        private IDataVault _dataVault;
        private IDataVault _wreckVaultBufferGuardVault;
        private ulong _wreckVaultBufferGuardMask;
        private bool _wreckVaultBufferGuardHeld;
        private int _activeNavGridObstacleId;
        private int _pendingLootReadIndex;
        private int _pendingLootCount;
        private bool _registeredLootTick;
        private bool _registeredLootLateFrame;
        private bool _registeredWreckSlowTick;
        private bool _initialized;
        private int _activeGridResolution;
        private int _activePlacementLimit;
        private int _lootRecordCount;
        private int _debrisRecordCount;
        private int _debrisClusterCount;
        private int _artifactRecordCount;
        private int _scorchDecalCount;
        private int _burialCutRecordCount;
        private int _sealedModuleCount;
        private int _openedSealedModuleCount;
        private int _rupturedModuleCount;
        private int _telemetryCursor;
        private int _telemetryWrittenCount;
        private int _debrisNearFieldCursor;
        private int _debrisGravityCursor;
        private int _artifactDiscoveryCursor;
        private int _generationInFlight;
        private int _wreckVaultEpoch;
        private int _activeGenerationVaultEpoch = -1;
        private bool _blackBoxDumpRequested;
        private bool _blackBoxDumpWritten;
        private bool _blackBoxDumpFailed;
        private bool _registeredHotSwapListener;
        private uint _activeGenerationSeed;
        private Bounds _activeWorldBounds;
        private Vector3 _activeRuntimeOrigin;
        private AbsoluteUniversePosition _activeCenterAup;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            RestoreRuntimeRegistrations();
        }

        private void OnDisable()
        {
            FlushBlackBoxDumpIfRequested();
            ClearPendingLootQueue();
            TryUnregisterLootTick();
            TryUnregisterWreckSlowTick();
            DespawnActiveCollisionProxy();
            TryUnregisterHotSwapListener();
            ClearCachedRegistryServices();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Tick(float deltaTime)
        {
            if (_pendingLootCount <= 0)
            {
                TryUnregisterLootTick();
                return;
            }

            TryRegisterLootTick();
        }

        public void LateFrameTick()
        {
            FlushBlackBoxDumpIfRequested();
            if (!_blackBoxDumpRequested)
                TryUnregisterLootTick();
        }

        public void SlowTick()
        {
            FlushOneQueuedLootSpawn();
            ProcessNearFieldDebris();
            ProcessArtifactDiscovery();
            UpdateDebrisGravityStateless();
            ValidateBlackBoxState();
            if (_pendingLootCount <= 0 && _debrisRecordCount <= 0 && _artifactRecordCount <= 0)
                TryUnregisterWreckSlowTick();
            if (_pendingLootCount <= 0 && !_blackBoxDumpRequested)
                TryUnregisterLootTick();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.ObjectPool:
                    CacheObjectPoolService(currentService as ObjectPoolManager);
                    if (_pendingLootCount > 0)
                        TryRegisterLootTick();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    UnregisterDispatcherLanesForHotSwap();
                    _dispatcher = currentService as ITickDispatcher;
                    if (currentService != null && isActiveAndEnabled)
                    {
                        if (_pendingLootCount > 0 || _blackBoxDumpRequested)
                            TryRegisterLootTick();
                        if (_debrisRecordCount > 0 || _artifactRecordCount > 0)
                            TryRegisterWreckSlowTick();
                    }
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.VoxelEngineRuntime:
                    _voxelEngine = currentService as HectonVoxelEngine;
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    ClearPendingLootQueue();
                    TryUnregisterLootTick();
                    TryUnregisterWreckSlowTick();
                    ReleaseWreckVaultBuffers();
                    _dataVault = currentService as IDataVault;
                    _initialized = false;
                    if (_dataVault != null && isActiveAndEnabled)
                        Initialize();
                    break;
            }
        }

        private void OnValidate()
        {
            gridResolution = ClampPowerOfTwo(gridResolution, 4, 32);
            cellSizeMeters = Mathf.Max(1f, cellSizeMeters);
            maxPlacements = Mathf.Clamp(maxPlacements, 1, MaxScalabilityPlacementCap);
            terrainSnapVerticalOffsetMeters = Mathf.Max(0f, terrainSnapVerticalOffsetMeters);
            collapseWatchdogPerCell = Mathf.Max(1, collapseWatchdogPerCell);
            propagationWatchdogPerCell = Mathf.Max(1, propagationWatchdogPerCell);
            maxContradictionsBeforeFallback = Mathf.Max(0, maxContradictionsBeforeFallback);
            navAgentRadius = Mathf.Max(0.1f, navAgentRadius);
            navAgentHeight = Mathf.Max(0.5f, navAgentHeight);
            navAgentSlope = Mathf.Clamp(navAgentSlope, 0f, 60f);
            navAgentClimb = Mathf.Clamp(navAgentClimb, 0f, 1f);
            maxEditorPreviewCells = Mathf.Clamp(maxEditorPreviewCells, 0, MaxEditorPreviewCellBudget);
            brgScatterRadiusMeters = Mathf.Max(0f, brgScatterRadiusMeters);
            brgScatterVerticalMeters = Mathf.Max(0f, brgScatterVerticalMeters);
            brgScatterYawDegrees = Mathf.Clamp(brgScatterYawDegrees, 0f, 180f);
            brgMinFragmentCount = Mathf.Clamp(brgMinFragmentCount, 50, 200);
            brgMaxFragmentCount = Mathf.Clamp(Mathf.Max(brgMinFragmentCount, brgMaxFragmentCount), 50, 200);
            brgFragmentMinScale = Mathf.Max(0.05f, brgFragmentMinScale);
            brgFragmentMaxScale = Mathf.Max(brgFragmentMinScale, brgFragmentMaxScale);
            wreckSiteJunctionThreshold = math.saturate(wreckSiteJunctionThreshold);
            wreckSiteRidgeThreshold = math.saturate(wreckSiteRidgeThreshold);
            minLootCount = Mathf.Clamp(minLootCount, 0, 8);
            maxLootCount = Mathf.Clamp(Mathf.Max(minLootCount, maxLootCount), 0, 8);
            lootSpawnRadiusMeters = Mathf.Max(0.5f, lootSpawnRadiusMeters);
            maxDebrisRecords = Mathf.Clamp(maxDebrisRecords, 0, MaxDebrisRecords);
            buriedWreckCutFraction = math.saturate(buriedWreckCutFraction);
            wreckInteriorCutHalfHeight = Mathf.Max(0.01f, wreckInteriorCutHalfHeight);
            wreckSiteGateParameters = ResolveWreckSiteParameters();
        }

#if UNITY_EDITOR
        [ContextMenu("Bake Compound Colliders From Child Meshes")]
        private void BakeCompoundCollidersFromChildMeshes()
        {
            Hecton8.World.HectonCompoundColliderAutoFitter.BakeSelectionRoot(gameObject);
        }

        [MenuItem("Hecton/Physics/Bake Compound Colliders From Selection", priority = 217)]
        private static void BakeSelectedCompoundColliders()
        {
            GameObject[] selected = Selection.gameObjects;
            for (int i = 0; i < selected.Length; i++)
                Hecton8.World.HectonCompoundColliderAutoFitter.BakeSelectionRoot(selected[i]);
        }

        private void OnDrawGizmos()
        {
            if (!drawEditorPreviewGizmos)
                return;

            int sanitizedGridResolution = ClampPowerOfTwo(gridResolution, 4, 32);
            float sanitizedCellSize = Mathf.Max(1f, cellSizeMeters);
            int totalCellCount = sanitizedGridResolution * sanitizedGridResolution * sanitizedGridResolution;
            int previewCellCount = math.min(totalCellCount, Mathf.Clamp(maxEditorPreviewCells, 0, MaxEditorPreviewCellBudget));
            if (previewCellCount <= 0)
                return;

            float span = sanitizedGridResolution * sanitizedCellSize;
            float halfSpan = span * 0.5f;
            float halfCell = sanitizedCellSize * 0.5f;
            Vector3 anchor = transform.position;

            // Editor preview is bounded and uses serialized dimensions only.
            // It never touches the runtime WFC grid or runs the collapse solver.
            Gizmos.color = new Color(0.18f, 0.72f, 0.86f, 0.9f);
            Gizmos.DrawWireCube(anchor, Vector3.one * span);

            Gizmos.color = new Color(0.94f, 0.58f, 0.17f, 0.35f);
            for (int mortonIndex = 0; mortonIndex < previewCellCount; mortonIndex++)
            {
                int3 coord = DecodeMorton3(mortonIndex);
                Vector3 cellCenter = anchor + new Vector3(
                    (-halfSpan + halfCell) + (coord.x * sanitizedCellSize),
                    (-halfSpan + halfCell) + (coord.y * sanitizedCellSize),
                    (-halfSpan + halfCell) + (coord.z * sanitizedCellSize));
                Gizmos.DrawWireCube(cellCenter, Vector3.one * (sanitizedCellSize * 0.9f));
            }
        }
#endif

        /// <summary>
        /// Computes the deterministic AUP-derived seed used by the wreck WFC kernel.
        /// </summary>
        /// <param name="runtimePosition">Runtime position resolved through the floating-origin bridge.</param>
        /// <param name="salt">Optional additional salt.</param>
        /// <returns>Bit-safe deterministic seed.</returns>
        public static uint ComputeGenerationSeed(Vector3 runtimePosition, uint salt = 0u)
        {
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition aup))
                return MixFragmentSeed(salt ^ FallbackSectionSalt);

            return ComputeGenerationSeed(in aup, salt);
        }

        /// <summary>
        /// Generates a wreck around the requested chunk-space anchor.
        /// </summary>
        /// <param name="chunkAup">Absolute-universe chunk anchor.</param>
        /// <param name="seed">Stable caller-provided seed.</param>
        /// <returns>Generated wreck payload.</returns>
        public WreckageData Generate(int3 chunkAup, uint seed)
        {
            Initialize();
            if (!_initialized)
                return default;

            if (!TryBeginGeneration())
                return default;

            try
            {
                ApplyGenerationScalability();

                double spanMeters = ResolveActiveGridResolution() * cellSizeMeters;
                double3 absoluteOrigin = new double3(
                    chunkAup.x * spanMeters,
                    chunkAup.y * spanMeters,
                    chunkAup.z * spanMeters);

                AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromAbsolutePosition(absoluteOrigin);
                float3 runtimeOriginFloat = aup.ToRuntimeFloat3();
                Vector3 runtimeOrigin = new Vector3(runtimeOriginFloat.x, runtimeOriginFloat.y, runtimeOriginFloat.z);
                uint resolvedSeed = seed != 0u
                    ? seed
                    : ComputeGenerationSeed(in aup, worldGenerationVersionSalt ^ FallbackSectionSalt);
                if (!CanGenerateAtVoronoiWreckSite(in aup))
                {
                    MarkVoronoiGateRejected(resolvedSeed);
                    return default;
                }

                SnapOriginToTerrainHeight(ref aup, ref runtimeOrigin);
                return GenerateInternal(in aup, runtimeOrigin, resolvedSeed);
            }
            finally
            {
                EndGeneration();
            }
        }

        /// <summary>
        /// Generates a wreck through the non-blocking startup-safe pipeline.
        /// WFC collapse and placement resolution stay on the owner phase; mesh/nav
        /// finalize stages are spread across subsequent frames.
        /// </summary>
        /// <param name="chunkAup">Absolute-universe chunk anchor.</param>
        /// <param name="seed">Stable caller-provided seed.</param>
        /// <returns>Generated wreck payload.</returns>
        public async Awaitable<WreckageData> GenerateAsync(int3 chunkAup, uint seed)
        {
            Initialize();
            if (!_initialized)
                return default;

            if (!TryBeginGeneration())
                return default;

            try
            {
                ApplyGenerationScalability();

                double spanMeters = ResolveActiveGridResolution() * cellSizeMeters;
                double3 absoluteOrigin = new double3(
                    chunkAup.x * spanMeters,
                    chunkAup.y * spanMeters,
                    chunkAup.z * spanMeters);

                AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromAbsolutePosition(absoluteOrigin);
                float3 runtimeOriginFloat = aup.ToRuntimeFloat3();
                Vector3 runtimeOrigin = new Vector3(runtimeOriginFloat.x, runtimeOriginFloat.y, runtimeOriginFloat.z);
                uint resolvedSeed = seed != 0u
                    ? seed
                    : ComputeGenerationSeed(in aup, worldGenerationVersionSalt ^ FallbackSectionSalt);
                if (!CanGenerateAtVoronoiWreckSite(in aup))
                {
                    MarkVoronoiGateRejected(resolvedSeed);
                    return default;
                }

                SnapOriginToTerrainHeight(ref aup, ref runtimeOrigin);
                return await GenerateInternalAsync(aup, runtimeOrigin, resolvedSeed);
            }
            finally
            {
                EndGeneration();
            }
        }

        /// <summary>
        /// Generates a wreck payload directly from the existing mega-wreck streaming section contract.
        /// </summary>
        /// <param name="section">Published mega-wreck section payload.</param>
        /// <returns>Generated wreck payload.</returns>
        public WreckageData Generate(in HectonMapMagicVegetationBridge.MegaWreckStreamSection section)
        {
            Initialize();
            if (!_initialized)
                return default;

            if (!TryBeginGeneration())
                return default;

            try
            {
                ApplyGenerationScalability();
                if (!TryResolveAupFromRuntimeOrigin(section.WorldCenter, out AbsoluteUniversePosition aup))
                    return default;

                uint resolvedSeed = ComputeGenerationSeed(in aup, (uint)section.SectionSeed ^ worldGenerationVersionSalt);
                if (!CanGenerateAtVoronoiWreckSite(in aup))
                {
                    MarkVoronoiGateRejected(resolvedSeed);
                    return default;
                }

                Vector3 runtimeOrigin = section.WorldCenter;
                SnapOriginToTerrainHeight(ref aup, ref runtimeOrigin);
                return GenerateInternal(in aup, runtimeOrigin, resolvedSeed);
            }
            finally
            {
                EndGeneration();
            }
        }

        /// <summary>
        /// Generates a wreck payload directly from the mega-wreck stream section
        /// contract without blocking scene startup.
        /// </summary>
        /// <param name="section">Published mega-wreck section payload.</param>
        /// <returns>Generated wreck payload.</returns>
        public async Awaitable<WreckageData> GenerateAsync(HectonMapMagicVegetationBridge.MegaWreckStreamSection section)
        {
            Initialize();
            if (!_initialized)
                return default;

            if (!TryBeginGeneration())
                return default;

            try
            {
                ApplyGenerationScalability();
                if (!TryResolveAupFromRuntimeOrigin(section.WorldCenter, out AbsoluteUniversePosition aup))
                    return default;

                uint resolvedSeed = ComputeGenerationSeed(in aup, (uint)section.SectionSeed ^ worldGenerationVersionSalt);
                if (!CanGenerateAtVoronoiWreckSite(in aup))
                {
                    MarkVoronoiGateRejected(resolvedSeed);
                    return default;
                }

                Vector3 runtimeOrigin = section.WorldCenter;
                SnapOriginToTerrainHeight(ref aup, ref runtimeOrigin);
                return await GenerateInternalAsync(aup, runtimeOrigin, resolvedSeed);
            }
            finally
            {
                EndGeneration();
            }
        }

        /// <summary>
        /// Releases all persistent native state owned by this generator.
        /// </summary>
        public void Dispose()
        {
            ClearPendingLootQueue();
            TryUnregisterLootTick();
            TryUnregisterWreckSlowTick();
            DespawnActiveCollisionProxy();
            TryUnregisterHotSwapListener();

            if (_activeNavigationHandles != null)
            {
                int handleCount = _activeNavigationHandles.Count;
                for (int i = 0; i < handleCount; i++)
                    _activeNavigationHandles[i]?.Dispose();

                _activeNavigationHandles.Clear();
            }

            ReleaseWreckVaultBuffers();

            _initialized = false;
            ClearCachedRegistryServices();
        }

        private bool TryBeginGeneration()
        {
            if (Interlocked.CompareExchange(ref _generationInFlight, 1, 0) == 0)
            {
                Volatile.Write(ref _activeGenerationVaultEpoch, Volatile.Read(ref _wreckVaultEpoch));
                return true;
            }

            WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 48u, 0f, 0f);
            return false;
        }

        private void EndGeneration()
        {
            Volatile.Write(ref _activeGenerationVaultEpoch, -1);
            Volatile.Write(ref _generationInFlight, 0);
        }

        private bool CanContinueGeneration()
        {
            int generationEpoch = Volatile.Read(ref _activeGenerationVaultEpoch);
            int currentEpoch = Volatile.Read(ref _wreckVaultEpoch);
            if ((!Application.isPlaying || isActiveAndEnabled) &&
                generationEpoch >= 0 &&
                generationEpoch == currentEpoch &&
                _initialized &&
                AreWreckVaultBuffersCreated())
            {
                return true;
            }

            WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 49u, 0f, 0f);
            return false;
        }

        private static uint ComputeGenerationSeed(in AbsoluteUniversePosition aup, uint salt)
        {
            uint gridX = unchecked((uint)aup.GridX);
            uint gridY = unchecked((uint)aup.GridY);
            uint gridZ = unchecked((uint)aup.GridZ);
            uint localX = math.asuint(aup.LocalX);
            uint localY = math.asuint(aup.LocalY);
            uint localZ = math.asuint(aup.LocalZ);

            // AUP bit patterns only. Never hash reconstructed absolute floats here.
            uint hash =
                (gridX * 73856093u) ^
                (gridY * 19349663u) ^
                (gridZ * 83492791u) ^
                (localX * 2654435761u) ^
                (localY * 2246822519u) ^
                (localZ * 3266489917u);

            return (hash ^ (hash >> 16)) ^ salt;
        }

        private void ApplyGenerationScalability()
        {
            int authoredGridResolution = ClampPowerOfTwo(gridResolution, 4, 32);
            float qualityWeight01 = ResolveWreckQualityWeight01();
            _activeGridResolution = ResolveScalabilityGridResolution(authoredGridResolution, qualityWeight01);

            int tierPlacementCap = ResolveScalabilityPlacementCap(qualityWeight01);
            int storageCapacity = _allPlacements.IsCreated ? _allPlacements.Capacity : maxPlacements;
            _activePlacementLimit = math.max(1, math.min(math.min(maxPlacements, tierPlacementCap), storageCapacity));
        }

        private int ResolveActiveGridResolution()
        {
            return _activeGridResolution > 0 ? _activeGridResolution : gridResolution;
        }

        private int ResolveActivePlacementLimit()
        {
            int storageCapacity = _allPlacements.IsCreated ? _allPlacements.Capacity : maxPlacements;
            int resolved = _activePlacementLimit > 0 ? _activePlacementLimit : maxPlacements;
            return math.max(1, math.min(resolved, storageCapacity));
        }

        private static int ResolveScalabilityGridResolution(int authoredGridResolution, float qualityWeight01)
        {
            int safeAuthored = ClampPowerOfTwo(authoredGridResolution, 4, 32);
            int minimumResolution = math.min(safeAuthored, 16);
            float quality = SanitizeQualityWeight01(qualityWeight01);
            float targetResolution = math.lerp(minimumResolution, safeAuthored, quality);
            return ClampPowerOfTwoDown((int)math.round(targetResolution), 4, safeAuthored);
        }

        private static int ResolveScalabilityPlacementCap(float qualityWeight01)
        {
            float quality = SanitizeQualityWeight01(qualityWeight01);
            float curved = quality * quality;
            return math.clamp((int)math.round(math.lerp(50f, MaxScalabilityPlacementCap, curved)), 50, MaxScalabilityPlacementCap);
        }

        private void SnapOriginToTerrainHeight(ref AbsoluteUniversePosition aup, ref Vector3 runtimeOrigin)
        {
            if (!snapToMapMagicTerrainHeight)
                return;

            MapMagicBridge bridge = null;
            if (!WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref bridge) || !bridge.IsAvailable)
                return;

            double3 absolute = aup.ToAbsoluteDouble3();
            if (!TryResolveCurrentRuntimeOriginAbsolute(out double3 originAbsolute))
                return;

            double3 localDelta = absolute - originAbsolute;
            if (!math.all(math.isfinite(localDelta)))
                return;

            float localX = ClampDoubleToFloat(localDelta.x);
            float localZ = ClampDoubleToFloat(localDelta.z);
            if (!bridge.TryGetHeight(localX, localZ, out float terrainHeight) || !math.isfinite(terrainHeight))
                return;

            double snappedAbsoluteY = originAbsolute.y + terrainHeight + terrainSnapVerticalOffsetMeters;
            if (!math.isfinite(snappedAbsoluteY))
                return;

            AbsoluteUniversePosition snappedAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(absolute.x, snappedAbsoluteY, absolute.z));
            if (!IsFiniteAup(in snappedAup) ||
                !snappedAup.TryToRuntimeFloat3(out float3 snappedRuntime) ||
                !math.all(math.isfinite(snappedRuntime)))
            {
                return;
            }

            aup = snappedAup;
            runtimeOrigin = new Vector3(snappedRuntime.x, snappedRuntime.y, snappedRuntime.z);
        }

        private static float ClampDoubleToFloat(double value)
        {
            if (!math.isfinite(value))
                return 0f;

            const double MaxFloat = 3.4028234663852886E+38d;
            double clamped = math.clamp(value, -MaxFloat, MaxFloat);
            return (float)clamped;
        }

        private bool CanGenerateAtVoronoiWreckSite(in AbsoluteUniversePosition aup)
        {
            if (!requireVoronoiWreckSite)
                return true;

            HectonSandboxAbyssalShelfParams parameters = ResolveSandboxWreckSiteParameters();
            HectonSandboxAbyssalShelfMath.EvaluateVoronoiRidgeData(
                in aup,
                in parameters,
                out HectonSandboxAbyssalShelfRidgeData ridge);

            return ridge.JunctionMask >= wreckSiteJunctionThreshold &&
                   ridge.RidgeMask >= wreckSiteRidgeThreshold;
        }

        private void MarkVoronoiGateRejected(uint seed)
        {
            _debugLastSeed = seed;
            _debugLastPlacementCount = 0;
            _debugLastCellCount = 0;
            _debugLastCombinedBoundsValid = false;
            _debugLastWorldBoundsValid = false;
            _debugLastProxyBoundsValid = false;
            _debugLastNavigationState = WreckNavigationState.None;
            _debugLastSolveTermination = WreckSolveTermination.Completed;
        }

        private WreckSiteVoronoiGateParameters ResolveWreckSiteParameters()
        {
            WreckSiteVoronoiGateParameters parameters = wreckSiteGateParameters;
            if (parameters.Seed == 0u &&
                parameters.AupCellSizeMeters <= 0.0 &&
                parameters.PlateCellSizeMeters <= 0.0)
            {
                parameters = DefaultWreckSiteGateParameters;
            }

            SanitizeWreckSiteParameters(ref parameters);
            return parameters;
        }

        private HectonSandboxAbyssalShelfParams ResolveSandboxWreckSiteParameters()
        {
            WreckSiteVoronoiGateParameters parameters = ResolveWreckSiteParameters();
            return ToSandboxShelfParameters(in parameters);
        }

        private static HectonSandboxAbyssalShelfParams ToSandboxShelfParameters(
            in WreckSiteVoronoiGateParameters parameters)
        {
            return new HectonSandboxAbyssalShelfParams
            {
                AupCellSizeMeters = parameters.AupCellSizeMeters,
                DescentRadiusMeters = 15000.0,
                PlateCellSizeMeters = parameters.PlateCellSizeMeters,
                HighWorldY = 2000f,
                LowWorldY = -5000f,
                RidgeHeightMeters = 700f,
                RidgeMultiplier = 0.08f,
                RidgeWidthMeters = parameters.RidgeWidthMeters,
                JunctionWidthMeters = parameters.JunctionWidthMeters,
                PlateUniformity = parameters.PlateUniformity,
                DomainWarpMeters = parameters.DomainWarpMeters,
                DomainWarpFrequency = parameters.DomainWarpFrequency,
                SlopeNoiseFrequency = 0.00003125f,
                MacroExponentialFalloff = 3.1f,
                ShelfRunMeters = 15000f,
                ShelfTargetSlopeDegrees = 30f,
                TrenchDepthMeters = 5000f,
                TrenchWidthMeters = parameters.TrenchWidthMeters,
                TrenchSharpness = 2.4f,
                IslandCenterRadiusMeters = parameters.IslandCenterRadiusMeters,
                IslandJunctionThreshold = parameters.IslandJunctionThreshold,
                Seed = parameters.Seed,
                MacroGeologyArtifactVersion = WorldMacroGeologyFields.ArtifactVersion
            };
        }

        private static void SanitizeWreckSiteParameters(ref WreckSiteVoronoiGateParameters parameters)
        {
            if (parameters.AupCellSizeMeters <= 0.0)
                parameters.AupCellSizeMeters = AbsoluteUniversePosition.CellSizeMeters;
            if (parameters.PlateCellSizeMeters <= 0.0)
                parameters.PlateCellSizeMeters = 4200.0;

            parameters.RidgeWidthMeters = math.max(0.001f, parameters.RidgeWidthMeters);
            parameters.JunctionWidthMeters = math.max(0.001f, parameters.JunctionWidthMeters);
            parameters.PlateUniformity = math.saturate(parameters.PlateUniformity);
            parameters.DomainWarpMeters = math.max(0f, parameters.DomainWarpMeters);
            parameters.DomainWarpFrequency = math.max(0.000001f, parameters.DomainWarpFrequency);
            parameters.TrenchWidthMeters = math.max(1f, parameters.TrenchWidthMeters);
            parameters.IslandCenterRadiusMeters = math.max(1f, parameters.IslandCenterRadiusMeters);
            parameters.IslandJunctionThreshold = math.saturate(parameters.IslandJunctionThreshold);
            if (parameters.Seed == 0u)
                parameters.Seed = HectonSandboxAbyssalShelfMath.CombineWorldSeed(880031u, 0);
        }

        private void Initialize()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();

            if (_initialized)
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            gridResolution = ClampPowerOfTwo(gridResolution, 4, 32);
            maxPlacements = math.clamp(maxPlacements, 1, MaxScalabilityPlacementCap);
            int maxCellCount = gridResolution * gridResolution * gridResolution;
            int placementStorageCapacity = maxPlacements;

            _grid.Bind(
                vault,
                vault.EnsureGenerationHandle<WreckGridCell>(WreckGeneratorGridBufferId, maxCellCount, WreckVaultOwner, NativeArrayOptions.UninitializedMemory),
                maxCellCount);
            _propagationQueue.Bind(
                vault,
                vault.EnsureGenerationHandle<int3>(WreckGeneratorPropagationQueueBufferId, maxCellCount, WreckVaultOwner, NativeArrayOptions.UninitializedMemory),
                maxCellCount);
            _propagationQueueCount = 0;
            _propagationQueueCapacity = maxCellCount;
            _propagationQueueDroppedCount = 0;
            _allPlacements.Bind(
                vault,
                vault.EnsureGenerationHandle<WreckModulePlacement>(WreckGeneratorAllPlacementsBufferId, placementStorageCapacity, WreckVaultOwner, NativeArrayOptions.UninitializedMemory),
                placementStorageCapacity);
            _filteredPlacements.Bind(
                vault,
                vault.EnsureGenerationHandle<WreckModulePlacement>(WreckGeneratorFilteredPlacementsBufferId, placementStorageCapacity, WreckVaultOwner, NativeArrayOptions.UninitializedMemory),
                placementStorageCapacity);
            _runtimeDefinitions.Bind(
                vault,
                vault.EnsureGenerationHandle<WreckModuleRuntimeDefinition>(WreckGeneratorRuntimeDefinitionsBufferId, MaxModuleDefinitions, WreckVaultOwner, NativeArrayOptions.ClearMemory),
                MaxModuleDefinitions);
            _lootRecords.Bind(
                vault,
                vault.EnsureGenerationHandle<WreckLootRecord>(WreckGeneratorLootRecordsBufferId, MaxLootRecords, WreckVaultOwner, NativeArrayOptions.ClearMemory),
                MaxLootRecords);
            _debrisRecords.Bind(
                vault,
                vault.EnsureGenerationHandle<WreckDebrisRecord>(WreckGeneratorDebrisRecordsBufferId, MaxDebrisRecords, WreckVaultOwner, NativeArrayOptions.ClearMemory),
                MaxDebrisRecords);
            _debrisSpatialKeys.Bind(
                vault,
                vault.EnsureGenerationHandle<int>(WreckGeneratorDebrisSpatialKeysBufferId, MaxDebrisRecords, WreckVaultOwner, NativeArrayOptions.ClearMemory),
                MaxDebrisRecords);
            _debrisClusters.Bind(
                vault,
                vault.EnsureGenerationHandle<WreckDebrisCluster>(WreckGeneratorDebrisClustersBufferId, MaxDebrisClusters, WreckVaultOwner, NativeArrayOptions.ClearMemory),
                MaxDebrisClusters);
            _artifactRecords.Bind(
                vault,
                vault.EnsureGenerationHandle<WreckArtifactRecord>(WreckGeneratorArtifactRecordsBufferId, placementStorageCapacity, WreckVaultOwner, NativeArrayOptions.ClearMemory),
                placementStorageCapacity);
            _scorchDecalRecords.Bind(
                vault,
                vault.EnsureGenerationHandle<WreckScorchDecalRecord>(WreckGeneratorScorchDecalRecordsBufferId, placementStorageCapacity, WreckVaultOwner, NativeArrayOptions.ClearMemory),
                placementStorageCapacity);
            _burialCutRecords.Bind(
                vault,
                vault.EnsureGenerationHandle<WreckBurialCutRecord>(WreckGeneratorBurialCutRecordsBufferId, placementStorageCapacity, WreckVaultOwner, NativeArrayOptions.ClearMemory),
                placementStorageCapacity);
            _telemetryEntries.Bind(
                vault,
                vault.EnsureGenerationHandle<WreckTelemetryEntry>(WreckGeneratorTelemetryEntriesBufferId, MaxWreckBlackBoxFrames, WreckVaultOwner, NativeArrayOptions.ClearMemory),
                MaxWreckBlackBoxFrames);
            int renderPayloadCapacity = ResolveRenderPayloadCapacity();
            _renderWorldMatrices.Bind(
                vault,
                vault.EnsureGenerationHandle<Matrix4x4>(WreckGeneratorRenderWorldMatricesBufferId, renderPayloadCapacity, WreckVaultOwner, NativeArrayOptions.UninitializedMemory),
                renderPayloadCapacity);
            _renderModuleIds.Bind(
                vault,
                vault.EnsureGenerationHandle<byte>(WreckGeneratorRenderModuleIdsBufferId, renderPayloadCapacity, WreckVaultOwner, NativeArrayOptions.UninitializedMemory),
                renderPayloadCapacity);
            _renderAges.Bind(
                vault,
                vault.EnsureGenerationHandle<float>(WreckGeneratorRenderAgesBufferId, renderPayloadCapacity, WreckVaultOwner, NativeArrayOptions.UninitializedMemory),
                renderPayloadCapacity);

            if (!AreWreckVaultBuffersCreated())
            {
                ReleaseWreckVaultBuffers();
                return;
            }

            _renderWorldMatrixSnapshot = new Matrix4x4[renderPayloadCapacity]; // COLD ALLOC: Matrix4x4[renderPayloadCapacity] - BRG payload snapshot copied out of Vault before renderer publish - owner: ProceduralWreckGenerator
            _renderModuleIdSnapshot = new byte[renderPayloadCapacity]; // COLD ALLOC: byte[renderPayloadCapacity] - BRG module id snapshot copied out of Vault before renderer publish - owner: ProceduralWreckGenerator
            _renderAgeSnapshot = new float[renderPayloadCapacity]; // COLD ALLOC: float[renderPayloadCapacity] - BRG age snapshot copied out of Vault before renderer publish - owner: ProceduralWreckGenerator
            buildAsyncNavigationBake = false;
            _activeNavigationHandles = null;
            // COLD ALLOC: Mesh.MeshDataArray[placementStorageCapacity] - merged mesh read-only source snapshots - owner: ProceduralWreckGenerator
            _readOnlyMeshSnapshots = new Mesh.MeshDataArray[placementStorageCapacity];
            _activeGridResolution = gridResolution;
            _activePlacementLimit = placementStorageCapacity;

            RefreshRuntimeDefinitions();
            RefreshLootRecords();
            _initialized = true;
        }

        private void CacheRegistryServicesCold()
        {
            CacheObjectPoolService(null);

            if (_dispatcher == null)
                _dispatcher = GlobalRegistry.Dispatcher;

            if (_playerRuntimeContext == null)
                _playerRuntimeContext = GlobalRegistry.Player;

            if (_voxelEngine == null)
                WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref _voxelEngine);

            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
        }

        private void CacheObjectPoolService(ObjectPoolManager candidate)
        {
            ObjectPoolManager pool = candidate;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(pool) ||
                ObjectPoolManager.TryResolveActiveRuntime(ref pool))
            {
                _objectPool = pool;
                return;
            }

            _objectPool = null;
        }

        private bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = _objectPool as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = cached;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                _objectPool = resolved;
                pool = resolved;
                return true;
            }

            _objectPool = null;
            pool = null;
            return false;
        }

        private void ClearCachedRegistryServices()
        {
            _objectPool = null;
            _dispatcher = null;
            _playerRuntimeContext = null;
            _voxelEngine = null;
            _dataVault = null;
        }

        private bool AreWreckVaultBuffersCreated()
        {
            return _grid.IsCreated &&
                   _propagationQueue.IsCreated &&
                   _allPlacements.IsCreated &&
                   _filteredPlacements.IsCreated &&
                   _runtimeDefinitions.IsCreated &&
                   _lootRecords.IsCreated &&
                   _debrisRecords.IsCreated &&
                   _debrisSpatialKeys.IsCreated &&
                   _debrisClusters.IsCreated &&
                   _artifactRecords.IsCreated &&
                   _scorchDecalRecords.IsCreated &&
                   _burialCutRecords.IsCreated &&
                   _telemetryEntries.IsCreated &&
                   _renderWorldMatrices.IsCreated &&
                   _renderModuleIds.IsCreated &&
                   _renderAges.IsCreated;
        }

        private void ReleaseWreckVaultBuffers()
        {
            Interlocked.Increment(ref _wreckVaultEpoch);
            ReleaseWreckVaultBufferGuard();

            _grid.Dispose();
            _propagationQueue.Dispose();
            _propagationQueueCount = 0;
            _propagationQueueCapacity = 0;
            _propagationQueueDroppedCount = 0;
            _allPlacements.Dispose();
            _filteredPlacements.Dispose();
            _runtimeDefinitions.Dispose();
            _lootRecords.Dispose();
            _debrisRecords.Dispose();
            _debrisSpatialKeys.Dispose();
            _debrisClusters.Dispose();
            _artifactRecords.Dispose();
            _scorchDecalRecords.Dispose();
            _burialCutRecords.Dispose();
            _telemetryEntries.Dispose();
            _renderWorldMatrices.Dispose();
            _renderModuleIds.Dispose();
            _renderAges.Dispose();
            _renderWorldMatrixSnapshot = null;
            _renderModuleIdSnapshot = null;
            _renderAgeSnapshot = null;
            _readOnlyMeshSnapshots = null;
            _activePlacementLimit = 0;
            _lootRecordCount = 0;
            _debrisRecordCount = 0;
            _debrisClusterCount = 0;
            _artifactRecordCount = 0;
            _scorchDecalCount = 0;
            _burialCutRecordCount = 0;
            _telemetryCursor = 0;
            _debrisNearFieldCursor = 0;
            _debrisGravityCursor = 0;
            _artifactDiscoveryCursor = 0;
            ClearRuntimeDefinitionCache();
            ClearLootRecordCaches();
        }

        private bool TryLockWreckVaultBuffer(BufferID bufferId)
        {
            return TryLockWreckVaultBuffers(WreckVaultMutationGuardBit(bufferId));
        }

        private bool TryLockWreckVaultBuffers(ulong guardMask)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                guardMask == 0UL ||
                vault.IsCompactionFenceActive ||
                _wreckVaultBufferGuardHeld ||
                !vault.TryAcquireMutationGuard(guardMask))
            {
                return false;
            }

            _wreckVaultBufferGuardVault = vault;
            _wreckVaultBufferGuardMask = guardMask;
            _wreckVaultBufferGuardHeld = true;
            if (!vault.IsCompactionFenceActive)
                return true;

            UnlockWreckVaultBuffers(guardMask);
            return false;
        }

        private void UnlockWreckVaultBuffer(BufferID bufferId)
        {
            UnlockWreckVaultBuffers(WreckVaultMutationGuardBit(bufferId));
        }

        private void UnlockWreckVaultBuffers(ulong guardMask)
        {
            if (!_wreckVaultBufferGuardHeld || _wreckVaultBufferGuardMask != guardMask)
                return;

            ReleaseWreckVaultBufferGuard();
        }

        private void ReleaseWreckVaultBufferGuard()
        {
            if (!_wreckVaultBufferGuardHeld)
                return;

            IDataVault vault = _wreckVaultBufferGuardVault;
            if (vault != null)
                vault.ReleaseMutationGuard(_wreckVaultBufferGuardMask);

            _wreckVaultBufferGuardVault = null;
            _wreckVaultBufferGuardMask = 0UL;
            _wreckVaultBufferGuardHeld = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private WreckageData GenerateInternal(in AbsoluteUniversePosition centerAup, Vector3 runtimeOrigin, uint seed)
        {
            double generationStartTime = Time.realtimeSinceStartupAsDouble;
            RefreshRuntimeDefinitions();

            int moduleCount = ResolveRuntimeModuleCount();
            if (moduleCount <= 0)
                return default;

            if (ShouldUseBrgOnlyWreckPath(moduleCount))
                return GenerateBrgWreckOnly(in centerAup, runtimeOrigin, seed, moduleCount, generationStartTime);

            int activeGridResolution = ResolveActiveGridResolution();
            int cellCount = activeGridResolution * activeGridResolution * activeGridResolution;
            ushort initialMask = ResolveInitialMask(moduleCount);
            if (!InitializeGrid(cellCount, initialMask, true))
                return default;

            Mesh combinedMesh = null;
            Mesh essentialMesh = null;
            Mesh detailMesh = null;
            Mesh clutterMesh = null;
            Mesh proxyMesh = null;
            bool generatedProxyMesh = false;
            bool meshOwnershipTransferred = false;

            try
            {
                XorShift32State rng = new XorShift32State(seed);
                CollapseGrid(cellCount, moduleCount, ref rng);
                BuildPlacements(cellCount, seed);
#if UNITY_EDITOR
                if (ShouldBuildMergedMeshFallback())
                {
                    combinedMesh = BuildMergedMesh(_allPlacements);
                    essentialMesh = BuildMergedMeshForTier((byte)WreckLodTier.Essential);
                    detailMesh = BuildMergedMeshForTier((byte)WreckLodTier.Detail);
                    clutterMesh = BuildMergedMeshForTier((byte)WreckLodTier.Clutter);
                }
#endif
                proxyMesh = ResolveNavigationProxyMesh();
                generatedProxyMesh = proxyMesh != null && wreckCollisionProxyMesh == null;

                Bounds localBounds = CalculateLocalBounds(_allPlacements);
                Bounds worldBounds = TranslateBounds(localBounds, runtimeOrigin);
                Bounds renderWorldBounds = ExpandBoundsForBrgScatter(worldBounds);
                PrepareWreckWorldState(in centerAup, runtimeOrigin, renderWorldBounds, seed, moduleCount, 0);
                PublishWreckRenderPayload(in centerAup, runtimeOrigin, renderWorldBounds, seed);
                PublishCollisionProxy(worldBounds);
                SpawnWreckLoot(renderWorldBounds, seed);

                WreckNavigationHandle navigationHandle = null;
                UnityEngine.Object navigationData = null;
                AsyncOperation navigationOperation = null;
                if (buildAsyncNavigationBake && proxyMesh != null)
                    BuildDisabledNavigationBake(runtimeOrigin, worldBounds, proxyMesh, out navigationHandle, out navigationData, out navigationOperation);

                _debugLastSeed = seed;
                _debugLastPlacementCount = _allPlacements.Length;
                _debugLastCellCount = cellCount;
                _debugLastCombinedBoundsValid = _allPlacements.IsCreated && _allPlacements.Length > 0;
                _debugLastWorldBoundsValid = IsFiniteBounds(renderWorldBounds);
                _debugLastProxyBoundsValid = proxyMesh != null && IsFiniteBounds(proxyMesh.bounds);
                _debugLastNavigationState = navigationHandle != null ? navigationHandle.State : WreckNavigationState.None;
                PublishWreckSolveBudgetWarningIfNeeded((Time.realtimeSinceStartupAsDouble - generationStartTime) * 1000.0);

                WreckageData data = new WreckageData
                {
                    CombinedMesh = combinedMesh,
                    EssentialMesh = essentialMesh,
                    DetailMesh = detailMesh,
                    ClutterMesh = clutterMesh,
                    ProxyMesh = proxyMesh,
                    Navigation = navigationData,
                    NavigationBuild = navigationOperation,
                    NavigationHandle = navigationHandle,
                    NavigationState = navigationHandle != null ? navigationHandle.State : WreckNavigationState.None,
                    WorldBounds = renderWorldBounds,
                    RuntimeOrigin = runtimeOrigin,
                    GenerationSeed = seed
                };

                meshOwnershipTransferred = true;
                return data;
            }
            finally
            {
                if (!meshOwnershipTransferred)
                {
                    ReleaseGeneratedMesh(combinedMesh);
                    ReleaseGeneratedMesh(essentialMesh);
                    ReleaseGeneratedMesh(detailMesh);
                    ReleaseGeneratedMesh(clutterMesh);
                    if (generatedProxyMesh)
                        ReleaseGeneratedMesh(proxyMesh);
                }
            }
        }

        private async Awaitable<WreckageData> GenerateInternalAsync(AbsoluteUniversePosition centerAup, Vector3 runtimeOrigin, uint seed)
        {
            RefreshRuntimeDefinitions();

            int moduleCount = ResolveRuntimeModuleCount();
            if (moduleCount <= 0)
                return default;

            if (ShouldUseBrgOnlyWreckPath(moduleCount))
                return await GenerateBrgWreckOnlyAsync(centerAup, runtimeOrigin, seed, moduleCount);

            int activeGridResolution = ResolveActiveGridResolution();
            int cellCount = activeGridResolution * activeGridResolution * activeGridResolution;
            ushort initialMask = ResolveInitialMask(moduleCount);

            Mesh combinedMesh = null;
            Mesh essentialMesh = null;
            Mesh detailMesh = null;
            Mesh clutterMesh = null;
            Mesh proxyMesh = null;
            bool generatedProxyMesh = false;
            bool meshOwnershipTransferred = false;

            try
            {
                double stageStartTime = Time.realtimeSinceStartupAsDouble;
                if (!await SolveGridAsync(cellCount, initialMask, moduleCount, seed))
                    return default;
                if (!CanContinueGeneration())
                    return default;
                await YieldAfterGenerationStageAsync(stageStartTime);
                if (!CanContinueGeneration())
                    return default;

                stageStartTime = Time.realtimeSinceStartupAsDouble;
#if UNITY_EDITOR
                if (ShouldBuildMergedMeshFallback())
                {
                    combinedMesh = await BuildMergedMeshAsync(_allPlacements);
                    if (!CanContinueGeneration())
                        return default;
                    essentialMesh = await BuildMergedMeshForTierAsync((byte)WreckLodTier.Essential);
                    if (!CanContinueGeneration())
                        return default;
                    detailMesh = await BuildMergedMeshForTierAsync((byte)WreckLodTier.Detail);
                    if (!CanContinueGeneration())
                        return default;
                    clutterMesh = await BuildMergedMeshForTierAsync((byte)WreckLodTier.Clutter);
                    if (!CanContinueGeneration())
                        return default;
                }
#endif
                await YieldAfterGenerationStageAsync(stageStartTime);
                if (!CanContinueGeneration())
                    return default;

                stageStartTime = Time.realtimeSinceStartupAsDouble;
                proxyMesh = ResolveNavigationProxyMesh();
                generatedProxyMesh = proxyMesh != null && wreckCollisionProxyMesh == null;
                if (!CanContinueGeneration())
                    return default;
                Bounds localBounds = CalculateLocalBounds(_allPlacements);
                Bounds worldBounds = TranslateBounds(localBounds, runtimeOrigin);
                Bounds renderWorldBounds = ExpandBoundsForBrgScatter(worldBounds);
                await YieldAfterGenerationStageAsync(stageStartTime);
                if (!CanContinueGeneration())
                    return default;

                PrepareWreckWorldState(in centerAup, runtimeOrigin, renderWorldBounds, seed, moduleCount, 0);
                PublishWreckRenderPayload(in centerAup, runtimeOrigin, renderWorldBounds, seed);
                PublishCollisionProxy(worldBounds);
                SpawnWreckLoot(renderWorldBounds, seed);

                WreckNavigationHandle navigationHandle = null;
                UnityEngine.Object navigationData = null;
                AsyncOperation navigationOperation = null;
                if (buildAsyncNavigationBake && proxyMesh != null)
                    BuildDisabledNavigationBake(runtimeOrigin, worldBounds, proxyMesh, out navigationHandle, out navigationData, out navigationOperation);

                _debugLastSeed = seed;
                _debugLastPlacementCount = _allPlacements.Length;
                _debugLastCellCount = cellCount;
                _debugLastCombinedBoundsValid = _allPlacements.IsCreated && _allPlacements.Length > 0;
                _debugLastWorldBoundsValid = IsFiniteBounds(renderWorldBounds);
                _debugLastProxyBoundsValid = proxyMesh != null && IsFiniteBounds(proxyMesh.bounds);
                _debugLastNavigationState = navigationHandle != null ? navigationHandle.State : WreckNavigationState.None;

                WreckageData data = new WreckageData
                {
                    CombinedMesh = combinedMesh,
                    EssentialMesh = essentialMesh,
                    DetailMesh = detailMesh,
                    ClutterMesh = clutterMesh,
                    ProxyMesh = proxyMesh,
                    Navigation = navigationData,
                    NavigationBuild = navigationOperation,
                    NavigationHandle = navigationHandle,
                    NavigationState = navigationHandle != null ? navigationHandle.State : WreckNavigationState.None,
                    WorldBounds = renderWorldBounds,
                    RuntimeOrigin = runtimeOrigin,
                    GenerationSeed = seed
                };

                meshOwnershipTransferred = true;
                return data;
            }
            finally
            {
                if (!meshOwnershipTransferred)
                {
                    ReleaseGeneratedMesh(combinedMesh);
                    ReleaseGeneratedMesh(essentialMesh);
                    ReleaseGeneratedMesh(detailMesh);
                    ReleaseGeneratedMesh(clutterMesh);
                    if (generatedProxyMesh)
                        ReleaseGeneratedMesh(proxyMesh);
                }
            }
        }

        private bool ShouldUseBrgOnlyWreckPath(int moduleCount)
        {
            return wreckMaterialRegistry != null && !HasWfcRenderableModule(moduleCount);
        }

#if UNITY_EDITOR
        private static bool ShouldBuildMergedMeshFallback()
        {
            return !Application.isPlaying;
        }
#endif

        private bool HasWfcRenderableModule(int moduleCount)
        {
            int safeCount = math.min(moduleCount, _runtimeDefinitions.IsCreated ? _runtimeDefinitions.Length : 0);
            for (int i = 0; i < safeCount; i++)
            {
                if (!_runtimeDefinitions.TryGet(i, out WreckModuleRuntimeDefinition definition))
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 24u, i, safeCount);
                    return false;
                }

                if ((definition.EmitsGeometry | definition.EmitsNavProxy) != 0)
                    return true;
            }

            return false;
        }

        private WreckageData GenerateBrgWreckOnly(
            in AbsoluteUniversePosition centerAup,
            Vector3 runtimeOrigin,
            uint seed,
            int moduleCount,
            double generationStartTime)
        {
            int fragmentCount = ResolveBrgFragmentCount(seed);
            Bounds worldBounds = CalculateBrgWreckWorldBounds(runtimeOrigin);

            PrepareWreckWorldState(in centerAup, runtimeOrigin, worldBounds, seed, moduleCount, fragmentCount);
            PublishBrgScatterPayload(centerAup, runtimeOrigin, worldBounds, seed, fragmentCount, moduleCount);
            PublishCollisionProxy(worldBounds);
            SpawnWreckLoot(worldBounds, seed);

            _debugLastSeed = seed;
            _debugLastPlacementCount = fragmentCount;
            _debugLastCellCount = 0;
            _debugLastCombinedBoundsValid = true;
            _debugLastWorldBoundsValid = IsFiniteBounds(worldBounds);
            _debugLastProxyBoundsValid = _activeCollisionCollider != null && IsFiniteBounds(worldBounds);
            _debugLastNavigationState = WreckNavigationState.None;
            _debugLastCollapseIterations = 0;
            _debugLastPropagationIterations = 0;
            _debugLastContradictionCount = 0;
            _debugLastForcedFallbackCellCount = 0;
            _debugLastSolveTermination = WreckSolveTermination.Completed;
            PublishWreckSolveBudgetWarningIfNeeded((Time.realtimeSinceStartupAsDouble - generationStartTime) * 1000.0);

            return new WreckageData
            {
                CombinedMesh = null,
                EssentialMesh = null,
                DetailMesh = null,
                ClutterMesh = null,
                ProxyMesh = null,
                Navigation = null,
                NavigationBuild = null,
                NavigationHandle = null,
                NavigationState = WreckNavigationState.None,
                WorldBounds = worldBounds,
                RuntimeOrigin = runtimeOrigin,
                GenerationSeed = seed
            };
        }

        private async Awaitable<WreckageData> GenerateBrgWreckOnlyAsync(
            AbsoluteUniversePosition centerAup,
            Vector3 runtimeOrigin,
            uint seed,
            int moduleCount)
        {
            double stageStartTime = Time.realtimeSinceStartupAsDouble;
            int fragmentCount = ResolveBrgFragmentCount(seed);
            Bounds worldBounds = CalculateBrgWreckWorldBounds(runtimeOrigin);

            await YieldAfterGenerationStageAsync(stageStartTime);
            if (!CanContinueGeneration())
                return default;

            PrepareWreckWorldState(in centerAup, runtimeOrigin, worldBounds, seed, moduleCount, fragmentCount);
            PublishBrgScatterPayload(centerAup, runtimeOrigin, worldBounds, seed, fragmentCount, moduleCount);
            PublishCollisionProxy(worldBounds);
            SpawnWreckLoot(worldBounds, seed);

            _debugLastSeed = seed;
            _debugLastPlacementCount = fragmentCount;
            _debugLastCellCount = 0;
            _debugLastCombinedBoundsValid = true;
            _debugLastWorldBoundsValid = IsFiniteBounds(worldBounds);
            _debugLastProxyBoundsValid = _activeCollisionCollider != null && IsFiniteBounds(worldBounds);
            _debugLastNavigationState = WreckNavigationState.None;
            _debugLastCollapseIterations = 0;
            _debugLastPropagationIterations = 0;
            _debugLastContradictionCount = 0;
            _debugLastForcedFallbackCellCount = 0;
            _debugLastSolveTermination = WreckSolveTermination.Completed;

            return new WreckageData
            {
                CombinedMesh = null,
                EssentialMesh = null,
                DetailMesh = null,
                ClutterMesh = null,
                ProxyMesh = null,
                Navigation = null,
                NavigationBuild = null,
                NavigationHandle = null,
                NavigationState = WreckNavigationState.None,
                WorldBounds = worldBounds,
                RuntimeOrigin = runtimeOrigin,
                GenerationSeed = seed
            };
        }

        private bool HasRenderPayloadSnapshotCapacity(int count)
        {
            return count >= 0 &&
                   _renderWorldMatrixSnapshot != null &&
                   _renderModuleIdSnapshot != null &&
                   _renderAgeSnapshot != null &&
                   count <= _renderWorldMatrixSnapshot.Length &&
                   count <= _renderModuleIdSnapshot.Length &&
                   count <= _renderAgeSnapshot.Length;
        }

        private bool TryCopyRenderPayloadSnapshotToVault(int count, out uint failureCode)
        {
            failureCode = 0u;
            if (!HasRenderPayloadSnapshotCapacity(count))
            {
                failureCode = 61u;
                return false;
            }

            if (!_renderWorldMatrices.TryLockForWrite(out NativeArray<Matrix4x4> worldMatrices))
            {
                failureCode = 56u;
                return false;
            }

            try
            {
                if (count > worldMatrices.Length)
                {
                    failureCode = 56u;
                    return false;
                }

                for (int i = 0; i < count; i++)
                    worldMatrices[i] = _renderWorldMatrixSnapshot[i];
            }
            finally
            {
                _renderWorldMatrices.ReleaseWriteLock();
            }

            if (!_renderModuleIds.TryLockForWrite(out NativeArray<byte> moduleIds))
            {
                failureCode = 57u;
                return false;
            }

            try
            {
                if (count > moduleIds.Length)
                {
                    failureCode = 57u;
                    return false;
                }

                for (int i = 0; i < count; i++)
                    moduleIds[i] = _renderModuleIdSnapshot[i];
            }
            finally
            {
                _renderModuleIds.ReleaseWriteLock();
            }

            if (!_renderAges.TryLockForWrite(out NativeArray<float> ages))
            {
                failureCode = 58u;
                return false;
            }

            try
            {
                if (count > ages.Length)
                {
                    failureCode = 58u;
                    return false;
                }

                for (int i = 0; i < count; i++)
                    ages[i] = _renderAgeSnapshot[i];
            }
            finally
            {
                _renderAges.ReleaseWriteLock();
            }

            return true;
        }

        private bool TryBuildBrgScatterSnapshot(
            in AbsoluteUniversePosition centerAup,
            Vector3 runtimeOrigin,
            uint seed,
            int fragmentCount,
            int moduleCount)
        {
            if (!HasRenderPayloadSnapshotCapacity(fragmentCount))
                return false;

            float3 runtimeOrigin3 = math.float3(runtimeOrigin.x, runtimeOrigin.y, runtimeOrigin.z);
            float scatterRadius = math.max(0f, brgScatterRadiusMeters);
            float scatterVertical = math.max(0f, brgScatterVerticalMeters);
            int scatterYawEnabled = brgScatterYawDegrees > 0.001f ? 1 : 0;
            float minScale = math.max(0.05f, brgFragmentMinScale);
            float maxScale = math.max(minScale, brgFragmentMaxScale);
            for (int index = 0; index < fragmentCount; index++)
            {
                BuildWreckScatterMatricesJob.ExecuteOne(
                    index,
                    in centerAup,
                    runtimeOrigin3,
                    moduleCount,
                    scatterRadius,
                    scatterVertical,
                    scatterYawEnabled,
                    minScale,
                    maxScale,
                    seed,
                    out _renderWorldMatrixSnapshot[index],
                    out _renderModuleIdSnapshot[index],
                    out _renderAgeSnapshot[index]);
            }

            return true;
        }

        private bool TryBuildWreckRenderSnapshot(
            in AbsoluteUniversePosition centerAup,
            Vector3 runtimeOrigin,
            uint seed,
            int placementCount,
            out uint failureCode)
        {
            failureCode = 0u;
            if (!HasRenderPayloadSnapshotCapacity(placementCount))
            {
                failureCode = 62u;
                return false;
            }

            float3 runtimeOrigin3 = math.float3(runtimeOrigin.x, runtimeOrigin.y, runtimeOrigin.z);
            float scatterRadius = math.max(0f, brgScatterRadiusMeters);
            float scatterVertical = math.max(0f, brgScatterVerticalMeters);
            int scatterYawEnabled = brgScatterYawDegrees > 0.001f ? 1 : 0;
            for (int index = 0; index < placementCount; index++)
            {
                if (!_allPlacements.TryGet(index, out WreckModulePlacement placement))
                {
                    failureCode = 22u;
                    return false;
                }

                BuildWreckRenderPayloadJob.ExecuteOne(
                    in placement,
                    index,
                    in centerAup,
                    runtimeOrigin3,
                    scatterRadius,
                    scatterVertical,
                    scatterYawEnabled,
                    seed,
                    out _renderWorldMatrixSnapshot[index],
                    out _renderModuleIdSnapshot[index],
                    out _renderAgeSnapshot[index]);
            }

            return true;
        }

        private void PublishBrgScatterPayload(
            AbsoluteUniversePosition centerAup,
            Vector3 runtimeOrigin,
            Bounds worldBounds,
            uint seed,
            int fragmentCount,
            int moduleCount)
        {
            if (wreckMaterialRegistry == null || fragmentCount <= 0)
                return;

            if (!_renderWorldMatrices.IsCreated ||
                !_renderModuleIds.IsCreated ||
                !_renderAges.IsCreated ||
                fragmentCount > _renderWorldMatrices.Length ||
                fragmentCount > _renderModuleIds.Length ||
                fragmentCount > _renderAges.Length)
            {
                WriteBlackBoxTelemetry(WreckTelemetryFailureHash, runtimeOrigin, 56u, fragmentCount, ResolveRenderPayloadCapacity());
                return;
            }

            uint failureCode = 0u;
            bool shouldPublishSnapshot = false;

            if (!TryBuildBrgScatterSnapshot(in centerAup, runtimeOrigin, seed, fragmentCount, moduleCount))
            {
                failureCode = 61u;
            }
            else if (!TryCopyRenderPayloadSnapshotToVault(fragmentCount, out failureCode))
            {
                shouldPublishSnapshot = false;
            }
            else
            {
                shouldPublishSnapshot = true;
            }

            if (failureCode == 0u && shouldPublishSnapshot)
            {
                wreckMaterialRegistry.Publish(
                    moduleDefinitions,
                    _renderWorldMatrixSnapshot,
                    _renderModuleIdSnapshot,
                    _renderAgeSnapshot,
                    fragmentCount,
                    worldBounds,
                    centerAup);
            }
            else if (failureCode != 0u)
            {
                WriteBlackBoxTelemetry(WreckTelemetryFailureHash, runtimeOrigin, failureCode, fragmentCount, ResolveRenderPayloadCapacity());
            }
        }

        private Bounds CalculateBrgWreckWorldBounds(Vector3 runtimeOrigin)
        {
            float radius = math.max(
                1f,
                math.max(brgScatterRadiusMeters + brgFragmentMaxScale, lootSpawnRadiusMeters));
            float verticalExtent = math.max(1f, brgScatterVerticalMeters + (brgFragmentMaxScale * 2f));
            Vector3 size = new Vector3(radius * 2f, verticalExtent * 2f, radius * 2f);
            return new Bounds(runtimeOrigin, size);
        }

        private int ResolveBrgFragmentCount(uint seed)
        {
            int scalabilityCap = ResolveScalabilityBrgFragmentCap(ResolveWreckQualityWeight01());
            int minCount = math.min(math.clamp(brgMinFragmentCount, 50, 200), scalabilityCap);
            int maxCount = math.min(math.clamp(math.max(minCount, brgMaxFragmentCount), 50, 200), scalabilityCap);
            if (minCount == maxCount)
                return minCount;

            uint hash = MixFragmentSeed(seed ^ worldGenerationVersionSalt ^ 0x57425247u);
            return minCount + (int)(hash % (uint)(maxCount - minCount + 1));
        }

        private static int ResolveScalabilityBrgFragmentCap(float qualityWeight01)
        {
            float quality = SanitizeQualityWeight01(qualityWeight01);
            return math.clamp((int)math.round(math.lerp(80f, 200f, quality)), 80, 200);
        }

        private static uint MixFragmentSeed(uint value)
        {
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return value;
        }

        private void PublishWreckRenderPayload(
            in AbsoluteUniversePosition centerAup,
            Vector3 runtimeOrigin,
            Bounds worldBounds,
            uint seed)
        {
            if (wreckMaterialRegistry == null || !_allPlacements.IsCreated || _allPlacements.Length <= 0)
                return;

            int placementCount = _allPlacements.Length;
            if (!_renderWorldMatrices.IsCreated ||
                !_renderModuleIds.IsCreated ||
                !_renderAges.IsCreated ||
                placementCount > _renderWorldMatrices.Length ||
                placementCount > _renderModuleIds.Length ||
                placementCount > _renderAges.Length)
            {
                WriteBlackBoxTelemetry(WreckTelemetryFailureHash, runtimeOrigin, 59u, placementCount, ResolveRenderPayloadCapacity());
                return;
            }

            uint failureCode = 0u;
            bool shouldPublishSnapshot = false;

            if (TryBuildWreckRenderSnapshot(in centerAup, runtimeOrigin, seed, placementCount, out failureCode) &&
                TryCopyRenderPayloadSnapshotToVault(placementCount, out failureCode))
            {
                shouldPublishSnapshot = true;
            }

            if (failureCode == 0u && shouldPublishSnapshot)
            {
                wreckMaterialRegistry.Publish(
                    moduleDefinitions,
                    _renderWorldMatrixSnapshot,
                    _renderModuleIdSnapshot,
                    _renderAgeSnapshot,
                    placementCount,
                    worldBounds,
                    centerAup);
            }
            else if (failureCode != 0u)
            {
                WriteBlackBoxTelemetry(WreckTelemetryFailureHash, runtimeOrigin, failureCode, placementCount, ResolveRenderPayloadCapacity());
            }
        }

        private void PublishCollisionProxy(Bounds worldBounds)
        {
            if (wreckCollisionProxyPrefab == null || !IsFiniteBounds(worldBounds))
                return;

            if (!TryResolveCachedObjectPool(out IObjectPoolService pool))
                return;

            DespawnActiveCollisionProxy();

            GameObject instance = pool.Spawn(wreckCollisionProxyPrefab, worldBounds.center, Quaternion.identity, allowExpand: false);
            if (instance == null)
                return;

            BoxCollider primitiveCollider = ResolvePrimitiveCollisionProxy(instance);
            if (primitiveCollider == null)
            {
                pool.Despawn(instance);
                return;
            }

            instance.transform.SetPositionAndRotation(worldBounds.center, Quaternion.identity);
            instance.transform.localScale = ResolveCollisionProxyScale(worldBounds);
            primitiveCollider.enabled = false;
            primitiveCollider.isTrigger = true;
            ConfigurePrimitiveCollisionProxy(primitiveCollider);
            primitiveCollider.enabled = true;
            _activeCollisionProxy = instance;
            _activeCollisionCollider = primitiveCollider;
            ConfigureIntegrityProxy(instance);
            PublishNavGridObstacle(primitiveCollider);
        }

        private void PublishNavGridObstacle(BoxCollider primitiveCollider)
        {
            if (primitiveCollider == null)
                return;

            int obstacleId = unchecked((int)EntityId.ToULong(GetEntityId()));
            if (obstacleId == 0)
                obstacleId = unchecked((int)EntityId.ToULong(primitiveCollider.GetEntityId()));

            _navGridBoxColliderScratch[0] = primitiveCollider;
            VoxelDynamicNavGridRuntime.RegisterModuleObstacle(obstacleId, _navGridBoxColliderScratch, s_EmptyCapsuleColliders);
            _activeNavGridObstacleId = obstacleId;
        }

        private static BoxCollider ResolvePrimitiveCollisionProxy(GameObject instance)
        {
            if (instance == null)
                return null;

            if (instance.TryGetComponent(out BoxCollider boxCollider) && boxCollider != null)
                return boxCollider;

            return null;
        }

        private static void ConfigurePrimitiveCollisionProxy(BoxCollider boxCollider)
        {
            boxCollider.center = Vector3.zero;
            boxCollider.size = Vector3.one;
        }

        private static Vector3 ResolveCollisionProxyScale(Bounds worldBounds)
        {
            Vector3 worldSize = worldBounds.size;
            float x = ResolveColliderScaleAxis(worldSize.x);
            float y = ResolveColliderScaleAxis(worldSize.y);
            float z = ResolveColliderScaleAxis(worldSize.z);
            return new Vector3(x, y, z);
        }

        private static float ResolveColliderScaleAxis(float worldSize)
        {
            if (!math.isfinite(worldSize) || worldSize <= 0.001f)
                worldSize = 1f;

            return math.max(0.001f, worldSize);
        }

        private void DespawnActiveCollisionProxy()
        {
            UnregisterNavGridObstacle();

            if (_activeCollisionProxy == null)
            {
                _activeCollisionCollider = null;
                return;
            }

            GameObject proxy = _activeCollisionProxy;
            _activeCollisionProxy = null;
            _activeCollisionCollider = null;

            TryResolveCachedObjectPool(out IObjectPoolService pool);
            if (pool != null && proxy.activeInHierarchy)
                pool.Despawn(proxy);
            else if (proxy != null)
                proxy.SetActive(false);
        }

        private void UnregisterNavGridObstacle()
        {
            if (_activeNavGridObstacleId == 0)
                return;

            VoxelDynamicNavGridRuntime.UnregisterModuleObstacle(_activeNavGridObstacleId);
            _activeNavGridObstacleId = 0;
            _navGridBoxColliderScratch[0] = null;
        }

        private void SpawnWreckLoot(Bounds worldBounds, uint seed)
        {
            if (itemCatalog == null ||
                _lootRecordCount <= 0 ||
                maxLootCount <= 0 ||
                !IsFiniteBounds(worldBounds))
            {
                return;
            }

            int desiredMin = math.clamp(minLootCount, 0, 8);
            int desiredMax = math.clamp(math.max(desiredMin, maxLootCount), 0, 8);
            if (desiredMax <= 0)
                return;

            XorShift32State rng = new XorShift32State(seed ^ 0x6D2B79F5u);
            int desiredCount = desiredMin == desiredMax
                ? desiredMin
                : desiredMin + (int)(rng.NextUInt() % (uint)(desiredMax - desiredMin + 1));
            int hashCount = _lootRecordCount;
            int maxAttempts = math.max(hashCount * 2, desiredCount);
            int spawnedCount = 0;
            Vector3 center = worldBounds.center;
            float radiusLimit = math.max(
                1f,
                math.min(lootSpawnRadiusMeters, math.max(worldBounds.extents.x, worldBounds.extents.z)));

            for (int attempt = 0; attempt < maxAttempts && spawnedCount < desiredCount; attempt++)
            {
                int hashIndex = (int)(rng.NextUInt() % (uint)hashCount);
                if (!_lootRecords.TryGet(hashIndex, out WreckLootRecord lootRecord))
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, center, 25u, hashIndex, hashCount);
                    return;
                }

                uint hashId = unchecked((uint)lootRecord.ItemHashId);
                if (hashId == 0u || !ItemTemplateRegistry.TryGetTemplate(hashId, out ItemTemplate template))
                    continue;

                Hecton8.Items.ItemData itemData = _lootItemDataCache[hashIndex];
                GameObject prefab = _lootPrefabCache[hashIndex];
                if (itemData == null || prefab == null)
                    continue;

                float lootExtent = radiusLimit * 0.7f;
                float horizontalX = (rng.NextFloat01() * 2f - 1f) * lootExtent;
                float horizontalZ = (rng.NextFloat01() * 2f - 1f) * lootExtent;
                float height = ResolveLootHeightBand(rng.NextUInt());
                Vector3 position = center + new Vector3(horizontalX, height, horizontalZ);
                Quaternion rotation = ResolveCardinalLootRotation(rng.NextUInt());
                int quantity = ResolveLootQuantity(in lootRecord, rng.NextUInt(), (int)template.MaxStackSize);
                if (!QueueWreckLootSpawn(prefab, itemData, quantity, position, rotation))
                    continue;

                spawnedCount++;
            }
        }

        private static float ResolveLootHeightBand(uint state)
        {
            uint band = state & 3u;
            return -0.35f + (band * 0.45f) + math.select(0f, 0.1f, band == 3u);
        }

        private static Quaternion ResolveCardinalLootRotation(uint state)
        {
            const float HalfTurnSin = 0.70710677f;
            switch (state & 3u)
            {
                case 0u:
                    return Quaternion.identity;
                case 1u:
                    return new Quaternion(0f, HalfTurnSin, 0f, HalfTurnSin);
                case 2u:
                    return new Quaternion(0f, 1f, 0f, 0f);
                default:
                    return new Quaternion(0f, -HalfTurnSin, 0f, HalfTurnSin);
            }
        }

        private bool QueueWreckLootSpawn(
            GameObject prefab,
            Hecton8.Items.ItemData itemData,
            int quantity,
            Vector3 position,
            Quaternion rotation,
            int debrisRecordIndex = -1)
        {
            if (prefab == null || itemData == null || _pendingLootCount >= MaxPendingLootSpawns)
                return false;

            int writeIndex = (_pendingLootReadIndex + _pendingLootCount) % MaxPendingLootSpawns;
            _pendingLootSpawns[writeIndex] = new PendingWreckLootSpawn
            {
                Prefab = prefab,
                ItemData = itemData,
                Position = position,
                Rotation = rotation,
                Quantity = math.max(1, quantity),
                DebrisRecordIndex = debrisRecordIndex
            };
            _pendingLootCount++;
            TryRegisterLootTick();
            return true;
        }

        private void ClearPendingLootQueue()
        {
            int pendingCount = _pendingLootCount;
            for (int offset = 0; offset < pendingCount; offset++)
            {
                int queueIndex = (_pendingLootReadIndex + offset) % MaxPendingLootSpawns;
                RollbackPendingDebrisPickup(in _pendingLootSpawns[queueIndex], 63u);
            }

            for (int i = 0; i < MaxPendingLootSpawns; i++)
                _pendingLootSpawns[i] = default;

            _pendingLootReadIndex = 0;
            _pendingLootCount = 0;
        }

        private void UnregisterDispatcherLanesForHotSwap()
        {
            if (_registeredLootLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLootLateFrame = false;
            }

            if (_registeredLootTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredLootTick = false;
            }

            if (_registeredWreckSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredWreckSlowTick = false;
            }
        }

        private void TryUnregisterLootTick()
        {
            if (_registeredLootLateFrame && !_blackBoxDumpRequested)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLootLateFrame = false;
            }

            if (!_registeredLootTick || _pendingLootCount > 0)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registeredLootTick = false;
        }

        private void TryRegisterLootTick()
        {
            bool hasPendingLoot = _pendingLootCount > 0;
            bool needsLateFrame = _blackBoxDumpRequested;
            if (!hasPendingLoot && !needsLateFrame)
                return;

            if (!Application.isPlaying || _dispatcher == null)
                return;

            if (hasPendingLoot)
                TryRegisterWreckSlowTick();

            if (needsLateFrame && !_registeredLootLateFrame)
                _registeredLootLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void FlushOneQueuedLootSpawn()
        {
            if (_pendingLootCount <= 0)
                return;

            if (!TryResolveCachedObjectPool(out IObjectPoolService pool))
                return;

            PendingWreckLootSpawn spawn = _pendingLootSpawns[_pendingLootReadIndex];
            _pendingLootSpawns[_pendingLootReadIndex] = default;
            _pendingLootReadIndex = (_pendingLootReadIndex + 1) % MaxPendingLootSpawns;
            _pendingLootCount--;

            if (spawn.Prefab == null || spawn.ItemData == null)
            {
                RollbackPendingDebrisPickup(in spawn, 60u);
                return;
            }

            GameObject instance = pool.Spawn(spawn.Prefab, spawn.Position, spawn.Rotation, allowExpand: false);
            if (instance == null)
            {
                RollbackPendingDebrisPickup(in spawn, 61u);
                return;
            }

            if (pool.TryGetPooledComponent(instance, out PickupItem pickup))
            {
                if (!CommitPendingDebrisPickupSpawned(in spawn, 64u))
                {
                    pool.Despawn(instance);
                    RollbackPendingDebrisPickup(in spawn, 65u);
                    return;
                }

                pickup.Configure(spawn.ItemData, math.max(1, spawn.Quantity));
                return;
            }

            pool.Despawn(instance);
            RollbackPendingDebrisPickup(in spawn, 62u);
        }

        private bool CommitPendingDebrisPickupSpawned(in PendingWreckLootSpawn spawn, uint failureCode)
        {
            int debrisIndex = spawn.DebrisRecordIndex;
            if (debrisIndex < 0)
                return true;

            if (!_debrisRecords.TryGet(debrisIndex, out WreckDebrisRecord record))
            {
                WriteBlackBoxTelemetry(WreckTelemetryFailureHash, spawn.Position, failureCode, debrisIndex, 0f);
                return false;
            }

            if ((record.Flags & (byte)WreckDebrisFlags.Harvested) == 0)
            {
                record.Flags = (byte)((record.Flags | (byte)WreckDebrisFlags.Harvested) &
                    ~(byte)(WreckDebrisFlags.Pickable | WreckDebrisFlags.ActivePickup | WreckDebrisFlags.DotOnly));
            }

            if (_debrisRecords.TrySet(debrisIndex, in record))
                return true;

            WriteBlackBoxTelemetry(WreckTelemetryFailureHash, ToVector3(record.Position), failureCode, debrisIndex, 1f);
            return false;
        }

        private void RollbackPendingDebrisPickup(in PendingWreckLootSpawn spawn, uint failureCode)
        {
            int debrisIndex = spawn.DebrisRecordIndex;
            if (debrisIndex < 0)
                return;

            if (!_debrisRecords.TryGet(debrisIndex, out WreckDebrisRecord record))
            {
                WriteBlackBoxTelemetry(WreckTelemetryFailureHash, spawn.Position, failureCode, debrisIndex, 0f);
                return;
            }

            if ((record.Flags & (byte)WreckDebrisFlags.Harvested) == 0)
            {
                record.Flags = (byte)((record.Flags | (byte)(WreckDebrisFlags.Pickable | WreckDebrisFlags.DotOnly)) &
                    ~(byte)WreckDebrisFlags.ActivePickup);
            }

            if (!_debrisRecords.TrySet(debrisIndex, in record))
                WriteBlackBoxTelemetry(WreckTelemetryFailureHash, ToVector3(record.Position), failureCode, debrisIndex, 1f);
        }

        private void RefreshLootRecords()
        {
            _lootRecordCount = 0;
            if (!_lootRecords.IsCreated)
                return;

            ClearLootRecordCaches();

            int sourceCount = math.min(wreckLootItemHashes != null ? wreckLootItemHashes.Length : 0, _lootRecords.Length);
            for (int sourceIndex = 0; sourceIndex < sourceCount; sourceIndex++)
            {
                uint hashId = wreckLootItemHashes[sourceIndex];
                if (hashId == 0u)
                    continue;

                int signedHashId = unchecked((int)hashId);
                Hecton8.Items.ItemData itemData = itemCatalog != null ? itemCatalog.FindByHash(signedHashId) : null;
                if (itemData == null)
                    continue;

                ushort maxQuantity = 1;
                if (ItemTemplateRegistry.TryGetTemplate(hashId, out ItemTemplate template))
                    maxQuantity = (ushort)math.clamp((int)template.MaxStackSize, 1, ushort.MaxValue);

                WreckLootRecord lootRecord = new WreckLootRecord
                {
                    ItemHashId = unchecked((int)hashId),
                    MinQuantity = 1,
                    MaxQuantity = (ushort)math.max(1, math.min(3, maxQuantity)),
                    StableDropHash = MixFragmentSeed(hashId ^ worldGenerationVersionSalt),
                    Flags = 0u
                };

                int targetIndex = _lootRecordCount;
                _lootRecordBuildCache[targetIndex] = lootRecord;
                _lootItemDataCache[targetIndex] = itemData;
                _lootRecordCount = targetIndex + 1;
            }

            if (!_lootRecords.TryLockForWrite(out NativeArray<WreckLootRecord> lootRecords))
            {
                _lootRecordCount = 0;
                ClearLootRecordCaches();

                WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 8u, sourceCount, _lootRecords.Length);
                return;
            }

            try
            {
                int writeCount = math.min(_lootRecords.Length, lootRecords.Length);
                for (int i = 0; i < writeCount; i++)
                    lootRecords[i] = i < _lootRecordCount ? _lootRecordBuildCache[i] : default;
            }
            finally
            {
                _lootRecords.ReleaseWriteLock();
            }

            RefreshCommittedLootPrefabCache();
        }

        private void RefreshCommittedLootPrefabCache()
        {
            ItemCatalog catalog = itemCatalog;
            if (catalog == null || _lootRecordCount <= 0)
                return;

            int committedCount = math.min(_lootRecordCount, MaxLootRecords);
            for (int i = 0; i < committedCount; i++)
            {
                int signedHashId = _lootRecordBuildCache[i].ItemHashId;
                if (signedHashId == 0)
                    continue;

                catalog.QueueWorldPrefabPrewarm(signedHashId);
                if (catalog.PollLoadedWorldPrefab(signedHashId, out GameObject loadedPrefab))
                    _lootPrefabCache[i] = loadedPrefab;
            }
        }

        private void ClearLootRecordCaches()
        {
            for (int i = 0; i < MaxLootRecords; i++)
            {
                _lootRecordBuildCache[i] = default;
                _lootItemDataCache[i] = null;
                _lootPrefabCache[i] = null;
            }
        }

        private static int ResolveLootQuantity(in WreckLootRecord record, uint randomState, int templateMaxStack)
        {
            int minQuantity = math.max(1, record.MinQuantity);
            int maxQuantity = math.max(minQuantity, math.min(math.max(1, templateMaxStack), record.MaxQuantity));
            int range = math.max(1, maxQuantity - minQuantity + 1);
            int randomized = minQuantity + (int)(randomState % (uint)range);
            return math.select(minQuantity, randomized, maxQuantity > minQuantity);
        }

        private void PrepareWreckWorldState(
            in AbsoluteUniversePosition centerAup,
            Vector3 runtimeOrigin,
            Bounds worldBounds,
            uint seed,
            int moduleCount,
            int brgFragmentCount)
        {
            RefreshLootRecords();
            ResetWreckWorldState();

            _activeCenterAup = centerAup;
            _activeRuntimeOrigin = runtimeOrigin;
            _activeWorldBounds = worldBounds;
            _activeGenerationSeed = seed;

            ResolveIntegrityCounters(moduleCount);
            BuildDebrisSpatialHash(worldBounds, seed);
            BuildArtifactFragmentHash(runtimeOrigin, worldBounds, seed, moduleCount, brgFragmentCount);
            BuildScorchDecalRecords(runtimeOrigin, worldBounds, seed, moduleCount);
            BuildBurialCutRecords(runtimeOrigin, worldBounds, seed);
            ApplyBurialCutsToVoxelSurgeon();
            PublishWreckLightingGlobals(worldBounds, seed);
            TryRegisterWreckSlowTick();
            WriteBlackBoxTelemetry(WreckTelemetryGenerationHash, runtimeOrigin, 0u, 0f, 0f);
            ValidateBlackBoxState();
        }

        private void ResetWreckWorldState()
        {
            _debrisRecordCount = 0;
            _debrisClusterCount = 0;
            _artifactRecordCount = 0;
            _scorchDecalCount = 0;
            _burialCutRecordCount = 0;
            _sealedModuleCount = 0;
            _openedSealedModuleCount = 0;
            _rupturedModuleCount = 0;
            _debrisNearFieldCursor = 0;
            _debrisGravityCursor = 0;
            _artifactDiscoveryCursor = 0;
            _blackBoxDumpRequested = false;
            _blackBoxDumpWritten = false;
            _blackBoxDumpFailed = false;
            ClearDebrisClusters();
        }

        private void ClearDebrisClusters()
        {
            if (!_debrisClusters.IsCreated)
                return;

            if (!_debrisClusters.TryLockForWrite(out NativeArray<WreckDebrisCluster> clusters))
            {
                WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 53u, MaxDebrisClusters, 0f);
                return;
            }

            try
            {
                int count = math.min(MaxDebrisClusters, _debrisClusters.Length);
                for (int i = 0; i < count; i++)
                    clusters[i] = default;
            }
            finally
            {
                _debrisClusters.ReleaseWriteLock();
            }
        }

        private void ResolveIntegrityCounters(int moduleCount)
        {
            if (_allPlacements.IsCreated && _allPlacements.Length > 0)
            {
                int placementCount = _allPlacements.Length;
                for (int i = 0; i < placementCount; i++)
                {
                    if (!_allPlacements.TryGet(i, out WreckModulePlacement placement))
                    {
                        WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 26u, i, placementCount);
                        return;
                    }

                    AccumulateIntegrityState(placement.IntegrityState);
                }
                return;
            }

            int safeModuleCount = math.min(moduleCount, _runtimeDefinitions.IsCreated ? _runtimeDefinitions.Length : 0);
            for (int i = 0; i < safeModuleCount; i++)
            {
                if (!_runtimeDefinitions.TryGet(i, out WreckModuleRuntimeDefinition definition))
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 27u, i, safeModuleCount);
                    return;
                }

                AccumulateIntegrityState(definition.IntegrityState);
            }
        }

        private void AccumulateIntegrityState(byte state)
        {
            if (state == (byte)WreckIntegrityState.Sealed)
                _sealedModuleCount++;
            else if (state == (byte)WreckIntegrityState.Ruptured)
                _rupturedModuleCount++;
        }

        private bool TryCopyDebrisRecordsBuildCacheToVault(
            int stagingBudget,
            int builtCount,
            out uint failureCode,
            out float failureValue0,
            out float failureValue1)
        {
            failureCode = 0u;
            failureValue0 = 0f;
            failureValue1 = 0f;
            if (!_debrisRecords.TryLockForWrite(out NativeArray<WreckDebrisRecord> debrisRecords))
            {
                failureCode = 53u;
                failureValue0 = stagingBudget;
                return false;
            }

            try
            {
                if (debrisRecords.Length < stagingBudget)
                {
                    failureCode = 55u;
                    failureValue0 = stagingBudget;
                    failureValue1 = debrisRecords.Length;
                    return false;
                }

                int recordWriteCount = math.min(debrisRecords.Length, _debrisRecords.Length);
                for (int i = 0; i < recordWriteCount; i++)
                    debrisRecords[i] = i < builtCount ? _debrisRecordBuildCache[i] : default;
            }
            finally
            {
                _debrisRecords.ReleaseWriteLock();
            }

            return true;
        }

        private bool TryCopyDebrisSpatialKeyBuildCacheToVault(
            int stagingBudget,
            int builtCount,
            out uint failureCode,
            out float failureValue0,
            out float failureValue1)
        {
            failureCode = 0u;
            failureValue0 = 0f;
            failureValue1 = 0f;
            if (!_debrisSpatialKeys.TryLockForWrite(out NativeArray<int> spatialKeys))
            {
                failureCode = 54u;
                failureValue0 = stagingBudget;
                return false;
            }

            try
            {
                if (spatialKeys.Length < stagingBudget)
                {
                    failureCode = 55u;
                    failureValue0 = stagingBudget;
                    failureValue1 = spatialKeys.Length;
                    return false;
                }

                int keyWriteCount = math.min(spatialKeys.Length, _debrisSpatialKeys.Length);
                for (int i = 0; i < keyWriteCount; i++)
                    spatialKeys[i] = i < builtCount ? _debrisSpatialKeyBuildCache[i] : 0;
            }
            finally
            {
                _debrisSpatialKeys.ReleaseWriteLock();
            }

            return true;
        }

        private bool TryCopyDebrisClusterBuildCacheToVault(
            int builtClusterCount,
            out uint failureCode,
            out float failureValue0,
            out float failureValue1)
        {
            failureCode = 0u;
            failureValue0 = 0f;
            failureValue1 = 0f;
            if (builtClusterCount > MaxDebrisClusters)
            {
                failureCode = 55u;
                failureValue0 = builtClusterCount;
                failureValue1 = MaxDebrisClusters;
                return false;
            }

            if (!_debrisClusters.TryLockForWrite(out NativeArray<WreckDebrisCluster> clusters))
            {
                failureCode = 56u;
                failureValue0 = MaxDebrisClusters;
                return false;
            }

            try
            {
                if (clusters.Length < MaxDebrisClusters)
                {
                    failureCode = 55u;
                    failureValue0 = MaxDebrisClusters;
                    failureValue1 = clusters.Length;
                    return false;
                }

                int clusterWriteCount = math.min(clusters.Length, MaxDebrisClusters);
                for (int i = 0; i < clusterWriteCount; i++)
                    clusters[i] = _debrisClusterBuildCache[i];
            }
            finally
            {
                _debrisClusters.ReleaseWriteLock();
            }

            return true;
        }

        private void BuildDebrisSpatialHash(Bounds worldBounds, uint seed)
        {
            if (!_debrisRecords.IsCreated || !_debrisSpatialKeys.IsCreated || !IsFiniteBounds(worldBounds))
                return;

            float qualityWeight01 = ResolveWreckQualityWeight01();
            int budget = math.min(math.min(maxDebrisRecords, MaxDebrisRecords), ResolveDebrisBudget(qualityWeight01));
            if (budget <= 0 || _lootRecordCount <= 0)
                return;

            int stagingBudget = math.min(budget, math.min(_debrisRecordBuildCache.Length, _debrisSpatialKeyBuildCache.Length));
            XorShift32State rng = new XorShift32State(seed ^ DebrisFieldSalt);
            Vector3 center = worldBounds.center;
            Vector3 extents = worldBounds.extents;
            float horizontalX = math.max(1f, extents.x);
            float horizontalZ = math.max(1f, extents.z);
            float vertical = math.max(0.25f, extents.y);

            bool hasDeferredFailureTelemetry = stagingBudget < budget;
            uint deferredFailureFlags = hasDeferredFailureTelemetry ? 55u : 0u;
            float deferredFailureValue0 = budget;
            float deferredFailureValue1 = stagingBudget;
            Vector3 deferredFailurePosition = center;
            int builtCount = 0;
            int builtClusterCount = 0;

            for (int i = 0; i < MaxDebrisClusters; i++)
                _debrisClusterBuildCache[i] = default;

            for (int i = 0; i < stagingBudget; i++)
            {
                uint stableId = MixFragmentSeed(seed ^ DebrisFieldSalt ^ (uint)(i + 1));
                float x = ((stableId & 0xFFu) * (1f / 255f) * 2f - 1f) * horizontalX;
                stableId = MixFragmentSeed(stableId ^ 0x9E3779B9u);
                float z = ((stableId & 0xFFu) * (1f / 255f) * 2f - 1f) * horizontalZ;
                stableId = MixFragmentSeed(stableId ^ 0xBB67AE85u);
                float y = center.y + (((stableId & 0xFFu) * (1f / 255f) * 2f - 1f) * vertical * 0.25f);
                Vector3 position = new Vector3(center.x + x, y, center.z + z);
                float terrainY = ResolveDebrisVisualSinkHeight(position, worldBounds, stableId, qualityWeight01);
                int lootIndex = _lootRecordCount > 0 ? (int)(rng.NextUInt() % (uint)_lootRecordCount) : 0;
                if (!_lootRecords.TryGet(lootIndex, out WreckLootRecord lootRecord))
                {
                    hasDeferredFailureTelemetry = true;
                    deferredFailurePosition = position;
                    deferredFailureFlags = 28u;
                    deferredFailureValue0 = lootIndex;
                    deferredFailureValue1 = _lootRecordCount;
                    break;
                }

                int quantity = ResolveLootQuantity(in lootRecord, rng.NextUInt(), 3);
                int spatialKey = ResolveDebrisSpatialHashKey(position);
                int clusterKey = ResolveDebrisClusterKey(position);
                int clusterIndex = ResolveDebrisClusterIndex(clusterKey, position, _debrisClusterBuildCache, ref builtClusterCount);
                if (clusterIndex < 0)
                {
                    hasDeferredFailureTelemetry = true;
                    deferredFailurePosition = position;
                    deferredFailureFlags = 29u;
                    deferredFailureValue0 = clusterKey;
                    deferredFailureValue1 = MaxDebrisClusters;
                    break;
                }

                _debrisRecordBuildCache[i] = new WreckDebrisRecord
                {
                    Position = new float3(position.x, position.y, position.z),
                    InitialY = position.y,
                    TerrainY = terrainY,
                    SpatialHashKey = spatialKey,
                    ClusterIndex = clusterIndex,
                    ItemHashId = lootRecord.ItemHashId,
                    Quantity = (ushort)math.max(1, math.min(quantity, ushort.MaxValue)),
                    Flags = (byte)(WreckDebrisFlags.Pickable | WreckDebrisFlags.DotOnly),
                    LootTableIndex = (byte)lootIndex,
                    StableId = stableId,
                    SinkMetersPerSlowTick = 0.04f + ((stableId & 7u) * 0.01f),
                    PickupRadiusSq = DebrisActivationDistanceSq
                };
                _debrisSpatialKeyBuildCache[i] = spatialKey;
                builtCount = i + 1;
            }

            bool copiedDebrisPayload = false;
            if (!TryCopyDebrisRecordsBuildCacheToVault(stagingBudget, builtCount, out uint copyFailureCode, out float copyFailureValue0, out float copyFailureValue1))
            {
                hasDeferredFailureTelemetry = true;
                deferredFailurePosition = center;
                deferredFailureFlags = copyFailureCode;
                deferredFailureValue0 = copyFailureValue0;
                deferredFailureValue1 = copyFailureValue1;
            }
            else if (!TryCopyDebrisSpatialKeyBuildCacheToVault(stagingBudget, builtCount, out copyFailureCode, out copyFailureValue0, out copyFailureValue1))
            {
                hasDeferredFailureTelemetry = true;
                deferredFailurePosition = center;
                deferredFailureFlags = copyFailureCode;
                deferredFailureValue0 = copyFailureValue0;
                deferredFailureValue1 = copyFailureValue1;
            }
            else if (!TryCopyDebrisClusterBuildCacheToVault(builtClusterCount, out copyFailureCode, out copyFailureValue0, out copyFailureValue1))
            {
                hasDeferredFailureTelemetry = true;
                deferredFailurePosition = center;
                deferredFailureFlags = copyFailureCode;
                deferredFailureValue0 = copyFailureValue0;
                deferredFailureValue1 = copyFailureValue1;
            }
            else
            {
                copiedDebrisPayload = true;
            }

            _debrisRecordCount = copiedDebrisPayload ? builtCount : 0;
            _debrisClusterCount = copiedDebrisPayload ? builtClusterCount : 0;
            if (hasDeferredFailureTelemetry)
            {
                WriteBlackBoxTelemetry(
                    WreckTelemetryFailureHash,
                    deferredFailurePosition,
                    deferredFailureFlags,
                    deferredFailureValue0,
                    deferredFailureValue1);
            }
        }

        private int ResolveDebrisClusterIndex(int clusterKey, Vector3 position, WreckDebrisCluster[] clusters, ref int clusterCount)
        {
            int clusterIndex = (int)((uint)clusterKey % (uint)MaxDebrisClusters);
            if (clusters == null || clusters.Length < MaxDebrisClusters)
                return -1;

            WreckDebrisCluster cluster = clusters[clusterIndex];
            if (cluster.DebrisCount <= 0 || cluster.ClusterKey != clusterKey)
            {
                cluster.Center = new float3(position.x, position.y, position.z);
                cluster.Extents = new float3(DebrisClusterSizeMeters * 0.5f, math.max(1f, brgScatterVerticalMeters + 1f), DebrisClusterSizeMeters * 0.5f);
                cluster.ClusterKey = clusterKey;
                cluster.DebrisCount = 0;
                cluster.Visible = 1;
                clusterCount = math.min(MaxDebrisClusters, clusterCount + 1);
            }

            cluster.DebrisCount = math.min(cluster.DebrisCount + 1, int.MaxValue);
            clusters[clusterIndex] = cluster;
            return clusterIndex;
        }

        private void BuildArtifactFragmentHash(
            Vector3 runtimeOrigin,
            Bounds worldBounds,
            uint seed,
            int moduleCount,
            int brgFragmentCount)
        {
            if (!_artifactRecords.IsCreated)
                return;

            int capacity = _artifactRecords.Length;
            if (capacity <= 0)
                return;

            uint deferredFailureCode = 0u;
            Vector3 deferredFailurePosition = runtimeOrigin;
            float deferredFailureValue0 = 0f;
            float deferredFailureValue1 = 0f;
            int stagingCapacity = math.min(capacity, _artifactRecordBuildCache.Length);
            int builtCount = 0;

            if (_allPlacements.IsCreated && _allPlacements.Length > 0)
            {
                int placementCount = math.min(_allPlacements.Length, stagingCapacity);
                for (int i = 0; i < placementCount; i++)
                {
                    if (!_allPlacements.TryGet(i, out WreckModulePlacement placement))
                    {
                        deferredFailureCode = 30u;
                        deferredFailureValue0 = i;
                        deferredFailureValue1 = placementCount;
                        break;
                    }

                    Vector3 recordPosition = runtimeOrigin + ToVector3(placement.Position);
                    if (!TryBuildArtifactRecord(recordPosition, seed, i, placement.ModuleId, placement.MortonIndex, out WreckArtifactRecord record, out uint buildFailureCode))
                    {
                        if (buildFailureCode != 0u)
                        {
                            deferredFailureCode = buildFailureCode;
                            deferredFailurePosition = recordPosition;
                            deferredFailureValue0 = placement.ModuleId;
                            deferredFailureValue1 = _runtimeDefinitions.Length;
                            break;
                        }

                        continue;
                    }

                    _artifactRecordBuildCache[builtCount] = record;
                    builtCount++;
                    if (builtCount >= stagingCapacity)
                        break;
                }
            }
            else
            {
                int fallbackCount = math.min(math.max(moduleCount, brgFragmentCount > 0 ? moduleCount : 0), stagingCapacity);
                for (int i = 0; i < fallbackCount; i++)
                {
                    byte moduleId = (byte)math.min(i, math.max(0, moduleCount - 1));
                    Vector3 position = ResolveDeterministicPointInBounds(worldBounds, seed ^ LoreFragmentSalt ^ (uint)(i + 1));
                    if (!TryBuildArtifactRecord(position, seed, i, moduleId, i, out WreckArtifactRecord record, out uint buildFailureCode))
                    {
                        if (buildFailureCode != 0u)
                        {
                            deferredFailureCode = buildFailureCode;
                            deferredFailurePosition = position;
                            deferredFailureValue0 = moduleId;
                            deferredFailureValue1 = _runtimeDefinitions.Length;
                            break;
                        }

                        continue;
                    }

                    _artifactRecordBuildCache[builtCount] = record;
                    builtCount++;
                    if (builtCount >= stagingCapacity)
                        break;
                }
            }

            if (!_artifactRecords.TryLockForWrite(out NativeArray<WreckArtifactRecord> artifactRecords))
            {
                _artifactRecordCount = 0;
                WriteBlackBoxTelemetry(WreckTelemetryFailureHash, runtimeOrigin, 4u, 0f, capacity);
                return;
            }

            try
            {
                int writeCount = math.min(capacity, artifactRecords.Length);
                for (int i = 0; i < writeCount; i++)
                {
                    artifactRecords[i] = i < builtCount ? _artifactRecordBuildCache[i] : default;
                }
            }
            finally
            {
                _artifactRecords.ReleaseWriteLock();
            }

            _artifactRecordCount = builtCount;
            if (deferredFailureCode != 0u)
                WriteBlackBoxTelemetry(WreckTelemetryFailureHash, deferredFailurePosition, deferredFailureCode, deferredFailureValue0, deferredFailureValue1);
        }

        private bool TryBuildArtifactRecord(
            Vector3 position,
            uint seed,
            int moduleIndex,
            byte moduleId,
            int salt,
            out WreckArtifactRecord record,
            out uint failureCode)
        {
            record = default;
            failureCode = 0u;
            if (!_runtimeDefinitions.IsCreated || moduleId >= _runtimeDefinitions.Length)
                return false;

            if (!_runtimeDefinitions.TryGet(moduleId, out WreckModuleRuntimeDefinition definition))
            {
                failureCode = 31u;
                return false;
            }

            ushort chance = definition.LoreFragmentChancePermille;
            if (chance == 0)
                return false;

            uint stableId = MixFragmentSeed(seed ^ LoreFragmentSalt ^ unchecked((uint)(salt + 1)) ^ ((uint)moduleId << 16));
            if ((stableId % 1000u) >= chance)
                return false;

            record = new WreckArtifactRecord
            {
                EntryHash = MixFragmentSeed(stableId ^ 0x41524348u),
                Position = new float3(position.x, position.y, position.z),
                ModuleIndex = moduleIndex,
                State = 0,
                ModuleId = moduleId,
                ChancePermille = chance,
                StableId = stableId,
                DiscoveryRadiusSq = ArtifactDiscoveryDistanceSq
            };
            return true;
        }

        private void BuildScorchDecalRecords(Vector3 runtimeOrigin, Bounds worldBounds, uint seed, int moduleCount)
        {
            if (!_scorchDecalRecords.IsCreated)
                return;

            int capacity = _scorchDecalRecords.Length;
            if (capacity <= 0)
                return;

            uint deferredFailureCode = 0u;
            Vector3 deferredFailurePosition = worldBounds.center;
            float deferredFailureValue0 = 0f;
            float deferredFailureValue1 = 0f;
            int stagingCapacity = math.min(capacity, _scorchDecalBuildCache.Length);
            int builtCount = 0;

            if (_allPlacements.IsCreated && _allPlacements.Length > 0)
            {
                int placementCount = _allPlacements.Length;
                for (int i = 0; i < placementCount && builtCount < stagingCapacity; i++)
                {
                    if (!_allPlacements.TryGet(i, out WreckModulePlacement placement))
                    {
                        deferredFailureCode = 32u;
                        deferredFailurePosition = runtimeOrigin;
                        deferredFailureValue0 = i;
                        deferredFailureValue1 = placementCount;
                        break;
                    }

                    if (placement.IntegrityState != (byte)WreckIntegrityState.Ruptured)
                        continue;

                    Vector3 position = runtimeOrigin + ToVector3(placement.Position + placement.BoundsCenter);
                    _scorchDecalBuildCache[builtCount] = CreateScorchDecalRecord(position, placement.ModuleId, seed, i);
                    builtCount++;
                }
            }
            else
            {
                int safeModuleCount = math.min(moduleCount, _runtimeDefinitions.IsCreated ? _runtimeDefinitions.Length : 0);
                for (int i = 0; i < safeModuleCount && builtCount < stagingCapacity; i++)
                {
                    if (!_runtimeDefinitions.TryGet(i, out WreckModuleRuntimeDefinition definition))
                    {
                        deferredFailureCode = 33u;
                        deferredFailureValue0 = i;
                        deferredFailureValue1 = safeModuleCount;
                        break;
                    }

                    if (definition.IntegrityState != (byte)WreckIntegrityState.Ruptured)
                        continue;

                    Vector3 position = ResolveDeterministicPointInBounds(worldBounds, seed ^ ScorchDecalSalt ^ (uint)(i + 1));
                    _scorchDecalBuildCache[builtCount] = CreateScorchDecalRecord(position, (byte)i, seed, i);
                    builtCount++;
                }
            }

            if (!_scorchDecalRecords.TryLockForWrite(out NativeArray<WreckScorchDecalRecord> scorchDecalRecords))
            {
                _scorchDecalCount = 0;
                WriteBlackBoxTelemetry(WreckTelemetryFailureHash, worldBounds.center, 11u, 0f, capacity);
                return;
            }

            try
            {
                int writeCount = math.min(capacity, scorchDecalRecords.Length);
                for (int i = 0; i < writeCount; i++)
                {
                    scorchDecalRecords[i] = i < builtCount ? _scorchDecalBuildCache[i] : default;
                }
            }
            finally
            {
                _scorchDecalRecords.ReleaseWriteLock();
            }

            _scorchDecalCount = builtCount;
            if (deferredFailureCode != 0u)
                WriteBlackBoxTelemetry(WreckTelemetryFailureHash, deferredFailurePosition, deferredFailureCode, deferredFailureValue0, deferredFailureValue1);
        }

        private static WreckScorchDecalRecord CreateScorchDecalRecord(Vector3 position, byte moduleId, uint seed, int index)
        {
            uint stableId = MixFragmentSeed(seed ^ ScorchDecalSalt ^ unchecked((uint)(index + 1)));
            float radius = 0.45f + ((stableId & 7u) * 0.09f);
            return new WreckScorchDecalRecord
            {
                Position = new float3(position.x, position.y, position.z),
                Normal = new float3(0f, 1f, 0f),
                Radius = radius,
                Intensity = 0.62f + ((stableId & 3u) * 0.08f),
                StableId = stableId,
                ModuleId = moduleId
            };
        }

        private void BuildBurialCutRecords(Vector3 runtimeOrigin, Bounds worldBounds, uint seed)
        {
            if (!_burialCutRecords.IsCreated || buriedWreckCutFraction <= 0f)
                return;

            if (!TryResolveCurrentRuntimeOriginAbsolute(out double3 originAbsolute))
                return;

            int capacity = _burialCutRecords.Length;
            int stagingCapacity = math.min(capacity, _burialCutBuildCache.Length);
            int placementCount = _allPlacements.IsCreated ? math.min(_allPlacements.Length, stagingCapacity) : 0;
            uint deferredFailureCode = 0u;
            Vector3 deferredFailurePosition = runtimeOrigin;
            float deferredFailureValue0 = 0f;
            float deferredFailureValue1 = 0f;
            int builtCount = 0;

            for (int i = 0; i < placementCount; i++)
            {
                if (!_allPlacements.TryGet(i, out WreckModulePlacement placement))
                {
                    deferredFailureCode = 34u;
                    deferredFailureValue0 = i;
                    deferredFailureValue1 = placementCount;
                    break;
                }

                float burialHash = (MixFragmentSeed(seed ^ unchecked((uint)(placement.MortonIndex + 1))) & 0xFFu) * (1f / 255f);
                if (burialHash > buriedWreckCutFraction)
                    continue;

                float3 localCenter = placement.Position + placement.BoundsCenter;
                Vector3 runtimeCenter = runtimeOrigin + ToVector3(localCenter);
                if (!TryResolveRuntimeAbsoluteDouble(runtimeCenter, originAbsolute, out double3 absoluteCenter))
                    continue;

                float3 halfExtents = SanitizeBoundsSize(placement.BoundsSize) * 0.5f;
                halfExtents.y = math.max(0.05f, math.min(halfExtents.y, wreckInteriorCutHalfHeight));
                _burialCutBuildCache[builtCount] = new WreckBurialCutRecord
                {
                    AbsoluteCenter = absoluteCenter,
                    HalfExtents = halfExtents,
                    BlendStrength = math.max(0.25f, math.cmin(halfExtents) * 0.35f),
                    MaterialId = 0,
                    Applied = 0,
                    StableId = MixFragmentSeed(seed ^ unchecked((uint)(i + 1)) ^ 0x43565442u)
                };
                builtCount++;

                if (builtCount >= stagingCapacity)
                    break;
            }

            if (builtCount == 0 && IsFiniteBounds(worldBounds) && stagingCapacity > 0)
            {
                if (!TryResolveRuntimeAbsoluteDouble(worldBounds.center, originAbsolute, out double3 absoluteCenter))
                    return;

                Vector3 halfExtents = worldBounds.extents;
                _burialCutBuildCache[0] = new WreckBurialCutRecord
                {
                    AbsoluteCenter = absoluteCenter,
                    HalfExtents = new float3(math.max(1f, halfExtents.x * 0.25f), wreckInteriorCutHalfHeight, math.max(1f, halfExtents.z * 0.25f)),
                    BlendStrength = math.max(0.25f, wreckInteriorCutHalfHeight * 0.35f),
                    MaterialId = 0,
                    Applied = 0,
                    StableId = MixFragmentSeed(seed ^ 0x43565442u)
                };
                builtCount = 1;
            }

            if (!_burialCutRecords.TryLockForWrite(out NativeArray<WreckBurialCutRecord> burialCutRecords))
            {
                _burialCutRecordCount = 0;
                WriteBlackBoxTelemetry(WreckTelemetryFailureHash, worldBounds.center, 12u, 0f, capacity);
                return;
            }

            try
            {
                int writeCount = math.min(capacity, burialCutRecords.Length);
                for (int i = 0; i < writeCount; i++)
                    burialCutRecords[i] = i < builtCount ? _burialCutBuildCache[i] : default;
            }
            finally
            {
                _burialCutRecords.ReleaseWriteLock();
            }

            _burialCutRecordCount = builtCount;
            if (deferredFailureCode != 0u)
                WriteBlackBoxTelemetry(WreckTelemetryFailureHash, deferredFailurePosition, deferredFailureCode, deferredFailureValue0, deferredFailureValue1);
        }

        private void ApplyBurialCutsToVoxelSurgeon()
        {
            if (wreckVoxelCutVolume == null || _burialCutRecordCount <= 0)
                return;

            HectonVoxelEngine engine = _voxelEngine;
            if (engine == null || engine.DeltaProcessor == null)
                return;

            int count = math.min(_burialCutRecordCount, _burialCutRecords.Length);
            for (int i = 0; i < count; i++)
            {
                if (!_burialCutRecords.TryGet(i, out WreckBurialCutRecord record))
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 35u, i, count);
                    return;
                }

                if (record.Applied != 0)
                    continue;

                record.Applied = 1;
                if (!_burialCutRecords.TrySet(i, in record))
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, ToVector3(record.HalfExtents), 13u, i, count);
                    return;
                }

                Vector3 halfExtents = new Vector3(record.HalfExtents.x, record.HalfExtents.y, record.HalfExtents.z);
                engine.DeltaProcessor.ApplyImmediateAbsoluteBoxCrater(wreckVoxelCutVolume, record.AbsoluteCenter, halfExtents, record.MaterialId);
            }
        }

        private void PublishWreckLightingGlobals(Bounds worldBounds, uint seed)
        {
            if (!IsFiniteBounds(worldBounds))
                return;

            uint flickerHash = MixFragmentSeed(seed ^ 0x454D5247u);
            float flicker = 0.45f + ((flickerHash & 0xFFu) * (1f / 255f) * 0.55f);
            Shader.SetGlobalFloat(_wreckEmergencyFlickerId, flicker);
            Shader.SetGlobalFloat(_wreckEmergencyPhaseId, (flickerHash >> 8) * (1f / 16777215f));
        }

        private void ProcessNearFieldDebris()
        {
            if (_debrisRecordCount <= 0 || !_debrisSpatialKeys.IsCreated || !_debrisRecords.IsCreated)
                return;

            IPlayerRuntimeContext player = _playerRuntimeContext;
            Transform playerTransform = player != null ? player.PlayerTransform : null;
            if (playerTransform == null)
                return;

            Vector3 playerPosition = playerTransform.position;
            int2 playerCell = ResolveDebrisSpatialCell(playerPosition);
            int count = math.min(_debrisRecordCount, _debrisRecords.Length);
            if (count <= 0)
                return;

            int pickupDebrisIndex = -1;
            WreckDebrisRecord pickupRecord = default;
            GameObject pickupPrefab = null;
            Hecton8.Items.ItemData pickupItemData = null;
            int pickupQuantity = 0;
            Vector3 pickupPosition = default;
            Quaternion pickupRotation = default;
            int scanCount = math.min(count, ResolveDebrisNearFieldScanSlice(ResolveWreckQualityWeight01()));

            float3 playerPosition3 = new float3(playerPosition.x, playerPosition.y, playerPosition.z);
            for (int processed = 0; processed < scanCount; processed++)
            {
                int debrisIndex = (_debrisNearFieldCursor + processed) % count;
                if (!_debrisSpatialKeys.TryGet(debrisIndex, out int spatialKey))
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, playerPosition, 54u, debrisIndex, count);
                    return;
                }

                if (!IsNearDebrisSpatialCell(spatialKey, playerCell))
                    continue;

                if (!_debrisRecords.TryGet(debrisIndex, out WreckDebrisRecord record))
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, playerPosition, 54u, debrisIndex, count);
                    return;
                }

                if ((record.Flags & (byte)(WreckDebrisFlags.Harvested | WreckDebrisFlags.ActivePickup)) != 0)
                    continue;

                float3 delta = record.Position - playerPosition3;
                if (math.lengthsq(delta) > record.PickupRadiusSq)
                    continue;

                if (!TryResolveDebrisPickupSpawn(
                        in record,
                        out pickupPrefab,
                        out pickupItemData,
                        out pickupQuantity,
                        out pickupPosition,
                        out pickupRotation))
                {
                    continue;
                }

                record.Flags = (byte)((record.Flags | (byte)WreckDebrisFlags.ActivePickup) & ~(byte)WreckDebrisFlags.DotOnly);
                pickupRecord = record;
                pickupDebrisIndex = debrisIndex;
                _debrisNearFieldCursor = (debrisIndex + 1) % count;
                break;
            }

            if (pickupDebrisIndex < 0)
            {
                _debrisNearFieldCursor = (_debrisNearFieldCursor + scanCount) % count;
                return;
            }

            if (!_debrisRecords.TrySet(pickupDebrisIndex, in pickupRecord))
            {
                WriteBlackBoxTelemetry(WreckTelemetryFailureHash, ToVector3(pickupRecord.Position), 5u, pickupDebrisIndex, 0f);
                return;
            }

            if (QueueWreckLootSpawn(pickupPrefab, pickupItemData, pickupQuantity, pickupPosition, pickupRotation, pickupDebrisIndex))
                return;

            pickupRecord.Flags = (byte)((pickupRecord.Flags | (byte)(WreckDebrisFlags.Pickable | WreckDebrisFlags.DotOnly)) &
                ~(byte)WreckDebrisFlags.ActivePickup);
            if (!_debrisRecords.TrySet(pickupDebrisIndex, in pickupRecord))
                WriteBlackBoxTelemetry(WreckTelemetryFailureHash, ToVector3(pickupRecord.Position), 55u, pickupDebrisIndex, 0f);
        }

        private bool TryResolveDebrisPickupSpawn(
            in WreckDebrisRecord record,
            out GameObject prefab,
            out Hecton8.Items.ItemData itemData,
            out int quantity,
            out Vector3 position,
            out Quaternion rotation)
        {
            prefab = null;
            itemData = null;
            quantity = 0;
            position = default;
            rotation = default;

            if (record.ItemHashId == 0 || _pendingLootCount >= MaxPendingLootSpawns)
                return false;

            int lootIndex = record.LootTableIndex;
            if ((uint)lootIndex >= (uint)_lootRecordCount ||
                (uint)lootIndex >= (uint)_lootItemDataCache.Length)
                return false;

            itemData = _lootItemDataCache[lootIndex];
            prefab = _lootPrefabCache[lootIndex];
            if (itemData == null || prefab == null)
                return false;

            quantity = math.max(1, record.Quantity);
            position = new Vector3(record.Position.x, record.Position.y, record.Position.z);
            rotation = ResolveCardinalLootRotation(record.StableId);
            return true;
        }

        private void ProcessArtifactDiscovery()
        {
            if (_artifactRecordCount <= 0 || !_artifactRecords.IsCreated)
                return;

            IPlayerRuntimeContext player = _playerRuntimeContext;
            Transform playerTransform = player != null ? player.PlayerTransform : null;
            if (playerTransform == null)
                return;

            float3 playerPosition = new float3(playerTransform.position.x, playerTransform.position.y, playerTransform.position.z);
            int count = math.min(_artifactRecordCount, _artifactRecords.Length);
            int scanCount = math.min(count, ResolveArtifactDiscoveryScanSlice(ResolveWreckQualityWeight01()));
            for (int processed = 0; processed < scanCount; processed++)
            {
                int i = (_artifactDiscoveryCursor + processed) % count;
                if (!_artifactRecords.TryGet(i, out WreckArtifactRecord record))
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, playerTransform.position, 38u, i, count);
                    return;
                }

                if (record.State != 0 || record.EntryHash == 0u)
                    continue;

                if (math.lengthsq(record.Position - playerPosition) > record.DiscoveryRadiusSq)
                    continue;

                record.State = 1;
                if (!_artifactRecords.TrySet(i, in record))
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, ToVector3(record.Position), 6u, i, 0f);
                    return;
                }

                if (!ScanEvents.TryRaiseEntryDiscovered(record.EntryHash, 0u, 0u, 0u, ScanEntryKind.Scannable))
                {
                    record.State = 0;
                    if (!_artifactRecords.TrySet(i, in record))
                        WriteBlackBoxTelemetry(WreckTelemetryFailureHash, ToVector3(record.Position), 57u, i, 0f);
                    continue;
                }

                WriteBlackBoxTelemetry(WreckTelemetryGenerationHash, ToVector3(record.Position), record.EntryHash, 1f, 0f);
                _artifactDiscoveryCursor = (i + 1) % count;
                return;
            }

            _artifactDiscoveryCursor = (_artifactDiscoveryCursor + scanCount) % count;
        }

        private void UpdateDebrisGravityStateless()
        {
            if (_debrisRecordCount <= 0 || !_debrisRecords.IsCreated)
                return;

            int count = math.min(_debrisRecordCount, _debrisRecords.Length);
            int sliceCount = math.min(count, ResolveDebrisGravitySlice(ResolveWreckQualityWeight01()));
            int frameBucket = Hecton8.Core.SystemDispatcher.CurrentFrameIndex & 4095;
            if (sliceCount <= 0)
                return;

            if (sliceCount > _debrisGravityIndexSnapshot.Length ||
                sliceCount > _debrisGravityYSnapshot.Length)
            {
                WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 64u, sliceCount, _debrisGravityIndexSnapshot.Length);
                return;
            }

            int gravityWriteCount = 0;
            for (int processed = 0; processed < sliceCount; processed++)
            {
                int i = (_debrisGravityCursor + processed) % count;
                if (!_debrisRecords.TryGet(i, out WreckDebrisRecord record))
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 39u, i, count);
                    return;
                }

                if ((record.Flags & (byte)WreckDebrisFlags.Harvested) != 0)
                    continue;

                float nextY = ResolveStatelessDebrisGravityY(record.InitialY, record.TerrainY, record.StableId, frameBucket, record.SinkMetersPerSlowTick);
                if (math.abs(nextY - record.Position.y) <= 0.001f)
                    continue;

                _debrisGravityIndexSnapshot[gravityWriteCount] = i;
                _debrisGravityYSnapshot[gravityWriteCount] = nextY;
                gravityWriteCount++;
            }

            if (gravityWriteCount <= 0)
            {
                _debrisGravityCursor = (_debrisGravityCursor + sliceCount) % count;
                return;
            }

            if (!_debrisRecords.TryLockForWrite(out NativeArray<WreckDebrisRecord> debrisRecords))
            {
                WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 39u, _debrisGravityCursor, count);
                return;
            }

            try
            {
                for (int writeIndex = 0; writeIndex < gravityWriteCount; writeIndex++)
                {
                    int i = _debrisGravityIndexSnapshot[writeIndex];
                    WreckDebrisRecord record = debrisRecords[i];
                    if ((record.Flags & (byte)WreckDebrisFlags.Harvested) != 0)
                        continue;

                    record.Position.y = _debrisGravityYSnapshot[writeIndex];
                    debrisRecords[i] = record;
                }
            }
            finally
            {
                _debrisRecords.ReleaseWriteLock();
            }

            _debrisGravityCursor = (_debrisGravityCursor + sliceCount) % count;
        }

        private static float ResolveStatelessDebrisGravityY(float initialY, float terrainY, uint stableId, int frameBucket, float sinkMetersPerSlowTick)
        {
            float phaseOffset = (stableId & 31u) * 0.03f;
            float sink = (frameBucket * math.max(0.001f, sinkMetersPerSlowTick) * 0.05f) + phaseOffset;
            return math.max(terrainY, initialY - sink);
        }

        private void ConfigureIntegrityProxy(GameObject instance)
        {
            if (instance == null)
                return;

            if (!instance.TryGetComponent(out WreckIntegritySignalProxy proxy) || proxy == null)
                return;

            proxy.Configure(this, unchecked((int)EntityId.ToULong(GetEntityId())));
        }

        internal void ApplyWreckInteractionSignal(in InteractionSignal signal, Vector3 runtimeHitPoint)
        {
            uint capabilityMask = ToolCapabilityMasks.ResolveCapabilityMask((InteractionEffectType)signal.EffectType);
            if ((capabilityMask & ToolCapabilityMasks.Laser) == 0)
                return;

            if (_openedSealedModuleCount >= _sealedModuleCount)
                return;

            _openedSealedModuleCount++;
            ScanEvents.TryRaiseWreckSignalPing(new float3(runtimeHitPoint.x, runtimeHitPoint.y, runtimeHitPoint.z), 6f);
            WriteBlackBoxTelemetry(WreckTelemetryInteractionHash, runtimeHitPoint, capabilityMask, signal.PowerDelivered, _openedSealedModuleCount);
        }

        private void TryRegisterWreckSlowTick()
        {
            if (_registeredWreckSlowTick || !Application.isPlaying || _dispatcher == null)
                return;

            if (_debrisRecordCount <= 0 && _artifactRecordCount <= 0 && _pendingLootCount <= 0)
                return;

            _registeredWreckSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void RestoreRuntimeRegistrations()
        {
            if (!Application.isPlaying || _dispatcher == null)
                return;

            if (_pendingLootCount > 0 || _blackBoxDumpRequested)
                TryRegisterLootTick();

            if (_debrisRecordCount > 0 || _artifactRecordCount > 0)
                TryRegisterWreckSlowTick();
        }

        private void TryUnregisterWreckSlowTick()
        {
            if (!_registeredWreckSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredWreckSlowTick = false;
        }

        private void ValidateBlackBoxState()
        {
            if (!IsFiniteBounds(_activeWorldBounds))
            {
                WriteBlackBoxTelemetry(WreckTelemetryNanHash, _activeRuntimeOrigin, 1u, 0f, 0f);
                RequestBlackBoxDump();
                return;
            }

            float3 runtime = new float3(_activeRuntimeOrigin.x, _activeRuntimeOrigin.y, _activeRuntimeOrigin.z);
            if (!math.all(math.isfinite(runtime)))
            {
                WriteBlackBoxTelemetry(WreckTelemetryNanHash, Vector3.zero, 2u, 0f, 0f);
                RequestBlackBoxDump();
            }
        }

        private void WriteBlackBoxTelemetry(uint eventHash, Vector3 position, uint flags, float value0, float value1)
        {
            if (!_telemetryEntries.IsCreated || _telemetryEntries.Length <= 0)
                return;

            int index = _telemetryCursor % _telemetryEntries.Length;
            WreckTelemetryEntry entry = new WreckTelemetryEntry
            {
                FrameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                EventHash = eventHash,
                Seed = _activeGenerationSeed,
                Flags = flags,
                Position = new float3(position.x, position.y, position.z),
                DebrisCount = _debrisRecordCount,
                ArtifactCount = _artifactRecordCount,
                SealedCount = _sealedModuleCount - _openedSealedModuleCount,
                RupturedCount = _rupturedModuleCount,
                Value0 = value0,
                Value1 = value1
            };
            if (!_telemetryEntries.TrySet(index, in entry))
                return;

            _telemetryCursor = (_telemetryCursor + 1) % _telemetryEntries.Length;
            _telemetryWrittenCount = math.min(_telemetryWrittenCount + 1, _telemetryEntries.Length);
        }

        private void RequestBlackBoxDump()
        {
            if (_blackBoxDumpWritten || _blackBoxDumpFailed)
                return;

            _blackBoxDumpRequested = true;
            if (Application.isPlaying && _dispatcher != null)
            {
                TryRegisterLootTick();
                if (_registeredLootLateFrame)
                    return;
            }

            FlushBlackBoxDumpIfRequested();
        }

        private void FlushBlackBoxDumpIfRequested()
        {
            if (!_blackBoxDumpRequested || _blackBoxDumpWritten || _blackBoxDumpFailed)
                return;

            _blackBoxDumpRequested = false;
            if (DumpBlackBox())
                _blackBoxDumpWritten = true;
            else
                _blackBoxDumpFailed = true;
        }

        private bool DumpBlackBox()
        {
            if (!_telemetryEntries.TryResolveReadOnly(out NativeArray<WreckTelemetryEntry>.ReadOnly telemetryEntries) ||
                telemetryEntries.Length <= 0)
                return false;

            NativeArray<byte> payload = default;
            try
            {
                int capacity = telemetryEntries.Length;
                int count = math.min(_telemetryWrittenCount, capacity);
                int start = count >= capacity
                    ? _telemetryCursor % capacity
                    : (_telemetryCursor - count + capacity) % capacity;
                const int headerBytes = 16;
                int byteCount = headerBytes + count * WreckBlackBoxEntrySizeBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(ProceduralWreckGenerator),
                    "ProceduralWreckBlackBoxDumpPayload");

                unsafe
                {
                    byte* bytes = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                    WriteUInt32LittleEndian(bytes, 0, WreckBlackBoxDumpMagic);
                    WriteInt32LittleEndian(bytes, 4, WreckBlackBoxEntrySizeBytes);
                    WriteInt32LittleEndian(bytes, 8, count);
                    WriteInt32LittleEndian(bytes, 12, _telemetryCursor);

                    int cursor = headerBytes;
                    for (int i = 0; i < count; i++)
                    {
                        int index = (start + i) % capacity;
                        WreckTelemetryEntry entry = telemetryEntries[index];
                        UnsafeUtility.CopyStructureToPtr(ref entry, bytes + cursor);
                        cursor += WreckBlackBoxEntrySizeBytes;
                    }
                }

                return NativeFaultDumpWriter.TryWriteAll(ResolveBlackBoxDumpPath(), payload, byteCount);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(ProceduralWreckGenerator),
                    "ProceduralWreckBlackBoxDumpPayload");
            }
        }

        private static string ResolveBlackBoxDumpPath()
        {
#if UNITY_EDITOR
            string dataPath = Application.dataPath;
            if (!string.IsNullOrEmpty(dataPath))
            {
                DirectoryInfo assetsDirectory = Directory.GetParent(dataPath);
                if (assetsDirectory != null)
                    return Path.Combine(assetsDirectory.FullName, BlackBoxDumpPath);
            }
#endif
            return BlackBoxDumpPath;
        }

        private static unsafe void WriteInt32LittleEndian(byte* data, int offset, int value)
        {
            WriteUInt32LittleEndian(data, offset, unchecked((uint)value));
        }

        private static unsafe void WriteUInt32LittleEndian(byte* data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        private static int ResolveDebrisBudget(float qualityWeight01)
        {
            float quality = SanitizeQualityWeight01(qualityWeight01);
            float curved = quality * quality;
            return math.clamp((int)math.round(math.lerp(MinQualityDebrisRecords, MaxDebrisRecords, curved)), MinQualityDebrisRecords, MaxDebrisRecords);
        }

        private static float ResolveDebrisVisualSinkHeight(Vector3 position, Bounds worldBounds, uint stableId, float qualityWeight01)
        {
            float quality = SanitizeQualityWeight01(qualityWeight01);
            float hash01 = ((stableId >> 8) & 0xFFu) * (1f / 255f);
            float roughness = (hash01 * 2f - 1f) * math.lerp(0.04f, 0.22f, quality);
            float sinkDepth = math.lerp(0.18f, 0.55f, quality);
            float localVisualFloor = math.min(position.y - 0.18f, worldBounds.min.y + roughness);
            return localVisualFloor - sinkDepth;
        }

        private static int ResolveDebrisNearFieldScanSlice(float qualityWeight01)
        {
            float quality = SanitizeQualityWeight01(qualityWeight01);
            float curved = quality * quality;
            return math.clamp((int)math.round(math.lerp(192f, 2048f, curved)), 192, 2048);
        }

        private static int ResolveDebrisGravitySlice(float qualityWeight01)
        {
            float quality = SanitizeQualityWeight01(qualityWeight01);
            float curved = quality * quality;
            return math.clamp((int)math.round(math.lerp(256f, 2048f, curved)), 256, 2048);
        }

        private static int ResolveArtifactDiscoveryScanSlice(float qualityWeight01)
        {
            float quality = SanitizeQualityWeight01(qualityWeight01);
            float curved = quality * quality;
            return math.clamp((int)math.round(math.lerp(MinArtifactDiscoveryScanSlice, MaxArtifactDiscoveryScanSlice, curved)), MinArtifactDiscoveryScanSlice, MaxArtifactDiscoveryScanSlice);
        }

        private int ResolveRenderPayloadCapacity()
        {
            return math.max(1, math.max(maxPlacements, math.max(brgMinFragmentCount, brgMaxFragmentCount)));
        }

        private static int2 ResolveDebrisSpatialCell(Vector3 position)
        {
            return new int2(
                (int)math.floor(position.x * DebrisSpatialCellSizeMetersInv),
                (int)math.floor(position.z * DebrisSpatialCellSizeMetersInv));
        }

        private static int ResolveDebrisSpatialHashKey(Vector3 position)
        {
            return ResolveDebrisSpatialHashKey(ResolveDebrisSpatialCell(position));
        }

        private static int ResolveDebrisSpatialHashKey(int2 cell)
        {
            unchecked
            {
                return (cell.x * 73856093) ^ (cell.y * 19349663);
            }
        }

        private static bool IsNearDebrisSpatialCell(int spatialKey, int2 centerCell)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (spatialKey == ResolveDebrisSpatialHashKey(centerCell + new int2(dx, dz)))
                        return true;
                }
            }

            return false;
        }

        private static int ResolveDebrisClusterKey(Vector3 position)
        {
            int x = (int)math.floor(position.x * DebrisClusterSizeMetersInv);
            int z = (int)math.floor(position.z * DebrisClusterSizeMetersInv);
            unchecked
            {
                return (x * 83492791) ^ (z * 265443576);
            }
        }

        private static Vector3 ResolveDeterministicPointInBounds(Bounds bounds, uint seed)
        {
            uint hash = MixFragmentSeed(seed);
            float x = ((hash & 0xFFu) * (1f / 255f) * 2f - 1f) * bounds.extents.x;
            hash = MixFragmentSeed(hash ^ 0x9E3779B9u);
            float y = ((hash & 0xFFu) * (1f / 255f) * 2f - 1f) * bounds.extents.y;
            hash = MixFragmentSeed(hash ^ 0xBB67AE85u);
            float z = ((hash & 0xFFu) * (1f / 255f) * 2f - 1f) * bounds.extents.z;
            return bounds.center + new Vector3(x, y, z);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private async Awaitable<bool> SolveGridAsync(int cellCount, ushort initialMask, int moduleCount, uint seed)
        {
            if (cellCount <= 0 || moduleCount <= 0 || !_grid.IsCreated || !_allPlacements.IsCreated)
                return false;

            double stageStartTime = Time.realtimeSinceStartupAsDouble;
            if (!InitializeGrid(cellCount, initialMask, false))
            {
                WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 14u, cellCount, moduleCount);
                return false;
            }

            await YieldAfterGenerationStageAsync(stageStartTime);
            if (!CanContinueGeneration())
                return false;
            stageStartTime = Time.realtimeSinceStartupAsDouble;

            XorShift32State rng = new XorShift32State(seed);
            CollapseGrid(cellCount, moduleCount, ref rng);
            await YieldAfterGenerationStageAsync(stageStartTime);
            if (!CanContinueGeneration())
                return false;
            stageStartTime = Time.realtimeSinceStartupAsDouble;

            BuildPlacements(cellCount, seed);
            await YieldAfterGenerationStageAsync(stageStartTime);
            if (!CanContinueGeneration())
                return false;

            return _allPlacements.IsCreated && _allPlacements.Length > 0;
        }

        private static async Awaitable YieldAfterGenerationStageAsync(double stageStartTime)
        {
            double elapsedSeconds = Time.realtimeSinceStartupAsDouble - stageStartTime;
            PublishWreckSolveBudgetWarningIfNeeded(elapsedSeconds * 1000.0);

            if (!Application.isPlaying || AsyncGenerationStageYieldThresholdFrames <= 0)
                return;

            if (elapsedSeconds < AsyncGenerationStageMainThreadBudgetSeconds)
                return;

            await AwaitableDebtMonitor.NextFrameAsync();
        }

        private static async Awaitable<bool> YieldMeshBuildFrameAsync(int waitedFrames)
        {
            if (!Application.isPlaying)
                return true;

            if (waitedFrames >= AsyncMeshBuildYieldWatchdogFrames)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(WreckSolveBudgetWarningHash, WreckBrgContextHash, waitedFrames);
                return false;
            }

            await AwaitableDebtMonitor.NextFrameAsync();
            return true;
        }

        private static bool ShouldYieldMeshBuildSlice(int processedWorkCount, double sliceStartTime)
        {
            if (!Application.isPlaying || processedWorkCount <= 0)
                return false;

            if ((processedWorkCount % AsyncMeshBuildSliceCheckInterval) != 0)
                return false;

            return (Time.realtimeSinceStartupAsDouble - sliceStartTime) >= AsyncMeshBuildMainThreadBudgetSeconds;
        }

        private void RefreshRuntimeDefinitions()
        {
            ClearRuntimeDefinitionCache();

            int count = math.min(moduleDefinitions != null ? moduleDefinitions.Length : 0, MaxModuleDefinitions);
            for (int i = 0; i < count; i++)
            {
                ProceduralWreckModuleDefinition source = moduleDefinitions[i];
                bool isFallbackConnector = i == 0 || source.UniversalConnector;
                ushort universalMask = 0xFFFF;

                WreckModuleRuntimeDefinition runtimeDefinition = new WreckModuleRuntimeDefinition
                {
                    NorthSocket = isFallbackConnector ? universalMask : source.NorthSocket,
                    EastSocket = isFallbackConnector ? universalMask : source.EastSocket,
                    SouthSocket = isFallbackConnector ? universalMask : source.SouthSocket,
                    WestSocket = isFallbackConnector ? universalMask : source.WestSocket,
                    TopSocket = isFallbackConnector ? universalMask : source.TopSocket,
                    BottomSocket = isFallbackConnector ? universalMask : source.BottomSocket,
                    BoundsCenter = new float3(source.LocalBounds.center.x, source.LocalBounds.center.y, source.LocalBounds.center.z),
                    BoundsSize = new float3(source.LocalBounds.size.x, source.LocalBounds.size.y, source.LocalBounds.size.z),
                    DrawCallPriority = (byte)math.clamp((int)source.DrawCallPriority, 0, 2),
                    EmitsGeometry = (byte)((source.EmitsGeometry && source.StructuralMesh != null) ? 1 : 0),
                    EmitsNavProxy = (byte)(source.EmitsNavProxy ? 1 : 0),
                    UniversalConnector = (byte)(isFallbackConnector ? 1 : 0),
                    IntegrityState = (byte)math.clamp((int)source.IntegrityState, 0, 2),
                    RequiresLaserCutter = (byte)(source.RequiresLaserCutter ? 1 : 0),
                    LootTableIndex = (byte)math.clamp(source.LootTableIndex, 0, MaxLootRecords - 1),
                    LoreFragmentChancePermille = (ushort)math.clamp(source.LoreFragmentChancePermille, 0, 1000)
                };
                _runtimeDefinitionBuildCache[i] = runtimeDefinition;
            }

            if (!_runtimeDefinitions.TryLockForWrite(out NativeArray<WreckModuleRuntimeDefinition> runtimeDefinitions))
            {
                ClearRuntimeDefinitionCache();
                WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 15u, count, MaxModuleDefinitions);
                return;
            }

            try
            {
                int writeCount = math.min(_runtimeDefinitions.Length, runtimeDefinitions.Length);
                for (int i = 0; i < writeCount; i++)
                    runtimeDefinitions[i] = i < count ? _runtimeDefinitionBuildCache[i] : default;
            }
            finally
            {
                _runtimeDefinitions.ReleaseWriteLock();
            }
        }

        private void ClearRuntimeDefinitionCache()
        {
            for (int i = 0; i < MaxModuleDefinitions; i++)
                _runtimeDefinitionBuildCache[i] = default;
        }

        private int ResolveRuntimeModuleCount()
        {
            if (moduleDefinitions == null || moduleDefinitions.Length == 0)
                return 0;

            return math.min(moduleDefinitions.Length, MaxModuleDefinitions);
        }

        private ushort ResolveInitialMask(int moduleCount)
        {
            ushort initialMask = 0xFFFF;
            if (moduleCount >= MaxModuleDefinitions)
                return initialMask;

            return (ushort)(initialMask & ((1 << moduleCount) - 1));
        }

        private bool InitializeGrid(int cellCount, ushort initialMask, bool emitTelemetry)
        {
            for (int mortonIndex = 0; mortonIndex < cellCount; mortonIndex++)
            {
                byte constraints = ResolveBoundaryConstraintMask(mortonIndex);
                ushort constrainedMask = ApplyBoundaryConstraints(initialMask, constraints);
                if (constrainedMask == 0)
                    constrainedMask = 1;

                WreckGridCell cell = new WreckGridCell
                {
                    PossibleModuleMask = constrainedMask,
                    CollapsedModuleId = UncollapsedModuleId,
                    SocketConstraints = constraints,
                    Entropy = ComputeEntropy(constrainedMask)
                };
                if (!_grid.TrySet(mortonIndex, in cell))
                {
                    if (emitTelemetry)
                        WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 17u, mortonIndex, cellCount);

                    return false;
                }
            }

            return true;
        }

        private void CollapseGrid(int cellCount, int moduleCount, ref XorShift32State rng)
        {
            int collapsedCount = 0;
            int collapseIterations = 0;
            int contradictionCount = 0;
            int maxObservedPropagationIterations = 0;
            int forcedFallbackCellCount = 0;
            int maxCollapseIterations = math.max(cellCount * collapseWatchdogPerCell, cellCount);
            int maxPropagationIterations = math.max(cellCount * propagationWatchdogPerCell, cellCount);
            WreckSolveTermination termination = WreckSolveTermination.Completed;

            while (collapsedCount < cellCount)
            {
                if (collapseIterations++ >= maxCollapseIterations)
                {
                    forcedFallbackCellCount = ForceResolveRemainingCellsToFallback(cellCount);
                    termination = WreckSolveTermination.CollapseWatchdogTriggered;
                    LogSolveFallback(termination, cellCount, collapseIterations, maxObservedPropagationIterations, contradictionCount, forcedFallbackCellCount);
                    break;
                }

                int selectedIndex = SelectNextCell(cellCount, ref rng, out bool selectionReadFailed);
                if (selectionReadFailed)
                {
                    termination = WreckSolveTermination.DataVaultReadFailed;
                    LogSolveFallback(termination, cellCount, collapseIterations, maxObservedPropagationIterations, contradictionCount, forcedFallbackCellCount);
                    break;
                }

                if (selectedIndex < 0)
                {
                    forcedFallbackCellCount = ForceResolveRemainingCellsToFallback(cellCount);
                    termination = WreckSolveTermination.SelectionFailed;
                    LogSolveFallback(termination, cellCount, collapseIterations, maxObservedPropagationIterations, contradictionCount, forcedFallbackCellCount);
                    break;
                }

                if (!_grid.TryGet(selectedIndex, out WreckGridCell selectedCell))
                {
                    termination = WreckSolveTermination.DataVaultReadFailed;
                    LogSolveFallback(termination, cellCount, collapseIterations, maxObservedPropagationIterations, contradictionCount, forcedFallbackCellCount);
                    break;
                }

                byte chosenModule = SelectModuleFromMask(selectedCell.PossibleModuleMask, moduleCount, ref rng);
                selectedCell.CollapsedModuleId = chosenModule;
                selectedCell.PossibleModuleMask = (ushort)(1 << chosenModule);
                selectedCell.Entropy = 0f;
                if (!_grid.TrySet(selectedIndex, in selectedCell))
                {
                    termination = WreckSolveTermination.DataVaultWriteFailed;
                    LogSolveFallback(termination, cellCount, collapseIterations, maxObservedPropagationIterations, contradictionCount, forcedFallbackCellCount);
                    break;
                }

                collapsedCount++;

                ClearPropagationQueue();
                if (!TryEnqueuePropagationCell(DecodeMorton3(selectedIndex)))
                {
                    forcedFallbackCellCount = ForceResolveRemainingCellsToFallback(cellCount);
                    termination = WreckSolveTermination.PropagationQueueOverflow;
                    LogSolveFallback(termination, cellCount, collapseIterations, maxObservedPropagationIterations, contradictionCount, forcedFallbackCellCount);
                    break;
                }

                WreckSolveTermination propagationTermination = PropagateConstraints(
                    moduleCount,
                    maxPropagationIterations,
                    ref contradictionCount,
                    out int propagationIterations);
                maxObservedPropagationIterations = math.max(maxObservedPropagationIterations, propagationIterations);
                if (propagationTermination != WreckSolveTermination.Completed)
                {
                    forcedFallbackCellCount = ForceResolveRemainingCellsToFallback(cellCount);
                    termination = propagationTermination;
                    LogSolveFallback(termination, cellCount, collapseIterations, maxObservedPropagationIterations, contradictionCount, forcedFallbackCellCount);
                    break;
                }
            }

            _debugLastCollapseIterations = collapseIterations;
            _debugLastPropagationIterations = maxObservedPropagationIterations;
            _debugLastContradictionCount = contradictionCount;
            _debugLastForcedFallbackCellCount = forcedFallbackCellCount;
            _debugLastSolveTermination = termination;
        }

        private void ClearPropagationQueue()
        {
            int clearIterations = 0;
            int maxClearIterations = math.max(_grid.IsCreated ? _grid.Length * 6 : 0, 256);
            while (_propagationQueue.TryDequeue(out _))
            {
                if (clearIterations++ >= maxClearIterations)
                {
                    LogPropagationQueueClearWatchdog(maxClearIterations);
                    break;
                }
            }

            _propagationQueueCount = 0;
        }

        private bool TryEnqueuePropagationCell(int3 coordinate)
        {
            if (!_propagationQueue.IsCreated)
                return false;

            int capacity = _propagationQueueCapacity > 0 ? _propagationQueueCapacity : (_grid.IsCreated ? _grid.Length : 0);
            if (_propagationQueueCount >= capacity)
            {
                if (_propagationQueueDroppedCount < int.MaxValue)
                    _propagationQueueDroppedCount++;
                return false;
            }

            if (!_propagationQueue.TryEnqueue(in coordinate))
                return false;

            _propagationQueueCount++;
            return true;
        }

        private int SelectNextCell(int cellCount, ref XorShift32State rng, out bool readFailed)
        {
            readFailed = false;
            float bestEntropy = float.MaxValue;
            int bestIndex = -1;

            for (int mortonIndex = 0; mortonIndex < cellCount; mortonIndex++)
            {
                if (!_grid.TryGet(mortonIndex, out WreckGridCell cell))
                {
                    readFailed = true;
                    return -1;
                }

                if (cell.CollapsedModuleId != UncollapsedModuleId)
                    continue;

                float candidateEntropy = cell.Entropy;
                if (candidateEntropy < bestEntropy)
                {
                    bestEntropy = candidateEntropy;
                    bestIndex = mortonIndex;
                    continue;
                }

                if (math.abs(candidateEntropy - bestEntropy) > 0.0001f)
                    continue;

                uint mortonHash = unchecked((uint)mortonIndex);
                uint cellHash = unchecked((mortonHash * 747796405u) + 2891336453u);
                XorShift32State tieBreaker = new XorShift32State(rng.State ^ cellHash);
                if (tieBreaker.NextFloat01() > 0.5f)
                    bestIndex = mortonIndex;
            }

            return bestIndex;
        }

        private WreckSolveTermination PropagateConstraints(
            int moduleCount,
            int maxPropagationIterations,
            ref int contradictionCount,
            out int propagationIterations)
        {
            propagationIterations = 0;
            while (_propagationQueue.TryDequeue(out int3 currentCoord))
            {
                if (_propagationQueueCount > 0)
                    _propagationQueueCount--;
                else
                    _propagationQueueCount = 0;

                if (propagationIterations++ >= maxPropagationIterations)
                    return WreckSolveTermination.PropagationWatchdogTriggered;

                int currentIndex = EncodeMorton3(currentCoord.x, currentCoord.y, currentCoord.z);
                if (!_grid.TryGet(currentIndex, out WreckGridCell currentCell))
                    return WreckSolveTermination.DataVaultReadFailed;

                for (byte direction = 0; direction < 6; direction++)
                {
                    if (!TryGetNeighbor(currentCoord, direction, out int neighborIndex))
                        continue;

                    if (!_grid.TryGet(neighborIndex, out WreckGridCell neighborCell))
                        return WreckSolveTermination.DataVaultReadFailed;

                    if (neighborCell.CollapsedModuleId != UncollapsedModuleId)
                        continue;

                    ushort compatibleMask = ComputeCompatibleNeighborMask(
                        currentCell.PossibleModuleMask,
                        neighborCell.PossibleModuleMask,
                        direction,
                        moduleCount);

                    compatibleMask = ApplyBoundaryConstraints(compatibleMask, neighborCell.SocketConstraints);
                    if (compatibleMask == 0)
                    {
                        contradictionCount++;
                        if (contradictionCount > maxContradictionsBeforeFallback)
                            return WreckSolveTermination.ContradictionBudgetExceeded;

                        compatibleMask = 1;
                    }

                    if (compatibleMask == neighborCell.PossibleModuleMask)
                        continue;

                    neighborCell.PossibleModuleMask = compatibleMask;
                    neighborCell.Entropy = ComputeEntropy(compatibleMask);
                    if (!_grid.TrySet(neighborIndex, in neighborCell))
                        return WreckSolveTermination.DataVaultWriteFailed;

                    if (!TryEnqueuePropagationCell(DecodeMorton3(neighborIndex)))
                        return WreckSolveTermination.PropagationQueueOverflow;
                }
            }

            return WreckSolveTermination.Completed;
        }

        private int ForceResolveRemainingCellsToFallback(int cellCount)
        {
            int forcedCount = 0;
            for (int mortonIndex = 0; mortonIndex < cellCount; mortonIndex++)
            {
                if (!_grid.TryGet(mortonIndex, out WreckGridCell cell))
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 40u, mortonIndex, cellCount);
                    break;
                }

                if (cell.CollapsedModuleId != UncollapsedModuleId)
                    continue;

                cell.CollapsedModuleId = 0;
                cell.PossibleModuleMask = 1;
                cell.Entropy = 0f;
                if (!_grid.TrySet(mortonIndex, in cell))
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 18u, mortonIndex, cellCount);
                    break;
                }

                forcedCount++;
            }

            ClearPropagationQueue();
            return forcedCount;
        }

        private void LogSolveFallback(
            WreckSolveTermination termination,
            int cellCount,
            int collapseIterations,
            int propagationIterations,
            int contradictionCount,
            int forcedFallbackCellCount)
        {
            WriteBlackBoxTelemetry(
                WreckSolveBudgetWarningHash,
                _activeRuntimeOrigin,
                unchecked((uint)termination),
                contradictionCount,
                forcedFallbackCellCount);
            GlobalTelemetryBus.PublishPerformanceWarning(WreckSolveBudgetWarningHash, unchecked((uint)termination), cellCount);
        }

        private void BuildPlacements(int cellCount, uint seed)
        {
            _allPlacements.Clear();

            int activeGridResolution = ResolveActiveGridResolution();
            int activePlacementLimit = ResolveActivePlacementLimit();
            float halfSpan = (activeGridResolution * cellSizeMeters) * 0.5f;
            float halfCell = cellSizeMeters * 0.5f;

            for (int mortonIndex = 0; mortonIndex < cellCount; mortonIndex++)
            {
                if (!_grid.TryGet(mortonIndex, out WreckGridCell cell))
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 41u, mortonIndex, cellCount);
                    break;
                }

                int moduleId = cell.CollapsedModuleId;
                if (moduleId < 0 || moduleId >= moduleDefinitions.Length)
                    continue;

                if (!_runtimeDefinitions.TryGet(moduleId, out WreckModuleRuntimeDefinition runtimeDefinition))
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 42u, moduleId, moduleDefinitions.Length);
                    break;
                }

                if (runtimeDefinition.EmitsGeometry == 0 && runtimeDefinition.EmitsNavProxy == 0)
                    continue;

                if (_allPlacements.Length >= activePlacementLimit)
                    break;

                int3 coord = DecodeMorton3(mortonIndex);
                float3 localPosition = new float3(
                    (-halfSpan + halfCell) + (coord.x * cellSizeMeters),
                    (-halfSpan + halfCell) + (coord.y * cellSizeMeters),
                    (-halfSpan + halfCell) + (coord.z * cellSizeMeters));
                if (!IsFiniteFloat3(localPosition))
                    continue;

                quaternion placementRotation = ResolvePlacementRotation(runtimeDefinition, seed, mortonIndex);
                float3 boundsCenter = IsFiniteFloat3(runtimeDefinition.BoundsCenter) ? runtimeDefinition.BoundsCenter : float3.zero;
                float3 boundsSize = SanitizeBoundsSize(runtimeDefinition.BoundsSize);
                if (!IsFiniteFloat3(boundsSize))
                    continue;

                WreckModulePlacement placementRecord = new WreckModulePlacement
                {
                    Position = localPosition,
                    Rotation = placementRotation,
                    BoundsCenter = boundsCenter,
                    BoundsSize = boundsSize,
                    MortonIndex = mortonIndex,
                    ModuleId = (byte)moduleId,
                    DrawPriority = runtimeDefinition.DrawCallPriority,
                    IntegrityState = runtimeDefinition.IntegrityState,
                    ModuleFlags = runtimeDefinition.RequiresLaserCutter
                };
                if (!_allPlacements.TryAddNoResize(in placementRecord))
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 19u, mortonIndex, activePlacementLimit);
                    break;
                }
            }
        }

#if UNITY_EDITOR
        private Mesh BuildMergedMeshForTier(byte tier)
        {
            if (Application.isPlaying)
                return null;

            _filteredPlacements.Clear();
            int placementCount = _allPlacements.Length;
            for (int i = 0; i < placementCount; i++)
            {
                if (!_allPlacements.TryGet(i, out WreckModulePlacement placement))
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 43u, i, placementCount);
                    break;
                }

                if (placement.DrawPriority != tier)
                    continue;

                if (_filteredPlacements.Length >= _filteredPlacements.Capacity)
                    break;

                if (!_filteredPlacements.TryAddNoResize(in placement))
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 20u, i, tier);
                    break;
                }
            }

            return BuildMergedMesh(_filteredPlacements);
        }

        private async Awaitable<Mesh> BuildMergedMeshForTierAsync(byte tier)
        {
            if (Application.isPlaying)
                return null;

            _filteredPlacements.Clear();
            int placementCount = _allPlacements.Length;
            for (int i = 0; i < placementCount; i++)
            {
                if (!_allPlacements.TryGet(i, out WreckModulePlacement placement))
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 43u, i, placementCount);
                    break;
                }

                if (placement.DrawPriority != tier)
                    continue;

                if (_filteredPlacements.Length >= _filteredPlacements.Capacity)
                    break;

                if (!_filteredPlacements.TryAddNoResize(in placement))
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 20u, i, tier);
                    break;
                }
            }

            return await BuildMergedMeshAsync(_filteredPlacements);
        }

        private Mesh BuildMergedMesh(VaultListBuffer<WreckModulePlacement> placements)
        {
            if (Application.isPlaying)
                return null;

            if (!placements.IsCreated || placements.Length <= 0)
                return null;

            int placementCount = placements.Length;
            int totalVertexCount = 0;
            int totalIndexCount = 0;
            int mergeableMeshCount = 0;

            for (int placementIndex = 0; placementIndex < placementCount; placementIndex++)
            {
                if (!placements.TryGet(placementIndex, out WreckModulePlacement placement))
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 44u, placementIndex, placementCount);
                    return null;
                }

                Mesh sourceMesh = ResolveStructuralMesh(placement.ModuleId);
                if (sourceMesh == null || !ValidateMeshLayout(sourceMesh))
                    continue;

                int sourceVertexCount = sourceMesh.vertexCount;
                int sourceIndexCount = ResolveIndexCount(sourceMesh);
                if (sourceVertexCount <= 0 ||
                    sourceIndexCount <= 0 ||
                    totalVertexCount > int.MaxValue - sourceVertexCount ||
                    totalIndexCount > int.MaxValue - sourceIndexCount)
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 53u, placementIndex, placementCount);
                    return null;
                }

                totalVertexCount += sourceVertexCount;
                totalIndexCount += sourceIndexCount;
                mergeableMeshCount++;
            }

            if (totalVertexCount <= 0 || totalIndexCount <= 0 || mergeableMeshCount <= 0)
                return null;

            Mesh.MeshDataArray writableMeshData = Mesh.AllocateWritableMeshData(1);
            bool meshApplied = false;
            bool meshOwnershipTransferred = false;
            Mesh result = null;
            int acquiredSnapshotCount = 0;

            try
            {
                Mesh.MeshData meshData = writableMeshData[0];
                meshData.SetVertexBufferParams(
                    totalVertexCount,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
                    new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4));
                meshData.SetIndexBufferParams(totalIndexCount, IndexFormat.UInt32);

                NativeArray<WreckMergedVertex> destinationVertices = meshData.GetVertexData<WreckMergedVertex>();
                NativeArray<uint> destinationIndices = meshData.GetIndexData<uint>();

                int vertexOffset = 0;
                int indexOffset = 0;
                int copiedMeshCount = 0;

                for (int placementIndex = 0; placementIndex < placementCount; placementIndex++)
                {
                    if (!placements.TryGet(placementIndex, out WreckModulePlacement placement))
                    {
                        WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 44u, placementIndex, placementCount);
                        return null;
                    }

                    Mesh sourceMesh = ResolveStructuralMesh(placement.ModuleId);
                    if (!TryAcquireValidatedMeshData(sourceMesh, out Mesh.MeshDataArray sourceMeshDataArray, out Mesh.MeshData sourceData))
                        continue;

                    _readOnlyMeshSnapshots[acquiredSnapshotCount] = sourceMeshDataArray;
                    acquiredSnapshotCount++;

                    int subMeshCount = sourceData.subMeshCount;
                    CombineMeshDataJob job = new CombineMeshDataJob
                    {
                        SourceMeshData = sourceData,
                        DestinationVertices = destinationVertices,
                        DestinationIndices = destinationIndices,
                        VertexOffset = vertexOffset,
                        IndexOffset = indexOffset,
                        PositionStream = sourceData.GetVertexAttributeStream(VertexAttribute.Position),
                        NormalStream = sourceData.HasVertexAttribute(VertexAttribute.Normal)
                            ? sourceData.GetVertexAttributeStream(VertexAttribute.Normal)
                            : -1,
                        UvStream = sourceData.HasVertexAttribute(VertexAttribute.TexCoord0)
                            ? sourceData.GetVertexAttributeStream(VertexAttribute.TexCoord0)
                            : -1,
                        ColorStream = HasCompatibleVertexColorLayout(sourceData)
                            ? sourceData.GetVertexAttributeStream(VertexAttribute.Color)
                            : -1,
                        PositionOffset = sourceData.GetVertexAttributeOffset(VertexAttribute.Position),
                        PositionStride = ResolveVertexAttributeStride(sourceData, VertexAttribute.Position),
                        NormalOffset = sourceData.HasVertexAttribute(VertexAttribute.Normal)
                            ? sourceData.GetVertexAttributeOffset(VertexAttribute.Normal)
                            : 0,
                        NormalStride = sourceData.HasVertexAttribute(VertexAttribute.Normal)
                            ? ResolveVertexAttributeStride(sourceData, VertexAttribute.Normal)
                            : 0,
                        UvOffset = sourceData.HasVertexAttribute(VertexAttribute.TexCoord0)
                            ? sourceData.GetVertexAttributeOffset(VertexAttribute.TexCoord0)
                            : 0,
                        UvStride = sourceData.HasVertexAttribute(VertexAttribute.TexCoord0)
                            ? ResolveVertexAttributeStride(sourceData, VertexAttribute.TexCoord0)
                            : 0,
                        ColorOffset = HasCompatibleVertexColorLayout(sourceData)
                            ? sourceData.GetVertexAttributeOffset(VertexAttribute.Color)
                            : 0,
                        ColorStride = HasCompatibleVertexColorLayout(sourceData)
                            ? ResolveVertexAttributeStride(sourceData, VertexAttribute.Color)
                            : 0,
                        SubMeshCount = subMeshCount,
                        AttributeFlags =
                            (sourceData.HasVertexAttribute(VertexAttribute.Normal) ? CombineMeshDataJob.AttributeFlagNormals : 0u) |
                            (sourceData.HasVertexAttribute(VertexAttribute.TexCoord0) ? CombineMeshDataJob.AttributeFlagUvs : 0u) |
                            (HasCompatibleVertexColorLayout(sourceData) ? CombineMeshDataJob.AttributeFlagColors : 0u),
                        LocalToWreck = float4x4.TRS(placement.Position, placement.Rotation, new float3(1f)),
                        Rotation = placement.Rotation
                    };

                    job.Execute();
                    copiedMeshCount++;

                    vertexOffset += sourceMesh.vertexCount;
                    indexOffset += ResolveIndexCount(sourceMesh);
                }

                for (int i = 0; i < acquiredSnapshotCount; i++)
                {
                    _readOnlyMeshSnapshots[i].Dispose();
                    _readOnlyMeshSnapshots[i] = default;
                }

                if (copiedMeshCount != mergeableMeshCount ||
                    vertexOffset != totalVertexCount ||
                    indexOffset != totalIndexCount)
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 54u, copiedMeshCount, mergeableMeshCount);
                    return null;
                }

                Bounds localBounds = CalculateLocalBounds(placements);
                meshData.subMeshCount = 1;
                meshData.SetSubMesh(0, new SubMeshDescriptor(0, totalIndexCount, MeshTopology.Triangles)
                {
                    bounds = localBounds,
                    vertexCount = totalVertexCount
                }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontValidateIndices);

                result = new Mesh();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                result.name = GeneratedMergedMeshName;
#endif
                Mesh.ApplyAndDisposeWritableMeshData(
                    writableMeshData,
                    result,
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontValidateIndices);
                meshApplied = true;
                result.bounds = localBounds;
                meshOwnershipTransferred = true;
                return result;
            }
            finally
            {
                for (int snapshotIndex = 0; snapshotIndex < acquiredSnapshotCount; snapshotIndex++)
                {
                    if (_readOnlyMeshSnapshots[snapshotIndex].Length > 0)
                    {
                        _readOnlyMeshSnapshots[snapshotIndex].Dispose();
                        _readOnlyMeshSnapshots[snapshotIndex] = default;
                    }
                }

                if (!meshApplied)
                    writableMeshData.Dispose();
                if (!meshOwnershipTransferred)
                    ReleaseGeneratedMesh(result);
            }
        }

        private async Awaitable<Mesh> BuildMergedMeshAsync(VaultListBuffer<WreckModulePlacement> placements)
        {
            if (Application.isPlaying)
                return null;

            if (!placements.IsCreated || placements.Length <= 0)
                return null;

            int placementCount = placements.Length;
            int totalVertexCount = 0;
            int totalIndexCount = 0;
            int mergedPlacementCount = 0;
            int sliceWorkCount = 0;
            int meshYieldFrames = 0;
            double sliceStartTime = Time.realtimeSinceStartupAsDouble;

            for (int placementIndex = 0; placementIndex < placementCount; placementIndex++)
            {
                if (!placements.TryGet(placementIndex, out WreckModulePlacement placement))
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 45u, placementIndex, placementCount);
                    return null;
                }

                Mesh sourceMesh = ResolveStructuralMesh(placement.ModuleId);
                if (sourceMesh == null || !ValidateMeshLayout(sourceMesh))
                    continue;

                int sourceVertexCount = sourceMesh.vertexCount;
                int sourceIndexCount = ResolveIndexCount(sourceMesh);
                if (sourceVertexCount <= 0 ||
                    sourceIndexCount <= 0 ||
                    totalVertexCount > int.MaxValue - sourceVertexCount ||
                    totalIndexCount > int.MaxValue - sourceIndexCount)
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 55u, placementIndex, placementCount);
                    return null;
                }

                totalVertexCount += sourceVertexCount;
                totalIndexCount += sourceIndexCount;
                mergedPlacementCount++;

                sliceWorkCount++;
                if (!ShouldYieldMeshBuildSlice(sliceWorkCount, sliceStartTime))
                    continue;

                if (!await YieldMeshBuildFrameAsync(meshYieldFrames++))
                    return null;
                if (!CanContinueGeneration())
                    return null;

                sliceStartTime = Time.realtimeSinceStartupAsDouble;
                sliceWorkCount = 0;
            }

            if (totalVertexCount <= 0 || totalIndexCount <= 0 || mergedPlacementCount <= 0)
                return null;

            Mesh.MeshDataArray writableMeshData = Mesh.AllocateWritableMeshData(1);
            bool meshApplied = false;
            bool meshOwnershipTransferred = false;
            Mesh result = null;
            int acquiredSnapshotCount = 0;

            try
            {
                Mesh.MeshData meshData = writableMeshData[0];
                meshData.SetVertexBufferParams(
                    totalVertexCount,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
                    new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4));
                meshData.SetIndexBufferParams(totalIndexCount, IndexFormat.UInt32);

                NativeArray<WreckMergedVertex> destinationVertices = meshData.GetVertexData<WreckMergedVertex>();
                NativeArray<uint> destinationIndices = meshData.GetIndexData<uint>();

                int vertexOffset = 0;
                int indexOffset = 0;
                int copiedMeshCount = 0;

                for (int placementIndex = 0; placementIndex < placementCount; placementIndex++)
                {
                    if (!placements.TryGet(placementIndex, out WreckModulePlacement placement))
                    {
                        WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 45u, placementIndex, placementCount);
                        return null;
                    }

                    Mesh sourceMesh = ResolveStructuralMesh(placement.ModuleId);
                    if (!TryAcquireValidatedMeshData(sourceMesh, out Mesh.MeshDataArray sourceMeshDataArray, out Mesh.MeshData sourceData))
                        continue;

                    _readOnlyMeshSnapshots[acquiredSnapshotCount] = sourceMeshDataArray;
                    acquiredSnapshotCount++;

                    CombineMeshDataJob job = new CombineMeshDataJob
                    {
                        SourceMeshData = sourceData,
                        DestinationVertices = destinationVertices,
                        DestinationIndices = destinationIndices,
                        VertexOffset = vertexOffset,
                        IndexOffset = indexOffset,
                        PositionStream = sourceData.GetVertexAttributeStream(VertexAttribute.Position),
                        NormalStream = sourceData.HasVertexAttribute(VertexAttribute.Normal)
                            ? sourceData.GetVertexAttributeStream(VertexAttribute.Normal)
                            : -1,
                        UvStream = sourceData.HasVertexAttribute(VertexAttribute.TexCoord0)
                            ? sourceData.GetVertexAttributeStream(VertexAttribute.TexCoord0)
                            : -1,
                        ColorStream = HasCompatibleVertexColorLayout(sourceData)
                            ? sourceData.GetVertexAttributeStream(VertexAttribute.Color)
                            : -1,
                        PositionOffset = sourceData.GetVertexAttributeOffset(VertexAttribute.Position),
                        PositionStride = ResolveVertexAttributeStride(sourceData, VertexAttribute.Position),
                        NormalOffset = sourceData.HasVertexAttribute(VertexAttribute.Normal)
                            ? sourceData.GetVertexAttributeOffset(VertexAttribute.Normal)
                            : 0,
                        NormalStride = sourceData.HasVertexAttribute(VertexAttribute.Normal)
                            ? ResolveVertexAttributeStride(sourceData, VertexAttribute.Normal)
                            : 0,
                        UvOffset = sourceData.HasVertexAttribute(VertexAttribute.TexCoord0)
                            ? sourceData.GetVertexAttributeOffset(VertexAttribute.TexCoord0)
                            : 0,
                        UvStride = sourceData.HasVertexAttribute(VertexAttribute.TexCoord0)
                            ? ResolveVertexAttributeStride(sourceData, VertexAttribute.TexCoord0)
                            : 0,
                        ColorOffset = HasCompatibleVertexColorLayout(sourceData)
                            ? sourceData.GetVertexAttributeOffset(VertexAttribute.Color)
                            : 0,
                        ColorStride = HasCompatibleVertexColorLayout(sourceData)
                            ? ResolveVertexAttributeStride(sourceData, VertexAttribute.Color)
                            : 0,
                        SubMeshCount = sourceData.subMeshCount,
                        AttributeFlags =
                            (sourceData.HasVertexAttribute(VertexAttribute.Normal) ? CombineMeshDataJob.AttributeFlagNormals : 0u) |
                            (sourceData.HasVertexAttribute(VertexAttribute.TexCoord0) ? CombineMeshDataJob.AttributeFlagUvs : 0u) |
                            (HasCompatibleVertexColorLayout(sourceData) ? CombineMeshDataJob.AttributeFlagColors : 0u),
                        LocalToWreck = float4x4.TRS(placement.Position, placement.Rotation, new float3(1f)),
                        Rotation = placement.Rotation
                    };

                    job.Execute();
                    copiedMeshCount++;

                    vertexOffset += sourceMesh.vertexCount;
                    indexOffset += ResolveIndexCount(sourceMesh);
                }

                if (copiedMeshCount > 0)
                {
                    for (int snapshotIndex = 0; snapshotIndex < acquiredSnapshotCount; snapshotIndex++)
                    {
                        _readOnlyMeshSnapshots[snapshotIndex].Dispose();
                        _readOnlyMeshSnapshots[snapshotIndex] = default;
                    }
                }

                if (copiedMeshCount != mergedPlacementCount ||
                    vertexOffset != totalVertexCount ||
                    indexOffset != totalIndexCount)
                {
                    WriteBlackBoxTelemetry(WreckTelemetryFailureHash, _activeRuntimeOrigin, 56u, copiedMeshCount, mergedPlacementCount);
                    return null;
                }

                Bounds localBounds = CalculateLocalBounds(placements);
                meshData.subMeshCount = 1;
                meshData.SetSubMesh(0, new SubMeshDescriptor(0, totalIndexCount, MeshTopology.Triangles)
                {
                    bounds = localBounds,
                    vertexCount = totalVertexCount
                }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontValidateIndices);
                result = new Mesh();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                result.name = GeneratedMergedMeshName;
#endif
                Mesh.ApplyAndDisposeWritableMeshData(
                    writableMeshData,
                    result,
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontValidateIndices);
                meshApplied = true;
                result.bounds = localBounds;
                meshOwnershipTransferred = true;
                return result;
            }
            finally
            {
                for (int snapshotIndex = 0; snapshotIndex < acquiredSnapshotCount; snapshotIndex++)
                {
                    if (_readOnlyMeshSnapshots[snapshotIndex].Length > 0)
                    {
                        _readOnlyMeshSnapshots[snapshotIndex].Dispose();
                        _readOnlyMeshSnapshots[snapshotIndex] = default;
                    }
                }

                if (!meshApplied)
                    writableMeshData.Dispose();
                if (!meshOwnershipTransferred)
                    ReleaseGeneratedMesh(result);
            }
        }

#endif

        private static void PublishWreckSolveBudgetWarningIfNeeded(double elapsedMilliseconds)
        {
            if (!Application.isPlaying || elapsedMilliseconds < WreckSolveTelemetryThresholdMs)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                WreckSolveBudgetWarningHash,
                WreckBrgContextHash,
                (float)elapsedMilliseconds);
        }

        private void BuildDisabledNavigationBake(
            Vector3 runtimeOrigin,
            Bounds worldBounds,
            Mesh proxyMesh,
            out WreckNavigationHandle navigationHandle,
            out UnityEngine.Object navigationData,
            out AsyncOperation navigationOperation)
        {
            navigationHandle = null;
            navigationData = null;
            navigationOperation = null;
            _debugLastNavigationState = WreckNavigationState.None;
        }

        private static void ReleaseGeneratedMesh(Mesh mesh)
        {
            if (mesh == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                return;
            }
#endif
            UnityEngine.Object.Destroy(mesh);
        }

        private Mesh ResolveNavigationProxyMesh()
        {
            if (!buildAsyncNavigationBake)
                return null;

            return wreckCollisionProxyMesh;
        }

#if UNITY_EDITOR
        private Mesh ResolveStructuralMesh(int moduleId)
        {
            if (moduleDefinitions == null || moduleId < 0 || moduleId >= moduleDefinitions.Length)
                return null;

            return moduleDefinitions[moduleId].StructuralMesh;
        }

        private bool ValidateMeshLayout(Mesh sourceMesh)
        {
            if (!CanReadMeshData(sourceMesh))
                return false;

            if (!ValidateMeshAttributeLayout(sourceMesh, VertexAttribute.Position, VertexAttributeFormat.Float32, 3, UnsafeUtility.SizeOf<Vector3>()))
                return false;

            if (sourceMesh.HasVertexAttribute(VertexAttribute.Normal) &&
                !ValidateMeshAttributeLayout(sourceMesh, VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, UnsafeUtility.SizeOf<Vector3>()))
            {
                return false;
            }

            if (sourceMesh.HasVertexAttribute(VertexAttribute.TexCoord0) &&
                !ValidateMeshAttributeLayout(sourceMesh, VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, UnsafeUtility.SizeOf<Vector2>()))
            {
                return false;
            }

            return true;
        }

        private static bool TryAcquireValidatedMeshData(Mesh sourceMesh, out Mesh.MeshDataArray meshDataArray, out Mesh.MeshData sourceData)
        {
            meshDataArray = default;
            sourceData = default;
            if (!CanReadMeshData(sourceMesh))
                return false;

            meshDataArray = Mesh.AcquireReadOnlyMeshData(sourceMesh);
            if (meshDataArray.Length <= 0)
            {
                meshDataArray.Dispose();
                meshDataArray = default;
                return false;
            }

            sourceData = meshDataArray[0];
            if (ValidateMeshDataLayout(sourceData))
                return true;

            meshDataArray.Dispose();
            meshDataArray = default;
            sourceData = default;
            return false;
        }

        private static bool CanReadMeshData(Mesh sourceMesh)
        {
            return sourceMesh != null && sourceMesh.isReadable && sourceMesh.vertexCount > 0;
        }

        private static bool ValidateMeshDataLayout(Mesh.MeshData sourceData)
        {
            if (!ValidateMeshDataAttributeLayout(sourceData, VertexAttribute.Position, VertexAttributeFormat.Float32, 3, UnsafeUtility.SizeOf<Vector3>()))
                return false;

            if (sourceData.HasVertexAttribute(VertexAttribute.Normal) &&
                !ValidateMeshDataAttributeLayout(sourceData, VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, UnsafeUtility.SizeOf<Vector3>()))
            {
                return false;
            }

            if (sourceData.HasVertexAttribute(VertexAttribute.TexCoord0) &&
                !ValidateMeshDataAttributeLayout(sourceData, VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, UnsafeUtility.SizeOf<Vector2>()))
            {
                return false;
            }

            return true;
        }

        private static bool HasCompatibleVertexColorLayout(Mesh.MeshData sourceData)
        {
            return sourceData.HasVertexAttribute(VertexAttribute.Color) &&
                   ValidateMeshDataAttributeLayout(sourceData, VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, UnsafeUtility.SizeOf<Color32>());
        }

        private static bool ValidateMeshAttributeLayout(Mesh sourceMesh, VertexAttribute attribute, VertexAttributeFormat expectedFormat, int expectedDimension, int expectedStride)
        {
            if (!sourceMesh.HasVertexAttribute(attribute) ||
                sourceMesh.GetVertexAttributeFormat(attribute) != expectedFormat ||
                sourceMesh.GetVertexAttributeDimension(attribute) != expectedDimension)
            {
                return false;
            }

            int stream = sourceMesh.GetVertexAttributeStream(attribute);
            if (stream < 0)
                return false;

            int offset = sourceMesh.GetVertexAttributeOffset(attribute);
            int stride = sourceMesh.GetVertexBufferStride(stream);
            return offset >= 0 &&
                   stride >= expectedStride &&
                   offset + expectedStride <= stride;
        }

        private static bool ValidateMeshDataAttributeLayout(Mesh.MeshData sourceData, VertexAttribute attribute, VertexAttributeFormat expectedFormat, int expectedDimension, int expectedStride)
        {
            if (!sourceData.HasVertexAttribute(attribute))
                return false;

            if (sourceData.GetVertexAttributeFormat(attribute) != expectedFormat ||
                sourceData.GetVertexAttributeDimension(attribute) != expectedDimension)
            {
                return false;
            }

            int stream = sourceData.GetVertexAttributeStream(attribute);
            if (stream < 0)
                return false;

            int offset = sourceData.GetVertexAttributeOffset(attribute);
            int stride = sourceData.GetVertexBufferStride(stream);
            return offset >= 0 &&
                   stride >= expectedStride &&
                   offset + expectedStride <= stride;
        }

        private static int ResolveVertexAttributeStride(Mesh.MeshData sourceData, VertexAttribute attribute)
        {
            int stream = sourceData.GetVertexAttributeStream(attribute);
            return stream >= 0 ? sourceData.GetVertexBufferStride(stream) : 0;
        }

        private static int ResolveIndexCount(Mesh sourceMesh)
        {
            int total = 0;
            int subMeshCount = sourceMesh.subMeshCount;
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
                total += (int)sourceMesh.GetIndexCount(subMeshIndex);

            return total;
        }
#endif

        private Bounds CalculateLocalBounds(VaultListBuffer<WreckModulePlacement> placements)
        {
            if (!placements.IsCreated || placements.Length <= 0)
                return CreateFallbackLocalBounds();

            Bounds bounds = default;
            bool initialized = false;
            int count = placements.Length;
            for (int i = 0; i < count; i++)
            {
                if (!placements.TryGet(i, out WreckModulePlacement placement) ||
                    !TryResolvePlacementBounds(in placement, out Bounds placementBounds))
                    continue;

                if (!initialized)
                {
                    bounds = placementBounds;
                    initialized = true;
                    continue;
                }

                bounds.Encapsulate(placementBounds);
            }

            return initialized && IsFiniteBounds(bounds) ? bounds : CreateFallbackLocalBounds();
        }

        private static Bounds TranslateBounds(Bounds localBounds, Vector3 runtimeOrigin)
        {
            localBounds.center += runtimeOrigin;
            return IsFiniteBounds(localBounds)
                ? localBounds
                : new Bounds(runtimeOrigin, Vector3.one * 0.25f);
        }

        private Bounds ExpandBoundsForBrgScatter(Bounds worldBounds)
        {
            if (wreckMaterialRegistry == null || !IsFiniteBounds(worldBounds))
                return worldBounds;

            float horizontal = math.max(0f, brgScatterRadiusMeters) * 2f;
            float vertical = math.max(0f, brgScatterVerticalMeters) * 2f;
            if (horizontal <= 0f && vertical <= 0f)
                return worldBounds;

            Vector3 expansion = new Vector3(horizontal, vertical, horizontal);
            worldBounds.Expand(expansion);
            return IsFiniteBounds(worldBounds)
                ? worldBounds
                : new Bounds(worldBounds.center, Vector3.one);
        }

        private static float ComputeEntropy(ushort mask)
        {
            int optionCount = math.countbits((uint)mask);
            if (optionCount <= 0)
                return float.MaxValue;

            return math.log2(optionCount);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogPropagationQueueClearWatchdog(int maxIterations)
        {
            GlobalTelemetryBus.PublishPerformanceWarning(WreckSolveBudgetWarningHash, WreckBrgContextHash, maxIterations);
        }

        private byte SelectModuleFromMask(ushort mask, int moduleCount, ref XorShift32State rng)
        {
            int optionCount = math.countbits((uint)mask);
            if (optionCount <= 0)
                return 0;

            int selected = (int)(rng.NextUInt() % (uint)optionCount);
            for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
            {
                if ((mask & (1 << moduleIndex)) == 0)
                    continue;

                if (selected == 0)
                    return (byte)moduleIndex;

                selected--;
            }

            return 0;
        }

        private ushort ComputeCompatibleNeighborMask(ushort currentMask, ushort neighborMask, byte direction, int moduleCount)
        {
            ushort compatibleMask = 0;
            byte oppositeDirection = OppositeDirection(direction);
            uint moduleBitMask = ResolveModuleBitMask(moduleCount);

            for (int currentModuleIndex = 0; currentModuleIndex < moduleCount; currentModuleIndex++)
            {
                if ((currentMask & (1 << currentModuleIndex)) == 0)
                    continue;

                uint currentSocket = ResolveSocket(currentModuleIndex, direction);
                if (currentSocket == 0u)
                    continue;

                uint4 currentSockets = new uint4(currentSocket);
                for (int neighborBase = 0; neighborBase < moduleCount; neighborBase += 4)
                {
                    uint4 neighborSockets = ResolveSocketQuad(neighborBase, oppositeDirection, moduleCount);
                    bool4 enabledLanes = ResolveEnabledNeighborLanes(neighborMask, neighborBase, moduleCount);
                    bool4 compatibleLanes = enabledLanes & ((currentSockets & neighborSockets) != uint4.zero);
                    uint laneMask = (uint)math.bitmask(compatibleLanes);
                    compatibleMask |= (ushort)(laneMask << neighborBase);
                }
            }

            return (ushort)(compatibleMask & moduleBitMask);
        }

        private ushort ResolveSocket(int moduleIndex, byte direction)
        {
            if (!_runtimeDefinitions.TryGet(moduleIndex, out WreckModuleRuntimeDefinition definition))
                return 0;

            return direction switch
            {
                DirectionNorth => definition.NorthSocket,
                DirectionEast => definition.EastSocket,
                DirectionSouth => definition.SouthSocket,
                DirectionWest => definition.WestSocket,
                DirectionTop => definition.TopSocket,
                DirectionBottom => definition.BottomSocket,
                _ => 0
            };
        }

        private uint4 ResolveSocketQuad(int baseIndex, byte direction, int moduleCount)
        {
            return new uint4(
                ResolveSocketOrZero(baseIndex + 0, direction, moduleCount),
                ResolveSocketOrZero(baseIndex + 1, direction, moduleCount),
                ResolveSocketOrZero(baseIndex + 2, direction, moduleCount),
                ResolveSocketOrZero(baseIndex + 3, direction, moduleCount));
        }

        private uint ResolveSocketOrZero(int moduleIndex, byte direction, int moduleCount)
        {
            if ((uint)moduleIndex >= (uint)moduleCount)
                return 0u;

            return ResolveSocket(moduleIndex, direction);
        }

        private static bool4 ResolveEnabledNeighborLanes(ushort neighborMask, int baseIndex, int moduleCount)
        {
            return new bool4(
                baseIndex + 0 < moduleCount && (neighborMask & (1 << (baseIndex + 0))) != 0,
                baseIndex + 1 < moduleCount && (neighborMask & (1 << (baseIndex + 1))) != 0,
                baseIndex + 2 < moduleCount && (neighborMask & (1 << (baseIndex + 2))) != 0,
                baseIndex + 3 < moduleCount && (neighborMask & (1 << (baseIndex + 3))) != 0);
        }

        private static uint ResolveModuleBitMask(int moduleCount)
        {
            if (moduleCount >= MaxModuleDefinitions)
                return 0xFFFFu;

            return moduleCount <= 0 ? 0u : ((1u << moduleCount) - 1u);
        }

        private ushort ApplyBoundaryConstraints(ushort mask, byte boundaryConstraints)
        {
            ushort constrainedMask = mask;
            if ((boundaryConstraints & (1 << DirectionNorth)) != 0)
                constrainedMask = FilterBoundary(constrainedMask, DirectionNorth);
            if ((boundaryConstraints & (1 << DirectionEast)) != 0)
                constrainedMask = FilterBoundary(constrainedMask, DirectionEast);
            if ((boundaryConstraints & (1 << DirectionSouth)) != 0)
                constrainedMask = FilterBoundary(constrainedMask, DirectionSouth);
            if ((boundaryConstraints & (1 << DirectionWest)) != 0)
                constrainedMask = FilterBoundary(constrainedMask, DirectionWest);
            if ((boundaryConstraints & (1 << DirectionTop)) != 0)
                constrainedMask = FilterBoundary(constrainedMask, DirectionTop);
            if ((boundaryConstraints & (1 << DirectionBottom)) != 0)
                constrainedMask = FilterBoundary(constrainedMask, DirectionBottom);
            return constrainedMask;
        }

        private ushort FilterBoundary(ushort mask, byte direction)
        {
            ushort filteredMask = 0;
            int moduleCount = ResolveRuntimeModuleCount();
            for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
            {
                if ((mask & (1 << moduleIndex)) == 0)
                    continue;

                if (ResolveSocket(moduleIndex, direction) != 0)
                    filteredMask |= (ushort)(1 << moduleIndex);
            }

            return filteredMask;
        }

        private byte ResolveBoundaryConstraintMask(int mortonIndex)
        {
            int3 coord = DecodeMorton3(mortonIndex);
            byte constraints = 0;
            int boundary = ResolveActiveGridResolution() - 1;

            if (coord.z >= boundary)
                constraints |= (byte)(1 << DirectionNorth);
            if (coord.x >= boundary)
                constraints |= (byte)(1 << DirectionEast);
            if (coord.z <= 0)
                constraints |= (byte)(1 << DirectionSouth);
            if (coord.x <= 0)
                constraints |= (byte)(1 << DirectionWest);
            if (coord.y >= boundary)
                constraints |= (byte)(1 << DirectionTop);
            if (coord.y <= 0)
                constraints |= (byte)(1 << DirectionBottom);

            return constraints;
        }

        private bool TryGetNeighbor(int3 coord, byte direction, out int neighborIndex)
        {
            int3 offset = direction switch
            {
                DirectionNorth => new int3(0, 0, 1),
                DirectionEast => new int3(1, 0, 0),
                DirectionSouth => new int3(0, 0, -1),
                DirectionWest => new int3(-1, 0, 0),
                DirectionTop => new int3(0, 1, 0),
                DirectionBottom => new int3(0, -1, 0),
                _ => int3.zero
            };

            int3 next = coord + offset;
            int activeGridResolution = ResolveActiveGridResolution();
            if (next.x < 0 || next.y < 0 || next.z < 0 ||
                next.x >= activeGridResolution || next.y >= activeGridResolution || next.z >= activeGridResolution)
            {
                neighborIndex = -1;
                return false;
            }

            neighborIndex = EncodeMorton3(next.x, next.y, next.z);
            return true;
        }

        private static byte OppositeDirection(byte direction)
        {
            return direction switch
            {
                DirectionNorth => DirectionSouth,
                DirectionEast => DirectionWest,
                DirectionSouth => DirectionNorth,
                DirectionWest => DirectionEast,
                DirectionTop => DirectionBottom,
                DirectionBottom => DirectionTop,
                _ => DirectionNorth
            };
        }

        private quaternion ResolvePlacementRotation(WreckModuleRuntimeDefinition runtimeDefinition, uint seed, int mortonIndex)
        {
            if (runtimeDefinition.EmitsNavProxy != 0 || runtimeDefinition.DrawCallPriority != (byte)WreckLodTier.Clutter)
                return quaternion.identity;

            uint cellHash = unchecked((((uint)mortonIndex + 1u) * 1597334677u) ^ seed);
            return ResolveClutterYawJitter(MixFragmentSeed(cellHash));
        }

        private static quaternion ResolveClutterYawJitter(uint state)
        {
            switch (state & 3u)
            {
                case 0u:
                    return new quaternion(0f, -0.04361939f, 0f, 0.99904823f);
                case 1u:
                    return new quaternion(0f, -0.02181489f, 0f, 0.99976203f);
                case 2u:
                    return new quaternion(0f, 0.02181489f, 0f, 0.99976203f);
                default:
                    return new quaternion(0f, 0.04361939f, 0f, 0.99904823f);
            }
        }

        private static bool TryResolvePlacementBounds(in WreckModulePlacement placement, out Bounds bounds)
        {
            quaternion rotation = math.all(math.isfinite(placement.Rotation.value)) ? placement.Rotation : quaternion.identity;
            float3 center = placement.Position + math.rotate(rotation, placement.BoundsCenter);
            float3 size = SanitizeBoundsSize(placement.BoundsSize);
            if (!IsFiniteFloat3(center) || !IsFiniteFloat3(size))
            {
                bounds = default;
                return false;
            }

            float3 halfExtents = size * 0.5f;
            float3x3 rotationMatrix = new float3x3(rotation);
            float3 worldExtents =
                math.abs(rotationMatrix.c0) * halfExtents.x +
                math.abs(rotationMatrix.c1) * halfExtents.y +
                math.abs(rotationMatrix.c2) * halfExtents.z;

            if (!IsFiniteFloat3(worldExtents))
            {
                bounds = default;
                return false;
            }

            worldExtents = math.max(worldExtents, new float3(0.005f));
            bounds = new Bounds(
                new Vector3(center.x, center.y, center.z),
                new Vector3(worldExtents.x * 2f, worldExtents.y * 2f, worldExtents.z * 2f));
            return true;
        }

        private Bounds CreateFallbackLocalBounds()
        {
            float minimumSize = math.max(0.25f, cellSizeMeters);
            return new Bounds(Vector3.zero, Vector3.one * minimumSize);
        }

        private static float3 SanitizeBoundsSize(float3 size)
        {
            if (!IsFiniteFloat3(size))
                return new float3(0.25f, 0.25f, 0.25f);

            return math.max(math.abs(size), new float3(0.01f, 0.01f, 0.01f));
        }

        private static bool IsFiniteFloat3(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static bool IsFiniteBounds(Bounds bounds)
        {
            float3 center = new float3(bounds.center.x, bounds.center.y, bounds.center.z);
            float3 size = new float3(bounds.size.x, bounds.size.y, bounds.size.z);
            return IsFiniteFloat3(center) && IsFiniteFloat3(size);
        }

        internal void HandleNavigationBakeCompleted(WreckNavigationHandle navigationHandle)
        {
            if (navigationHandle != null)
                navigationHandle.MarkFailed();

            _debugLastNavigationState = WreckNavigationState.None;
        }

        internal void ReleaseNavigationHandle(WreckNavigationHandle navigationHandle)
        {
            if (navigationHandle == null || _activeNavigationHandles == null)
                return;

            _activeNavigationHandles.Remove(navigationHandle);
        }

        private static int ClampPowerOfTwo(int value, int min, int max)
        {
            int safeMin = math.max(1, min);
            int safeMax = math.max(safeMin, max);
            int clamped = math.clamp(value, safeMin, safeMax);
            if ((clamped & (clamped - 1)) == 0)
                return clamped;

            int nextPower = RoundUpPowerOfTwo(clamped);
            if (nextPower > safeMax)
                nextPower >>= 1;
            if (nextPower < safeMin)
                nextPower = safeMin;
            return nextPower;
        }

        private static int ClampPowerOfTwoDown(int value, int min, int max)
        {
            int safeMin = math.max(1, min);
            int safeMax = math.max(safeMin, max);
            int clamped = math.clamp(value, safeMin, safeMax);
            int power = RoundDownPowerOfTwo(clamped);
            return math.clamp(power, safeMin, safeMax);
        }

        private static int RoundUpPowerOfTwo(int value)
        {
            int result = math.max(1, value);
            result--;
            result |= result >> 1;
            result |= result >> 2;
            result |= result >> 4;
            result |= result >> 8;
            result |= result >> 16;
            return result + 1;
        }

        private static int RoundDownPowerOfTwo(int value)
        {
            int result = math.max(1, value);
            result |= result >> 1;
            result |= result >> 2;
            result |= result >> 4;
            result |= result >> 8;
            result |= result >> 16;
            return result - (result >> 1);
        }

        private static float ResolveWreckQualityWeight01()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            return SanitizeQualityWeight01(math.select(1f, qualityWeight, math.isfinite(qualityWeight)));
        }

        private static float SanitizeQualityWeight01(float qualityWeight01)
        {
            return math.clamp(
                math.select(WreckMaximumGenerationQuality01, qualityWeight01, math.isfinite(qualityWeight01)),
                WreckMinimumGenerationQuality01,
                WreckMaximumGenerationQuality01);
        }

        private static int EncodeMorton3(int x, int y, int z)
        {
            return Part1By2(x) | (Part1By2(y) << 1) | (Part1By2(z) << 2);
        }

        private static int3 DecodeMorton3(int morton)
        {
            return new int3(Compact1By2(morton), Compact1By2(morton >> 1), Compact1By2(morton >> 2));
        }

        private static int Part1By2(int value)
        {
            uint x = (uint)value & 0x000003FFu;
            x = (x ^ (x << 16)) & 0xFF0000FFu;
            x = (x ^ (x << 8)) & 0x0300F00Fu;
            x = (x ^ (x << 4)) & 0x030C30C3u;
            x = (x ^ (x << 2)) & 0x09249249u;
            return (int)x;
        }

        private static int Compact1By2(int value)
        {
            uint x = (uint)value & 0x09249249u;
            x = (x ^ (x >> 2)) & 0x030C30C3u;
            x = (x ^ (x >> 4)) & 0x0300F00Fu;
            x = (x ^ (x >> 8)) & 0xFF0000FFu;
            x = (x ^ (x >> 16)) & 0x000003FFu;
            return (int)x;
        }

        private static bool TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup)
        {
            originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            return IsFiniteAup(in originAup);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.isfinite(position.LocalX) &&
                   math.isfinite(position.LocalY) &&
                   math.isfinite(position.LocalZ);
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        private static bool TryResolveCurrentRuntimeOriginAbsolute(out double3 originAbsolute)
        {
            originAbsolute = default;
            if (!TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup))
                return false;

            originAbsolute = originAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(originAbsolute));
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            AbsoluteUniversePosition originAup = default;
            if (!IsFinite(runtimePosition) ||
                !TryResolveCurrentRuntimeOriginAup(out originAup))
            {
                return false;
            }

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in positionAup);
        }

        private static bool TryResolveRuntimeAbsoluteDouble(Vector3 runtimePosition, out double3 absolutePosition)
        {
            absolutePosition = default;
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition positionAup))
                return false;

            absolutePosition = positionAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absolutePosition));
        }

        private static bool TryResolveRuntimeAbsoluteDouble(
            Vector3 runtimePosition,
            double3 originAbsolute,
            out double3 absolutePosition)
        {
            absolutePosition = default;
            if (!IsFinite(runtimePosition) || !math.all(math.isfinite(originAbsolute)))
                return false;

            absolutePosition = originAbsolute + new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            return math.all(math.isfinite(absolutePosition));
        }
    }

    [DisallowMultipleComponent]
    public sealed class WreckIntegritySignalProxy : MonoBehaviour, IInteractionSignalConsumer, IInteractionVulnerabilitySource, IPoolable
    {
        private ProceduralWreckGenerator _owner;
        private int _wreckId;
        private bool _interactableRegistered;

        public uint VulnerabilityMask => ToolCapabilityMasks.PlasmaCut;

        internal void Configure(ProceduralWreckGenerator owner, int wreckId)
        {
            if (_interactableRegistered && (_owner != owner || _wreckId != wreckId))
                UnregisterInteractableTree();

            _owner = owner;
            _wreckId = wreckId;
            TryRegisterInteractableTree();
        }

        public void ApplyInteractionSignal(in InteractionSignal signal, Vector3 runtimeHitPoint)
        {
            if (_owner == null || _wreckId == 0)
                return;

            _owner.ApplyWreckInteractionSignal(in signal, runtimeHitPoint);
        }

        public void OnSpawn()
        {
            TryRegisterInteractableTree();
        }

        public void OnDespawn()
        {
            UnregisterInteractableTree();
            _owner = null;
            _wreckId = 0;
        }

        private void OnEnable()
        {
            TryRegisterInteractableTree();
        }

        private void OnDisable()
        {
            UnregisterInteractableTree();
        }

        private void OnDestroy()
        {
            UnregisterInteractableTree();
        }

        private void TryRegisterInteractableTree()
        {
            if (_interactableRegistered || _owner == null || _wreckId == 0 || !isActiveAndEnabled)
                return;

            InteractableRegistry.RegisterTree(this);
            _interactableRegistered = true;
        }

        private void UnregisterInteractableTree()
        {
            if (!_interactableRegistered)
                return;

            InteractableRegistry.InvalidateTree(this);
            _interactableRegistered = false;
        }
    }

#if UNITY_EDITOR
    public static class HectonCompoundColliderAutoFitter
    {
        private const string GeneratedRootName = "__CompoundCollider_AUTO";
        private const float MinimumColliderSize = 0.025f;
        private const float CapsuleAspectThreshold = 2.25f;
        private const float CapsuleCircularityTolerance = 0.35f;
        private const int PowerIterationCount = 12;

        private struct SymmetricCovariance
        {
            public float XX;
            public float XY;
            public float XZ;
            public float YY;
            public float YZ;
            public float ZZ;

            public Vector3 Multiply(Vector3 value)
            {
                return new Vector3(
                    XX * value.x + XY * value.y + XZ * value.z,
                    XY * value.x + YY * value.y + YZ * value.z,
                    XZ * value.x + YZ * value.y + ZZ * value.z);
            }
        }

        private struct OrientedColliderFit
        {
            public Vector3 Center;
            public Quaternion Rotation;
            public Vector3 Size;
            public byte UseCapsule;
            public int CapsuleDirection;
            public float CapsuleRadius;
            public float CapsuleHeight;
        }

        public static int BakeSelectionRoot(GameObject root)
        {
            if (root == null)
                return 0;

            Undo.RegisterFullObjectHierarchyUndo(root, "Bake compound primitive colliders");
            Transform rootTransform = root.transform;
            Transform existingGeneratedRoot = rootTransform.Find(GeneratedRootName);
            if (existingGeneratedRoot != null)
                Undo.DestroyObjectImmediate(existingGeneratedRoot.gameObject);

            MeshCollider[] meshColliders = root.GetComponentsInChildren<MeshCollider>(true);
            for (int i = 0; i < meshColliders.Length; i++)
            {
                MeshCollider meshCollider = meshColliders[i];
                if (meshCollider != null)
                    Undo.DestroyObjectImmediate(meshCollider);
            }

            GameObject generatedRootObject = new GameObject(GeneratedRootName);
            Undo.RegisterCreatedObjectUndo(generatedRootObject, "Create compound collider root");
            generatedRootObject.layer = root.layer;
            Transform generatedRoot = generatedRootObject.transform;
            generatedRoot.SetParent(rootTransform, false);
            generatedRoot.localPosition = Vector3.zero;
            generatedRoot.localRotation = Quaternion.identity;
            generatedRoot.localScale = Vector3.one;

            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            int fittedCount = 0;
            for (int filterIndex = 0; filterIndex < meshFilters.Length; filterIndex++)
            {
                MeshFilter meshFilter = meshFilters[filterIndex];
                if (meshFilter == null ||
                    meshFilter.transform == generatedRoot ||
                    meshFilter.transform.IsChildOf(generatedRoot))
                {
                    continue;
                }

                Mesh mesh = meshFilter.sharedMesh;
                if (mesh == null || mesh.vertexCount <= 0)
                    continue;

                Matrix4x4 sourceToRoot = rootTransform.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;
                int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
                for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
                {
                    if (!TryFitSubMesh(mesh, subMeshIndex, sourceToRoot, out OrientedColliderFit fit))
                        continue;

                    CreateColliderChild(generatedRoot, meshFilter.name, subMeshIndex, fit, root.layer);
                    fittedCount++;
                }
            }

            if (fittedCount <= 0)
                Undo.DestroyObjectImmediate(generatedRootObject);

            EditorUtility.SetDirty(root);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.Log($"[HectonCompoundColliderAutoFitter] Root={root.name} MeshCollidersRemoved={meshColliders.Length} PrimitiveColliders={fittedCount}", root);
#endif
            return fittedCount;
        }

        private static unsafe bool TryFitSubMesh(Mesh mesh, int subMeshIndex, Matrix4x4 sourceToRoot, out OrientedColliderFit fit)
        {
            fit = default;
            if (mesh == null)
                return false;

            using Mesh.MeshDataArray readOnlyMeshData = Mesh.AcquireReadOnlyMeshData(mesh);
            Mesh.MeshData sourceData = readOnlyMeshData[0];
            if (subMeshIndex < 0 ||
                subMeshIndex >= sourceData.subMeshCount ||
                sourceData.vertexCount <= 0 ||
                !sourceData.HasVertexAttribute(VertexAttribute.Position) ||
                sourceData.GetVertexAttributeFormat(VertexAttribute.Position) != VertexAttributeFormat.Float32 ||
                sourceData.GetVertexAttributeDimension(VertexAttribute.Position) < 3)
            {
                return false;
            }

            SubMeshDescriptor descriptor = sourceData.GetSubMesh(subMeshIndex);
            bool useUInt32Indices = sourceData.indexFormat == IndexFormat.UInt32;
            NativeArray<uint> indices32 = useUInt32Indices ? sourceData.GetIndexData<uint>() : default;
            NativeArray<ushort> indices16 = useUInt32Indices ? default : sourceData.GetIndexData<ushort>();
            int sourceIndexCapacity = useUInt32Indices ? indices32.Length : indices16.Length;
            if (descriptor.indexStart < 0 ||
                descriptor.indexCount <= 0 ||
                descriptor.indexStart > sourceIndexCapacity - descriptor.indexCount)
            {
                return false;
            }

            int positionStream = sourceData.GetVertexAttributeStream(VertexAttribute.Position);
            int positionOffset = sourceData.GetVertexAttributeOffset(VertexAttribute.Position);
            int positionStride = sourceData.GetVertexBufferStride(positionStream);
            int positionBytes = UnsafeUtility.SizeOf<Vector3>();
            if (positionStream < 0 ||
                positionOffset < 0 ||
                positionStride < positionBytes ||
                positionOffset + positionBytes > positionStride)
            {
                return false;
            }

            byte* positionBase = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(sourceData.GetVertexData<byte>(positionStream)) + positionOffset;

            Vector3 centroid = Vector3.zero;
            Vector3 aabbMin = default;
            Vector3 aabbMax = default;
            bool hasPoint = false;
            int pointCount = 0;
            int descriptorEnd = descriptor.indexStart + descriptor.indexCount;
            for (int sourceIndex = descriptor.indexStart; sourceIndex < descriptorEnd; sourceIndex++)
            {
                if (!TryReadIndexedSubMeshPoint(
                        sourceIndex,
                        descriptor.baseVertex,
                        useUInt32Indices,
                        indices32,
                        indices16,
                        positionBase,
                        positionStride,
                        sourceData.vertexCount,
                        sourceToRoot,
                        out Vector3 point))
                {
                    continue;
                }

                centroid += point;
                pointCount++;
                if (!hasPoint)
                {
                    aabbMin = point;
                    aabbMax = point;
                    hasPoint = true;
                    continue;
                }

                aabbMin = Vector3.Min(aabbMin, point);
                aabbMax = Vector3.Max(aabbMax, point);
            }

            if (pointCount <= 2)
                return false;

            centroid /= pointCount;
            SymmetricCovariance covariance = default;
            for (int sourceIndex = descriptor.indexStart; sourceIndex < descriptorEnd; sourceIndex++)
            {
                if (!TryReadIndexedSubMeshPoint(
                        sourceIndex,
                        descriptor.baseVertex,
                        useUInt32Indices,
                        indices32,
                        indices16,
                        positionBase,
                        positionStride,
                        sourceData.vertexCount,
                        sourceToRoot,
                        out Vector3 point))
                {
                    continue;
                }

                Vector3 d = point - centroid;
                covariance.XX += d.x * d.x;
                covariance.XY += d.x * d.y;
                covariance.XZ += d.x * d.z;
                covariance.YY += d.y * d.y;
                covariance.YZ += d.y * d.z;
                covariance.ZZ += d.z * d.z;
            }

            float invCount = 1f / pointCount;
            covariance.XX *= invCount;
            covariance.XY *= invCount;
            covariance.XZ *= invCount;
            covariance.YY *= invCount;
            covariance.YZ *= invCount;
            covariance.ZZ *= invCount;

            Vector3 seed = ResolveLargestAabbAxis(aabbMax - aabbMin);
            Vector3 axis0 = ResolvePrincipalAxis(covariance, seed, Vector3.zero);
            Vector3 axis1 = ResolvePrincipalAxis(covariance, ResolveFallbackAxis(axis0), axis0);
            Vector3 axis2 = Vector3.Cross(axis0, axis1);
            if (axis2.sqrMagnitude <= 0.000001f)
                return false;

            axis2.Normalize();
            axis1 = Vector3.Cross(axis2, axis0).normalized;
            if (!IsFinite(axis0) || !IsFinite(axis1) || !IsFinite(axis2))
                return false;

            float min0 = float.PositiveInfinity;
            float min1 = float.PositiveInfinity;
            float min2 = float.PositiveInfinity;
            float max0 = float.NegativeInfinity;
            float max1 = float.NegativeInfinity;
            float max2 = float.NegativeInfinity;
            for (int sourceIndex = descriptor.indexStart; sourceIndex < descriptorEnd; sourceIndex++)
            {
                if (!TryReadIndexedSubMeshPoint(
                        sourceIndex,
                        descriptor.baseVertex,
                        useUInt32Indices,
                        indices32,
                        indices16,
                        positionBase,
                        positionStride,
                        sourceData.vertexCount,
                        sourceToRoot,
                        out Vector3 point))
                {
                    continue;
                }

                float d0 = Vector3.Dot(point, axis0);
                float d1 = Vector3.Dot(point, axis1);
                float d2 = Vector3.Dot(point, axis2);
                min0 = Mathf.Min(min0, d0);
                min1 = Mathf.Min(min1, d1);
                min2 = Mathf.Min(min2, d2);
                max0 = Mathf.Max(max0, d0);
                max1 = Mathf.Max(max1, d1);
                max2 = Mathf.Max(max2, d2);
            }

            Vector3 size = new Vector3(
                Mathf.Max(MinimumColliderSize, max0 - min0),
                Mathf.Max(MinimumColliderSize, max1 - min1),
                Mathf.Max(MinimumColliderSize, max2 - min2));
            if (!IsFinite(size) || size.sqrMagnitude <= MinimumColliderSize * MinimumColliderSize)
                return false;

            Vector3 center =
                axis0 * ((min0 + max0) * 0.5f) +
                axis1 * ((min1 + max1) * 0.5f) +
                axis2 * ((min2 + max2) * 0.5f);
            Quaternion rotation = Quaternion.LookRotation(axis2, axis1);
            if (!IsFinite(rotation))
                rotation = Quaternion.identity;

            fit.Center = center;
            fit.Rotation = rotation;
            fit.Size = size;
            ResolveCapsuleFit(size, out bool useCapsule, out fit.CapsuleDirection, out fit.CapsuleRadius, out fit.CapsuleHeight);
            fit.UseCapsule = useCapsule ? (byte)1 : (byte)0;
            return true;
        }

        private static unsafe bool TryReadIndexedSubMeshPoint(
            int sourceIndex,
            int baseVertex,
            bool useUInt32Indices,
            NativeArray<uint> indices32,
            NativeArray<ushort> indices16,
            byte* positionBase,
            int positionStride,
            int vertexCount,
            Matrix4x4 sourceToRoot,
            out Vector3 point)
        {
            point = default;
            long rawIndex = useUInt32Indices ? indices32[sourceIndex] : indices16[sourceIndex];
            long vertexIndexLong = rawIndex + baseVertex;
            if ((ulong)vertexIndexLong >= (ulong)vertexCount)
                return false;

            Vector3 sourcePoint = UnsafeUtility.ReadArrayElementWithStride<Vector3>(positionBase, (int)vertexIndexLong, positionStride);
            point = sourceToRoot.MultiplyPoint3x4(sourcePoint);
            return IsFinite(point);
        }

        private static void CreateColliderChild(Transform generatedRoot, string sourceName, int subMeshIndex, OrientedColliderFit fit, int layer)
        {
            GameObject child = new GameObject($"{sourceName}_SM{subMeshIndex:00}_Collider");
            Undo.RegisterCreatedObjectUndo(child, "Create primitive collider");
            child.layer = layer;
            Transform childTransform = child.transform;
            childTransform.SetParent(generatedRoot, false);
            childTransform.localPosition = fit.Center;
            childTransform.localRotation = fit.Rotation;
            childTransform.localScale = Vector3.one;

            if (fit.UseCapsule != 0)
            {
                CapsuleCollider capsule = child.AddComponent<CapsuleCollider>();
                capsule.center = Vector3.zero;
                capsule.direction = fit.CapsuleDirection;
                capsule.radius = fit.CapsuleRadius;
                capsule.height = fit.CapsuleHeight;
                capsule.isTrigger = false;
                return;
            }

            BoxCollider box = child.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = fit.Size;
            box.isTrigger = false;
        }

        private static Vector3 ResolvePrincipalAxis(SymmetricCovariance covariance, Vector3 seed, Vector3 rejectAxis)
        {
            Vector3 axis = Orthogonalize(seed, rejectAxis);
            if (axis.sqrMagnitude <= 0.000001f)
                axis = Orthogonalize(Vector3.right, rejectAxis);
            if (axis.sqrMagnitude <= 0.000001f)
                axis = Orthogonalize(Vector3.up, rejectAxis);
            if (axis.sqrMagnitude <= 0.000001f)
                axis = Vector3.forward;

            axis.Normalize();
            for (int i = 0; i < PowerIterationCount; i++)
            {
                Vector3 next = Orthogonalize(covariance.Multiply(axis), rejectAxis);
                if (next.sqrMagnitude <= 0.000001f)
                    break;

                axis = next.normalized;
            }

            return axis.sqrMagnitude > 0.000001f ? axis.normalized : Vector3.right;
        }

        private static Vector3 Orthogonalize(Vector3 value, Vector3 rejectAxis)
        {
            if (rejectAxis.sqrMagnitude <= 0.000001f)
                return value;

            Vector3 normalizedReject = rejectAxis.normalized;
            return value - normalizedReject * Vector3.Dot(value, normalizedReject);
        }

        private static Vector3 ResolveLargestAabbAxis(Vector3 size)
        {
            if (size.x >= size.y && size.x >= size.z)
                return Vector3.right;
            if (size.y >= size.z)
                return Vector3.up;
            return Vector3.forward;
        }

        private static Vector3 ResolveFallbackAxis(Vector3 axis)
        {
            float x = Mathf.Abs(Vector3.Dot(axis, Vector3.right));
            float y = Mathf.Abs(Vector3.Dot(axis, Vector3.up));
            float z = Mathf.Abs(Vector3.Dot(axis, Vector3.forward));
            if (x <= y && x <= z)
                return Vector3.right;
            if (y <= z)
                return Vector3.up;
            return Vector3.forward;
        }

        private static void ResolveCapsuleFit(Vector3 size, out bool useCapsule, out int direction, out float radius, out float height)
        {
            direction = ResolveDominantAxis(size);
            float dominant = GetAxis(size, direction);
            int axisA = (direction + 1) % 3;
            int axisB = (direction + 2) % 3;
            float secondaryA = GetAxis(size, axisA);
            float secondaryB = GetAxis(size, axisB);
            float secondaryMax = Mathf.Max(secondaryA, secondaryB);
            float circularity = Mathf.Abs(secondaryA - secondaryB) / Mathf.Max(secondaryMax, MinimumColliderSize);
            useCapsule = dominant >= secondaryMax * CapsuleAspectThreshold && circularity <= CapsuleCircularityTolerance;
            radius = Mathf.Max(MinimumColliderSize, secondaryMax * 0.5f);
            height = Mathf.Max(radius * 2f, dominant);
        }

        private static int ResolveDominantAxis(Vector3 size)
        {
            if (size.x >= size.y && size.x >= size.z)
                return 0;
            if (size.y >= size.z)
                return 1;
            return 2;
        }

        private static float GetAxis(Vector3 value, int axis)
        {
            return axis == 0 ? value.x : (axis == 1 ? value.y : value.z);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.isfinite(position.LocalX) &&
                   math.isfinite(position.LocalY) &&
                   math.isfinite(position.LocalZ);
        }

        private static bool TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup)
        {
            originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            return IsFiniteAup(in originAup);
        }

        private static bool TryResolveCurrentRuntimeOriginAbsolute(out double3 originAbsolute)
        {
            originAbsolute = default;
            if (!TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup))
                return false;

            originAbsolute = originAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(originAbsolute));
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFinite(runtimePosition) ||
                !TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup))
            {
                return false;
            }

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in positionAup);
        }

        private static bool TryResolveRuntimeAbsoluteDouble(Vector3 runtimePosition, out double3 absolutePosition)
        {
            absolutePosition = default;
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition positionAup))
                return false;

            absolutePosition = positionAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absolutePosition));
        }

        private static bool TryResolveRuntimeAbsoluteDouble(
            Vector3 runtimePosition,
            double3 originAbsolute,
            out double3 absolutePosition)
        {
            absolutePosition = default;
            if (!IsFinite(runtimePosition) || !math.all(math.isfinite(originAbsolute)))
                return false;

            absolutePosition = originAbsolute + new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            return math.all(math.isfinite(absolutePosition));
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z) &&
                   float.IsFinite(value.w);
        }
    }
#endif
}
