using System;
using System.IO;
using System.Reflection;
using Crest;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Cold-path reflection bridge for the Crest depth-cache runtime API.
    /// </summary>
    internal static class HectonCrestOceanDepthCacheRuntimeBridge
    {
        private const float HectonMinimumCameraHeightAboveSeaLevel = 8f;
        private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo LayersField = ResolveField("_layers");
        private static readonly FieldInfo ResolutionField = ResolveField("_resolution");
        private static readonly FieldInfo TypeField = ResolveField("_type");
        private static readonly FieldInfo RefreshModeField = ResolveField("_refreshMode");
        private static readonly FieldInfo CameraMaxTerrainHeightField = ResolveField("_cameraMaxTerrainHeight");
        private static readonly FieldInfo TerrainPixelErrorOverrideField = ResolveField("_terrainPixelErrorOverride");
        private static readonly FieldInfo LodBiasOverrideField = ResolveField("_lodBiasOverride");
        private static readonly FieldInfo MaximumLodLevelOverrideField = ResolveField("_maximumLodLevelOverride");
        private static readonly FieldInfo RelativeField = ResolveField("_relative");
        private static readonly FieldInfo CameraField = ResolveField("_camDepthCache");
        private static readonly FieldInfo CacheTextureField = ResolveField("_cacheTexture");
        private static readonly MethodInfo InitObjectsMethod = typeof(OceanDepthCache).GetMethod("InitObjects", InstanceFlags, null, new[] { typeof(bool) }, null);

        internal static void HectonConfigureRealtimeCapture(
            this OceanDepthCache depthCache,
            int layerMask,
            int resolution,
            float cameraMaxTerrainHeight,
            bool relativeToSeaLevel)
        {
            if (depthCache == null)
                return;

            SetFieldValue(LayersField, depthCache, layerMask);
            SetFieldValue(ResolutionField, depthCache, Mathf.Clamp(resolution, 128, 1024));
            SetEnumFieldValue(TypeField, depthCache, "Realtime");
            SetEnumFieldValue(RefreshModeField, depthCache, "OnDemand");
            SetFieldValue(
                CameraMaxTerrainHeightField,
                depthCache,
                Mathf.Max(HectonMinimumCameraHeightAboveSeaLevel, cameraMaxTerrainHeight));
            SetFieldValue(TerrainPixelErrorOverrideField, depthCache, 0f);
            SetFieldValue(LodBiasOverrideField, depthCache, Mathf.Infinity);
            SetFieldValue(MaximumLodLevelOverrideField, depthCache, 0);
            SetFieldValue(RelativeField, depthCache, relativeToSeaLevel);
        }

        internal static Camera HectonEnsureCaptureCamera(this OceanDepthCache depthCache, bool updateComponents)
        {
            if (depthCache == null)
                return null;

            if (InitObjectsMethod == null)
                return null;

            object initResult = InitObjectsMethod.Invoke(depthCache, new object[] { updateComponents });
            if (initResult is bool initialized && !initialized)
                return null;

            return CameraField?.GetValue(depthCache) as Camera;
        }

        internal static void HectonAlignCaptureCamera(
            this OceanDepthCache depthCache,
            Camera captureCamera,
            Vector3 runtimeCacheCenter,
            float cameraMaxTerrainHeight,
            float cameraFarPlane,
            float coverageSize,
            int layerMask)
        {
            if (depthCache == null || captureCamera == null)
                return;

            float resolvedCameraHeight = Mathf.Max(HectonMinimumCameraHeightAboveSeaLevel, cameraMaxTerrainHeight);
            Transform cameraTransform = captureCamera.transform;
            cameraTransform.position = runtimeCacheCenter + Vector3.up * resolvedCameraHeight;
            cameraTransform.rotation = Quaternion.Euler(90f, 0f, 0f);
            captureCamera.orthographic = true;
            captureCamera.orthographicSize = Mathf.Max(coverageSize * 0.5f, 1f);
            captureCamera.nearClipPlane = 0.05f;
            captureCamera.farClipPlane = Mathf.Max(cameraFarPlane, resolvedCameraHeight + 8f);
            captureCamera.cullingMask = layerMask;
        }

        internal static bool HectonSaveDepthCacheTexturePng(this OceanDepthCache depthCache, string absolutePath)
        {
            if (depthCache == null || string.IsNullOrWhiteSpace(absolutePath))
                return false;

            RenderTexture cacheTexture = CacheTextureField?.GetValue(depthCache) as RenderTexture;
            if (cacheTexture == null)
                return false;

            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            RenderTexture previousActive = RenderTexture.active;
            Texture2D readbackTexture = null;
            try
            {
                RenderTexture.active = cacheTexture;
                readbackTexture = new Texture2D(cacheTexture.width, cacheTexture.height, TextureFormat.RGBA32, false, true)
                {
                    name = "__HectonDepthCacheDebugReadback",
                    hideFlags = HideFlags.HideAndDontSave
                }; // COLD ALLOC: Texture2D[1] — one-shot depth-cache forensic readback for PNG dump — owner: HectonCrestOceanDepthCacheRuntimeBridge
                readbackTexture.ReadPixels(new Rect(0f, 0f, cacheTexture.width, cacheTexture.height), 0, 0, false);
                readbackTexture.Apply(false, false);
                File.WriteAllBytes(absolutePath, readbackTexture.EncodeToPNG());
                return true;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (readbackTexture != null)
                    UnityEngine.Object.DestroyImmediate(readbackTexture);
            }
        }

        private static FieldInfo ResolveField(string name)
        {
            return typeof(OceanDepthCache).GetField(name, InstanceFlags);
        }

        private static void SetFieldValue(FieldInfo field, object target, object value)
        {
            if (field == null || target == null)
                return;

            if (field.FieldType == typeof(LayerMask) && value is int layerMaskValue)
            {
                field.SetValue(target, (LayerMask)layerMaskValue);
                return;
            }

            field.SetValue(target, value);
        }

        private static void SetEnumFieldValue(FieldInfo field, object target, string enumName)
        {
            if (field == null || target == null || string.IsNullOrEmpty(enumName))
                return;

            Type enumType = field.FieldType;
            if (!enumType.IsEnum)
                return;

            try
            {
                object enumValue = Enum.Parse(enumType, enumName, ignoreCase: true);
                field.SetValue(target, enumValue);
            }
            catch (ArgumentException)
            {
            }
        }
    }
}
