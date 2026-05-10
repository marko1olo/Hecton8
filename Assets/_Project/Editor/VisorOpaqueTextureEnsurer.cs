// File: Scripts/Visor/VisorOpaqueTextureEnsurer.cs
// Ubezhdaemsya chto Opaque Texture vklyuchena (nuzhna dlya refraktsii)
using UnityEngine;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;

namespace NASAPunk.Visor
{
    public static class VisorOpaqueTextureEnsurer
    {
        [MenuItem("Tools/Hecton/Dev/Scene/Validate Visor Opaque Texture", priority = 232)]
        private static void ValidateOpaqueTextureSupport()
        {
            CheckOpaqueTextureSupport();
        }

        private static void CheckOpaqueTextureSupport()
        {
            EditorApplication.delayCall -= CheckOpaqueTextureSupport;

            if (InternalEditorUtility.inBatchMode)
                return;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += CheckOpaqueTextureSupport;
                return;
            }

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
