// File: Scripts/Visor/VisorOpaqueTextureEnsurer.cs
// Убеждаемся что Opaque Texture включена (нужна для рефракции)
using UnityEngine;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;

namespace NASAPunk.Visor
{
    [InitializeOnLoad]
    public static class VisorOpaqueTextureEnsurer
    {
        static VisorOpaqueTextureEnsurer()
        {
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline
                as UniversalRenderPipelineAsset;

            if (pipeline != null && !pipeline.supportsCameraOpaqueTexture)
            {
                Debug.LogWarning(
                    "[SuitVisor] Opaque Texture is disabled in URP Asset. " +
                    "Refraction won't work. Enable it in URP Settings → " +
                    "General → Opaque Texture.");
            }
        }
    }
}
#endif