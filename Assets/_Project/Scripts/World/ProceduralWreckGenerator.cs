
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
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

    internal enum WreckSolveTermination : byte
    {
        Completed = 0,
        CollapseWatchdogTriggered = 1,
        PropagationWatchdogTriggered = 2,
        ContradictionBudgetExceeded = 3,
        SelectionFailed = 4
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

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    internal struct WreckGridCell
    {
        public ushort PossibleModuleMask;
        public byte CollapsedModuleId;
        public byte SocketConstraints;
        public float Entropy;
        private uint _reserved0;
        private uint _reserved1;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WreckModuleRuntimeDefinition
    {
        public ushort NorthSocket;
        public ushort EastSocket;
        public ushort SouthSocket;
        public ushort WestSocket;
        public ushort TopSocket;
        public ushort BottomSocket;
        public float3 BoundsCenter;
        public float3 BoundsSize;
        public byte DrawCallPriority;
        public byte EmitsGeometry;
        public byte EmitsNavProxy;
        public byte UniversalConnector;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WreckModulePlacement
    {
        public float3 Position;
        public quaternion Rotation;
        public float3 BoundsCenter;
        public float3 BoundsSize;
        public int MortonIndex;
        public byte ModuleId;
        public byte DrawPriority;
        private ushort _reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WreckMergedVertex
    {
        public float3 Position;
        public float3 Normal;
        public float2 UV;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct CombineMeshDataJob : IJob
    {
        [ReadOnly] public Mesh.MeshData SourceMeshData;
        [NativeDisableParallelForRestriction] public NativeArray<WreckMergedVertex> DestinationVertices;
        [NativeDisableParallelForRestriction] public NativeArray<uint> DestinationIndices;
        public int VertexOffset;
        public int IndexOffset;
        public int PositionStream;
        public int NormalStream;
        public int UvStream;
        public int SubMeshCount;
        public bool HasNormals;
        public bool HasUvs;
        public float4x4 LocalToWreck;
        public quaternion Rotation;

        public void Execute()
        {
            NativeArray<Vector3> sourcePositions = SourceMeshData.GetVertexData<Vector3>(PositionStream);
            NativeArray<Vector3> sourceNormals = HasNormals
                ? SourceMeshData.GetVertexData<Vector3>(NormalStream)
                : default;
            NativeArray<Vector2> sourceUvs = HasUvs
                ? SourceMeshData.GetVertexData<Vector2>(UvStream)
                : default;

            int vertexCount = SourceMeshData.vertexCount;
            for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
            {
                Vector3 sourcePosition = sourcePositions[vertexIndex];
                float3 transformedPosition = math.transform(LocalToWreck, new float3(sourcePosition.x, sourcePosition.y, sourcePosition.z));
                if (!math.all(math.isfinite(transformedPosition)))
                    transformedPosition = float3.zero;

                float3 transformedNormal = new float3(0f, 1f, 0f);
                if (HasNormals)
                {
                    Vector3 sourceNormal = sourceNormals[vertexIndex];
                    float3 rotatedNormal = math.rotate(Rotation, new float3(sourceNormal.x, sourceNormal.y, sourceNormal.z));
                    float normalLengthSq = math.lengthsq(rotatedNormal);
                    transformedNormal = math.select(
                        transformedNormal,
                        rotatedNormal * math.rsqrt(math.max(normalLengthSq, 0.000001f)),
                        math.isfinite(normalLengthSq) && normalLengthSq > 0.000001f);
                }

                float2 uv = float2.zero;
                if (HasUvs)
                {
                    Vector2 sourceUv = sourceUvs[vertexIndex];
                    uv = new float2(sourceUv.x, sourceUv.y);
                }

                DestinationVertices[VertexOffset + vertexIndex] = new WreckMergedVertex
                {
                    Position = transformedPosition,
                    Normal = transformedNormal,
                    UV = uv
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
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct BuildProxyMeshJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<WreckModulePlacement> Placements;
        [NativeDisableParallelForRestriction] public NativeArray<float3> Positions;
        [NativeDisableParallelForRestriction] public NativeArray<uint> Indices;

        public void Execute(int index)
        {
            WreckModulePlacement placement = Placements[index];
            float3 halfExtents = placement.BoundsSize * 0.5f;
            float3 localCenter = placement.Position + math.rotate(placement.Rotation, placement.BoundsCenter);

            int vertexBase = index * 8;
            int indexBase = index * 36;

            float3 p0 = math.rotate(placement.Rotation, new float3(-halfExtents.x, -halfExtents.y, -halfExtents.z)) + localCenter;
            float3 p1 = math.rotate(placement.Rotation, new float3(halfExtents.x, -halfExtents.y, -halfExtents.z)) + localCenter;
            float3 p2 = math.rotate(placement.Rotation, new float3(halfExtents.x, halfExtents.y, -halfExtents.z)) + localCenter;
            float3 p3 = math.rotate(placement.Rotation, new float3(-halfExtents.x, halfExtents.y, -halfExtents.z)) + localCenter;
            float3 p4 = math.rotate(placement.Rotation, new float3(-halfExtents.x, -halfExtents.y, halfExtents.z)) + localCenter;
            float3 p5 = math.rotate(placement.Rotation, new float3(halfExtents.x, -halfExtents.y, halfExtents.z)) + localCenter;
            float3 p6 = math.rotate(placement.Rotation, new float3(halfExtents.x, halfExtents.y, halfExtents.z)) + localCenter;
            float3 p7 = math.rotate(placement.Rotation, new float3(-halfExtents.x, halfExtents.y, halfExtents.z)) + localCenter;

            Positions[vertexBase + 0] = p0;
            Positions[vertexBase + 1] = p1;
            Positions[vertexBase + 2] = p2;
            Positions[vertexBase + 3] = p3;
            Positions[vertexBase + 4] = p4;
            Positions[vertexBase + 5] = p5;
            Positions[vertexBase + 6] = p6;
            Positions[vertexBase + 7] = p7;

            WriteQuad(Indices, indexBase + 0, vertexBase, 0, 2, 1, 3);
            WriteQuad(Indices, indexBase + 6, vertexBase, 4, 5, 6, 7);
            WriteQuad(Indices, indexBase + 12, vertexBase, 0, 1, 5, 4);
            WriteQuad(Indices, indexBase + 18, vertexBase, 1, 2, 6, 5);
            WriteQuad(Indices, indexBase + 24, vertexBase, 2, 3, 7, 6);
            WriteQuad(Indices, indexBase + 30, vertexBase, 3, 0, 4, 7);
        }

        private static void WriteQuad(NativeArray<uint> indices, int targetIndex, int vertexBase, int a, int b, int c, int d)
        {
            indices[targetIndex + 0] = (uint)(vertexBase + a);
            indices[targetIndex + 1] = (uint)(vertexBase + b);
            indices[targetIndex + 2] = (uint)(vertexBase + c);
            indices[targetIndex + 3] = (uint)(vertexBase + a);
            indices[targetIndex + 4] = (uint)(vertexBase + c);
            indices[targetIndex + 5] = (uint)(vertexBase + d);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct BuildWreckRenderPayloadJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<WreckModulePlacement> Placements;
        public NativeArray<Matrix4x4> WorldMatrices;
        public NativeArray<byte> ModuleIds;
        public NativeArray<float> Ages;
        public AbsoluteUniversePosition CenterAup;
        public float3 RuntimeOrigin;
        public float ScatterRadiusMeters;
        public float ScatterVerticalMeters;
        public int ScatterYawEnabled;
        public uint Seed;

        public void Execute(int index)
        {
            WreckModulePlacement placement = Placements[index];
            quaternion rotation = math.all(math.isfinite(placement.Rotation.value))
                ? placement.Rotation
                : quaternion.identity;
            uint hash = ComputeInstanceHash(in CenterAup, Seed, placement.MortonIndex, index, placement.ModuleId);
            float3 scatterOffset = ResolveScatterOffset(hash, ScatterRadiusMeters, ScatterVerticalMeters);
            quaternion scatterRotation = ResolveScatterRotation(hash, ScatterYawEnabled);
            float3 worldPosition = placement.Position + RuntimeOrigin + scatterOffset;
            if (!math.all(math.isfinite(worldPosition)))
                worldPosition = RuntimeOrigin;

            rotation = math.mul(scatterRotation, rotation);
            float4x4 localToWorld = float4x4.TRS(worldPosition, rotation, new float3(1f));
            WorldMatrices[index] = ToMatrix4x4(localToWorld);
            ModuleIds[index] = placement.ModuleId;
            Ages[index] = ComputeAge01(hash);
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

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct BuildWreckScatterMatricesJob : IJobParallelFor
    {
        public NativeArray<Matrix4x4> WorldMatrices;
        public NativeArray<byte> ModuleIds;
        public NativeArray<float> Ages;
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
            int safeModuleCount = math.max(1, math.min(ModuleCount, 16));
            uint hash = ComputeInstanceHash(in CenterAup, Seed, index);
            float3 offset = ResolveScatterOffset(hash, ScatterRadiusMeters, ScatterVerticalMeters);
            quaternion rotation = ResolveScatterRotation(hash, ScatterYawEnabled);
            float scale = ResolveScale(hash, MinScale, MaxScale);
            float3 worldPosition = RuntimeOrigin + offset;
            if (!math.all(math.isfinite(worldPosition)))
                worldPosition = RuntimeOrigin;

            float4x4 localToWorld = float4x4.TRS(worldPosition, rotation, new float3(scale));
            WorldMatrices[index] = ToMatrix4x4(localToWorld);
            ModuleIds[index] = (byte)(Mix(hash ^ 0xA24BAED5u) % (uint)safeModuleCount);
            Ages[index] = ComputeAge01(hash);
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
    public sealed class ProceduralWreckGenerator : MonoBehaviour, IProceduralGenerator, IUpdatable
    {
        private const int MaxModuleDefinitions = 16;
        private const byte UncollapsedModuleId = byte.MaxValue;
        private const byte DirectionNorth = 0;
        private const byte DirectionEast = 1;
        private const byte DirectionSouth = 2;
        private const byte DirectionWest = 3;
        private const byte DirectionTop = 4;
        private const byte DirectionBottom = 5;
        private const int ProxyVerticesPerPlacement = 8;
        private const int ProxyIndicesPerPlacement = 36;
        private const int AsyncGenerationStageYieldThresholdFrames = 1;
        private const int AsyncMeshBuildSliceCheckInterval = 4;
        private const int AsyncJobWaitWatchdogFrames = 600;
        private const int AsyncMeshBuildYieldWatchdogFrames = 600;
        private const double AsyncGenerationStageMainThreadBudgetSeconds = 0.0015d;
        private const double AsyncMeshBuildMainThreadBudgetSeconds = 0.0045d;
        private const int MaxEditorPreviewCellBudget = 256;
        private const uint FallbackSectionSalt = 0xA511E9B3u;
        private const float WreckSolveTelemetryThresholdMs = 0.2f;
        private const int MaxPendingLootSpawns = 8;
        private const uint WreckSolveBudgetWarningHash = 0x57534C56u; // WSLV
        private const uint WreckBrgContextHash = 0x57425247u; // WBRG
        private const string GeneratedMergedMeshName = "ProceduralWreckGenerator_Merged";
        private const string GeneratedProxyMeshName = "ProceduralWreckGenerator_Proxy";
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

        [StructLayout(LayoutKind.Sequential)]
        private struct PendingWreckLootSpawn
        {
            public GameObject Prefab;
            public Hecton8.Items.ItemData ItemData;
            public Vector3 Position;
            public Quaternion Rotation;
            public int Quantity;
        }

        [Header("WFC")]
        [SerializeField, Range(4, 32)]
        [Tooltip("Power-of-two grid resolution used by the wreck WFC kernel. Stored in Morton order for coherent neighbor traversal.")]
        private int gridResolution = 16;

        [SerializeField, Min(1f)]
        [Tooltip("Edge length for each WFC cell in meters.")]
        private float cellSizeMeters = 6f;

        [SerializeField, Min(1)]
        [Tooltip("Hard ceiling for generated structural placements. Capacity is pre-allocated once and generation truncates safely if exceeded.")]
        private int maxPlacements = 256;

        [SerializeField]
        [Tooltip("Additional deterministic salt mixed into the AUP hash to separate generation revisions.")]
        private uint worldGenerationVersionSalt = 1u;

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
        private NativeArray<WreckGridCell> _grid;
        private NativeQueue<int3> _propagationQueue;
        private NativeList<WreckModulePlacement> _allPlacements;
        private NativeList<WreckModulePlacement> _filteredPlacements;
        private NativeArray<WreckModuleRuntimeDefinition> _runtimeDefinitions;
        private string _propagationQueueSentinelLabel;
        private string _allPlacementsSentinelLabel;
        private string _filteredPlacementsSentinelLabel;
        private List<WreckNavigationHandle> _activeNavigationHandles;
        private Mesh.MeshDataArray[] _readOnlyMeshSnapshots;
        private JobHandle[] _copyHandles;
        private readonly PendingWreckLootSpawn[] _pendingLootSpawns = new PendingWreckLootSpawn[MaxPendingLootSpawns]; // COLD ALLOC: PendingWreckLootSpawn[8] - one-per-frame wreck loot spawn queue - owner: ProceduralWreckGenerator
        private GameObject _activeCollisionProxy;
        private Collider _activeCollisionCollider;
        private int _pendingLootReadIndex;
        private int _pendingLootCount;
        private bool _registeredLootTick;
        private bool _initialized;

        private void Awake()
        {
            Initialize();
        }

        private void OnDisable()
        {
            ClearPendingLootQueue();
            TryUnregisterLootTick();
            DespawnActiveCollisionProxy();
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

            FlushOneQueuedLootSpawn();
            if (_pendingLootCount <= 0)
                TryUnregisterLootTick();
        }

        private void OnValidate()
        {
            gridResolution = ClampPowerOfTwo(gridResolution, 4, 32);
            cellSizeMeters = Mathf.Max(1f, cellSizeMeters);
            maxPlacements = Mathf.Max(1, maxPlacements);
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
            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
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

            double spanMeters = gridResolution * cellSizeMeters;
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

            return GenerateInternal(in aup, runtimeOrigin, resolvedSeed);
        }

        /// <summary>
        /// Generates a wreck through the non-blocking startup-safe pipeline.
        /// WFC collapse and placement resolution run off the main thread, while
        /// mesh/nav finalize stages are spread across subsequent frames.
        /// </summary>
        /// <param name="chunkAup">Absolute-universe chunk anchor.</param>
        /// <param name="seed">Stable caller-provided seed.</param>
        /// <returns>Generated wreck payload.</returns>
        public async Awaitable<WreckageData> GenerateAsync(int3 chunkAup, uint seed)
        {
            Initialize();

            double spanMeters = gridResolution * cellSizeMeters;
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

            return await GenerateInternalAsync(aup, runtimeOrigin, resolvedSeed);
        }

        /// <summary>
        /// Generates a wreck payload directly from the existing mega-wreck streaming section contract.
        /// </summary>
        /// <param name="section">Published mega-wreck section payload.</param>
        /// <returns>Generated wreck payload.</returns>
        public WreckageData Generate(in HectonMapMagicVegetationBridge.MegaWreckStreamSection section)
        {
            Initialize();
            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(section.WorldCenter);
            uint resolvedSeed = ComputeGenerationSeed(in aup, (uint)section.SectionSeed ^ worldGenerationVersionSalt);
            if (!CanGenerateAtVoronoiWreckSite(in aup))
            {
                MarkVoronoiGateRejected(resolvedSeed);
                return default;
            }

            return GenerateInternal(in aup, section.WorldCenter, resolvedSeed);
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
            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(section.WorldCenter);
            uint resolvedSeed = ComputeGenerationSeed(in aup, (uint)section.SectionSeed ^ worldGenerationVersionSalt);
            if (!CanGenerateAtVoronoiWreckSite(in aup))
            {
                MarkVoronoiGateRejected(resolvedSeed);
                return default;
            }

            return await GenerateInternalAsync(aup, section.WorldCenter, resolvedSeed);
        }

        /// <summary>
        /// Releases all persistent native state owned by this generator.
        /// </summary>
        public void Dispose()
        {
            ClearPendingLootQueue();
            TryUnregisterLootTick();
            DespawnActiveCollisionProxy();

            if (_activeNavigationHandles != null)
            {
                int handleCount = _activeNavigationHandles.Count;
                for (int i = 0; i < handleCount; i++)
                    _activeNavigationHandles[i]?.Dispose();

                _activeNavigationHandles.Clear();
            }

            if (_grid.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_grid);
                _grid.Dispose();
            }

            if (_propagationQueue.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ProceduralWreckGenerator), _propagationQueueSentinelLabel);
                _propagationQueue.Dispose();
            }

            if (_allPlacements.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(nameof(ProceduralWreckGenerator), _allPlacementsSentinelLabel);
                _allPlacements.Dispose();
            }

            if (_filteredPlacements.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(nameof(ProceduralWreckGenerator), _filteredPlacementsSentinelLabel);
                _filteredPlacements.Dispose();
            }

            if (_runtimeDefinitions.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_runtimeDefinitions);
                _runtimeDefinitions.Dispose();
            }

            _initialized = false;
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
                Seed = parameters.Seed
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
            if (_initialized)
                return;

            gridResolution = ClampPowerOfTwo(gridResolution, 4, 32);
            int maxCellCount = gridResolution * gridResolution * gridResolution;

            // COLD ALLOC: NativeArray<WreckGridCell>[maxCellCount] - Morton-ordered WFC state grid - owner: ProceduralWreckGenerator
            _grid = new NativeArray<WreckGridCell>(maxCellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            NativeMemorySentinel.RegisterNativeArray(_grid, nameof(ProceduralWreckGenerator), nameof(_grid), NativeAllocationLifetime.Scene);
            // COLD ALLOC: NativeQueue<int3>(Persistent) - deterministic WFC propagation frontier - owner: ProceduralWreckGenerator
            _propagationQueue = new NativeQueue<int3>(Allocator.Persistent);
            string sentinelSuffix = string.Concat("_", EntityId.ToULong(GetEntityId()));
            _propagationQueueSentinelLabel = string.Concat(nameof(_propagationQueue), sentinelSuffix);
            _allPlacementsSentinelLabel = string.Concat(nameof(_allPlacements), sentinelSuffix);
            _filteredPlacementsSentinelLabel = string.Concat(nameof(_filteredPlacements), sentinelSuffix);
            NativeMemorySentinel.RegisterNativeQueue(
                _propagationQueue,
                maxCellCount,
                nameof(ProceduralWreckGenerator),
                _propagationQueueSentinelLabel,
            NativeAllocationLifetime.Scene);
            PrewarmQueue(ref _propagationQueue, maxCellCount);
            // COLD ALLOC: NativeList<WreckModulePlacement>[maxPlacements] - merged structural placement list - owner: ProceduralWreckGenerator
            _allPlacements = new NativeList<WreckModulePlacement>(maxPlacements, Allocator.Persistent);
            NativeMemorySentinel.RegisterNativeList(_allPlacements, nameof(ProceduralWreckGenerator), _allPlacementsSentinelLabel, NativeAllocationLifetime.Scene);
            // COLD ALLOC: NativeList<WreckModulePlacement>[maxPlacements] - per-tier mesh merge filter scratch - owner: ProceduralWreckGenerator
            _filteredPlacements = new NativeList<WreckModulePlacement>(maxPlacements, Allocator.Persistent);
            NativeMemorySentinel.RegisterNativeList(_filteredPlacements, nameof(ProceduralWreckGenerator), _filteredPlacementsSentinelLabel, NativeAllocationLifetime.Scene);
            // COLD ALLOC: NativeArray<WreckModuleRuntimeDefinition>[16] - native WFC module table - owner: ProceduralWreckGenerator
            _runtimeDefinitions = new NativeArray<WreckModuleRuntimeDefinition>(MaxModuleDefinitions, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(_runtimeDefinitions, nameof(ProceduralWreckGenerator), nameof(_runtimeDefinitions), NativeAllocationLifetime.Scene);
            buildAsyncNavigationBake = false;
            _activeNavigationHandles = null;
            // COLD ALLOC: Mesh.MeshDataArray[maxPlacements] - scheduled read-only source snapshots - owner: ProceduralWreckGenerator
            _readOnlyMeshSnapshots = new Mesh.MeshDataArray[maxPlacements];
            // COLD ALLOC: JobHandle[maxPlacements] - scheduled mesh copy handles - owner: ProceduralWreckGenerator
            _copyHandles = new JobHandle[maxPlacements];

            RefreshRuntimeDefinitions();
            _initialized = true;
        }

        private WreckageData GenerateInternal(in AbsoluteUniversePosition centerAup, Vector3 runtimeOrigin, uint seed)
        {
            double generationStartTime = Time.realtimeSinceStartupAsDouble;
            RefreshRuntimeDefinitions();

            int moduleCount = ResolveRuntimeModuleCount();
            if (moduleCount <= 0)
                return default;

            if (ShouldUseBrgOnlyWreckPath())
                return GenerateBrgWreckOnly(in centerAup, runtimeOrigin, seed, moduleCount, generationStartTime);

            int cellCount = gridResolution * gridResolution * gridResolution;
            ushort initialMask = ResolveInitialMask(moduleCount);
            InitializeGrid(cellCount, initialMask);

            XorShift32State rng = new XorShift32State(seed);
            CollapseGrid(cellCount, moduleCount, ref rng);
            BuildPlacements(cellCount, seed);
            Mesh combinedMesh = wreckMaterialRegistry != null ? null : BuildMergedMesh(_allPlacements);
            Mesh proxyMesh = ResolveNavigationProxyMesh();

            Bounds localBounds = CalculateLocalBounds(_allPlacements);
            Bounds worldBounds = TranslateBounds(localBounds, runtimeOrigin);
            Bounds renderWorldBounds = ExpandBoundsForBrgScatter(worldBounds);
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

            return new WreckageData
            {
                CombinedMesh = combinedMesh,
                EssentialMesh = null,
                DetailMesh = null,
                ClutterMesh = null,
                ProxyMesh = proxyMesh,
                Navigation = navigationData,
                NavigationBuild = navigationOperation,
                NavigationHandle = navigationHandle,
                NavigationState = navigationHandle != null ? navigationHandle.State : WreckNavigationState.None,
                WorldBounds = renderWorldBounds,
                RuntimeOrigin = runtimeOrigin,
                GenerationSeed = seed
            };
        }

        private async Awaitable<WreckageData> GenerateInternalAsync(AbsoluteUniversePosition centerAup, Vector3 runtimeOrigin, uint seed)
        {
            RefreshRuntimeDefinitions();

            int moduleCount = ResolveRuntimeModuleCount();
            if (moduleCount <= 0)
                return default;

            if (ShouldUseBrgOnlyWreckPath())
                return await GenerateBrgWreckOnlyAsync(centerAup, runtimeOrigin, seed, moduleCount);

            int cellCount = gridResolution * gridResolution * gridResolution;
            ushort initialMask = ResolveInitialMask(moduleCount);

            double stageStartTime = Time.realtimeSinceStartupAsDouble;
            await SolveGridAsync(cellCount, initialMask, moduleCount, seed);
            await YieldAfterGenerationStageAsync(stageStartTime);

            stageStartTime = Time.realtimeSinceStartupAsDouble;
            Mesh combinedMesh = wreckMaterialRegistry != null ? null : await BuildMergedMeshAsync(_allPlacements);
            await YieldAfterGenerationStageAsync(stageStartTime);

            stageStartTime = Time.realtimeSinceStartupAsDouble;
            Mesh proxyMesh = await ResolveNavigationProxyMeshAsync();
            Bounds localBounds = CalculateLocalBounds(_allPlacements);
            Bounds worldBounds = TranslateBounds(localBounds, runtimeOrigin);
            Bounds renderWorldBounds = ExpandBoundsForBrgScatter(worldBounds);
            PublishWreckRenderPayload(in centerAup, runtimeOrigin, renderWorldBounds, seed);
            PublishCollisionProxy(worldBounds);
            SpawnWreckLoot(renderWorldBounds, seed);
            await YieldAfterGenerationStageAsync(stageStartTime);

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

            return new WreckageData
            {
                CombinedMesh = combinedMesh,
                EssentialMesh = null,
                DetailMesh = null,
                ClutterMesh = null,
                ProxyMesh = proxyMesh,
                Navigation = navigationData,
                NavigationBuild = navigationOperation,
                NavigationHandle = navigationHandle,
                NavigationState = navigationHandle != null ? navigationHandle.State : WreckNavigationState.None,
                WorldBounds = renderWorldBounds,
                RuntimeOrigin = runtimeOrigin,
                GenerationSeed = seed
            };
        }

        private bool ShouldUseBrgOnlyWreckPath()
        {
            return wreckMaterialRegistry != null;
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

            PublishBrgScatterPayload(centerAup, runtimeOrigin, worldBounds, seed, fragmentCount, moduleCount);
            PublishCollisionProxy(worldBounds);
            SpawnWreckLoot(worldBounds, seed);
            await YieldAfterGenerationStageAsync(stageStartTime);

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

            NativeArray<Matrix4x4> worldMatrices = new NativeArray<Matrix4x4>(fragmentCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<byte> moduleIds = new NativeArray<byte>(fragmentCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<float> ages = new NativeArray<float>(fragmentCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeMemorySentinel.RegisterNativeArray(worldMatrices, nameof(ProceduralWreckGenerator), nameof(worldMatrices), NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(moduleIds, nameof(ProceduralWreckGenerator), nameof(moduleIds), NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(ages, nameof(ProceduralWreckGenerator), nameof(ages), NativeAllocationLifetime.TempJob);

            try
            {
                var job = new BuildWreckScatterMatricesJob
                {
                    WorldMatrices = worldMatrices,
                    ModuleIds = moduleIds,
                    Ages = ages,
                    CenterAup = centerAup,
                    RuntimeOrigin = new float3(runtimeOrigin.x, runtimeOrigin.y, runtimeOrigin.z),
                    ModuleCount = moduleCount,
                    ScatterRadiusMeters = math.max(0f, brgScatterRadiusMeters),
                    ScatterVerticalMeters = math.max(0f, brgScatterVerticalMeters),
                    ScatterYawEnabled = brgScatterYawDegrees > 0.001f ? 1 : 0,
                    MinScale = math.max(0.05f, brgFragmentMinScale),
                    MaxScale = math.max(math.max(0.05f, brgFragmentMinScale), brgFragmentMaxScale),
                    Seed = seed
                };

                JobHandle handle = job.Schedule(fragmentCount, 64);
                // BLOCKING_SYNC_POINT: cold synchronous generator publish, outside Tick cadence.
                DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);

                wreckMaterialRegistry.Publish(
                    moduleDefinitions,
                    worldMatrices,
                    moduleIds,
                    ages,
                    fragmentCount,
                    worldBounds,
                    centerAup);
            }
            finally
            {
                if (ages.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(ages);
                    ages.Dispose();
                }
                if (moduleIds.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(moduleIds);
                    moduleIds.Dispose();
                }
                if (worldMatrices.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(worldMatrices);
                    worldMatrices.Dispose();
                }
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
            int minCount = math.clamp(brgMinFragmentCount, 50, 200);
            int maxCount = math.clamp(math.max(minCount, brgMaxFragmentCount), 50, 200);
            if (minCount == maxCount)
                return minCount;

            uint hash = MixFragmentSeed(seed ^ worldGenerationVersionSalt ^ 0x57425247u);
            return minCount + (int)(hash % (uint)(maxCount - minCount + 1));
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
            NativeArray<Matrix4x4> worldMatrices = new NativeArray<Matrix4x4>(placementCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<byte> moduleIds = new NativeArray<byte>(placementCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<float> ages = new NativeArray<float>(placementCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeMemorySentinel.RegisterNativeArray(worldMatrices, nameof(ProceduralWreckGenerator), nameof(worldMatrices), NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(moduleIds, nameof(ProceduralWreckGenerator), nameof(moduleIds), NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(ages, nameof(ProceduralWreckGenerator), nameof(ages), NativeAllocationLifetime.TempJob);

            try
            {
                var job = new BuildWreckRenderPayloadJob
                {
                    Placements = _allPlacements.AsArray(),
                    WorldMatrices = worldMatrices,
                    ModuleIds = moduleIds,
                    Ages = ages,
                    CenterAup = centerAup,
                    RuntimeOrigin = new float3(runtimeOrigin.x, runtimeOrigin.y, runtimeOrigin.z),
                    ScatterRadiusMeters = wreckMaterialRegistry != null ? math.max(0f, brgScatterRadiusMeters) : 0f,
                    ScatterVerticalMeters = wreckMaterialRegistry != null ? math.max(0f, brgScatterVerticalMeters) : 0f,
                    ScatterYawEnabled = wreckMaterialRegistry != null && brgScatterYawDegrees > 0.001f ? 1 : 0,
                    Seed = seed
                };
                JobHandle handle = job.Schedule(placementCount, 64);
                // BLOCKING_SYNC_POINT: cold generation publish, outside Tick. BRG consumes the payload immediately.
                DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);

                wreckMaterialRegistry.Publish(
                    moduleDefinitions,
                    worldMatrices,
                    moduleIds,
                    ages,
                    placementCount,
                    worldBounds,
                    centerAup);
            }
            finally
            {
                if (ages.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(ages);
                    ages.Dispose();
                }
                if (moduleIds.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(moduleIds);
                    moduleIds.Dispose();
                }
                if (worldMatrices.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(worldMatrices);
                    worldMatrices.Dispose();
                }
            }
        }

        private void PublishCollisionProxy(Bounds worldBounds)
        {
            if (wreckCollisionProxyPrefab == null || !IsFiniteBounds(worldBounds))
                return;

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (pool == null)
                return;

            DespawnActiveCollisionProxy();

            GameObject instance = pool.Spawn(wreckCollisionProxyPrefab, worldBounds.center, Quaternion.identity);
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
            if (_activeCollisionProxy == null)
            {
                _activeCollisionCollider = null;
                return;
            }

            GameObject proxy = _activeCollisionProxy;
            _activeCollisionProxy = null;
            _activeCollisionCollider = null;

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (pool != null && proxy.activeInHierarchy)
                pool.Despawn(proxy);
            else if (proxy != null)
                proxy.SetActive(false);
        }

        private void SpawnWreckLoot(Bounds worldBounds, uint seed)
        {
            if (itemCatalog == null ||
                wreckLootItemHashes == null ||
                wreckLootItemHashes.Length <= 0 ||
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
            int hashCount = wreckLootItemHashes.Length;
            int maxAttempts = math.max(hashCount * 2, desiredCount);
            int spawnedCount = 0;
            Vector3 center = worldBounds.center;
            float radiusLimit = math.max(
                1f,
                math.min(lootSpawnRadiusMeters, math.max(worldBounds.extents.x, worldBounds.extents.z)));

            for (int attempt = 0; attempt < maxAttempts && spawnedCount < desiredCount; attempt++)
            {
                int hashIndex = (int)(rng.NextUInt() % (uint)hashCount);
                uint hashId = wreckLootItemHashes[hashIndex];
                if (hashId == 0u || !ItemTemplateRegistry.TryGetTemplate(hashId, out ItemTemplate template))
                    continue;

                int signedHashId = unchecked((int)hashId);
                Hecton8.Items.ItemData itemData = itemCatalog.FindByHash(signedHashId);
                if (itemData == null)
                    continue;

                if (!itemCatalog.TryGetLoadedWorldPrefab(signedHashId, out GameObject prefab) || prefab == null)
                {
                    itemCatalog.QueueWorldPrefabPrewarm(signedHashId);
                    continue;
                }

                float lootExtent = radiusLimit * 0.7f;
                float horizontalX = (rng.NextFloat01() * 2f - 1f) * lootExtent;
                float horizontalZ = (rng.NextFloat01() * 2f - 1f) * lootExtent;
                float height = ResolveLootHeightBand(rng.NextUInt());
                Vector3 position = center + new Vector3(horizontalX, height, horizontalZ);
                Quaternion rotation = ResolveCardinalLootRotation(rng.NextUInt());
                int quantity = math.max(1, math.min(3, (int)template.MaxStackSize));
                if (!QueueWreckLootSpawn(prefab, itemData, quantity, position, rotation))
                    continue;

                spawnedCount++;
            }
        }

        private static float ResolveLootHeightBand(uint state)
        {
            switch (state & 3u)
            {
                case 0u:
                    return -0.35f;
                case 1u:
                    return 0.1f;
                case 2u:
                    return 0.55f;
                default:
                    return 1.1f;
            }
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
            Quaternion rotation)
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
                Quantity = math.max(1, quantity)
            };
            _pendingLootCount++;
            TryRegisterLootTick();
            return true;
        }

        private void ClearPendingLootQueue()
        {
            for (int i = 0; i < MaxPendingLootSpawns; i++)
                _pendingLootSpawns[i] = default;

            _pendingLootReadIndex = 0;
            _pendingLootCount = 0;
        }

        private void TryUnregisterLootTick()
        {
            if (!_registeredLootTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registeredLootTick = false;
        }

        private void TryRegisterLootTick()
        {
            if (_registeredLootTick || _pendingLootCount <= 0 || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLootTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void FlushOneQueuedLootSpawn()
        {
            if (_pendingLootCount <= 0)
                return;

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (pool == null)
                return;

            PendingWreckLootSpawn spawn = _pendingLootSpawns[_pendingLootReadIndex];
            _pendingLootSpawns[_pendingLootReadIndex] = default;
            _pendingLootReadIndex = (_pendingLootReadIndex + 1) % MaxPendingLootSpawns;
            _pendingLootCount--;

            if (spawn.Prefab == null || spawn.ItemData == null)
                return;

            GameObject instance = pool.Spawn(spawn.Prefab, spawn.Position, spawn.Rotation);
            if (instance == null)
                return;

            if (instance.TryGetComponent(out PickupItem pickup))
            {
                pickup.Configure(spawn.ItemData, math.max(1, spawn.Quantity));
                return;
            }

            pool.Despawn(instance);
        }

        private async Awaitable SolveGridAsync(int cellCount, ushort initialMask, int moduleCount, uint seed)
        {
            ExceptionDispatchInfo capturedException = null;

            await Awaitable.BackgroundThreadAsync();
            try
            {
                InitializeGrid(cellCount, initialMask);

                XorShift32State rng = new XorShift32State(seed);
                CollapseGrid(cellCount, moduleCount, ref rng);
                BuildPlacements(cellCount, seed);
            }
            catch (Exception ex)
            {
                capturedException = ExceptionDispatchInfo.Capture(ex);
            }

            await Awaitable.MainThreadAsync();
            capturedException?.Throw();
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

        private static async Awaitable<bool> YieldMeshBuildFrameAsync(string context, int waitedFrames)
        {
            if (!Application.isPlaying)
                return true;

            if (waitedFrames >= AsyncMeshBuildYieldWatchdogFrames)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[ProceduralWreckGenerator] Async mesh-build yield watchdog tripped in {context} after {waitedFrames} frames. Aborting generated mesh stage.");
#endif
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

        private static async Awaitable<bool> WaitForJobHandleAsync(JobHandle handle, string context)
        {
            int waitFrames = 0;
            while (!handle.IsCompleted)
            {
                if (waitFrames >= AsyncJobWaitWatchdogFrames)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError($"[ProceduralWreckGenerator] Async job wait timeout in {context} after {waitFrames} frames. Forcing cleanup completion and aborting stage.");
#endif
                    DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
                    return false;
                }

                waitFrames++;
                await AwaitableDebtMonitor.NextFrameAsync();
            }

            DispatcherJobSwap.TryComplete(ref handle, forceComplete: false);
            return true;
        }

        private JobHandle CombineScheduledCopyHandles(int handleCount)
        {
            if (handleCount <= 0)
                return default;

            JobHandle combinedHandle = _copyHandles[0];
            for (int handleIndex = 1; handleIndex < handleCount; handleIndex++)
                combinedHandle = JobHandle.CombineDependencies(combinedHandle, _copyHandles[handleIndex]);

            return combinedHandle;
        }

        private void RefreshRuntimeDefinitions()
        {
            for (int i = 0; i < MaxModuleDefinitions; i++)
                _runtimeDefinitions[i] = default;

            int count = math.min(moduleDefinitions != null ? moduleDefinitions.Length : 0, MaxModuleDefinitions);
            for (int i = 0; i < count; i++)
            {
                ProceduralWreckModuleDefinition source = moduleDefinitions[i];
                bool isFallbackConnector = i == 0 || source.UniversalConnector;
                ushort universalMask = 0xFFFF;

                _runtimeDefinitions[i] = new WreckModuleRuntimeDefinition
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
                    UniversalConnector = (byte)(isFallbackConnector ? 1 : 0)
                };
            }
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

        private void InitializeGrid(int cellCount, ushort initialMask)
        {
            for (int mortonIndex = 0; mortonIndex < cellCount; mortonIndex++)
            {
                byte constraints = ResolveBoundaryConstraintMask(mortonIndex);
                ushort constrainedMask = ApplyBoundaryConstraints(initialMask, constraints);
                if (constrainedMask == 0)
                    constrainedMask = 1;

                _grid[mortonIndex] = new WreckGridCell
                {
                    PossibleModuleMask = constrainedMask,
                    CollapsedModuleId = UncollapsedModuleId,
                    SocketConstraints = constraints,
                    Entropy = ComputeEntropy(constrainedMask)
                };
            }
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

                int selectedIndex = SelectNextCell(cellCount, ref rng);
                if (selectedIndex < 0)
                {
                    forcedFallbackCellCount = ForceResolveRemainingCellsToFallback(cellCount);
                    termination = WreckSolveTermination.SelectionFailed;
                    LogSolveFallback(termination, cellCount, collapseIterations, maxObservedPropagationIterations, contradictionCount, forcedFallbackCellCount);
                    break;
                }

                WreckGridCell selectedCell = _grid[selectedIndex];
                byte chosenModule = SelectModuleFromMask(selectedCell.PossibleModuleMask, moduleCount, ref rng);
                selectedCell.CollapsedModuleId = chosenModule;
                selectedCell.PossibleModuleMask = (ushort)(1 << chosenModule);
                selectedCell.Entropy = 0f;
                _grid[selectedIndex] = selectedCell;
                collapsedCount++;

                ClearPropagationQueue();
                _propagationQueue.Enqueue(DecodeMorton3(selectedIndex));
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

        private int SelectNextCell(int cellCount, ref XorShift32State rng)
        {
            float bestEntropy = float.MaxValue;
            int bestIndex = -1;

            for (int mortonIndex = 0; mortonIndex < cellCount; mortonIndex++)
            {
                WreckGridCell cell = _grid[mortonIndex];
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
                if (propagationIterations++ >= maxPropagationIterations)
                    return WreckSolveTermination.PropagationWatchdogTriggered;

                int currentIndex = EncodeMorton3(currentCoord.x, currentCoord.y, currentCoord.z);
                WreckGridCell currentCell = _grid[currentIndex];

                for (byte direction = 0; direction < 6; direction++)
                {
                    if (!TryGetNeighbor(currentCoord, direction, out int neighborIndex))
                        continue;

                    WreckGridCell neighborCell = _grid[neighborIndex];
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
                    _grid[neighborIndex] = neighborCell;
                    _propagationQueue.Enqueue(DecodeMorton3(neighborIndex));
                }
            }

            return WreckSolveTermination.Completed;
        }

        private int ForceResolveRemainingCellsToFallback(int cellCount)
        {
            int forcedCount = 0;
            for (int mortonIndex = 0; mortonIndex < cellCount; mortonIndex++)
            {
                WreckGridCell cell = _grid[mortonIndex];
                if (cell.CollapsedModuleId != UncollapsedModuleId)
                    continue;

                cell.CollapsedModuleId = 0;
                cell.PossibleModuleMask = 1;
                cell.Entropy = 0f;
                _grid[mortonIndex] = cell;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(
                $"[ProceduralWreckGenerator] WFC fallback engaged. reason={termination} cells={cellCount} collapseIterations={collapseIterations} propagationIterations={propagationIterations} contradictions={contradictionCount} forcedFallbackCells={forcedFallbackCellCount}",
                this);
#endif
        }

        private void BuildPlacements(int cellCount, uint seed)
        {
            _allPlacements.Clear();

            float halfSpan = (gridResolution * cellSizeMeters) * 0.5f;
            float halfCell = cellSizeMeters * 0.5f;

            for (int mortonIndex = 0; mortonIndex < cellCount; mortonIndex++)
            {
                WreckGridCell cell = _grid[mortonIndex];
                int moduleId = cell.CollapsedModuleId;
                if (moduleId < 0 || moduleId >= moduleDefinitions.Length)
                    continue;

                WreckModuleRuntimeDefinition runtimeDefinition = _runtimeDefinitions[moduleId];
                if (runtimeDefinition.EmitsGeometry == 0 && runtimeDefinition.EmitsNavProxy == 0)
                    continue;

                if (_allPlacements.Length >= _allPlacements.Capacity)
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

                _allPlacements.AddNoResize(new WreckModulePlacement
                {
                    Position = localPosition,
                    Rotation = placementRotation,
                    BoundsCenter = boundsCenter,
                    BoundsSize = boundsSize,
                    MortonIndex = mortonIndex,
                    ModuleId = (byte)moduleId,
                    DrawPriority = runtimeDefinition.DrawCallPriority
                });
            }
        }

        private Mesh BuildMergedMeshForTier(byte tier)
        {
            _filteredPlacements.Clear();
            int placementCount = _allPlacements.Length;
            for (int i = 0; i < placementCount; i++)
            {
                WreckModulePlacement placement = _allPlacements[i];
                if (placement.DrawPriority != tier)
                    continue;

                if (_filteredPlacements.Length >= _filteredPlacements.Capacity)
                    break;

                _filteredPlacements.AddNoResize(placement);
            }

            return BuildMergedMesh(_filteredPlacements);
        }

        private async Awaitable<Mesh> BuildMergedMeshForTierAsync(byte tier)
        {
            _filteredPlacements.Clear();
            int placementCount = _allPlacements.Length;
            for (int i = 0; i < placementCount; i++)
            {
                WreckModulePlacement placement = _allPlacements[i];
                if (placement.DrawPriority != tier)
                    continue;

                if (_filteredPlacements.Length >= _filteredPlacements.Capacity)
                    break;

                _filteredPlacements.AddNoResize(placement);
            }

            return await BuildMergedMeshAsync(_filteredPlacements);
        }

        private Mesh BuildMergedMesh(NativeList<WreckModulePlacement> placements)
        {
            if (!placements.IsCreated || placements.Length <= 0)
                return null;

            int placementCount = placements.Length;
            int totalVertexCount = 0;
            int totalIndexCount = 0;
            int scheduledJobCount = 0;

            for (int placementIndex = 0; placementIndex < placementCount; placementIndex++)
            {
                WreckModulePlacement placement = placements[placementIndex];
                Mesh sourceMesh = ResolveStructuralMesh(placement.ModuleId);
                if (sourceMesh == null || !ValidateMeshLayout(sourceMesh))
                    continue;

                totalVertexCount += sourceMesh.vertexCount;
                totalIndexCount += ResolveIndexCount(sourceMesh);
                scheduledJobCount++;
            }

            if (totalVertexCount <= 0 || totalIndexCount <= 0 || scheduledJobCount <= 0)
                return null;

            Mesh.MeshDataArray writableMeshData = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData meshData = writableMeshData[0];
            meshData.SetVertexBufferParams(
                totalVertexCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2));
            meshData.SetIndexBufferParams(totalIndexCount, IndexFormat.UInt32);

            NativeArray<WreckMergedVertex> destinationVertices = meshData.GetVertexData<WreckMergedVertex>();
            NativeArray<uint> destinationIndices = meshData.GetIndexData<uint>();

            int vertexOffset = 0;
            int indexOffset = 0;
            scheduledJobCount = 0;

            for (int placementIndex = 0; placementIndex < placementCount; placementIndex++)
            {
                WreckModulePlacement placement = placements[placementIndex];
                Mesh sourceMesh = ResolveStructuralMesh(placement.ModuleId);
                if (sourceMesh == null || !ValidateMeshLayout(sourceMesh))
                    continue;

                _readOnlyMeshSnapshots[scheduledJobCount] = Mesh.AcquireReadOnlyMeshData(sourceMesh);
                Mesh.MeshData sourceData = _readOnlyMeshSnapshots[scheduledJobCount][0];

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
                    SubMeshCount = subMeshCount,
                    HasNormals = sourceData.HasVertexAttribute(VertexAttribute.Normal),
                    HasUvs = sourceData.HasVertexAttribute(VertexAttribute.TexCoord0),
                    LocalToWreck = float4x4.TRS(placement.Position, placement.Rotation, new float3(1f)),
                    Rotation = placement.Rotation
                };

                job.Execute();
                scheduledJobCount++;

                vertexOffset += sourceMesh.vertexCount;
                indexOffset += ResolveIndexCount(sourceMesh);
            }

            for (int i = 0; i < scheduledJobCount; i++)
                _readOnlyMeshSnapshots[i].Dispose();

            Bounds localBounds = CalculateLocalBounds(placements);
            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, totalIndexCount, MeshTopology.Triangles)
            {
                bounds = localBounds,
                vertexCount = totalVertexCount
            }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontValidateIndices);

            Mesh result = new Mesh();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            result.name = GeneratedMergedMeshName;
#endif
            Mesh.ApplyAndDisposeWritableMeshData(
                writableMeshData,
                result,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontValidateIndices);
            result.bounds = localBounds;
            return result;
        }

        private async Awaitable<Mesh> BuildMergedMeshAsync(NativeList<WreckModulePlacement> placements)
        {
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
                WreckModulePlacement placement = placements[placementIndex];
                Mesh sourceMesh = ResolveStructuralMesh(placement.ModuleId);
                if (sourceMesh == null || !ValidateMeshLayout(sourceMesh))
                    continue;

                totalVertexCount += sourceMesh.vertexCount;
                totalIndexCount += ResolveIndexCount(sourceMesh);
                mergedPlacementCount++;

                sliceWorkCount++;
                if (!ShouldYieldMeshBuildSlice(sliceWorkCount, sliceStartTime))
                    continue;

                if (!await YieldMeshBuildFrameAsync("merged mesh source scan", meshYieldFrames++))
                    return null;

                sliceStartTime = Time.realtimeSinceStartupAsDouble;
                sliceWorkCount = 0;
            }

            if (totalVertexCount <= 0 || totalIndexCount <= 0 || mergedPlacementCount <= 0)
                return null;

            Mesh.MeshDataArray writableMeshData = Mesh.AllocateWritableMeshData(1);
            bool meshApplied = false;
            int acquiredSnapshotCount = 0;

            try
            {
                sliceStartTime = Time.realtimeSinceStartupAsDouble;
                sliceWorkCount = 0;

                Mesh.MeshData meshData = writableMeshData[0];
                meshData.SetVertexBufferParams(
                    totalVertexCount,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2));
                meshData.SetIndexBufferParams(totalIndexCount, IndexFormat.UInt32);

                NativeArray<WreckMergedVertex> destinationVertices = meshData.GetVertexData<WreckMergedVertex>();
                NativeArray<uint> destinationIndices = meshData.GetIndexData<uint>();

                int vertexOffset = 0;
                int indexOffset = 0;
                int scheduledJobCount = 0;

                for (int placementIndex = 0; placementIndex < placementCount; placementIndex++)
                {
                    WreckModulePlacement placement = placements[placementIndex];
                    Mesh sourceMesh = ResolveStructuralMesh(placement.ModuleId);
                    if (sourceMesh == null || !ValidateMeshLayout(sourceMesh))
                        continue;

                    _readOnlyMeshSnapshots[acquiredSnapshotCount] = Mesh.AcquireReadOnlyMeshData(sourceMesh);
                    Mesh.MeshData sourceData = _readOnlyMeshSnapshots[acquiredSnapshotCount][0];
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
                        SubMeshCount = sourceData.subMeshCount,
                        HasNormals = sourceData.HasVertexAttribute(VertexAttribute.Normal),
                        HasUvs = sourceData.HasVertexAttribute(VertexAttribute.TexCoord0),
                        LocalToWreck = float4x4.TRS(placement.Position, placement.Rotation, new float3(1f)),
                        Rotation = placement.Rotation
                    };

                    _copyHandles[scheduledJobCount] = job.Schedule();
                    scheduledJobCount++;

                    vertexOffset += sourceMesh.vertexCount;
                    indexOffset += ResolveIndexCount(sourceMesh);

                    sliceWorkCount++;
                    if (ShouldYieldMeshBuildSlice(sliceWorkCount, sliceStartTime))
                    {
                        if (!await YieldMeshBuildFrameAsync("merged mesh copy scheduling", meshYieldFrames++))
                            return null;

                        sliceStartTime = Time.realtimeSinceStartupAsDouble;
                        sliceWorkCount = 0;
                    }
                }

                if (scheduledJobCount > 0)
                {
                    JobHandle dependency = CombineScheduledCopyHandles(scheduledJobCount);
                    JobHandle.ScheduleBatchedJobs();
                    if (!await WaitForJobHandleAsync(dependency, "merged mesh copy"))
                        return null;

                    for (int snapshotIndex = 0; snapshotIndex < acquiredSnapshotCount; snapshotIndex++)
                    {
                        _readOnlyMeshSnapshots[snapshotIndex].Dispose();
                        _readOnlyMeshSnapshots[snapshotIndex] = default;
                    }

                    if (!await YieldMeshBuildFrameAsync("merged mesh post-copy", meshYieldFrames++))
                        return null;

                    sliceStartTime = Time.realtimeSinceStartupAsDouble;
                    sliceWorkCount = 0;
                }

                Bounds localBounds = CalculateLocalBounds(placements);
                meshData.subMeshCount = 1;
                meshData.SetSubMesh(0, new SubMeshDescriptor(0, totalIndexCount, MeshTopology.Triangles)
                {
                    bounds = localBounds,
                    vertexCount = totalVertexCount
                }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontValidateIndices);

                if (!await YieldMeshBuildFrameAsync("merged mesh apply", meshYieldFrames++))
                    return null;

                Mesh result = new Mesh();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                result.name = GeneratedMergedMeshName;
#endif
                Mesh.ApplyAndDisposeWritableMeshData(
                    writableMeshData,
                    result,
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontValidateIndices);
                meshApplied = true;
                result.bounds = localBounds;
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
            }
        }

        private Mesh BuildProxyMesh()
        {
            _filteredPlacements.Clear();
            int placementCount = _allPlacements.Length;
            for (int i = 0; i < placementCount; i++)
            {
                WreckModulePlacement placement = _allPlacements[i];
                if (_runtimeDefinitions[placement.ModuleId].EmitsNavProxy == 0)
                    continue;

                if (_filteredPlacements.Length >= _filteredPlacements.Capacity)
                    break;

                _filteredPlacements.AddNoResize(placement);
            }

            if (_filteredPlacements.Length <= 0)
                return null;

            int proxyPlacementCount = _filteredPlacements.Length;
            int totalVertexCount = proxyPlacementCount * ProxyVerticesPerPlacement;
            int totalIndexCount = proxyPlacementCount * ProxyIndicesPerPlacement;

            Mesh.MeshDataArray writableMeshData = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData meshData = writableMeshData[0];
            meshData.SetVertexBufferParams(
                totalVertexCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3));
            meshData.SetIndexBufferParams(totalIndexCount, IndexFormat.UInt32);

            NativeArray<float3> positions = meshData.GetVertexData<float3>();
            NativeArray<uint> indices = meshData.GetIndexData<uint>();

            BuildProxyMeshJob job = new BuildProxyMeshJob
            {
                Placements = _filteredPlacements.AsArray(),
                Positions = positions,
                Indices = indices
            };

            for (int index = 0; index < proxyPlacementCount; index++)
                job.Execute(index);

            Bounds localBounds = CalculateLocalBounds(_filteredPlacements);
            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, totalIndexCount, MeshTopology.Triangles)
            {
                bounds = localBounds,
                vertexCount = totalVertexCount
            }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontValidateIndices);

            Mesh result = new Mesh();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            result.name = GeneratedProxyMeshName;
#endif
            Mesh.ApplyAndDisposeWritableMeshData(
                writableMeshData,
                result,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontValidateIndices);
            result.bounds = localBounds;
            return result;
        }

        private static void PublishWreckSolveBudgetWarningIfNeeded(double elapsedMilliseconds)
        {
            if (!Application.isPlaying || elapsedMilliseconds < WreckSolveTelemetryThresholdMs)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                WreckSolveBudgetWarningHash,
                WreckBrgContextHash,
                (float)elapsedMilliseconds);
        }

        private async Awaitable<Mesh> BuildProxyMeshAsync()
        {
            _filteredPlacements.Clear();
            int placementCount = _allPlacements.Length;
            for (int i = 0; i < placementCount; i++)
            {
                WreckModulePlacement placement = _allPlacements[i];
                if (_runtimeDefinitions[placement.ModuleId].EmitsNavProxy == 0)
                    continue;

                if (_filteredPlacements.Length >= _filteredPlacements.Capacity)
                    break;

                _filteredPlacements.AddNoResize(placement);
            }

            if (_filteredPlacements.Length <= 0)
                return null;

            int proxyPlacementCount = _filteredPlacements.Length;
            int totalVertexCount = proxyPlacementCount * ProxyVerticesPerPlacement;
            int totalIndexCount = proxyPlacementCount * ProxyIndicesPerPlacement;

            Mesh.MeshDataArray writableMeshData = Mesh.AllocateWritableMeshData(1);
            bool meshApplied = false;

            try
            {
                Mesh.MeshData meshData = writableMeshData[0];
                meshData.SetVertexBufferParams(
                    totalVertexCount,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3));
                meshData.SetIndexBufferParams(totalIndexCount, IndexFormat.UInt32);

                NativeArray<float3> positions = meshData.GetVertexData<float3>();
                NativeArray<uint> indices = meshData.GetIndexData<uint>();

                BuildProxyMeshJob job = new BuildProxyMeshJob
                {
                    Placements = _filteredPlacements.AsArray(),
                    Positions = positions,
                    Indices = indices
                };

                JobHandle handle = job.Schedule(proxyPlacementCount, 32);
                JobHandle.ScheduleBatchedJobs();
                if (!await WaitForJobHandleAsync(handle, "proxy mesh build"))
                    return null;

                Bounds localBounds = CalculateLocalBounds(_filteredPlacements);
                meshData.subMeshCount = 1;
                meshData.SetSubMesh(0, new SubMeshDescriptor(0, totalIndexCount, MeshTopology.Triangles)
                {
                    bounds = localBounds,
                    vertexCount = totalVertexCount
                }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontValidateIndices);

                Mesh result = new Mesh();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                result.name = GeneratedProxyMeshName;
#endif
                Mesh.ApplyAndDisposeWritableMeshData(
                    writableMeshData,
                    result,
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontValidateIndices);
                meshApplied = true;
                result.bounds = localBounds;
                return result;
            }
            finally
            {
                if (!meshApplied)
                    writableMeshData.Dispose();
            }
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

        private Mesh ResolveNavigationProxyMesh()
        {
            if (!buildAsyncNavigationBake)
                return null;

            return wreckCollisionProxyMesh != null
                ? wreckCollisionProxyMesh
                : BuildProxyMesh();
        }

        private async Awaitable<Mesh> ResolveNavigationProxyMeshAsync()
        {
            if (!buildAsyncNavigationBake)
                return null;

            return wreckCollisionProxyMesh != null
                ? wreckCollisionProxyMesh
                : await BuildProxyMeshAsync();
        }

        private Mesh ResolveStructuralMesh(int moduleId)
        {
            if (moduleDefinitions == null || moduleId < 0 || moduleId >= moduleDefinitions.Length)
                return null;

            return moduleDefinitions[moduleId].StructuralMesh;
        }

        private bool ValidateMeshLayout(Mesh sourceMesh)
        {
            using Mesh.MeshDataArray meshDataArray = Mesh.AcquireReadOnlyMeshData(sourceMesh);
            Mesh.MeshData sourceData = meshDataArray[0];

            if (!ValidateAttributeLayout(sourceData, VertexAttribute.Position, UnsafeUtility.SizeOf<Vector3>()))
                return false;

            if (sourceData.HasVertexAttribute(VertexAttribute.Normal) &&
                !ValidateAttributeLayout(sourceData, VertexAttribute.Normal, UnsafeUtility.SizeOf<Vector3>()))
            {
                return false;
            }

            if (sourceData.HasVertexAttribute(VertexAttribute.TexCoord0) &&
                !ValidateAttributeLayout(sourceData, VertexAttribute.TexCoord0, UnsafeUtility.SizeOf<Vector2>()))
            {
                return false;
            }

            return true;
        }

        private static bool ValidateAttributeLayout(Mesh.MeshData sourceData, VertexAttribute attribute, int expectedStride)
        {
            if (!sourceData.HasVertexAttribute(attribute))
                return false;

            int stream = sourceData.GetVertexAttributeStream(attribute);
            if (stream < 0)
                return false;

            return sourceData.GetVertexAttributeOffset(attribute) == 0 &&
                   sourceData.GetVertexBufferStride(stream) == expectedStride;
        }

        private static int ResolveIndexCount(Mesh sourceMesh)
        {
            int total = 0;
            int subMeshCount = sourceMesh.subMeshCount;
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
                total += (int)sourceMesh.GetIndexCount(subMeshIndex);

            return total;
        }

        private Bounds CalculateLocalBounds(NativeList<WreckModulePlacement> placements)
        {
            if (!placements.IsCreated || placements.Length <= 0)
                return CreateFallbackLocalBounds();

            Bounds bounds = default;
            bool initialized = false;
            int count = placements.Length;
            for (int i = 0; i < count; i++)
            {
                if (!TryResolvePlacementBounds(placements[i], out Bounds placementBounds))
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[ProceduralWreckGenerator] Propagation queue clear watchdog triggered at {maxIterations} iterations.");
#endif
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
            WreckModuleRuntimeDefinition definition = _runtimeDefinitions[moduleIndex];
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
            int boundary = gridResolution - 1;

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
            if (next.x < 0 || next.y < 0 || next.z < 0 ||
                next.x >= gridResolution || next.y >= gridResolution || next.z >= gridResolution)
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
            public bool UseCapsule;
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
            Debug.Log($"[HectonCompoundColliderAutoFitter] Root={root.name} MeshCollidersRemoved={meshColliders.Length} PrimitiveColliders={fittedCount}", root);
#endif
            return fittedCount;
        }

        private static bool TryFitSubMesh(Mesh mesh, int subMeshIndex, Matrix4x4 sourceToRoot, out OrientedColliderFit fit)
        {
            fit = default;
            // COLD ALLOC: List<Vector3>[mesh.vertexCount] - editor collider fitting source vertices - owner: ProceduralWreckGenerator
            List<Vector3> vertices = new List<Vector3>(mesh.vertexCount);
            mesh.GetVertices(vertices);
            int[] indices = mesh.GetIndices(subMeshIndex);
            if (vertices.Count <= 0 || indices == null || indices.Length <= 0)
                return false;

            Vector3 centroid = Vector3.zero;
            Vector3 aabbMin = default;
            Vector3 aabbMax = default;
            bool hasPoint = false;
            int pointCount = 0;
            for (int i = 0; i < indices.Length; i++)
            {
                int vertexIndex = indices[i];
                if ((uint)vertexIndex >= (uint)vertices.Count)
                    continue;

                Vector3 point = sourceToRoot.MultiplyPoint3x4(vertices[vertexIndex]);
                if (!IsFinite(point))
                    continue;

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
            for (int i = 0; i < indices.Length; i++)
            {
                int vertexIndex = indices[i];
                if ((uint)vertexIndex >= (uint)vertices.Count)
                    continue;

                Vector3 point = sourceToRoot.MultiplyPoint3x4(vertices[vertexIndex]);
                if (!IsFinite(point))
                    continue;

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
            for (int i = 0; i < indices.Length; i++)
            {
                int vertexIndex = indices[i];
                if ((uint)vertexIndex >= (uint)vertices.Count)
                    continue;

                Vector3 point = sourceToRoot.MultiplyPoint3x4(vertices[vertexIndex]);
                if (!IsFinite(point))
                    continue;

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
            ResolveCapsuleFit(size, out fit.UseCapsule, out fit.CapsuleDirection, out fit.CapsuleRadius, out fit.CapsuleHeight);
            return true;
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

            if (fit.UseCapsule)
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
