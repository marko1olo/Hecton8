using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// GPU payload for one far-field octahedral impostor instance.
    /// Centers are authored in universe space; the shader applies the current AUP render offset.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct OctahedralImpostorInstance
    {
        [FieldOffset(0)]
        public Vector4 CenterFade;

        [FieldOffset(16)]
        public Vector4 SizeFlags;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OctahedralImpostorInstance Create(
            Vector3 universeCenter,
            Vector3 size,
            float fade01,
            float depthMeters,
            uint flags)
        {
            _ = depthMeters;
            float3 rawCenter = new float3(universeCenter.x, universeCenter.y, universeCenter.z);
            float3 rawSize = new float3(size.x, size.y, size.z);
            float3 safeCenter = math.select(float3.zero, rawCenter, math.isfinite(rawCenter));
            float3 safeSize = math.max(new float3(0.5f), math.select(new float3(0.5f), rawSize, math.isfinite(rawSize)));
            float safeFade = math.saturate(math.select(1f, fade01, math.isfinite(fade01)));

            return new OctahedralImpostorInstance
            {
                CenterFade = new Vector4(
                    safeCenter.x,
                    safeCenter.y,
                    safeCenter.z,
                    safeFade),
                SizeFlags = new Vector4(
                    safeSize.x,
                    safeSize.y,
                    safeSize.z,
                    math.asfloat(flags))
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OctahedralImpostorInstance CreateCameraRelative(
            in AbsoluteUniversePosition impostorAup,
            in AbsoluteUniversePosition cameraAup,
            Vector3 size,
            float fade01,
            uint flags)
        {
            float3 local = AbsoluteUniversePosition.ToCameraRelativeFloat3(in impostorAup, in cameraAup);
            return Create(new Vector3(local.x, local.y, local.z), size, fade01, 0f, flags);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bounds ToUniverseBounds()
        {
            float3 rawCenter = new float3(CenterFade.x, CenterFade.y, CenterFade.z);
            float3 rawSize = new float3(SizeFlags.x, SizeFlags.y, SizeFlags.z);
            float3 safeCenter = math.select(float3.zero, rawCenter, math.isfinite(rawCenter));
            float3 safeSize = math.max(new float3(0.5f), math.select(new float3(0.5f), rawSize, math.isfinite(rawSize)));
            return new Bounds(
                new Vector3(safeCenter.x, safeCenter.y, safeCenter.z),
                new Vector3(safeSize.x, safeSize.y, safeSize.z));
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct ImpostorConfigDTO
    {
        [FieldOffset(0)]
        public float2 AtlasGridSize;

        [FieldOffset(8)]
        public float DepthScale;

        [FieldOffset(12)]
        public uint Flags;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ImpostorConfigDTO Create(float2 atlasGridSize, float depthScale, uint flags)
        {
            float2 safeGridSize = math.select(new float2(1f, 1f), atlasGridSize, math.isfinite(atlasGridSize));
            float safeDepthScale = math.select(1f, depthScale, math.isfinite(depthScale));
            return new ImpostorConfigDTO
            {
                AtlasGridSize = math.max(safeGridSize, new float2(1f, 1f)),
                DepthScale = math.max(0.01f, safeDepthScale),
                Flags = flags
            };
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 160)]
    public struct HlodImpostorCaptureAngleRecord
    {
        [FieldOffset(0)]
        public float3 Direction;

        [FieldOffset(12)]
        public float OrthoSize;

        [FieldOffset(16)]
        public float3 CameraPosition;

        [FieldOffset(28)]
        public float CameraDistance;

        [FieldOffset(32)]
        public float4x4 ViewMatrix;

        [FieldOffset(96)]
        public float4x4 ProjectionMatrix;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HlodImpostorMockPoint
    {
        [FieldOffset(0)]
        public float3 Position;

        [FieldOffset(12)]
        public float RadiusMeters;

        [FieldOffset(16)]
        public float3 Normal;

        [FieldOffset(28)]
        public uint StableHash;
    }

    /// <summary>
    /// Distance and quality rules shared by streaming and impostor rendering.
    /// </summary>
    public static class HectonChunkImpostorResidency
    {
        public const float DefaultImpostorEnterDistanceMeters = 500f;
        public const float DefaultRealGeometryReturnDistanceMeters = 475f;
        public const byte FlagUseImpostor = 1 << 0;
        public const byte FlagRealGeometry = 1 << 1;
        public const byte FlagSurvivalSnap = 1 << 2;
        public const byte FlagDitherBlend = 1 << 3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldUseImpostor(double distanceSq, float enterDistanceMeters)
        {
            if (!math.isfinite(distanceSq) || distanceSq < 0d)
            {
                return false;
            }

            float safeDistance = math.max(1f, math.select(DefaultImpostorEnterDistanceMeters, enterDistanceMeters, math.isfinite(enterDistanceMeters)));
            return distanceSq > (double)safeDistance * safeDistance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveContinuousEnterDistanceMeters(float baseDistanceMeters, float globalQualityWeight)
        {
            float safeBaseDistance = math.max(1f, math.select(DefaultImpostorEnterDistanceMeters, baseDistanceMeters, math.isfinite(baseDistanceMeters)));
            float q = math.saturate(math.select(0f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            float survival = math.max(1f, safeBaseDistance * 0.58f);
            float middle = math.max(survival, safeBaseDistance);
            float overkill = math.max(middle, safeBaseDistance * 1.65f);
            float shaped = q * q * (3f - 2f * q);
            float survivalToMiddle = math.lerp(survival, middle, math.saturate(shaped * 2f));
            float middleToOverkill = math.lerp(middle, overkill, math.saturate((shaped - 0.5f) * 2f));
            float overkillGate = math.smoothstep(0.45f, 0.55f, q);
            return math.lerp(survivalToMiddle, middleToOverkill, overkillGate);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ResolveFlags(double distanceSq, float baseEnterDistanceMeters, float globalQualityWeight)
        {
            float enterDistanceMeters = ResolveContinuousEnterDistanceMeters(baseEnterDistanceMeters, globalQualityWeight);
            bool impostor = ShouldUseImpostor(distanceSq, enterDistanceMeters);
            byte flags = impostor ? FlagUseImpostor : FlagRealGeometry;
            flags |= FlagDitherBlend;
            return flags;
        }
    }
}
