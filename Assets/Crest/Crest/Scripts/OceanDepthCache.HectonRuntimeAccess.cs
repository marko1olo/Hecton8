using UnityEngine;

namespace Crest
{
    /// <summary>
    /// Typed first-party accessors for OceanDepthCache runtime configuration.
    /// Keeps HECTON-8 off reflection paths while preserving Crest ownership.
    /// </summary>
    public partial class OceanDepthCache
    {
        public void HectonApplyRuntimeSettings(
            int layerMask,
            int resolution,
            float cameraMaxTerrainHeight,
            bool relativeToSeaLevel)
        {
            _layers = layerMask;
            _resolution = Mathf.Clamp(resolution, 128, 1024);
            _type = OceanDepthCacheType.Realtime;
            _refreshMode = OceanDepthCacheRefreshMode.OnDemand;
            _cameraMaxTerrainHeight = Mathf.Max(8f, cameraMaxTerrainHeight);
#if UNITY_2022_2_OR_NEWER
            _terrainPixelErrorOverride = 0f;
#endif
            _lodBiasOverride = Mathf.Infinity;
            _maximumLodLevelOverride = 0;
            _relative = relativeToSeaLevel;
        }

        public Camera HectonGetOrCreateCaptureCamera(bool updateComponents)
        {
            return InitObjects(updateComponents) ? _camDepthCache : null;
        }
    }
}
