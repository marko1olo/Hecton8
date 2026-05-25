#if UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Rendering.OceanSinglePass;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    [InitializeOnLoad]
    internal static class OceanRenderLayoutValidator
    {
        static OceanRenderLayoutValidator()
        {
            ValidateLayouts();
        }

        [MenuItem("HECTON-8/Rendering/Validate Single-Pass Ocean Layouts")]
        public static void ValidateLayouts()
        {
            AssertSize<OceanVisualOverridesDTO>(OceanSinglePassConstants.VisualOverridesStrideBytes);
            AssertOffset<OceanVisualOverridesDTO>(nameof(OceanVisualOverridesDTO.FoamAndShadowParams), 0);
            AssertOffset<OceanVisualOverridesDTO>(nameof(OceanVisualOverridesDTO.ShorelineDepthParams), 16);
            AssertNoProperties<OceanVisualOverridesDTO>();

            AssertSize<OceanGuillotineTuningDTO>(OceanSinglePassConstants.TuningStrideBytes);
            AssertSize<OceanAestheticProfileDTO>(OceanSinglePassConstants.AestheticProfileStrideBytes);
            AssertSize<OceanRenderTelemetryEntry>(OceanSinglePassConstants.TelemetryEntryStrideBytes);
            AssertSize<OceanMockRenderStateDTO>(64);
            AssertOffset<OceanRenderTelemetryEntry>(nameof(OceanRenderTelemetryEntry.WakeScrollOffset), 32);
            AssertOffset<OceanRenderTelemetryEntry>(nameof(OceanRenderTelemetryEntry.StateHash), 48);
        }

        [MenuItem("HECTON-8/Rendering/Validate Crest Guillotine Source")]
        public static void ValidateCrestGuillotineSource()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                throw new InvalidOperationException("Project root unavailable.");

            AssertFileContains(projectRoot, "Assets/Crest/Crest/Scripts/LodData/OceanDepthCache.cs", "HectonRealtimeDepthCacheDisabled = true");
            AssertFileContains(projectRoot, "Assets/Crest/Crest/Scripts/Reflection/OceanPlanarReflection.cs", "HectonPlanarReflectionDisabled = true");
            AssertFileContains(projectRoot, "Assets/_Project/Scripts/Plugins/Crest/HectonCrestOceanDepthCacheRuntimeBridge.cs", "HectonRuntimeDepthCacheCameraDisabled = true");
            AssertFileContains(projectRoot, "Assets/_Project/Scripts/Plugins/Crest/HectonCrestOceanDepthCacheBootstrap.cs", "HectonRuntimeDepthCacheCameraDisabled = true");
            AssertFileContains(projectRoot, "Assets/_Project/Prefabs/Ocean_Crest.prefab", "_createSeaFloorDepthData: 0");
            AssertFileContains(projectRoot, "Assets/_Project/Prefabs/Ocean_Crest.prefab", "_createFoamSim: 0");
        }

        private static void AssertSize<T>(int expectedBytes) where T : struct
        {
            int actualBytes = UnsafeUtility.SizeOf<T>();
            if (actualBytes != expectedBytes)
                throw new InvalidOperationException(typeof(T).Name + " size " + actualBytes + " != " + expectedBytes);
        }

        private static void AssertOffset<T>(string fieldName, int expectedOffset) where T : struct
        {
            int actualOffset = Marshal.OffsetOf<T>(fieldName).ToInt32();
            if (actualOffset != expectedOffset)
                throw new InvalidOperationException(typeof(T).Name + "." + fieldName + " offset " + actualOffset + " != " + expectedOffset);
        }

        private static void AssertNoProperties<T>() where T : struct
        {
            if (typeof(T).GetProperties().Length != 0)
                throw new InvalidOperationException(typeof(T).Name + " must expose raw fields only.");
        }

        private static void AssertFileContains(string projectRoot, string relativePath, string requiredToken)
        {
            string absolutePath = Path.Combine(projectRoot, relativePath);
            if (!File.Exists(absolutePath))
                throw new FileNotFoundException(relativePath);

            foreach (string line in File.ReadLines(absolutePath))
            {
                if (line.IndexOf(requiredToken, StringComparison.Ordinal) >= 0)
                    return;
            }

            throw new InvalidOperationException(relativePath + " missing " + requiredToken);
        }
    }
}
#endif
