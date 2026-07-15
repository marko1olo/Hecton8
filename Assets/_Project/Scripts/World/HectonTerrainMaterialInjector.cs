using UnityEngine;

namespace Hecton8.World
{
    [ExecuteAlways]
    [RequireComponent(typeof(UnityEngine.Terrain))]
    public class HectonTerrainMaterialInjector : MonoBehaviour
    {
        private const int MaxExpectedTerrainMaterialInstances = 256;

        public Material customTerrainMaterial;

        private static int s_liveInstanceCount;

        private UnityEngine.Terrain _terrain;
        private TerrainData _cachedTerrainData;
        private Material _instancedMaterial;
        private Texture2D[] _cachedAlphamaps;
        private int _cachedAlphamapTextureCount = -1;
        private int _cachedAlphamapLayers = -1;

        private void OnEnable()
        {
            _terrain = GetComponent<UnityEngine.Terrain>();
            SubscribeTerrainCallbacks();
            ForceUpdate();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!isActiveAndEnabled)
                return;

            _terrain = GetComponent<UnityEngine.Terrain>();
            ForceUpdate();
        }
#endif

        public void ForceUpdate()
        {
            ApplyMaterial();
            RefreshTerrainBindings(forceAlphamapRefresh: true);
        }

        private void RefreshTerrainBindings(bool forceAlphamapRefresh)
        {
            if (_terrain == null || _terrain.terrainData == null)
                return;

            TerrainData terrainData = _terrain.terrainData;
            _terrain.basemapDistance = 100000.0f;

            if (_terrain.materialTemplate != _instancedMaterial && _instancedMaterial != null)
                _terrain.materialTemplate = _instancedMaterial;

            if (_instancedMaterial == null || customTerrainMaterial == null)
                return;

            CopyCustomMaterialProperties();
            RefreshCachedAlphamaps(terrainData, forceAlphamapRefresh);
            ApplyCachedAlphamaps(terrainData);
        }

