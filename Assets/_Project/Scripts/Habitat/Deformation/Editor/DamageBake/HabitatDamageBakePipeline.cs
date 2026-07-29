#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Hecton8.Habitat.Deformation.Contracts;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Hecton8.Habitat.Deformation.Editor
{
    internal static class HabitatDamageBakeConstants
    {
        public const string OutputFolder = "Assets/_Project/BakedGeometry/HabitatDamage";
        public const string BakeReportPath = "Docs/Reports/HABITAT_BAKE_REPORT.json";
        public const string ScannerReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";
        public const string PreviousScannerReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_PREVIOUS_SHINOBU_210.json";
        public const string BlackboxDumpPath = "Docs/AgentLogs/Dump_SHINOBU_210.bin";
        public const string ProfileCsvPath = "Docs/Data/habitat_crush_profiles.csv";
        public const double DefaultSeaLevelAupY = 14.02d;
        public const int ComplexityCriticalTriangleBudget = 20000;
        public const int MaxHullCount = 8;
        public const int MaxEditorProfileNameBytes = 64;
        public const uint AgentHash = 0x53323130u; // S210
        public const int BlackboxDumpVersion = 1;
    }

    internal static class HabitatDamageBakeMath
    {
        private const float LengthEpsilon = 0.00000001f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SafeLength(float2 value)
        {
            float lengthSq = math.dot(value, value);
            return lengthSq * math.rsqrt(math.max(lengthSq, LengthEpsilon));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SafeLength(float3 value)
        {
            float lengthSq = math.dot(value, value);
            return lengthSq * math.rsqrt(math.max(lengthSq, LengthEpsilon));
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct HabitatCrushProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float CrushIntensity;
        [FieldOffset(8)] public float TearThreshold;
        [FieldOffset(12)] public float MaterialYieldStrength;
        [FieldOffset(16)] public float StressColorIntensity;
        [FieldOffset(20)] public float GlobalQualityWeight;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct HabitatDamageBakedVertex
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public ushort NormalX;
        [FieldOffset(14)] public ushort NormalY;
        [FieldOffset(16)] public ushort NormalZ;
        [FieldOffset(18)] public ushort NormalW;
        [FieldOffset(20)] public uint TangentSnorm;
        [FieldOffset(24)] public ushort UvX;
        [FieldOffset(26)] public ushort UvY;
        [FieldOffset(28)] public uint ColorRgba;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct HabitatDamageSourceVertex
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public float4 Tangent;
        [FieldOffset(40)] public float2 Uv0;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct HabitatDamageWorkingVertex
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public float4 Tangent;
        [FieldOffset(40)] public float2 Uv0;
        [FieldOffset(48)] public float3 OriginalPosition;
        [FieldOffset(60)] public float Stress01;
        [FieldOffset(64)] public float Tear01;
        [FieldOffset(68)] public uint Flags;
        [FieldOffset(72)] public ulong _pad0;
        [FieldOffset(80)] public ulong _pad1;
        [FieldOffset(88)] public ulong _pad2;
        [FieldOffset(96)] public ulong _pad3;
        [FieldOffset(104)] public ulong _pad4;
        [FieldOffset(112)] public ulong _pad5;
        [FieldOffset(120)] public ulong _pad6;
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    internal struct HabitatDamageBakeSettings
    {
        [FieldOffset(0)] public double3 ModuleAup;
        [FieldOffset(24)] public double3 SeaLevelAup;
        [FieldOffset(48)] public float CrushIntensity;
        [FieldOffset(52)] public float TearThreshold;
        [FieldOffset(56)] public float MaterialYieldStrength;
        [FieldOffset(60)] public float StressColorIntensity;
        [FieldOffset(64)] public float GlobalQualityWeight;
        [FieldOffset(68)] public uint Flags;
        [FieldOffset(72)] public uint _pad0;
        [FieldOffset(76)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct HabitatDamageIndexRangeDTO
    {
        [FieldOffset(0)] public int SourceStart;
        [FieldOffset(4)] public int DestinationStart;
        [FieldOffset(8)] public int Count;
        [FieldOffset(12)] public int BaseVertex;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct ExtractSourceVertexJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<byte> PositionBytes;
        [ReadOnly] public NativeArray<byte> NormalBytes;
        [ReadOnly] public NativeArray<byte> TangentBytes;
        [ReadOnly] public NativeArray<byte> UvBytes;
        [WriteOnly] [NoAlias] public NativeArray<HabitatDamageSourceVertex> Output;
        public int PositionOffset;
        public int PositionStride;
        public int NormalOffset;
        public int NormalStride;
        public int TangentOffset;
        public int TangentStride;
        public int UvOffset;
        public int UvStride;
        public int HasNormal;
        public int HasTangent;
        public int HasUv;

        public void Execute(int index)
        {
            HabitatDamageSourceVertex vertex = default;
            vertex.Position = ReadFloat3(PositionBytes, PositionOffset, PositionStride, index, float3.zero);
            vertex.Normal = HasNormal != 0
                ? math.normalizesafe(ReadFloat3(NormalBytes, NormalOffset, NormalStride, index, new float3(0f, 1f, 0f)), new float3(0f, 1f, 0f))
                : new float3(0f, 1f, 0f);
            vertex.Tangent = HasTangent != 0
                ? ReadFloat4(TangentBytes, TangentOffset, TangentStride, index, new float4(1f, 0f, 0f, 1f))
                : new float4(1f, 0f, 0f, 1f);
            vertex.Uv0 = HasUv != 0 ? ReadFloat2(UvBytes, UvOffset, UvStride, index, float2.zero) : float2.zero;
            if (!math.all(math.isfinite(vertex.Position)))
                vertex.Position = float3.zero;
            if (!math.all(math.isfinite(vertex.Normal)))
                vertex.Normal = new float3(0f, 1f, 0f);
            if (!math.all(math.isfinite(vertex.Tangent)))
                vertex.Tangent = new float4(1f, 0f, 0f, 1f);
            if (!math.all(math.isfinite(vertex.Uv0)))
                vertex.Uv0 = float2.zero;
            Output[index] = vertex;
        }

        private static float3 ReadFloat3(NativeArray<byte> bytes, int offset, int stride, int index, float3 fallback)
        {
            if (!bytes.IsCreated || stride <= 0)
                return fallback;

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes) + offset;
            return UnsafeUtility.ReadArrayElementWithStride<float3>(ptr, index, stride);
        }

        private static float4 ReadFloat4(NativeArray<byte> bytes, int offset, int stride, int index, float4 fallback)
        {
            if (!bytes.IsCreated || stride <= 0)
                return fallback;

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes) + offset;
            return UnsafeUtility.ReadArrayElementWithStride<float4>(ptr, index, stride);
        }

        private static float2 ReadFloat2(NativeArray<byte> bytes, int offset, int stride, int index, float2 fallback)
        {
            if (!bytes.IsCreated || stride <= 0)
                return fallback;

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes) + offset;
            return UnsafeUtility.ReadArrayElementWithStride<float2>(ptr, index, stride);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct CopyIndex16Job : IJobParallelFor
    {
        [ReadOnly] [NoAlias] public NativeArray<ushort> Source;
        [ReadOnly] [NoAlias] public NativeArray<HabitatDamageIndexRangeDTO> Ranges;
        [WriteOnly] [NoAlias] public NativeArray<uint> Output;
        public int RangeCount;
        public int SourceVertexCount;

        public void Execute(int index)
        {
            for (int i = 0; i < RangeCount; i++)
            {
                HabitatDamageIndexRangeDTO range = Ranges[i];
                int local = index - range.DestinationStart;
                if ((uint)local >= (uint)range.Count)
                    continue;

                int sourceIndex = range.SourceStart + local;
                if ((uint)sourceIndex >= (uint)Source.Length)
                    break;
                int adjusted = Source[sourceIndex] + range.BaseVertex;
                Output[index] = (uint)math.clamp(adjusted, 0, math.max(0, SourceVertexCount - 1));
                return;
            }

            Output[index] = 0u;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct CopyIndex32Job : IJobParallelFor
    {
        [ReadOnly] [NoAlias] public NativeArray<uint> Source;
        [ReadOnly] [NoAlias] public NativeArray<HabitatDamageIndexRangeDTO> Ranges;
        [WriteOnly] [NoAlias] public NativeArray<uint> Output;
        public int RangeCount;
        public int SourceVertexCount;

        public void Execute(int index)
        {
            for (int i = 0; i < RangeCount; i++)
            {
                HabitatDamageIndexRangeDTO range = Ranges[i];
                int local = index - range.DestinationStart;
                if ((uint)local >= (uint)range.Count)
                    continue;

                int sourceIndex = range.SourceStart + local;
                if ((uint)sourceIndex >= (uint)Source.Length)
                    break;
                long adjusted = (long)Source[sourceIndex] + range.BaseVertex;
                long maxIndex = math.max(0, SourceVertexCount - 1);
                if (adjusted < 0L)
                    adjusted = 0L;
                if (adjusted > maxIndex)
                    adjusted = maxIndex;
                Output[index] = (uint)adjusted;
                return;
            }

            Output[index] = 0u;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct InitializeDamageWorkingVerticesJob : IJobParallelFor
    {
        [ReadOnly] [NoAlias] public NativeArray<HabitatDamageSourceVertex> Source;
        [WriteOnly] [NoAlias] public NativeArray<HabitatDamageWorkingVertex> Output;
        public int SourceCount;

        public void Execute(int index)
        {
            int sourceIndex = index % SourceCount;
            HabitatDamageSourceVertex source = Source[sourceIndex];
            Output[index] = new HabitatDamageWorkingVertex
            {
                Position = source.Position,
                Normal = source.Normal,
                Tangent = source.Tangent,
                Uv0 = source.Uv0,
                OriginalPosition = source.Position,
                Stress01 = 0f,
                Tear01 = 0f,
                Flags = 0u
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct GenerateMockHydrostaticPressureJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<HabitatDamageWorkingVertex> Vertices;
        public int RadialSegments;
        public int LengthSegments;
        public float RadiusMeters;
        public float LengthMeters;
        public float PressureIntensity;

        public void Execute(int index)
        {
            int radialCount = math.max(3, RadialSegments);
            int lengthCount = math.max(1, LengthSegments);
            int ring = index / radialCount;
            int segment = index - ring * radialCount;
            float lengthDenominator = math.max(1f, lengthCount - 1f);
            float angle = segment * (math.PI * 2f / radialCount);
            float z = ((ring / lengthDenominator) - 0.5f) * LengthMeters;
            float2 radial = new float2(
                Hecton8.Core.MathLodApproximation.ApproxCosBhaskara(angle),
                Hecton8.Core.MathLodApproximation.ApproxSinBhaskara(angle));
            float rib = Hecton8.Core.MathLodApproximation.ApproxSinBhaskara(z * 3.7f + radial.x * 2.1f) * 0.5f + 0.5f;
            float crush = math.saturate(PressureIntensity) * math.lerp(0.04f, 0.28f, rib);
            float radius = math.max(0.01f, RadiusMeters * (1f - crush));
            float3 position = new float3(radial.x * radius, radial.y * radius, z);
            float3 normal = math.normalizesafe(new float3(radial.x, radial.y, 0f), new float3(1f, 0f, 0f));

            HabitatDamageWorkingVertex* ptr = (HabitatDamageWorkingVertex*)NativeArrayUnsafeUtility.GetUnsafePtr(Vertices);
            ref HabitatDamageWorkingVertex vertex = ref UnsafeUtility.AsRef<HabitatDamageWorkingVertex>(ptr + index);
            vertex.Position = position;
            vertex.Normal = normal;
            vertex.Tangent = new float4(-radial.y, radial.x, 0f, 1f);
            vertex.Uv0 = new float2(segment / (float)radialCount, ring / lengthDenominator);
            vertex.OriginalPosition = new float3(radial.x * RadiusMeters, radial.y * RadiusMeters, z);
            vertex.Stress01 = crush;
            vertex.Tear01 = 0f;
            vertex.Flags = 1u;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct ApplyHydrostaticBucklingJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<HabitatDamageWorkingVertex> Vertices;
        public float PressureIntensity;
        public float MaterialYieldStrength;
        public float DepthMeters;
        public float DamageStage01;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            HabitatDamageWorkingVertex* ptr = (HabitatDamageWorkingVertex*)NativeArrayUnsafeUtility.GetUnsafePtr(Vertices);
            ref HabitatDamageWorkingVertex vertex = ref UnsafeUtility.AsRef<HabitatDamageWorkingVertex>(ptr + index);
            float3 original = vertex.OriginalPosition;
            float3 position = vertex.Position;
            float2 radial = new float2(position.x, position.y);
            float radialLength = math.max(0.0001f, HabitatDamageBakeMath.SafeLength(radial));
            float2 radialDirection = radial * math.rcp(radialLength);
            float pressure = math.saturate(PressureIntensity);
            float stage = math.saturate(DamageStage01);
            float quality = math.saturate(GlobalQualityWeight);
            float yieldStrength = math.max(0.01f, math.isfinite(MaterialYieldStrength) ? MaterialYieldStrength : 1f);
            float depthWave = Hecton8.Core.MathLodApproximation.ApproxSinBhaskara((float)DepthMeters * 0.0073f + original.y * 1.618f + original.x * 0.271f);
            float ribWave = Hecton8.Core.MathLodApproximation.ApproxSinBhaskara(original.z * 2.37f + original.x * 0.41f);
            float buckleMask = (depthWave * 0.5f + 0.5f) * (ribWave * 0.5f + 0.5f);
            float inward = pressure * stage * math.rcp(yieldStrength) * math.lerp(0.035f, 0.42f, buckleMask);
            inward *= math.lerp(0.65f, 1.35f, quality);
            position.x -= radialDirection.x * inward;
            position.y -= radialDirection.y * inward;
            position.z += Hecton8.Core.MathLodApproximation.ApproxSinBhaskara(original.x * 0.91f + original.z * 0.57f) * pressure * stage * math.lerp(0.015f, 0.045f, quality);

            if (!math.all(math.isfinite(position)))
                position = original;

            vertex.Position = position;
            vertex.Stress01 = math.saturate(HabitatDamageBakeMath.SafeLength(position - original) * math.lerp(3.5f, 9f, stage));
            vertex.Flags |= 2u;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct ApplyStructuralTearJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<HabitatDamageWorkingVertex> Vertices;
        public int SourceVertexCount;
        public float TearThreshold;
        public float TearIntensity;
        public float DamageStage01;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if (SourceVertexCount <= 0)
                return;

            HabitatDamageWorkingVertex* ptr = (HabitatDamageWorkingVertex*)NativeArrayUnsafeUtility.GetUnsafePtr(Vertices);
            ref HabitatDamageWorkingVertex vertex = ref UnsafeUtility.AsRef<HabitatDamageWorkingVertex>(ptr + index);
            float3 original = vertex.OriginalPosition;
            float seamA = math.abs(Hecton8.Core.MathLodApproximation.ApproxSinBhaskara(original.x * 0.73f + original.y * 1.91f));
            float seamB = math.abs(Hecton8.Core.MathLodApproximation.ApproxSinBhaskara(original.z * 0.61f - original.y * 1.17f));
            float seam = math.min(seamA, seamB);
            float threshold = math.saturate(TearThreshold);
            float stage = math.saturate(DamageStage01);
            float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f);
            float qualityCurve = math.smoothstep(0f, 1f, quality);
            float seamSharpness = math.lerp(2f, 7.5f, qualityCurve);
            float tear = math.saturate((threshold - seam) * seamSharpness) * math.saturate(TearIntensity) * stage;

            int duplicateSide = index >= SourceVertexCount ? 1 : -1;
            float3 sideAxis = math.normalizesafe(math.cross(vertex.Normal, new float3(0f, 1f, 0f)), new float3(1f, 0f, 0f));
            float maxGap = math.lerp(0.08f, 0.42f, qualityCurve);
            float gap = math.lerp(0.015f, maxGap, tear);
            float active = math.step(0.000001f, tear);
            vertex.Position += sideAxis * gap * duplicateSide * active;
            vertex.Tear01 = math.max(vertex.Tear01, tear * active);
            vertex.Stress01 = math.saturate(math.max(vertex.Stress01, tear * active));
            vertex.Flags |= math.select(0u, 4u, active > 0f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct BuildDamageIndexJob : IJobParallelFor
    {
        [ReadOnly] [NoAlias] public NativeArray<uint> SourceIndices;
        [ReadOnly] [NoAlias] public NativeArray<HabitatDamageWorkingVertex> Vertices;
        [WriteOnly] [NoAlias] public NativeArray<uint> OutputIndices;
        public int SourceVertexCount;
        public int TriangleCount;
        public int DamageState;
        public float TearThreshold;
        public float PressureIntensity;
        public float GlobalQualityWeight;

        public void Execute(int triangle)
        {
            if ((uint)triangle >= (uint)TriangleCount)
                return;

            int src = triangle * 3;
            uint i0 = SourceIndices[src];
            uint i1 = SourceIndices[src + 1];
            uint i2 = SourceIndices[src + 2];
            if (i0 >= SourceVertexCount || i1 >= SourceVertexCount || i2 >= SourceVertexCount)
            {
                OutputIndices[src] = 0u;
                OutputIndices[src + 1] = 0u;
                OutputIndices[src + 2] = 0u;
                return;
            }

            float3 center = (Vertices[(int)i0].OriginalPosition + Vertices[(int)i1].OriginalPosition + Vertices[(int)i2].OriginalPosition) * (1f / 3f);
            float seam = math.min(
                math.abs(Hecton8.Core.MathLodApproximation.ApproxSinBhaskara(center.x * 0.73f + center.y * 1.91f)),
                math.abs(Hecton8.Core.MathLodApproximation.ApproxSinBhaskara(center.z * 0.61f - center.y * 1.17f)));
            float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f);
            float qualityCurve = math.smoothstep(0f, 1f, quality);
            float pressureGate = math.lerp(0.72f, 0.48f, qualityCurve);
            float seamGate = math.max(0.005f, TearThreshold * math.lerp(0.08f, 0.25f, qualityCurve));
            bool breachHole = DamageState >= 2 &&
                PressureIntensity > pressureGate &&
                seam < seamGate;
            if (breachHole)
            {
                OutputIndices[src] = 0u;
                OutputIndices[src + 1] = 0u;
                OutputIndices[src + 2] = 0u;
                return;
            }

            uint duplicateOffset = DamageState >= 2 && SourceVertexCount > 0 && (center.x + center.z) >= 0f
                ? (uint)SourceVertexCount
                : 0u;
            OutputIndices[src] = i0 + duplicateOffset;
            OutputIndices[src + 1] = i1 + duplicateOffset;
            OutputIndices[src + 2] = i2 + duplicateOffset;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct RecalculateDeformedNormalsJob : IJob
    {
        [NoAlias] public NativeArray<HabitatDamageWorkingVertex> Vertices;
        [ReadOnly] [NoAlias] public NativeArray<uint> Indices;
        public int TriangleCount;

        public void Execute()
        {
            HabitatDamageWorkingVertex* vertices = (HabitatDamageWorkingVertex*)NativeArrayUnsafeUtility.GetUnsafePtr(Vertices);
            for (int i = 0; i < Vertices.Length; i++)
            {
                ref HabitatDamageWorkingVertex vertex = ref UnsafeUtility.AsRef<HabitatDamageWorkingVertex>(vertices + i);
                vertex.Normal = float3.zero;
            }

            for (int tri = 0; tri < TriangleCount; tri++)
            {
                int offset = tri * 3;
                int i0 = (int)Indices[offset];
                int i1 = (int)Indices[offset + 1];
                int i2 = (int)Indices[offset + 2];
                if ((uint)i0 >= (uint)Vertices.Length || (uint)i1 >= (uint)Vertices.Length || (uint)i2 >= (uint)Vertices.Length)
                    continue;
                if (i0 == i1 || i1 == i2 || i0 == i2)
                    continue;

                float3 p0 = vertices[i0].Position;
                float3 p1 = vertices[i1].Position;
                float3 p2 = vertices[i2].Position;
                float3 normal = math.cross(p1 - p0, p2 - p0);
                if (!math.all(math.isfinite(normal)) || math.lengthsq(normal) <= 0.00000001f)
                    continue;

                vertices[i0].Normal += normal;
                vertices[i1].Normal += normal;
                vertices[i2].Normal += normal;
            }

            for (int i = 0; i < Vertices.Length; i++)
            {
                ref HabitatDamageWorkingVertex vertex = ref UnsafeUtility.AsRef<HabitatDamageWorkingVertex>(vertices + i);
                float3 normal = math.normalizesafe(vertex.Normal, new float3(0f, 1f, 0f));
                float3 tangent = math.normalizesafe(math.cross(new float3(0f, 1f, 0f), normal), new float3(1f, 0f, 0f));
                if (math.lengthsq(tangent) < 0.0001f)
                    tangent = math.normalizesafe(math.cross(new float3(0f, 0f, 1f), normal), new float3(1f, 0f, 0f));
                vertex.Normal = normal;
                vertex.Tangent = new float4(tangent, 1f);
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct BakeStressColorsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<HabitatDamageWorkingVertex> Vertices;
        public float StressColorIntensity;

        public void Execute(int index)
        {
            HabitatDamageWorkingVertex* ptr = (HabitatDamageWorkingVertex*)NativeArrayUnsafeUtility.GetUnsafePtr(Vertices);
            ref HabitatDamageWorkingVertex vertex = ref UnsafeUtility.AsRef<HabitatDamageWorkingVertex>(ptr + index);
            float displacement = HabitatDamageBakeMath.SafeLength(vertex.Position - vertex.OriginalPosition);
            float stress = math.saturate((displacement * 8f + vertex.Tear01) * math.max(0f, StressColorIntensity));
            vertex.Stress01 = math.max(vertex.Stress01, stress);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct GenerateSimplifiedHullsJob : IJob
    {
        [ReadOnly] [NoAlias] public NativeArray<HabitatDamageWorkingVertex> Vertices;
        [NoAlias] public NativeArray<HabitatDamageHullDTO> Hulls;
        public uint ModuleHash;
        public byte State;
        public float PressureIntensity;
        public float GlobalQualityWeight;

        public void Execute()
        {
            if (!Hulls.IsCreated || Hulls.Length == 0)
                return;

            for (int i = 0; i < Hulls.Length; i++)
                Hulls[i] = default;

            if (!Vertices.IsCreated || Vertices.Length == 0)
                return;

            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);
            int finiteVertexCount = 0;
            for (int i = 0; i < Vertices.Length; i++)
            {
                float3 p = Vertices[i].Position;
                if (!math.all(math.isfinite(p)))
                    continue;
                min = math.min(min, p);
                max = math.max(max, p);
                finiteVertexCount++;
            }

            if (finiteVertexCount == 0 || !math.all(math.isfinite(min)) || !math.all(math.isfinite(max)))
                return;

            float3 center = (min + max) * 0.5f;
            float3 size = math.max(max - min, new float3(0.05f));
            float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f);
            float qualityCurve = math.smoothstep(0f, 1f, quality);
            int count = math.clamp((int)math.round(math.lerp(1f, 3f, qualityCurve)), 1, math.min(Hulls.Length, 3));

            for (int i = 0; i < count; i++)
            {
                float axis = (i - 1) * 0.31f;
                float crush = math.saturate(PressureIntensity) * 0.18f;
                float3 localCenter = center + new float3(axis * size.x, 0f, -axis * size.z);
                float3 localSize = new float3(
                    math.max(0.08f, size.x * math.lerp(0.85f, 0.52f, crush)),
                    math.max(0.08f, size.y * math.lerp(0.9f, 0.62f, crush)),
                    math.max(0.08f, size.z * (count == 1 ? 1f : 0.42f)));
                Hulls[i] = new HabitatDamageHullDTO
                {
                    Center = localCenter,
                    Shape = 1,
                    State = State,
                    Flags = 1,
                    Size = localSize,
                    Radius = math.cmax(localSize) * 0.5f,
                    Rotation = quaternion.identity,
                    ModuleHash = ModuleHash,
                    HullHash = Hash(ModuleHash, (uint)(State * 17 + i))
                };
            }
        }

        private static uint Hash(uint hash, uint value)
        {
            hash = (hash ^ value) * 16777619u;
            return hash == 0u ? 1u : hash;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct PackBakedVertexJob : IJobParallelFor
    {
        [ReadOnly] [NoAlias] public NativeArray<HabitatDamageWorkingVertex> Source;
        [WriteOnly] [NoAlias] public NativeArray<HabitatDamageBakedVertex> Output;

        public void Execute(int index)
        {
            HabitatDamageWorkingVertex vertex = Source[index];
            float3 position = math.all(math.isfinite(vertex.Position)) ? vertex.Position : float3.zero;
            float3 normal = math.normalizesafe(vertex.Normal, new float3(0f, 1f, 0f));
            float4 tangent = vertex.Tangent;
            if (!math.all(math.isfinite(tangent)))
                tangent = new float4(1f, 0f, 0f, 1f);
            float2 uv0 = math.all(math.isfinite(vertex.Uv0)) ? vertex.Uv0 : float2.zero;
            float stress = math.saturate(math.isfinite(vertex.Stress01) ? vertex.Stress01 : 0f);
            float tear01 = math.saturate(math.isfinite(vertex.Tear01) ? vertex.Tear01 : 0f);
            byte heat = (byte)math.clamp((int)math.round(stress * 255f), 0, 255);
            byte tear = (byte)math.clamp((int)math.round(tear01 * 255f), 0, 255);
            Output[index] = new HabitatDamageBakedVertex
            {
                Position = position,
                NormalX = (ushort)math.f32tof16(normal.x),
                NormalY = (ushort)math.f32tof16(normal.y),
                NormalZ = (ushort)math.f32tof16(normal.z),
                NormalW = (ushort)math.f32tof16(0f),
                TangentSnorm = PackSnorm4x8(tangent),
                UvX = (ushort)math.f32tof16(uv0.x),
                UvY = (ushort)math.f32tof16(uv0.y),
                // HARD-SURFACE vertex colour contract, 3dmodel.md section 4: R = exposed edge wear or
                // salt-polished rim, G = rust / oxidation / biofilm / fluid stain, B = baked ambient
                // occlusion and cavity darkness, A = optional emission / warning paint / decal
                // eligibility. This is the hard-surface set, NOT the organic one in
                // 3DMODEL_FLORA_CORAL.md section 2; only B carries the same meaning in both.
                //
                // B used to be a second copy of `heat`, which left baked occlusion with nowhere to
                // live and put a stress field where every other shader in the project expects
                // occlusion. There is no ambient occlusion available to write here: this job runs
                // per-vertex over Source[index] with only a position and a normal, and real occlusion
                // needs neighbouring geometry or a ray-traced bake. NoBakedOcclusion is the honest
                // no-data value rather than an invented gradient -- see its remarks.
                ColorRgba = PackColor(tear, heat, NoBakedOcclusion, 255)
            };
        }

        /// <summary>
        /// Vertex colour channel B is baked ambient occlusion in the hard-surface contract
        /// (3dmodel.md section 4). This job has no occlusion input, so 255 -- fully unoccluded -- is
        /// written instead of a substitute. That matches the missing-AO default the compliant Blender
        /// writer uses and the reason it states (h8forge/vertexcolor.py write_hard_surface_channels
        /// passes <c>get_b = channel(ao, 1.0)</c>: "a darkening default would bake fake shadow into
        /// every asset whose AO bake failed"). A curvature or normal-direction proxy is deliberately
        /// not substituted, because vertexcolor.py curvature_edge_wear is explicit that a geometric
        /// estimate is honest for wear and is NOT honest for occlusion.
        /// </summary>
        private const byte NoBakedOcclusion = 255;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint PackSnorm4x8(float4 value)
        {
            int x = PackSnorm(value.x);
            int y = PackSnorm(value.y);
            int z = PackSnorm(value.z);
            int w = PackSnorm(value.w);
            return (uint)(x | (y << 8) | (z << 16) | (w << 24));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PackSnorm(float value)
        {
            return ((int)math.round(math.clamp(value, -1f, 1f) * 127f)) & 0xFF;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint PackColor(byte r, byte g, byte b, byte a)
        {
            return r | ((uint)g << 8) | ((uint)b << 16) | ((uint)a << 24);
        }
    }

    internal sealed class HabitatDamageBakeReport : IDisposable
    {
        private const string NativeMemoryOwner = nameof(HabitatDamageBakeReport);
        private const string TelemetryRingLabel = "telemetryRing";

        public int MeshesProcessed;
        public int PristineTriangles;
        public int CollapsedTriangles;
        public int TornTriangles;
        public long BurstTicks;
        public readonly List<string> CriticalWarnings = new List<string>(16);
        private NativeArray<HabitatDamageBakeTelemetryEntry> _telemetryRing;
        private int _telemetryRingSentinelId;
        private int _telemetryCursor;

        public HabitatDamageBakeReport()
        {
            _telemetryRing = new NativeArray<HabitatDamageBakeTelemetryEntry>(
                HabitatDamageBakeVaultContract.TelemetryFrameCount,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            try
            {
                _telemetryRingSentinelId = HabitatDamageNativeMemorySentinelBridge.RegisterNativeArray(
                    _telemetryRing,
                    NativeMemoryOwner,
                    TelemetryRingLabel,
                    "Session");
            }
            catch
            {
                IntPtr trackedTelemetryRing;
                unsafe
                {
                    trackedTelemetryRing = (IntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_telemetryRing);
                }

                _telemetryRing.Dispose();
                _telemetryRing = default;

                if (_telemetryRingSentinelId > 0)
                    HabitatDamageNativeMemorySentinelBridge.Unregister(_telemetryRingSentinelId);
                else
                    HabitatDamageNativeMemorySentinelBridge.UnregisterPointer(trackedTelemetryRing);

                _telemetryRingSentinelId = 0;
                throw;
            }
        }

        public void Merge(in HabitatDamageStateBakeResult result)
        {
            MeshesProcessed++;
            PristineTriangles += result.SourceTriangleCount;
            if (result.State == HabitatDamageMeshState.Collapsed)
                CollapsedTriangles += result.OutputTriangleCount;
            TornTriangles += result.TornTriangleCount;
            BurstTicks += result.BurstTicks;
            uint faultFlags = 0u;
            if (result.OutputTriangleCount > HabitatDamageBakeConstants.ComplexityCriticalTriangleBudget)
            {
                faultFlags |= 1u;
                CriticalWarnings.Add(result.MeshName + " " + result.State + " exceeds " + HabitatDamageBakeConstants.ComplexityCriticalTriangleBudget + " tris");
            }
            PushTelemetry(result, faultFlags);
        }

        public void Write(string projectRoot)
        {
            string path = Path.Combine(projectRoot, HabitatDamageBakeConstants.BakeReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            double ms = BurstTicks * 1000.0 / Stopwatch.Frequency;
            int telemetryCount = math.min(_telemetryCursor, HabitatDamageBakeVaultContract.TelemetryFrameCount);
            WriteTelemetryDump(projectRoot, telemetryCount);
            StringBuilder sb = new StringBuilder(1024);
            sb.Append("{\n");
            sb.Append("  \"agent\": \"SHINOBU_210\",\n");
            sb.Append("  \"editorAssembly\": \"Hecton8.Habitat.Deformation.DamageBake.Editor\",\n");
            sb.Append("  \"editorAssemblyRuntimeReference\": false,\n");
            sb.Append("  \"meshesProcessed\": ").Append(MeshesProcessed).Append(",\n");
            sb.Append("  \"pristineTriangles\": ").Append(PristineTriangles).Append(",\n");
            sb.Append("  \"collapsedTriangles\": ").Append(CollapsedTriangles).Append(",\n");
            sb.Append("  \"tornTriangles\": ").Append(TornTriangles).Append(",\n");
            sb.Append("  \"burstJobMilliseconds\": ").Append(ms.ToString("0.000")).Append(",\n");
            sb.Append("  \"telemetryRingCapacity\": ").Append(HabitatDamageBakeVaultContract.TelemetryFrameCount).Append(",\n");
            sb.Append("  \"telemetryFramesRecorded\": ").Append(telemetryCount).Append(",\n");
            sb.Append("  \"blackboxDumpPath\": \"").Append(HabitatDamageBakeConstants.BlackboxDumpPath).Append("\",\n");
            sb.Append("  \"blackboxDumpVersion\": ").Append(HabitatDamageBakeConstants.BlackboxDumpVersion).Append(",\n");
            sb.Append("  \"blackboxEndian\": \"little\",\n");
            sb.Append("  \"blackboxHeaderBytes\": 24,\n");
            sb.Append("  \"status\": \"PENDING_VERIFICATION\",\n");
            sb.Append("  \"criticalWarnings\": [");
            for (int i = 0; i < CriticalWarnings.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append('"').Append(EscapeJson(CriticalWarnings[i])).Append('"');
            }
            sb.Append("]\n");
            sb.Append("}\n");
            File.WriteAllText(path, sb.ToString());
        }

        public void Dispose()
        {
            if (_telemetryRing.IsCreated)
            {
                IntPtr trackedTelemetryRing;
                unsafe
                {
                    trackedTelemetryRing = (IntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_telemetryRing);
                }

                _telemetryRing.Dispose();
                _telemetryRing = default;

                if (_telemetryRingSentinelId > 0)
                    HabitatDamageNativeMemorySentinelBridge.Unregister(_telemetryRingSentinelId);
                else
                    HabitatDamageNativeMemorySentinelBridge.UnregisterPointer(trackedTelemetryRing);

                _telemetryRingSentinelId = 0;
            }
        }

        private void PushTelemetry(in HabitatDamageStateBakeResult result, uint faultFlags)
        {
            if (!_telemetryRing.IsCreated || _telemetryRing.Length == 0)
                return;

            int index = _telemetryCursor % _telemetryRing.Length;
            _telemetryRing[index] = new HabitatDamageBakeTelemetryEntry
            {
                Frame = (uint)_telemetryCursor,
                ModuleHash = result.ModuleHash,
                StateHash = result.StateHash,
                SourceTriangleCount = result.SourceTriangleCount,
                OutputTriangleCount = result.OutputTriangleCount,
                TornTriangleCount = result.TornTriangleCount,
                HullCount = result.HullCount,
                GlobalQualityWeight = result.GlobalQualityWeight,
                BurstJobMilliseconds = (float)(result.BurstTicks * 1000.0 / Stopwatch.Frequency),
                FaultFlags = faultFlags,
                OutputMeshHash = result.MeshHash
            };
            _telemetryCursor++;
        }

        private void WriteTelemetryDump(string projectRoot, int telemetryCount)
        {
            string path = Path.Combine(projectRoot, HabitatDamageBakeConstants.BlackboxDumpPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.SequentialScan);
            WriteUInt32Le(stream, HabitatDamageBakeConstants.AgentHash);
            WriteInt32Le(stream, HabitatDamageBakeConstants.BlackboxDumpVersion);
            WriteInt32Le(stream, HabitatDamageBakeVaultContract.TelemetryFrameCount);
            WriteInt32Le(stream, telemetryCount);
            WriteInt32Le(stream, _telemetryCursor);
            WriteInt32Le(stream, UnsafeUtility.SizeOf<HabitatDamageBakeTelemetryEntry>());
            int start = _telemetryCursor > HabitatDamageBakeVaultContract.TelemetryFrameCount
                ? _telemetryCursor % HabitatDamageBakeVaultContract.TelemetryFrameCount
                : 0;
            for (int i = 0; i < telemetryCount; i++)
            {
                int index = (start + i) % HabitatDamageBakeVaultContract.TelemetryFrameCount;
                WriteTelemetryEntry(stream, _telemetryRing[index]);
            }
        }

        private static void WriteTelemetryEntry(FileStream stream, in HabitatDamageBakeTelemetryEntry entry)
        {
            WriteUInt32Le(stream, entry.Frame);
            WriteUInt32Le(stream, entry.ModuleHash);
            WriteUInt32Le(stream, entry.StateHash);
            WriteInt32Le(stream, entry.SourceTriangleCount);
            WriteInt32Le(stream, entry.OutputTriangleCount);
            WriteInt32Le(stream, entry.TornTriangleCount);
            WriteInt32Le(stream, entry.HullCount);
            WriteFloatLe(stream, entry.GlobalQualityWeight);
            WriteFloatLe(stream, entry.BurstJobMilliseconds);
            WriteUInt32Le(stream, entry.FaultFlags);
            WriteUInt32Le(stream, entry.OutputMeshHash);
            WriteUInt32Le(stream, entry._pad0);
            WriteUInt64Le(stream, entry._pad1);
            WriteUInt64Le(stream, entry._pad2);
        }

        private static void WriteInt32Le(FileStream stream, int value)
        {
            WriteUInt32Le(stream, unchecked((uint)value));
        }

        private static void WriteFloatLe(FileStream stream, float value)
        {
            WriteUInt32Le(stream, math.asuint(value));
        }

        private static unsafe void WriteUInt32Le(FileStream stream, uint value)
        {
            uint littleEndian = BitConverter.IsLittleEndian ? value : ReverseBytes32(value);
            byte* bytes = (byte*)&littleEndian;
            for (int i = 0; i < 4; i++)
                stream.WriteByte(bytes[i]);
        }

        private static unsafe void WriteUInt64Le(FileStream stream, ulong value)
        {
            ulong littleEndian = BitConverter.IsLittleEndian ? value : ReverseBytes64(value);
            byte* bytes = (byte*)&littleEndian;
            for (int i = 0; i < 8; i++)
                stream.WriteByte(bytes[i]);
        }

        private static ulong ReverseBytes64(ulong value)
        {
            uint lo = (uint)value;
            uint hi = (uint)(value >> 32);
            return ((ulong)ReverseBytes32(lo) << 32) | ReverseBytes32(hi);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReverseBytes32(uint value)
        {
            return ((value & 0x000000FFu) << 24) |
                ((value & 0x0000FF00u) << 8) |
                ((value & 0x00FF0000u) >> 8) |
                ((value & 0xFF000000u) >> 24);
        }

        private static string EscapeJson(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    internal static class HabitatDamageNativeMemorySentinelBridge
    {
        internal static int RegisterNativeArray<T>(
            NativeArray<T> array,
            string owner,
            string label,
            string lifetimeName)
            where T : struct
        {
            if (!array.IsCreated)
                return 0;

            Type sentinelType = FindType("Hecton8.Core.NativeMemorySentinel");
            Type lifetimeType = FindType("Hecton8.Core.NativeAllocationLifetime");
            if (sentinelType == null || lifetimeType == null)
                throw new InvalidOperationException("NativeMemorySentinel bridge unavailable for habitat damage bake telemetry.");

            MethodInfo method = sentinelType.GetMethod("RegisterNativeArray", BindingFlags.Public | BindingFlags.Static);
            if (method == null)
                throw new InvalidOperationException("NativeMemorySentinel.RegisterNativeArray unavailable for habitat damage bake telemetry.");

            object lifetime = Enum.Parse(lifetimeType, lifetimeName);
            object id = method.MakeGenericMethod(typeof(T)).Invoke(null, new object[] { array, owner, label, lifetime });
            if (id is int value && value != 0)
                return value;

            throw new InvalidOperationException("NativeMemorySentinel rejected habitat damage bake telemetry registration.");
        }

        internal static void Unregister(int sentinelId)
        {
            if (sentinelId <= 0)
                return;

            Type sentinelType = FindType("Hecton8.Core.NativeMemorySentinel");
            MethodInfo method = sentinelType != null
                ? sentinelType.GetMethod("Unregister", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(int) }, null)
                : null;
            if (method == null)
                throw new InvalidOperationException("NativeMemorySentinel.Unregister unavailable for habitat damage bake telemetry.");

            method.Invoke(null, new object[] { sentinelId });
        }

        internal static void UnregisterPointer(IntPtr trackedPointer)
        {
            if (trackedPointer == IntPtr.Zero)
                return;

            Type sentinelType = FindType("Hecton8.Core.NativeMemorySentinel");
            MethodInfo method = sentinelType != null
                ? sentinelType.GetMethod("UnregisterPointer", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(IntPtr) }, null)
                : null;
            if (method == null)
                throw new InvalidOperationException("NativeMemorySentinel.UnregisterPointer unavailable for habitat damage bake telemetry.");

            method.Invoke(null, new object[] { trackedPointer });
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                    return type;
            }

            return null;
        }
    }

    internal struct HabitatDamageStateBakeResult
    {
        public string MeshName;
        public HabitatDamageMeshState State;
        public uint ModuleHash;
        public uint StateHash;
        public int SourceTriangleCount;
        public int OutputTriangleCount;
        public int TornTriangleCount;
        public int HullCount;
        public float GlobalQualityWeight;
        public long BurstTicks;
        public uint MeshHash;
        public HabitatDamageHullDTO[] Hulls;
    }

    public sealed class HabitatDamageBakeManifest : ScriptableObject
    {
        [Header("Source")]
        [Tooltip("Pristine prefab used to produce the baked damage state meshes.")]
        public GameObject SourcePrefab;

        [Tooltip("Original mesh assets resolved from the source prefab.")]
        public Mesh[] PristineMeshes;

        [Header("Baked States")]
        [Tooltip("Editor-baked Stressed mesh states.")]
        public Mesh[] StressedMeshes;

        [Tooltip("Editor-baked Ruptured mesh states.")]
        public Mesh[] RupturedMeshes;

        [Tooltip("Editor-baked Collapsed mesh states.")]
        public Mesh[] CollapsedMeshes;

        [Header("Runtime Maps")]
        [Tooltip("ARM64-aligned hash mapping between pristine meshes and baked states.")]
        public ModuleDamageStateMappingDTO[] StateMappings;

        [Tooltip("Primitive physics lie for damaged module collision.")]
        public HabitatDamageHullDTO[] CollisionHulls;
    }

    internal static class HabitatDamageBakePipeline
    {
        private const string NativeMemoryOwner = nameof(HabitatDamageBakePipeline);

        private static readonly VertexAttributeDescriptor[] _bakedVertexLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float16, 4),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.SNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4)
        };

        internal static NativeArray<T> AllocateTrackedNativeArray<T>(
            int length,
            Allocator allocator,
            NativeArrayOptions options,
            string label)
            where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(math.max(1, length), allocator, options);
            try
            {
                HabitatDamageNativeMemorySentinelBridge.RegisterNativeArray(
                    array,
                    NativeMemoryOwner,
                    label,
                    NativeLifetimeName(allocator));
                return array;
            }
            catch
            {
                if (array.IsCreated)
                    array.Dispose();
                throw;
            }
        }

        internal static unsafe void DisposeTrackedNativeArray<T>(ref NativeArray<T> array)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            IntPtr trackedPointer = (IntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            array.Dispose();
            array = default;
            HabitatDamageNativeMemorySentinelBridge.UnregisterPointer(trackedPointer);
        }

        private static string NativeLifetimeName(Allocator allocator)
        {
            if (allocator == Allocator.Temp)
                return "Temp";
            if (allocator == Allocator.TempJob)
                return "TempJob";
            return "Session";
        }

        public static HabitatCrushProfileDTO DefaultProfile()
        {
            return new HabitatCrushProfileDTO
            {
                ProfileHash = HashLowerAscii("titanium_corridor"),
                CrushIntensity = 0.72f,
                TearThreshold = 0.18f,
                MaterialYieldStrength = 1f,
                StressColorIntensity = 1f,
                GlobalQualityWeight = 1f
            };
        }

        public static unsafe bool TryReadProfiles(string projectRoot, List<HabitatCrushProfileDTO> profiles, List<string> names)
        {
            profiles.Clear();
            names.Clear();
            string path = Path.Combine(projectRoot, HabitatDamageBakeConstants.ProfileCsvPath);
            if (!File.Exists(path))
            {
                profiles.Add(DefaultProfile());
                names.Add("Titanium_Corridor");
                return false;
            }

            FileInfo info = new FileInfo(path);
            if (info.Length <= 0L || info.Length > 1024L * 1024L)
            {
                profiles.Add(DefaultProfile());
                names.Add("Titanium_Corridor");
                return false;
            }

            NativeArray<byte> bytes = HabitatDamageBakePipeline.AllocateTrackedNativeArray<byte>(
                (int)info.Length,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory,
                "profileCsvBytes");
            try
            {
                byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(bytes);
                int totalRead = 0;
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan))
                {
                    Span<byte> destination = new Span<byte>(ptr, bytes.Length);
                    while (totalRead < destination.Length)
                    {
                        int read = stream.Read(destination.Slice(totalRead));
                        if (read <= 0)
                            break;

                        totalRead += read;
                    }
                }

                ReadOnlySpan<byte> source = new ReadOnlySpan<byte>(ptr, totalRead);
                int cursor = 0;
                while (TryReadLine(source, ref cursor, out int start, out int length))
                {
                    ReadOnlySpan<byte> line = Trim(source.Slice(start, length));
                    if (line.Length == 0 || line[0] == (byte)'#')
                        continue;

                    if (TryParseProfileLine(line, out HabitatCrushProfileDTO profile, out string name))
                    {
                        profiles.Add(profile);
                        names.Add(name);
                    }
                }
            }
            catch (IOException)
            {
                profiles.Clear();
                names.Clear();
                profiles.Add(DefaultProfile());
                names.Add("Titanium_Corridor");
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                profiles.Clear();
                names.Clear();
                profiles.Add(DefaultProfile());
                names.Add("Titanium_Corridor");
                return false;
            }
            finally
            {
                HabitatDamageBakePipeline.DisposeTrackedNativeArray(ref bytes);
            }

            if (profiles.Count == 0)
            {
                profiles.Add(DefaultProfile());
                names.Add("Titanium_Corridor");
            }

            return true;
        }

        public static string StartBake(Object folderObject, in HabitatDamageBakeSettings settings)
        {
            string folder = folderObject == null ? "Assets/_Project/Prefabs" : AssetDatabase.GetAssetPath(folderObject);
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
                folder = "Assets/_Project/Prefabs";

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            HabitatDamageBakeQueue.Start(guids, settings);
            return "Queued " + guids.Length + " prefab(s) from " + folder;
        }

        public static Mesh BakeDamageState(
            Mesh sourceMesh,
            HabitatDamageMeshState state,
            in HabitatDamageBakeSettings settings,
            out HabitatDamageStateBakeResult result,
            bool uploadMeshData = true)
        {
            result = default;
            result.MeshName = sourceMesh != null ? sourceMesh.name : "null";
            result.State = state;
            if (sourceMesh == null || sourceMesh.vertexCount <= 0)
                return null;

            using Mesh.MeshDataArray readOnly = Mesh.AcquireReadOnlyMeshData(sourceMesh);
            Mesh.MeshData sourceData = readOnly[0];
            if (!sourceData.HasVertexAttribute(VertexAttribute.Position))
                return null;
            if (sourceData.GetVertexAttributeFormat(VertexAttribute.Position) != VertexAttributeFormat.Float32 ||
                sourceData.GetVertexAttributeDimension(VertexAttribute.Position) < 3)
                return null;
            int sourceVertexCount = sourceData.vertexCount;
            NativeArray<HabitatDamageIndexRangeDTO> indexRanges = BuildTriangleIndexRanges(
                sourceData,
                Allocator.TempJob,
                out int indexCount,
                out int indexRangeCount);
            if (sourceVertexCount <= 0 || indexCount <= 0 || indexRangeCount <= 0)
            {
                DisposeTrackedNativeArray(ref indexRanges);
                return null;
            }
            if (state != HabitatDamageMeshState.Stressed && sourceVertexCount > int.MaxValue / 2)
            {
                DisposeTrackedNativeArray(ref indexRanges);
                return null;
            }

            uint sourceMeshHash = ResolveMeshHash(sourceMesh);
            int outputVertexCount = state == HabitatDamageMeshState.Stressed ? sourceVertexCount : sourceVertexCount * 2;
            int triangleCount = indexCount / 3;
            float stage = state == HabitatDamageMeshState.Stressed ? 0.28f : state == HabitatDamageMeshState.Ruptured ? 0.68f : 1f;
            float pressure = math.saturate(settings.CrushIntensity * stage);
            double depth = ResolveDepthMeters(settings.ModuleAup, settings.SeaLevelAup);
            Stopwatch stopwatch = Stopwatch.StartNew();

            NativeArray<HabitatDamageSourceVertex> sourceVertices = default;
            NativeArray<HabitatDamageWorkingVertex> workingVertices = default;
            NativeArray<uint> sourceIndices = default;
            NativeArray<uint> outputIndices = default;
            NativeArray<HabitatDamageHullDTO> hulls = default;
            NativeArray<HabitatDamageBakedVertex> packedVertices = default;
            try
            {
                sourceVertices = AllocateTrackedNativeArray<HabitatDamageSourceVertex>(
                    sourceVertexCount,
                    Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory,
                    nameof(sourceVertices));
                workingVertices = AllocateTrackedNativeArray<HabitatDamageWorkingVertex>(
                    outputVertexCount,
                    Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory,
                    nameof(workingVertices));
                sourceIndices = AllocateTrackedNativeArray<uint>(
                    indexCount,
                    Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory,
                    nameof(sourceIndices));
                outputIndices = AllocateTrackedNativeArray<uint>(
                    indexCount,
                    Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory,
                    nameof(outputIndices));
                hulls = AllocateTrackedNativeArray<HabitatDamageHullDTO>(
                    HabitatDamageBakeConstants.MaxHullCount,
                    Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory,
                    nameof(hulls));
                packedVertices = AllocateTrackedNativeArray<HabitatDamageBakedVertex>(
                    outputVertexCount,
                    Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory,
                    nameof(packedVertices));
                JobHandle handle = ScheduleExtract(sourceData, sourceVertices);
                handle = ScheduleIndexCopy(sourceData, sourceIndices, indexCount, indexRanges, indexRangeCount, handle);
                handle = new InitializeDamageWorkingVerticesJob
                {
                    Source = sourceVertices,
                    Output = workingVertices,
                    SourceCount = sourceVertexCount
                }.Schedule(outputVertexCount, 64, handle);
                handle = new ApplyHydrostaticBucklingJob
                {
                    Vertices = workingVertices,
                    PressureIntensity = pressure,
                    MaterialYieldStrength = settings.MaterialYieldStrength,
                    DepthMeters = (float)depth,
                    DamageStage01 = stage,
                    GlobalQualityWeight = settings.GlobalQualityWeight
                }.Schedule(outputVertexCount, 64, handle);
                handle = new ApplyStructuralTearJob
                {
                    Vertices = workingVertices,
                    SourceVertexCount = sourceVertexCount,
                    TearThreshold = settings.TearThreshold,
                    TearIntensity = pressure,
                    DamageStage01 = stage,
                    GlobalQualityWeight = settings.GlobalQualityWeight
                }.Schedule(outputVertexCount, 64, handle);
                handle = new BuildDamageIndexJob
                {
                    SourceIndices = sourceIndices,
                    Vertices = workingVertices,
                    OutputIndices = outputIndices,
                    SourceVertexCount = sourceVertexCount,
                    TriangleCount = triangleCount,
                    DamageState = (int)state,
                    TearThreshold = settings.TearThreshold,
                    PressureIntensity = pressure,
                    GlobalQualityWeight = settings.GlobalQualityWeight
                }.Schedule(triangleCount, 64, handle);
                handle = new RecalculateDeformedNormalsJob
                {
                    Vertices = workingVertices,
                    Indices = outputIndices,
                    TriangleCount = triangleCount
                }.Schedule(handle);
                handle = new BakeStressColorsJob
                {
                    Vertices = workingVertices,
                    StressColorIntensity = settings.StressColorIntensity
                }.Schedule(outputVertexCount, 64, handle);
                handle = new GenerateSimplifiedHullsJob
                {
                    Vertices = workingVertices,
                    Hulls = hulls,
                    ModuleHash = sourceMeshHash,
                    State = (byte)state,
                    PressureIntensity = pressure,
                    GlobalQualityWeight = settings.GlobalQualityWeight
                }.Schedule(handle);
                handle = new PackBakedVertexJob
                {
                    Source = workingVertices,
                    Output = packedVertices
                }.Schedule(outputVertexCount, 64, handle);
                handle.Complete();

                Bounds bounds = CalculateBounds(workingVertices);
                const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds |
                    MeshUpdateFlags.DontNotifyMeshUsers |
                    MeshUpdateFlags.DontValidateIndices;

                Mesh mesh = new Mesh();
                mesh.name = sourceMesh.name + "_" + state;
                mesh.SetVertexBufferParams(outputVertexCount, _bakedVertexLayout);
                mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
                mesh.SetVertexBufferData(packedVertices, 0, 0, outputVertexCount, 0, flags);
                mesh.SetIndexBufferData(outputIndices, 0, 0, indexCount, flags);
                mesh.subMeshCount = 1;
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles)
                {
                    bounds = bounds,
                    vertexCount = outputVertexCount
                }, flags);
                mesh.bounds = bounds;
                if (uploadMeshData)
                    mesh.UploadMeshData(true);

                stopwatch.Stop();
                result.ModuleHash = sourceMeshHash;
                result.StateHash = (uint)state;
                result.MeshHash = sourceMeshHash;
                result.SourceTriangleCount = triangleCount;
                result.OutputTriangleCount = CountLiveTriangles(outputIndices);
                result.TornTriangleCount = triangleCount - result.OutputTriangleCount;
                result.BurstTicks = stopwatch.ElapsedTicks;
                result.GlobalQualityWeight = settings.GlobalQualityWeight;
                result.Hulls = CopyHulls(hulls);
                result.HullCount = CountLiveHulls(hulls);
                return mesh;
            }
            finally
            {
                DisposeTrackedNativeArray(ref sourceVertices);
                DisposeTrackedNativeArray(ref workingVertices);
                DisposeTrackedNativeArray(ref sourceIndices);
                DisposeTrackedNativeArray(ref outputIndices);
                DisposeTrackedNativeArray(ref hulls);
                DisposeTrackedNativeArray(ref packedVertices);
                DisposeTrackedNativeArray(ref indexRanges);
            }
        }

        public static bool BuildPreview(
            Mesh sourceMesh,
            in HabitatDamageBakeSettings settings,
            out Vector3[] vertices,
            out uint[] indices)
        {
            vertices = Array.Empty<Vector3>();
            indices = Array.Empty<uint>();
            Mesh preview = BakeDamageState(sourceMesh, HabitatDamageMeshState.Ruptured, in settings, out _, false);
            if (preview == null)
                return false;

            vertices = preview.vertices;
            int[] raw = preview.triangles;
            indices = new uint[raw.Length];
            for (int i = 0; i < raw.Length; i++)
                indices[i] = (uint)raw[i];
            Object.DestroyImmediate(preview);
            return true;
        }

        public static bool RunMockHydrostaticPressureBenchmark(
            int radialSegments,
            int lengthSegments,
            in HabitatDamageBakeSettings settings,
            out long burstTicks,
            out int vertexCount)
        {
            burstTicks = 0L;
            int radial = math.clamp(radialSegments, 8, 512);
            int length = math.clamp(lengthSegments, 8, 512);
            vertexCount = radial * length;
            NativeArray<HabitatDamageWorkingVertex> vertices = AllocateTrackedNativeArray<HabitatDamageWorkingVertex>(
                vertexCount,
                Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory,
                nameof(vertices));
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                JobHandle handle = new GenerateMockHydrostaticPressureJob
                {
                    Vertices = vertices,
                    RadialSegments = radial,
                    LengthSegments = length,
                    RadiusMeters = 2.1f,
                    LengthMeters = 18f,
                    PressureIntensity = math.saturate(settings.CrushIntensity)
                }.Schedule(vertexCount, 64);
                handle = new ApplyHydrostaticBucklingJob
                {
                    Vertices = vertices,
                    PressureIntensity = math.saturate(settings.CrushIntensity),
                    MaterialYieldStrength = settings.MaterialYieldStrength,
                    DepthMeters = (float)ResolveDepthMeters(settings.ModuleAup, settings.SeaLevelAup),
                    DamageStage01 = 1f,
                    GlobalQualityWeight = settings.GlobalQualityWeight
                }.Schedule(vertexCount, 64, handle);
                handle.Complete();
                stopwatch.Stop();
                burstTicks = stopwatch.ElapsedTicks;
                return true;
            }
            finally
            {
                DisposeTrackedNativeArray(ref vertices);
            }
        }

        public static double ResolveDepthMeters(double3 moduleAup, double3 seaLevelAup)
        {
            if (!math.all(math.isfinite(moduleAup)))
                return 0d;

            double3 resolvedSeaLevelAup = SanitizeSeaLevelAup(seaLevelAup);
            double depth = resolvedSeaLevelAup.y - moduleAup.y;
            return math.isfinite(depth) ? math.max(0d, depth) : 0d;
        }

        private static double3 SanitizeSeaLevelAup(double3 candidateSeaLevelAup)
        {
            double x = math.isfinite(candidateSeaLevelAup.x) ? candidateSeaLevelAup.x : 0d;
            double y = ResolveSeaLevelAupY(candidateSeaLevelAup.y);
            double z = math.isfinite(candidateSeaLevelAup.z) ? candidateSeaLevelAup.z : 0d;
            return new double3(x, y, z);
        }

        private static double ResolveSeaLevelAupY(double candidateSeaLevelAupY)
        {
            return math.isfinite(candidateSeaLevelAupY) &&
                   math.abs(candidateSeaLevelAupY) > 0.0001d &&
                   math.abs(candidateSeaLevelAupY) <= 1000d
                ? candidateSeaLevelAupY
                : HabitatDamageBakeConstants.DefaultSeaLevelAupY;
        }

        public static uint ResolveMeshHash(Mesh mesh)
        {
            if (mesh == null)
                return 0u;

            uint hash = 2166136261u;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long localId))
            {
                for (int i = 0; i < guid.Length; i++)
                    hash = (hash ^ guid[i]) * 16777619u;
                hash = (hash ^ (uint)localId) * 16777619u;
                hash = (hash ^ (uint)(localId >> 32)) * 16777619u;
                return hash == 0u ? 1u : hash;
            }

            return HashLowerAscii(mesh.name);
        }

        public static uint HashLowerAscii(string value)
        {
            uint hash = 2166136261u;
            if (string.IsNullOrEmpty(value))
                return 0u;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);
                hash = (hash ^ c) * 16777619u;
            }
            return hash == 0u ? 1u : hash;
        }

        public static uint HashLowerAscii(ReadOnlySpan<byte> value)
        {
            uint hash = 2166136261u;
            if (value.Length == 0)
                return 0u;
            bool any = false;
            for (int i = 0; i < value.Length; i++)
            {
                byte c = value[i];
                if (c <= 32)
                    continue;
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash = (hash ^ c) * 16777619u;
                any = true;
            }
            return any ? (hash == 0u ? 1u : hash) : 0u;
        }

        private static JobHandle ScheduleExtract(Mesh.MeshData sourceData, NativeArray<HabitatDamageSourceVertex> output)
        {
            int positionStream = sourceData.GetVertexAttributeStream(VertexAttribute.Position);
            int normalStream = -1;
            int tangentStream = -1;
            int uvStream = -1;
            bool hasNormal = false;
            bool hasTangent = false;
            bool hasUv = false;
            if (sourceData.HasVertexAttribute(VertexAttribute.Normal))
            {
                hasNormal = sourceData.GetVertexAttributeFormat(VertexAttribute.Normal) == VertexAttributeFormat.Float32 &&
                            sourceData.GetVertexAttributeDimension(VertexAttribute.Normal) >= 3;
                normalStream = hasNormal ? sourceData.GetVertexAttributeStream(VertexAttribute.Normal) : -1;
            }
            if (sourceData.HasVertexAttribute(VertexAttribute.Tangent))
            {
                hasTangent = sourceData.GetVertexAttributeFormat(VertexAttribute.Tangent) == VertexAttributeFormat.Float32 &&
                             sourceData.GetVertexAttributeDimension(VertexAttribute.Tangent) >= 4;
                tangentStream = hasTangent ? sourceData.GetVertexAttributeStream(VertexAttribute.Tangent) : -1;
            }
            if (sourceData.HasVertexAttribute(VertexAttribute.TexCoord0))
            {
                hasUv = sourceData.GetVertexAttributeFormat(VertexAttribute.TexCoord0) == VertexAttributeFormat.Float32 &&
                        sourceData.GetVertexAttributeDimension(VertexAttribute.TexCoord0) >= 2;
                uvStream = hasUv ? sourceData.GetVertexAttributeStream(VertexAttribute.TexCoord0) : -1;
            }
            NativeArray<byte> positionBytes = sourceData.GetVertexData<byte>(positionStream);
            ExtractSourceVertexJob job = new ExtractSourceVertexJob
            {
                PositionBytes = positionBytes,
                NormalBytes = hasNormal ? sourceData.GetVertexData<byte>(normalStream) : positionBytes,
                TangentBytes = hasTangent ? sourceData.GetVertexData<byte>(tangentStream) : positionBytes,
                UvBytes = hasUv ? sourceData.GetVertexData<byte>(uvStream) : positionBytes,
                Output = output,
                PositionOffset = sourceData.GetVertexAttributeOffset(VertexAttribute.Position),
                PositionStride = sourceData.GetVertexBufferStride(positionStream),
                NormalOffset = hasNormal ? sourceData.GetVertexAttributeOffset(VertexAttribute.Normal) : 0,
                NormalStride = hasNormal ? sourceData.GetVertexBufferStride(normalStream) : 0,
                TangentOffset = hasTangent ? sourceData.GetVertexAttributeOffset(VertexAttribute.Tangent) : 0,
                TangentStride = hasTangent ? sourceData.GetVertexBufferStride(tangentStream) : 0,
                UvOffset = hasUv ? sourceData.GetVertexAttributeOffset(VertexAttribute.TexCoord0) : 0,
                UvStride = hasUv ? sourceData.GetVertexBufferStride(uvStream) : 0,
                HasNormal = hasNormal ? 1 : 0,
                HasTangent = hasTangent ? 1 : 0,
                HasUv = hasUv ? 1 : 0
            };
            return job.Schedule(output.Length, 64);
        }

        private static JobHandle ScheduleIndexCopy(
            Mesh.MeshData sourceData,
            NativeArray<uint> output,
            int indexCount,
            NativeArray<HabitatDamageIndexRangeDTO> ranges,
            int rangeCount,
            JobHandle dependency)
        {
            if (sourceData.indexFormat == IndexFormat.UInt16)
            {
                return new CopyIndex16Job
                {
                    Source = sourceData.GetIndexData<ushort>(),
                    Ranges = ranges,
                    Output = output,
                    RangeCount = rangeCount,
                    SourceVertexCount = sourceData.vertexCount
                }.Schedule(indexCount, 64, dependency);
            }

            return new CopyIndex32Job
            {
                Source = sourceData.GetIndexData<uint>(),
                Ranges = ranges,
                Output = output,
                RangeCount = rangeCount,
                SourceVertexCount = sourceData.vertexCount
            }.Schedule(indexCount, 64, dependency);
        }

        private static NativeArray<HabitatDamageIndexRangeDTO> BuildTriangleIndexRanges(
            Mesh.MeshData sourceData,
            Allocator allocator,
            out int indexCount,
            out int rangeCount)
        {
            int capacity = math.max(1, sourceData.subMeshCount);
            NativeArray<HabitatDamageIndexRangeDTO> ranges = AllocateTrackedNativeArray<HabitatDamageIndexRangeDTO>(
                capacity,
                allocator,
                NativeArrayOptions.UninitializedMemory,
                "indexRanges");

            try
            {
                indexCount = 0;
                rangeCount = 0;
                for (int i = 0; i < sourceData.subMeshCount; i++)
                {
                    SubMeshDescriptor subMesh = sourceData.GetSubMesh(i);
                    int count = subMesh.indexCount - (subMesh.indexCount % 3);
                    if (subMesh.topology != MeshTopology.Triangles || count <= 0)
                        continue;
                    if (count > int.MaxValue - indexCount)
                    {
                        count = int.MaxValue - indexCount;
                        count -= count % 3;
                        if (count <= 0)
                            break;
                    }

                    ranges[rangeCount] = new HabitatDamageIndexRangeDTO
                    {
                        SourceStart = subMesh.indexStart,
                        DestinationStart = indexCount,
                        Count = count,
                        BaseVertex = subMesh.baseVertex
                    };
                    indexCount += count;
                    rangeCount++;
                }

                if (indexCount <= 0)
                {
                    int fallbackCount = sourceData.indexFormat == IndexFormat.UInt16
                        ? sourceData.GetIndexData<ushort>().Length
                        : sourceData.GetIndexData<uint>().Length;
                    fallbackCount -= fallbackCount % 3;
                    if (fallbackCount > 0)
                    {
                        ranges[0] = new HabitatDamageIndexRangeDTO
                        {
                            SourceStart = 0,
                            DestinationStart = 0,
                            Count = fallbackCount,
                            BaseVertex = 0
                        };
                        indexCount = fallbackCount;
                        rangeCount = 1;
                    }
                }

                for (int i = rangeCount; i < ranges.Length; i++)
                    ranges[i] = default;
                return ranges;
            }
            catch
            {
                DisposeTrackedNativeArray(ref ranges);
                throw;
            }
        }

        private static Bounds CalculateBounds(NativeArray<HabitatDamageWorkingVertex> vertices)
        {
            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);
            int finiteVertexCount = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                float3 p = vertices[i].Position;
                if (!math.all(math.isfinite(p)))
                    continue;
                min = math.min(min, p);
                max = math.max(max, p);
                finiteVertexCount++;
            }

            if (finiteVertexCount == 0 || !math.all(math.isfinite(min)) || !math.all(math.isfinite(max)))
                return new Bounds(Vector3.zero, Vector3.one);
            Vector3 center = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, (min.z + max.z) * 0.5f);
            Vector3 size = new Vector3(math.max(0.01f, max.x - min.x), math.max(0.01f, max.y - min.y), math.max(0.01f, max.z - min.z));
            return new Bounds(center, size);
        }

        private static int CountLiveTriangles(NativeArray<uint> indices)
        {
            int count = 0;
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                uint a = indices[i];
                uint b = indices[i + 1];
                uint c = indices[i + 2];
                if (a != b && b != c && a != c)
                    count++;
            }
            return count;
        }

        private static HabitatDamageHullDTO[] CopyHulls(NativeArray<HabitatDamageHullDTO> hulls)
        {
            HabitatDamageHullDTO[] result = new HabitatDamageHullDTO[hulls.Length];
            for (int i = 0; i < hulls.Length; i++)
                result[i] = hulls[i];
            return result;
        }

        private static int CountLiveHulls(NativeArray<HabitatDamageHullDTO> hulls)
        {
            int count = 0;
            for (int i = 0; i < hulls.Length; i++)
            {
                if (hulls[i].HullHash != 0u)
                    count++;
            }
            return count;
        }

        private static bool TryReadLine(ReadOnlySpan<byte> bytes, ref int cursor, out int start, out int length)
        {
            start = 0;
            length = 0;
            if (cursor >= bytes.Length)
                return false;
            start = cursor;
            while (cursor < bytes.Length && bytes[cursor] != (byte)'\n')
                cursor++;
            int end = cursor;
            if (cursor < bytes.Length)
                cursor++;
            if (end > start && bytes[end - 1] == (byte)'\r')
                end--;
            length = math.max(0, end - start);
            return true;
        }

        private static bool TryParseProfileLine(ReadOnlySpan<byte> line, out HabitatCrushProfileDTO profile, out string name)
        {
            profile = DefaultProfile();
            name = string.Empty;
            int cursor = 0;
            if (!TryReadToken(line, ref cursor, out ReadOnlySpan<byte> nameToken))
                return false;
            name = ToEditorAsciiLabel(nameToken);
            if (!TryReadToken(line, ref cursor, out ReadOnlySpan<byte> crush) ||
                !TryReadToken(line, ref cursor, out ReadOnlySpan<byte> tear) ||
                !TryReadToken(line, ref cursor, out ReadOnlySpan<byte> yieldToken) ||
                !TryReadToken(line, ref cursor, out ReadOnlySpan<byte> stress))
            {
                return false;
            }

            TryReadToken(line, ref cursor, out ReadOnlySpan<byte> quality);
            profile.ProfileHash = HashLowerAscii(nameToken);
            profile.CrushIntensity = TryParseFloat(crush, out float c) ? math.saturate(c) : 0.72f;
            profile.TearThreshold = TryParseFloat(tear, out float t) ? math.saturate(t) : 0.18f;
            profile.MaterialYieldStrength = TryParseFloat(yieldToken, out float y) ? math.max(0.01f, y) : 1f;
            profile.StressColorIntensity = TryParseFloat(stress, out float s) ? math.max(0f, s) : 1f;
            profile.GlobalQualityWeight = TryParseFloat(quality, out float q) ? math.saturate(q) : 1f;
            return true;
        }

        private static unsafe string ToEditorAsciiLabel(ReadOnlySpan<byte> token)
        {
            int length = math.min(token.Length, HabitatDamageBakeConstants.MaxEditorProfileNameBytes);
            if (length <= 0)
                return string.Empty;

            char* chars = stackalloc char[length];
            for (int i = 0; i < length; i++)
            {
                byte value = token[i];
                chars[i] = value >= 32 && value <= 126 ? (char)value : '_';
            }

            return new string(chars, 0, length);
        }

        private static bool TryReadToken(ReadOnlySpan<byte> line, ref int cursor, out ReadOnlySpan<byte> token)
        {
            token = ReadOnlySpan<byte>.Empty;
            if (cursor > line.Length)
                return false;
            int start = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;
            int end = cursor;
            if (cursor < line.Length)
                cursor++;
            token = Trim(line.Slice(start, end - start));
            return token.Length > 0;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && value[start] <= 32)
                start++;
            while (end >= start && value[end] <= 32)
                end--;
            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> value, out float result)
        {
            result = 0f;
            value = Trim(value);
            if (value.Length == 0)
                return false;
            int i = 0;
            bool negative = false;
            if (value[i] == (byte)'-' || value[i] == (byte)'+')
            {
                negative = value[i] == (byte)'-';
                i++;
            }
            double parsed = 0d;
            bool hasDigit = false;
            while (i < value.Length && value[i] >= (byte)'0' && value[i] <= (byte)'9')
            {
                parsed = parsed * 10d + value[i] - (byte)'0';
                hasDigit = true;
                i++;
            }
            if (i < value.Length && value[i] == (byte)'.')
            {
                i++;
                double scale = 0.1d;
                while (i < value.Length && value[i] >= (byte)'0' && value[i] <= (byte)'9')
                {
                    parsed += (value[i] - (byte)'0') * scale;
                    scale *= 0.1d;
                    hasDigit = true;
                    i++;
                }
            }
            result = (float)(negative ? -parsed : parsed);
            return hasDigit && math.isfinite(result);
        }
    }

    internal static class HabitatDamageBakeQueue
    {
        private static string[] _guids = Array.Empty<string>();
        private static int _cursor;
        private static HabitatDamageBakeSettings _settings;
        private static HabitatDamageBakeReport _report;
        private static string _projectRoot;

        public static string Status = "Idle";
        public static bool Active;
        public static float Progress;

        public static void Start(string[] guids, in HabitatDamageBakeSettings settings)
        {
            Stop();
            _guids = guids ?? Array.Empty<string>();
            _cursor = 0;
            _settings = settings;
            _report = new HabitatDamageBakeReport();
            _projectRoot = ProjectRoot();
            Active = _guids.Length > 0;
            Progress = 0f;
            Status = Active ? "Queued " + _guids.Length + " prefab(s)" : "No prefabs found";
            if (Active)
            {
                EditorApplication.update += Tick;
            }
            else
            {
                _report.Write(_projectRoot);
                _report.Dispose();
                _report = null;
            }
        }

        public static void Stop()
        {
            EditorApplication.update -= Tick;
            if (Active)
                EditorUtility.ClearProgressBar();
            if (_report != null)
            {
                _report.Dispose();
                _report = null;
            }
            Active = false;
            Progress = 0f;
        }

        private static void Tick()
        {
            if (!Active)
                return;

            if (_cursor >= _guids.Length)
            {
                _report.Write(_projectRoot);
                Status = "Bake pass wrote report: " + HabitatDamageBakeConstants.BakeReportPath;
                Progress = 1f;
                Stop();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return;
            }

            string guid = _guids[_cursor++];
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Progress = _guids.Length == 0 ? 0f : _cursor / (float)_guids.Length;
            Status = "Baking " + path;
            EditorUtility.DisplayProgressBar("Habitat Crush Forge", Status, Progress);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                BakePrefab(prefab);
        }

        private static void BakePrefab(GameObject prefab)
        {
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            if (filters == null || filters.Length == 0)
                return;

            Directory.CreateDirectory(Path.Combine(_projectRoot, HabitatDamageBakeConstants.OutputFolder));
            List<Mesh> pristine = new List<Mesh>(filters.Length);
            List<Mesh> stressed = new List<Mesh>(filters.Length);
            List<Mesh> ruptured = new List<Mesh>(filters.Length);
            List<Mesh> collapsed = new List<Mesh>(filters.Length);
            List<ModuleDamageStateMappingDTO> mappings = new List<ModuleDamageStateMappingDTO>(filters.Length);
            List<HabitatDamageHullDTO> hulls = new List<HabitatDamageHullDTO>(filters.Length * HabitatDamageBakeConstants.MaxHullCount);

            for (int i = 0; i < filters.Length; i++)
            {
                Mesh source = filters[i] != null ? filters[i].sharedMesh : null;
                if (source == null)
                    continue;

                pristine.Add(source);
                Mesh stressedMesh = SaveStateMesh(prefab.name, source, HabitatDamageMeshState.Stressed, out HabitatDamageStateBakeResult stressedResult);
                Mesh rupturedMesh = SaveStateMesh(prefab.name, source, HabitatDamageMeshState.Ruptured, out HabitatDamageStateBakeResult rupturedResult);
                Mesh collapsedMesh = SaveStateMesh(prefab.name, source, HabitatDamageMeshState.Collapsed, out HabitatDamageStateBakeResult collapsedResult);
                stressed.Add(stressedMesh);
                ruptured.Add(rupturedMesh);
                collapsed.Add(collapsedMesh);
                _report.Merge(stressedResult);
                _report.Merge(rupturedResult);
                _report.Merge(collapsedResult);
                AppendHulls(hulls, stressedResult.Hulls);
                AppendHulls(hulls, rupturedResult.Hulls);
                AppendHulls(hulls, collapsedResult.Hulls);
                string stressedPath = stressedMesh != null ? AssetDatabase.GetAssetPath(stressedMesh) : string.Empty;
                string rupturedPath = rupturedMesh != null ? AssetDatabase.GetAssetPath(rupturedMesh) : string.Empty;
                string collapsedPath = collapsedMesh != null ? AssetDatabase.GetAssetPath(collapsedMesh) : string.Empty;
                mappings.Add(new ModuleDamageStateMappingDTO
                {
                    PristineMeshHash = HabitatDamageBakePipeline.ResolveMeshHash(source),
                    StressedMeshHash = HabitatDamageBakePipeline.HashLowerAscii(stressedPath),
                    RupturedMeshHash = HabitatDamageBakePipeline.HashLowerAscii(rupturedPath),
                    CollapsedMeshHash = HabitatDamageBakePipeline.HashLowerAscii(collapsedPath)
                });
            }

            HabitatDamageBakeManifest manifest = ScriptableObject.CreateInstance<HabitatDamageBakeManifest>();
            manifest.SourcePrefab = prefab;
            manifest.PristineMeshes = pristine.ToArray();
            manifest.StressedMeshes = stressed.ToArray();
            manifest.RupturedMeshes = ruptured.ToArray();
            manifest.CollapsedMeshes = collapsed.ToArray();
            manifest.StateMappings = mappings.ToArray();
            manifest.CollisionHulls = hulls.ToArray();
            string manifestPath = HabitatDamageBakeConstants.OutputFolder + "/" + Sanitize(prefab.name) + "_DamageManifest.asset";
            HabitatDamageBakeManifest existingManifest = AssetDatabase.LoadAssetAtPath<HabitatDamageBakeManifest>(manifestPath);
            if (existingManifest != null)
            {
                EditorUtility.CopySerialized(manifest, existingManifest);
                EditorUtility.SetDirty(existingManifest);
                Object.DestroyImmediate(manifest);
            }
            else
            {
                AssetDatabase.CreateAsset(manifest, manifestPath);
            }
        }

        private static Mesh SaveStateMesh(string prefabName, Mesh source, HabitatDamageMeshState state, out HabitatDamageStateBakeResult result)
        {
            Mesh mesh = HabitatDamageBakePipeline.BakeDamageState(source, state, in _settings, out result);
            if (mesh == null)
                return null;

            string path = HabitatDamageBakeConstants.OutputFolder + "/" + Sanitize(prefabName) + "_" + Sanitize(source.name) + "_" + state + ".asset";
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existingMesh != null)
            {
                EditorUtility.CopySerialized(mesh, existingMesh);
                EditorUtility.SetDirty(existingMesh);
                Object.DestroyImmediate(mesh);
                result.MeshHash = HabitatDamageBakePipeline.HashLowerAscii(path);
                return existingMesh;
            }

            AssetDatabase.CreateAsset(mesh, path);
            result.MeshHash = HabitatDamageBakePipeline.HashLowerAscii(path);
            return mesh;
        }

        private static void AppendHulls(List<HabitatDamageHullDTO> destination, HabitatDamageHullDTO[] source)
        {
            if (source == null)
                return;
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i].HullHash != 0u)
                    destination.Add(source[i]);
            }
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Unnamed";
            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                    chars[i] = '_';
            }
            return new string(chars);
        }

        private static string ProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        }
    }

    [InitializeOnLoad]
    internal static class HabitatDamageLayoutValidator
    {
        static HabitatDamageLayoutValidator()
        {
            Validate(false);
        }

        [MenuItem("Hecton8/Habitat/Validate Damage Bake Layouts")]
        public static void ValidateMenu()
        {
            Validate(true);
        }

        public static bool Validate(bool logSuccess)
        {
            bool ok = true;
            ok &= Size<ModuleDamageStateMappingDTO>(32);
            ok &= Offset<ModuleDamageStateMappingDTO>(nameof(ModuleDamageStateMappingDTO.PristineMeshHash), 0);
            ok &= Offset<ModuleDamageStateMappingDTO>(nameof(ModuleDamageStateMappingDTO.CollapsedMeshHash), 12);
            ok &= Offset<ModuleDamageStateMappingDTO>(nameof(ModuleDamageStateMappingDTO._pad3), 28);
            ok &= Size<HabitatDamageHullDTO>(64);
            ok &= Size<HabitatDamageBakeTelemetryEntry>(64);
            ok &= Size<HabitatCrushProfileDTO>(32);
            ok &= Size<HabitatDamageBakeSettings>(80);
            ok &= Offset<HabitatDamageBakeSettings>(nameof(HabitatDamageBakeSettings.ModuleAup), 0);
            ok &= Offset<HabitatDamageBakeSettings>(nameof(HabitatDamageBakeSettings.SeaLevelAup), 24);
            ok &= Offset<HabitatDamageBakeSettings>(nameof(HabitatDamageBakeSettings.GlobalQualityWeight), 64);
            ok &= Size<HabitatDamageIndexRangeDTO>(16);
            ok &= Offset<HabitatDamageIndexRangeDTO>(nameof(HabitatDamageIndexRangeDTO.BaseVertex), 12);
            ok &= Size<HabitatDamageSourceVertex>(64);
            ok &= Offset<HabitatDamageSourceVertex>(nameof(HabitatDamageSourceVertex.Uv0), 40);
            ok &= Size<HabitatDamageWorkingVertex>(128);
            ok &= Offset<HabitatDamageWorkingVertex>(nameof(HabitatDamageWorkingVertex.OriginalPosition), 48);
            ok &= Offset<HabitatDamageWorkingVertex>(nameof(HabitatDamageWorkingVertex.Flags), 68);
            ok &= Size<HabitatDamageBakedVertex>(32);
            ok &= Offset<HabitatDamageBakedVertex>(nameof(HabitatDamageBakedVertex.Position), 0);
            ok &= Offset<HabitatDamageBakedVertex>(nameof(HabitatDamageBakedVertex.TangentSnorm), 20);
            ok &= Offset<HabitatDamageBakedVertex>(nameof(HabitatDamageBakedVertex.ColorRgba), 28);
            if (ok && logSuccess)
                Debug.Log("[SHINOBU_210] Habitat damage bake layouts validated.");
            return ok;
        }

        private static bool Size<T>(int expected) where T : struct
        {
            int observed = UnsafeUtility.SizeOf<T>();
            if (observed == expected)
                return true;
            Debug.LogError("[SHINOBU_210] Layout size mismatch " + typeof(T).Name + " expected " + expected + " observed " + observed);
            return false;
        }

        private static bool Offset<T>(string fieldName, int expected) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            int observed = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (observed == expected)
                return true;
            Debug.LogError("[SHINOBU_210] Layout offset mismatch " + typeof(T).Name + "." + fieldName + " expected " + expected + " observed " + observed);
            return false;
        }
    }

    public sealed class HabitatCrushForgeWindow : EditorWindow
    {
        private readonly List<HabitatCrushProfileDTO> _profiles = new List<HabitatCrushProfileDTO>(16);
        private readonly List<string> _profileNames = new List<string>(16);
        private ObjectField _folderField;
        private ObjectField _previewMeshField;
        private PopupField<string> _profilePopup;
        private Slider _crushIntensity;
        private Slider _tearThreshold;
        private Slider _materialYield;
        private Slider _stressColor;
        private Slider _quality;
        private Label _status;
        private Vector3[] _previewVertices = Array.Empty<Vector3>();
        private uint[] _previewIndices = Array.Empty<uint>();

        [MenuItem("Hecton8/Habitat/Habitat Crush Forge")]
        public static void Open()
        {
            HabitatCrushForgeWindow window = GetWindow<HabitatCrushForgeWindow>();
            window.titleContent = new GUIContent("Habitat Crush Forge");
            window.minSize = new Vector2(460f, 420f);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawPreview;
            SceneView.duringSceneGui += DrawPreview;
            EditorApplication.update -= RefreshStatus;
            EditorApplication.update += RefreshStatus;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawPreview;
            EditorApplication.update -= RefreshStatus;
        }

        public void CreateGUI()
        {
            HabitatDamageBakePipeline.TryReadProfiles(ProjectRoot(), _profiles, _profileNames);
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            _status = new Label("Idle");
            root.Add(_status);
            _folderField = new ObjectField("Pristine Prefab Folder") { objectType = typeof(DefaultAsset), allowSceneObjects = false };
            _previewMeshField = new ObjectField("Preview Mesh") { objectType = typeof(Mesh), allowSceneObjects = false };
            root.Add(_folderField);
            root.Add(_previewMeshField);
            _profilePopup = new PopupField<string>("Crush Profile", _profileNames, 0);
            _profilePopup.RegisterValueChangedCallback(_ => ApplySelectedProfile());
            root.Add(_profilePopup);

            _crushIntensity = Slider("Crush Intensity", 0f, 1f);
            _tearThreshold = Slider("Tear Threshold", 0f, 1f);
            _materialYield = Slider("Material Yield Strength", 0.01f, 4f);
            _stressColor = Slider("Stress Color Intensity", 0f, 4f);
            _quality = Slider("Global Quality Weight", 0f, 1f);
            root.Add(_crushIntensity);
            root.Add(_tearThreshold);
            root.Add(_materialYield);
            root.Add(_stressColor);
            root.Add(_quality);
            ApplySelectedProfile();

            Button bake = new Button(Bake) { text = "BAKE DAMAGE STATES" };
            Button preview = new Button(RunPreview) { text = "Preview Buckling Overlay" };
            Button benchmark = new Button(RunMockBenchmark) { text = "Run Mock Pressure Benchmark" };
            Button scan = new Button(Runtime_Habitat_Destruction_Scanner.ScanMenu) { text = "Run Runtime Destruction Scanner" };
            root.Add(bake);
            root.Add(preview);
            root.Add(benchmark);
            root.Add(scan);
        }

        private Slider Slider(string label, float min, float max)
        {
            Slider slider = new Slider(label, min, max) { showInputField = true };
            slider.RegisterValueChangedCallback(_ => RunPreview());
            return slider;
        }

        private void ApplySelectedProfile()
        {
            int index = _profilePopup == null ? 0 : math.clamp(_profilePopup.index, 0, _profiles.Count - 1);
            HabitatCrushProfileDTO profile = _profiles.Count > 0 ? _profiles[index] : HabitatDamageBakePipeline.DefaultProfile();
            _crushIntensity?.SetValueWithoutNotify(profile.CrushIntensity);
            _tearThreshold?.SetValueWithoutNotify(profile.TearThreshold);
            _materialYield?.SetValueWithoutNotify(profile.MaterialYieldStrength);
            _stressColor?.SetValueWithoutNotify(profile.StressColorIntensity);
            _quality?.SetValueWithoutNotify(profile.GlobalQualityWeight);
            RunPreview();
        }

        private void Bake()
        {
            string status = HabitatDamageBakePipeline.StartBake(_folderField?.value, CurrentSettings());
            if (_status != null)
                _status.text = status;
        }

        private void RunPreview()
        {
            Mesh mesh = _previewMeshField != null ? _previewMeshField.value as Mesh : null;
            if (mesh == null)
                return;
            HabitatDamageBakePipeline.BuildPreview(mesh, CurrentSettings(), out _previewVertices, out _previewIndices);
            SceneView.RepaintAll();
        }

        private void RunMockBenchmark()
        {
            bool ok = HabitatDamageBakePipeline.RunMockHydrostaticPressureBenchmark(
                192,
                192,
                CurrentSettings(),
                out long ticks,
                out int vertexCount);
            double milliseconds = ticks * 1000.0 / Stopwatch.Frequency;
            if (_status != null)
            {
                _status.text = ok
                    ? "Mock pressure benchmark: " + vertexCount + " vertices, " + milliseconds.ToString("0.000") + " ms"
                    : "Mock pressure benchmark failed";
            }
        }

        private HabitatDamageBakeSettings CurrentSettings()
        {
            return new HabitatDamageBakeSettings
            {
                CrushIntensity = _crushIntensity != null ? _crushIntensity.value : 0.72f,
                TearThreshold = _tearThreshold != null ? _tearThreshold.value : 0.18f,
                MaterialYieldStrength = _materialYield != null ? _materialYield.value : 1f,
                StressColorIntensity = _stressColor != null ? _stressColor.value : 1f,
                GlobalQualityWeight = _quality != null ? _quality.value : 1f,
                ModuleAup = new double3(0d, -1000d, 0d),
                SeaLevelAup = new double3(0d, HabitatDamageBakeConstants.DefaultSeaLevelAupY, 0d)
            };
        }

        private void DrawPreview(SceneView sceneView)
        {
            if (_previewVertices == null || _previewIndices == null || _previewVertices.Length == 0)
                return;
            Handles.color = new Color(1f, 0.32f, 0.08f, 0.65f);
            int lineBudget = math.min(_previewIndices.Length / 3, 2500);
            for (int tri = 0; tri < lineBudget; tri++)
            {
                int src = tri * 3;
                int a = (int)_previewIndices[src];
                int b = (int)_previewIndices[src + 1];
                int c = (int)_previewIndices[src + 2];
                if ((uint)a >= (uint)_previewVertices.Length || (uint)b >= (uint)_previewVertices.Length || (uint)c >= (uint)_previewVertices.Length)
                    continue;
                if (a == b || b == c || a == c)
                    continue;
                Handles.DrawLine(_previewVertices[a], _previewVertices[b]);
                Handles.DrawLine(_previewVertices[b], _previewVertices[c]);
                Handles.DrawLine(_previewVertices[c], _previewVertices[a]);
            }
        }

        private void RefreshStatus()
        {
            if (_status != null && HabitatDamageBakeQueue.Active)
                _status.text = HabitatDamageBakeQueue.Status;
        }

        private static string ProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        }
    }

    public static class Runtime_Habitat_Destruction_Scanner
    {
        private const int MaxScannerFileBytes = 16 * 1024 * 1024;

        private static readonly string[] ForbiddenRuntimePatterns =
        {
            ".sharedMesh.vertices",
            ".mesh.vertices",
            ".vertices =",
            "SkinnedMeshRenderer",
            "blendShape",
            "AddComponent<Rigidbody",
            "GetComponent<Rigidbody",
            "new Rigidbody",
            "Instantiate(",
            "ParticleSystem",
            "MeshCollider.sharedMesh",
            "RecalculateNormals",
            "StateRingBuffer"
        };

        [MenuItem("Hecton8/Habitat/Runtime Habitat Destruction Scanner")]
        public static void ScanMenu()
        {
            Scan(ProjectRoot());
        }

        public static void Scan(string projectRoot)
        {
            string scriptsRoot = Path.Combine(projectRoot, "Assets/_Project/Scripts");
            string[] roots =
            {
                Path.Combine(scriptsRoot, "Habitat"),
                Path.Combine(scriptsRoot, "Environment")
            };
            List<string> findings = new List<string>(32);
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                if (!Directory.Exists(roots[rootIndex]))
                    continue;
                string[] files = Directory.GetFiles(roots[rootIndex], "*.cs", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                {
                    string file = files[fileIndex];
                    string normalized = file.Replace('\\', '/');
                    if (normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    ScanCodeFile(projectRoot, file, findings);
                }
            }

            string reportPath = Path.Combine(projectRoot, HabitatDamageBakeConstants.ScannerReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            bool previousReportPreserved = TryPreservePreviousScannerReport(
                projectRoot,
                reportPath,
                out long previousReportBytes,
                out uint previousReportHash);
            StringBuilder sb = new StringBuilder(1024);
            sb.Append("{\n");
            sb.Append("  \"agent\": \"SHINOBU_210\",\n");
            sb.Append("  \"verdict\": \"").Append(findings.Count == 0 ? "Runtime Habitat Deformations Eradicated" : "CRITICAL_WARNING").Append("\",\n");
            sb.Append("  \"evidenceClass\": \"STATIC_SOURCE_SCAN\",\n");
            sb.Append("  \"scannerMode\": \"COMMENT_STRING_AWARE_NATIVE_BYTE_TOKEN_SCAN\",\n");
            sb.Append("  \"forbiddenRuntimePatternCount\": ").Append(findings.Count).Append(",\n");
            sb.Append("  \"netcodeFence\": \"Baked mesh assets are immutable environmental data; scanner flags StateRingBuffer references in Habitat/Environment runtime.\",\n");
            sb.Append("  \"previousReportPreserved\": ").Append(previousReportPreserved ? "true" : "false").Append(",\n");
            sb.Append("  \"previousReportSidecar\": \"").Append(HabitatDamageBakeConstants.PreviousScannerReportPath).Append("\",\n");
            sb.Append("  \"previousReportBytes\": ").Append(previousReportBytes).Append(",\n");
            sb.Append("  \"previousReportFnv1a\": ").Append(previousReportHash).Append(",\n");
            sb.Append("  \"findings\": [");
            for (int i = 0; i < findings.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append('"').Append(EscapeJson(findings[i])).Append('"');
            }
            sb.Append("]\n");
            sb.Append("}\n");
            File.WriteAllText(reportPath, sb.ToString());
            AssetDatabase.Refresh();
            if (findings.Count == 0)
                Debug.Log("[SHINOBU_210] Runtime Habitat Deformations Eradicated. Report: " + HabitatDamageBakeConstants.ScannerReportPath);
            else
                Debug.LogWarning("[SHINOBU_210] Runtime habitat destruction scanner found " + findings.Count + " forbidden pattern(s).");
        }

        private static bool TryPreservePreviousScannerReport(string projectRoot, string reportPath, out long byteCount, out uint hash)
        {
            byteCount = 0L;
            hash = 0u;
            if (!File.Exists(reportPath))
                return false;

            string sidecarPath = Path.Combine(projectRoot, HabitatDamageBakeConstants.PreviousScannerReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(sidecarPath));
            uint runningHash = 2166136261u;
            Span<byte> buffer = stackalloc byte[4096];
            using (FileStream input = new FileStream(reportPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, buffer.Length, FileOptions.SequentialScan))
            using (FileStream output = new FileStream(sidecarPath, FileMode.Create, FileAccess.Write, FileShare.Read, buffer.Length, FileOptions.SequentialScan))
            {
                while (true)
                {
                    int read = input.Read(buffer);
                    if (read <= 0)
                        break;
                    ReadOnlySpan<byte> slice = buffer.Slice(0, read);
                    for (int i = 0; i < slice.Length; i++)
                        runningHash = (runningHash ^ slice[i]) * 16777619u;
                    output.Write(slice);
                    byteCount += read;
                }
            }

            hash = byteCount == 0L ? 0u : runningHash;
            return true;
        }

        private static unsafe void ScanCodeFile(string projectRoot, string file, List<string> findings)
        {
            long fileLength = new FileInfo(file).Length;
            if (fileLength <= 0)
                return;

            if (fileLength > MaxScannerFileBytes)
            {
                findings.Add(Relativize(projectRoot, file) + ":1 SCANNER_SKIPPED_OVERSIZE_FILE");
                return;
            }

            NativeArray<byte> bytes = HabitatDamageBakePipeline.AllocateTrackedNativeArray<byte>(
                (int)fileLength,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory,
                "scannerFileBytes");
            try
            {
                byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(bytes);
                Span<byte> destination = new Span<byte>(ptr, bytes.Length);
                int totalRead = 0;
                using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan))
                {
                    while (totalRead < destination.Length)
                    {
                        int read = stream.Read(destination.Slice(totalRead));
                        if (read <= 0)
                            break;
                        totalRead += read;
                    }
                }

                int line = 1;
                bool inLineComment = false;
                bool inBlockComment = false;
                bool inString = false;
                bool inVerbatimString = false;
                bool inRawString = false;
                bool inChar = false;

                for (int i = 0; i < totalRead; i++)
                {
                    byte current = bytes[i];
                    byte next = i + 1 < totalRead ? bytes[i + 1] : (byte)0;
                    byte next2 = i + 2 < totalRead ? bytes[i + 2] : (byte)0;

                    if (current == (byte)'\n')
                    {
                        line++;
                        inLineComment = false;
                        continue;
                    }

                    if (inLineComment)
                        continue;

                    if (inBlockComment)
                    {
                        if (current == (byte)'*' && next == (byte)'/')
                        {
                            inBlockComment = false;
                            i++;
                        }
                        continue;
                    }

                    if (inRawString)
                    {
                        if (current == (byte)'"' && next == (byte)'"' && next2 == (byte)'"')
                        {
                            inRawString = false;
                            i += 2;
                        }
                        continue;
                    }

                    if (inString)
                    {
                        if (inVerbatimString)
                        {
                            if (current == (byte)'"' && next == (byte)'"')
                            {
                                i++;
                                continue;
                            }

                            if (current == (byte)'"')
                            {
                                inString = false;
                                inVerbatimString = false;
                            }
                            continue;
                        }

                        if (current == (byte)'\\')
                        {
                            i++;
                            continue;
                        }

                        if (current == (byte)'"')
                            inString = false;
                        continue;
                    }

                    if (inChar)
                    {
                        if (current == (byte)'\\')
                        {
                            i++;
                            continue;
                        }

                        if (current == (byte)'\'')
                            inChar = false;
                        continue;
                    }

                    if (current == (byte)'/' && next == (byte)'/')
                    {
                        inLineComment = true;
                        i++;
                        continue;
                    }

                    if (current == (byte)'/' && next == (byte)'*')
                    {
                        inBlockComment = true;
                        i++;
                        continue;
                    }

                    if (current == (byte)'@' && next == (byte)'"')
                    {
                        inString = true;
                        inVerbatimString = true;
                        i++;
                        continue;
                    }

                    if (current == (byte)'"' && next == (byte)'"' && next2 == (byte)'"')
                    {
                        inRawString = true;
                        i += 2;
                        continue;
                    }

                    if (current == (byte)'"')
                    {
                        inString = true;
                        inVerbatimString = false;
                        continue;
                    }

                    if (current == (byte)'\'')
                    {
                        inChar = true;
                        continue;
                    }

                    for (int patternIndex = 0; patternIndex < ForbiddenRuntimePatterns.Length; patternIndex++)
                    {
                        if (MatchesAscii(bytes, totalRead, i, ForbiddenRuntimePatterns[patternIndex]))
                            findings.Add(Relativize(projectRoot, file) + ":" + line + " " + ForbiddenRuntimePatterns[patternIndex]);
                    }
                }
            }
            finally
            {
                HabitatDamageBakePipeline.DisposeTrackedNativeArray(ref bytes);
            }
        }

        private static bool MatchesAscii(NativeArray<byte> bytes, int length, int offset, string pattern)
        {
            if (offset + pattern.Length > length)
                return false;

            for (int i = 0; i < pattern.Length; i++)
            {
                if (bytes[offset + i] != (byte)pattern[i])
                    return false;
            }

            return true;
        }

        private static string ProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        }

        private static string Relativize(string root, string path)
        {
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/')
                : path.Replace('\\', '/');
        }

        private static string EscapeJson(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
#endif
