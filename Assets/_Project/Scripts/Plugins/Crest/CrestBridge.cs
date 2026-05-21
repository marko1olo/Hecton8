using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Physics
{
    /// <summary>
    /// Crest anti-corruption bridge base. Concrete Crest version adapters own all Crest API calls.
    /// </summary>
    public abstract class CrestBridge : HectonOceanKinematicsBridgeBase, IOceanVisualBridge
    {
        private static readonly int CrestCameraColorTextureId = Shader.PropertyToID("_Crest_CameraColorTexture");

        private Crest.UnderwaterRenderer _cachedUnderwaterRenderer;

        protected virtual Crest.OceanRenderer ReadBoundOceanRenderer()
        {
            return null;
        }

        public Material OceanMaterial
        {
            get
            {
                Crest.OceanRenderer oceanRenderer = ReadBoundOceanRenderer();
                return oceanRenderer != null ? oceanRenderer.OceanMaterial : null;
            }
        }

        public bool HasUnderwaterInstance => _cachedUnderwaterRenderer != null;

        public int CameraColorTextureId => CrestCameraColorTextureId;

        public bool HasUnderwaterPass(Camera camera)
        {
            return IsCachedUnderwaterRendererForCamera(camera);
        }

        public Component TryGetUnderwaterPass(Camera camera)
        {
            return IsCachedUnderwaterRendererForCamera(camera) ? _cachedUnderwaterRenderer : null;
        }

        public Component EnsureUnderwaterPass(Camera camera)
        {
            if (camera == null)
                return null;

            Crest.UnderwaterRenderer underwaterRenderer = camera.GetComponent<Crest.UnderwaterRenderer>();
            if (underwaterRenderer != null)
            {
                _cachedUnderwaterRenderer = underwaterRenderer;
                return underwaterRenderer;
            }

            _cachedUnderwaterRenderer = camera.gameObject.AddComponent<Crest.UnderwaterRenderer>();
            return _cachedUnderwaterRenderer;
        }

        public bool IsUnderwaterPassEnabled(Component renderer)
        {
            Crest.UnderwaterRenderer underwaterRenderer = renderer as Crest.UnderwaterRenderer;
            return underwaterRenderer != null && underwaterRenderer.enabled;
        }

        public void SetUnderwaterPassEnabled(Component renderer, bool enabled)
        {
            Crest.UnderwaterRenderer underwaterRenderer = renderer as Crest.UnderwaterRenderer;
            if (underwaterRenderer == null || underwaterRenderer.enabled == enabled)
                return;

            underwaterRenderer.enabled = enabled;
        }

        public bool IsUnderwaterPassActive(Component renderer)
        {
            Crest.UnderwaterRenderer underwaterRenderer = renderer as Crest.UnderwaterRenderer;
            return underwaterRenderer != null && underwaterRenderer.IsActive;
        }

        public void SetCopyOceanMaterialParamsEachFrame(Component renderer, bool enabled)
        {
            Crest.UnderwaterRenderer underwaterRenderer = renderer as Crest.UnderwaterRenderer;
            if (underwaterRenderer == null)
                return;

            underwaterRenderer._copyOceanMaterialParamsEachFrame = enabled;
        }

        public void CopyUnderwaterPassSettings(Component source, Component target)
        {
            Crest.UnderwaterRenderer sourceRenderer = source as Crest.UnderwaterRenderer;
            Crest.UnderwaterRenderer targetRenderer = target as Crest.UnderwaterRenderer;
            if (sourceRenderer == null || targetRenderer == null || ReferenceEquals(sourceRenderer, targetRenderer))
                return;

            targetRenderer._mode = sourceRenderer._mode;
            targetRenderer._depthFogDensityFactor = sourceRenderer._depthFogDensityFactor;
            targetRenderer._volumeGeometry = sourceRenderer._volumeGeometry;
            targetRenderer._invertCulling = sourceRenderer._invertCulling;
            targetRenderer._enableShaderAPI = sourceRenderer._enableShaderAPI;
            targetRenderer._copyOceanMaterialParamsEachFrame = sourceRenderer._copyOceanMaterialParamsEachFrame;
            targetRenderer._farPlaneMultiplier = sourceRenderer._farPlaneMultiplier;
            _cachedUnderwaterRenderer = targetRenderer;
        }

        public bool IsOceanCameraOwnedBy(Camera camera)
        {
            Crest.OceanRenderer oceanRenderer = ReadBoundOceanRenderer();
            return oceanRenderer != null &&
                   camera != null &&
                   ReferenceEquals(oceanRenderer.ViewCamera, camera) &&
                   ReferenceEquals(oceanRenderer.Viewpoint, camera.transform);
        }

        public void AssignOceanCamera(Camera camera)
        {
            Crest.OceanRenderer oceanRenderer = ReadBoundOceanRenderer();
            if (oceanRenderer == null || camera == null)
                return;

            oceanRenderer.ViewCamera = camera;
            oceanRenderer.Viewpoint = camera.transform;
        }

        public void ApplyUnderwaterGlobals(
            Material targetMaterial,
            Vector3 depthFogDensity,
            Color diffuse,
            Color diffuseGrazing,
            Color diffuseShadow,
            float subSurfaceSun,
            float subSurfaceBase,
            float subSurfaceSunFalloff)
        {
            if (!Application.isPlaying || targetMaterial == null)
                return;

            Shader.SetGlobalVector(
                Crest.UnderwaterRenderer.ShaderIDs.s_CrestDepthFogDensity,
                new Vector4(depthFogDensity.x, depthFogDensity.y, depthFogDensity.z, 0f));
            Shader.SetGlobalColor(Crest.UnderwaterRenderer.ShaderIDs.s_CrestDiffuse, diffuse.linear);
            Shader.SetGlobalColor(Crest.UnderwaterRenderer.ShaderIDs.s_CrestDiffuseGrazing, diffuseGrazing.linear);
            Shader.SetGlobalColor(Crest.UnderwaterRenderer.ShaderIDs.s_CrestDiffuseShadow, diffuseShadow.linear);
            Shader.SetGlobalColor(Crest.UnderwaterRenderer.ShaderIDs.s_CrestSubSurfaceColour, diffuseGrazing.linear);
            Shader.SetGlobalFloat(Crest.UnderwaterRenderer.ShaderIDs.s_CrestSubSurfaceSun, subSurfaceSun);
            Shader.SetGlobalFloat(Crest.UnderwaterRenderer.ShaderIDs.s_CrestSubSurfaceBase, subSurfaceBase);
            Shader.SetGlobalFloat(Crest.UnderwaterRenderer.ShaderIDs.s_CrestSubSurfaceSunFallOff, subSurfaceSunFalloff);
            Crest.Helpers.SetGlobalKeyword(
                "CREST_SUBSURFACESCATTERING_ON",
                targetMaterial.IsKeywordEnabled("_SUBSURFACESCATTERING_ON"));
            Crest.Helpers.SetGlobalKeyword(
                "CREST_SHADOWS_ON",
                targetMaterial.IsKeywordEnabled("_SHADOWS_ON"));
        }

        private bool IsCachedUnderwaterRendererForCamera(Camera camera)
        {
            if (camera == null || _cachedUnderwaterRenderer == null)
                return false;

            return ReferenceEquals(_cachedUnderwaterRenderer.gameObject, camera.gameObject);
        }
    }
}
