using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// GPU payload for one far-field octahedral impostor instance.
    /// Centers are authored in universe space; the shader applies the current AUP render offset.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
    public struct OctahedralImpostorInstance
    {
        public Vector4 CenterFade;
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
            Vector3 safeSize = new Vector3(
                Mathf.Max(0.5f, size.x),
                Mathf.Max(0.5f, size.y),
                Mathf.Max(0.5f, size.z));

            return new OctahedralImpostorInstance
            {
                CenterFade = new Vector4(
                    universeCenter.x,
                    universeCenter.y,
                    universeCenter.z,
                    Mathf.Clamp01(fade01)),
                SizeFlags = new Vector4(
                    safeSize.x,
                    safeSize.y,
                    safeSize.z,
                    flags)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bounds ToUniverseBounds()
        {
            return new Bounds(
                new Vector3(CenterFade.x, CenterFade.y, CenterFade.z),
                new Vector3(SizeFlags.x, SizeFlags.y, SizeFlags.z));
        }
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
        public const byte FlagLowTierSnap = 1 << 2;
        public const byte FlagDitherBlend = 1 << 3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldUseImpostor(double distanceSq, float enterDistanceMeters)
        {
            float safeDistance = Mathf.Max(1f, enterDistanceMeters);
            return distanceSq > (double)safeDistance * safeDistance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLowTier(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.Unknown ||
                   tier == HectonQualityTier.Low ||
                   tier == HectonQualityTier.Mx350;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ResolveFlags(double distanceSq, float enterDistanceMeters, HectonQualityTier tier)
        {
            bool impostor = ShouldUseImpostor(distanceSq, enterDistanceMeters);
            bool lowTier = IsLowTier(tier);
            byte flags = impostor ? FlagUseImpostor : FlagRealGeometry;
            flags |= lowTier ? FlagLowTierSnap : FlagDitherBlend;
            return flags;
        }
    }
}
