using System.Reflection;
using Hecton8.World.OfflineHadalArchBaker;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.World.OfflineHadalArchBaker.Editor
{
    [InitializeOnLoad]
    public static class HadalArchLayoutValidator
    {
        static HadalArchLayoutValidator()
        {
            Validate(logSuccess: false);
        }

        [MenuItem("HECTON-8/Hadal Structure Forge/Validate DTO Layouts")]
        public static void ValidateMenu()
        {
            Validate(logSuccess: true);
        }

        public static bool Validate(bool logSuccess)
        {
            bool ok = true;
            ok &= ValidateSize<SdfShapeDTO>(64);
            ok &= ValidateOffset<SdfShapeDTO>(nameof(SdfShapeDTO.ShapeType), 0);
            ok &= ValidateOffset<SdfShapeDTO>(nameof(SdfShapeDTO.Operation), 4);
            ok &= ValidateOffset<SdfShapeDTO>(nameof(SdfShapeDTO.Position), 8);
            ok &= ValidateOffset<SdfShapeDTO>(nameof(SdfShapeDTO.Extents), 20);
            ok &= ValidateOffset<SdfShapeDTO>(nameof(SdfShapeDTO.BlendRadius), 32);
            ok &= ValidateOffset<SdfShapeDTO>(nameof(SdfShapeDTO.NoiseWeight), 36);
            ok &= ValidateOffset<SdfShapeDTO>(nameof(SdfShapeDTO.Flags), 40);
            ok &= ValidateOffset<SdfShapeDTO>(nameof(SdfShapeDTO.MaterialHash), 44);
            ok &= ValidateOffset<SdfShapeDTO>(nameof(SdfShapeDTO._pad0), 48);
            ok &= ValidateOffset<SdfShapeDTO>(nameof(SdfShapeDTO._pad1), 56);
            ok &= ValidateSize<HadalArchVertexDTO>(64);
            ok &= ValidateOffset<HadalArchVertexDTO>(nameof(HadalArchVertexDTO.Position), 0);
            ok &= ValidateOffset<HadalArchVertexDTO>(nameof(HadalArchVertexDTO.Normal), 12);
            ok &= ValidateOffset<HadalArchVertexDTO>(nameof(HadalArchVertexDTO.Tangent), 24);
            ok &= ValidateOffset<HadalArchVertexDTO>(nameof(HadalArchVertexDTO.Uv0), 40);
            ok &= ValidateOffset<HadalArchVertexDTO>(nameof(HadalArchVertexDTO.PackedColor), 48);
            ok &= ValidateOffset<HadalArchVertexDTO>(nameof(HadalArchVertexDTO.Uv3AupLocal), 52);
            ok &= ValidateSize<HadalArchBakeConfigDTO>(128);
            ok &= ValidateOffset<HadalArchBakeConfigDTO>(nameof(HadalArchBakeConfigDTO.CenterAup), 0);
            ok &= ValidateOffset<HadalArchBakeConfigDTO>(nameof(HadalArchBakeConfigDTO.VolumeOriginAup), 24);
            ok &= ValidateOffset<HadalArchBakeConfigDTO>(nameof(HadalArchBakeConfigDTO.Resolution), 48);
            ok &= ValidateOffset<HadalArchBakeConfigDTO>(nameof(HadalArchBakeConfigDTO.VoxelSize), 60);
            ok &= ValidateOffset<HadalArchBakeConfigDTO>(nameof(HadalArchBakeConfigDTO.GlobalQualityWeight), 64);
            ok &= ValidateOffset<HadalArchBakeConfigDTO>(nameof(HadalArchBakeConfigDTO.NoiseFrequency), 68);
            ok &= ValidateOffset<HadalArchBakeConfigDTO>(nameof(HadalArchBakeConfigDTO.NoiseAmplitude), 72);
            ok &= ValidateOffset<HadalArchBakeConfigDTO>(nameof(HadalArchBakeConfigDTO.CavityRayDistance), 76);
            ok &= ValidateOffset<HadalArchBakeConfigDTO>(nameof(HadalArchBakeConfigDTO.CavityRayCount), 80);
            ok &= ValidateOffset<HadalArchBakeConfigDTO>(nameof(HadalArchBakeConfigDTO.Seed), 84);
            ok &= ValidateOffset<HadalArchBakeConfigDTO>(nameof(HadalArchBakeConfigDTO.Flags), 88);
            ok &= ValidateOffset<HadalArchBakeConfigDTO>(nameof(HadalArchBakeConfigDTO.ShapeCount), 92);
            ok &= ValidateOffset<HadalArchBakeConfigDTO>(nameof(HadalArchBakeConfigDTO.Lod1KeepRatio), 96);
            ok &= ValidateOffset<HadalArchBakeConfigDTO>(nameof(HadalArchBakeConfigDTO.Lod2KeepRatio), 100);
            ok &= ValidateOffset<HadalArchBakeConfigDTO>(nameof(HadalArchBakeConfigDTO.SurfaceBand), 104);
            ok &= ValidateOffset<HadalArchBakeConfigDTO>(nameof(HadalArchBakeConfigDTO.NoiseSeedJitter), 108);
            ok &= ValidateOffset<HadalArchBakeConfigDTO>(nameof(HadalArchBakeConfigDTO._pad2), 120);
            ok &= ValidateSize<HadalArchBakeTelemetryEntry>(64);
            ok &= ValidateSize<HadalStaticGeometryRollbackExclusionDTO>(32);

            if (ok && logSuccess)
                Debug.Log("[SHINOBU_215] Hadal arch DTO layouts validated.");

            return ok;
        }

        private static bool ValidateSize<T>(int expected) where T : struct
        {
            int observed = UnsafeUtility.SizeOf<T>();
            if (observed == expected)
                return true;

            Debug.LogError("[SHINOBU_215] Layout size mismatch: " + typeof(T).Name + " expected " + expected + " observed " + observed);
            return false;
        }

        private static bool ValidateOffset<T>(string fieldName, int expected) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            int observed = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (observed == expected)
                return true;

            Debug.LogError("[SHINOBU_215] Layout offset mismatch: " + typeof(T).Name + "." + fieldName + " expected " + expected + " observed " + observed);
            return false;
        }
    }
}
