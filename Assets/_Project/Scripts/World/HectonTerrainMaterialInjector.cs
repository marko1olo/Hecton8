using UnityEngine;

namespace Hecton8.World
{
    [ExecuteAlways]
    [RequireComponent(typeof(UnityEngine.Terrain))]
    public class HectonTerrainMaterialInjector : MonoBehaviour
    {
        public Material customTerrainMaterial;

        private UnityEngine.Terrain _terrain;
        private Material _instancedMaterial;

        private void OnEnable()
        {
            _terrain = GetComponent<UnityEngine.Terrain>();
            ApplyMaterial();
        }

        private float _lastUpdateTime;

        private void Update()
        {
            // Throttle to avoid GC allocation every frame (terrainData.alphamapTextures allocates)
            if (Time.time - _lastUpdateTime > 1.0f || !Application.isPlaying)
            {
                _lastUpdateTime = Time.time;
                ForceUpdate();
            }
        }

        public void ForceUpdate()
        {
            ApplyMaterial();

            // MapMagic might replace materialTemplate or regenerate TerrainData
            if (_terrain != null && _terrain.terrainData != null)
            {
                if (_terrain.materialTemplate != _instancedMaterial && _instancedMaterial != null)
                {
                    _terrain.materialTemplate = _instancedMaterial;
                }

                // Ensure splatmaps are assigned (MapMagic might update them)
                if (_instancedMaterial != null && _terrain.terrainData.alphamapTextureCount > 0)
                {
                    Texture2D[] alphamaps = _terrain.terrainData.alphamapTextures;
                    if (alphamaps.Length > 0 && alphamaps[0] != null)
                        _instancedMaterial.SetTexture("_Control", alphamaps[0]);
                    
                    if (alphamaps.Length > 1 && alphamaps[1] != null)
                        _instancedMaterial.SetTexture("_Control1", alphamaps[1]);

                    if (alphamaps.Length > 2 && alphamaps[2] != null)
                        _instancedMaterial.SetTexture("_Control2", alphamaps[2]);
                        
                    _instancedMaterial.SetFloat("_NumLayersCount", _terrain.terrainData.alphamapLayers);

                    // Update terrain size for triplanar scaling
                    _instancedMaterial.SetVector("_TerrainSize", new Vector4(_terrain.terrainData.size.x, _terrain.terrainData.size.y, _terrain.terrainData.size.z, 0));
                }
            }
        }

        private void ApplyMaterial()
        {
            if (customTerrainMaterial == null || _terrain == null) return;

            // Instantiating so each terrain chunk has its own material to avoid cross-chunk bleeding
            // but actually we can just use PropertyBlocks. However, Unity Terrain component doesn't support SetPropertyBlock well.
            // Creating an instanced material per chunk is fine.
            if (_instancedMaterial == null)
            {
                _instancedMaterial = new Material(customTerrainMaterial);
                _instancedMaterial.name = customTerrainMaterial.name + "_" + gameObject.name;
            }

            _terrain.materialTemplate = _instancedMaterial;
        }

        private void OnDestroy()
        {
            if (_instancedMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(_instancedMaterial);
                else
                    DestroyImmediate(_instancedMaterial);
            }
        }
    }
}