        private void CopyCustomMaterialProperties()
        {
            if (customTerrainMaterial.HasProperty("_AlbedoArray"))
                _instancedMaterial.SetTexture("_AlbedoArray", customTerrainMaterial.GetTexture("_AlbedoArray"));
            if (customTerrainMaterial.HasProperty("_NormalArray"))
                _instancedMaterial.SetTexture("_NormalArray", customTerrainMaterial.GetTexture("_NormalArray"));
            if (customTerrainMaterial.HasProperty("_MaskArray"))
                _instancedMaterial.SetTexture("_MaskArray", customTerrainMaterial.GetTexture("_MaskArray"));

#if UNITY_EDITOR
            if (_instancedMaterial.GetTexture("_AlbedoArray") == null)
                _instancedMaterial.SetTexture("_AlbedoArray", UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/DeepSea_AlbedoArray.asset"));
            if (_instancedMaterial.GetTexture("_NormalArray") == null)
                _instancedMaterial.SetTexture("_NormalArray", UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/DeepSea_NormalArray.asset"));
            if (_instancedMaterial.GetTexture("_MaskArray") == null)
                _instancedMaterial.SetTexture("_MaskArray", UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_MaskArray.asset"));
#endif

            if (customTerrainMaterial.HasProperty("_HectonUVScale"))
                _instancedMaterial.SetFloat("_HectonUVScale", customTerrainMaterial.GetFloat("_HectonUVScale"));
            if (customTerrainMaterial.HasProperty("_HectonTriplanarBlend"))
                _instancedMaterial.SetFloat("_HectonTriplanarBlend", customTerrainMaterial.GetFloat("_HectonTriplanarBlend"));
            if (customTerrainMaterial.HasProperty("_HectonMacroVariationStrength"))
                _instancedMaterial.SetFloat("_HectonMacroVariationStrength", customTerrainMaterial.GetFloat("_HectonMacroVariationStrength"));
        }

        private void RefreshCachedAlphamaps(TerrainData terrainData, bool force)
        {
            int textureCount = terrainData.alphamapTextureCount;
            int layerCount = terrainData.alphamapLayers;
            if (!force &&
                ReferenceEquals(_cachedTerrainData, terrainData) &&
                _cachedAlphamapTextureCount == textureCount &&
                _cachedAlphamapLayers == layerCount)
            {
                return;
            }

            _cachedTerrainData = terrainData;
            _cachedAlphamapTextureCount = textureCount;
            _cachedAlphamapLayers = layerCount;
            _cachedAlphamaps = textureCount > 0 ? terrainData.alphamapTextures : null;
        }

        private void ApplyCachedAlphamaps(TerrainData terrainData)
        {
            if (_cachedAlphamaps == null || _cachedAlphamaps.Length == 0)
                return;

            if (_cachedAlphamaps.Length > 0 && _cachedAlphamaps[0] != null)
                _instancedMaterial.SetTexture("_Control", _cachedAlphamaps[0]);
            if (_cachedAlphamaps.Length > 1 && _cachedAlphamaps[1] != null)
                _instancedMaterial.SetTexture("_Control1", _cachedAlphamaps[1]);
            if (_cachedAlphamaps.Length > 2 && _cachedAlphamaps[2] != null)
                _instancedMaterial.SetTexture("_Control2", _cachedAlphamaps[2]);

            _instancedMaterial.SetFloat("_NumLayersCount", terrainData.alphamapLayers);
            Vector3 size = terrainData.size;
            _instancedMaterial.SetVector("_TerrainSize", new Vector4(size.x, size.y, size.z, 0f));
        }

        private void ApplyMaterial()
        {
            if (customTerrainMaterial == null || _terrain == null)
                return;

            if (_instancedMaterial == null)
            {
                _instancedMaterial = new Material(customTerrainMaterial);
                _instancedMaterial.name = customTerrainMaterial.name + "_" + gameObject.name;
                _instancedMaterial.EnableKeyword("_NORMALMAP");
                _instancedMaterial.EnableKeyword("_TERRAIN_BLEND_HEIGHT");
                _instancedMaterial.EnableKeyword("_MASKMAP");
                s_liveInstanceCount++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (s_liveInstanceCount > MaxExpectedTerrainMaterialInstances)
                    Debug.LogWarning("[HectonTerrainMaterialInjector] Live terrain material instance count exceeds expected chunk maximum.");
#endif
            }

            _terrain.materialTemplate = _instancedMaterial;
        }

        private void OnDisable()
        {
            UnsubscribeTerrainCallbacks();
            ReleaseInstance();
        }

        private void OnDestroy()
        {
            ReleaseInstance();
        }

        private void SubscribeTerrainCallbacks()
        {
            TerrainCallbacks.heightmapChanged -= OnTerrainHeightmapChanged;
            TerrainCallbacks.heightmapChanged += OnTerrainHeightmapChanged;
            TerrainCallbacks.textureChanged -= OnTerrainTextureChanged;
            TerrainCallbacks.textureChanged += OnTerrainTextureChanged;
        }

        private void UnsubscribeTerrainCallbacks()
        {
            TerrainCallbacks.heightmapChanged -= OnTerrainHeightmapChanged;
            TerrainCallbacks.textureChanged -= OnTerrainTextureChanged;
        }

        private void OnTerrainHeightmapChanged(UnityEngine.Terrain terrain, RectInt heightRegion, bool synched)
        {
            if (terrain == _terrain)
                RefreshTerrainBindings(forceAlphamapRefresh: false);
        }

        private void OnTerrainTextureChanged(UnityEngine.Terrain terrain, string textureName, RectInt texelRegion, bool synched)
        {
            if (terrain == _terrain)
                RefreshTerrainBindings(forceAlphamapRefresh: true);
        }

        private void ReleaseInstance()
        {
            if (_terrain != null && _terrain.materialTemplate == _instancedMaterial)
                _terrain.materialTemplate = null;

            if (_instancedMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(_instancedMaterial);
                else
                    DestroyImmediate(_instancedMaterial);
                _instancedMaterial = null;
                s_liveInstanceCount = Mathf.Max(0, s_liveInstanceCount - 1);
            }

            _cachedTerrainData = null;
            _cachedAlphamaps = null;
            _cachedAlphamapTextureCount = -1;
            _cachedAlphamapLayers = -1;
        }
    }
}
