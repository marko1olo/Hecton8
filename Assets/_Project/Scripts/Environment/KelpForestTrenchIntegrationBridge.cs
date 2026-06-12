using UnityEngine;
using Hecton8.Core;
using Hecton8.World;

namespace Hecton8.Environment
{
    /// <summary>
    /// Bridges BiomeMatrixDirector events to WorldGenerativeGeologySeamExecutionDirector.
    /// Injects an active OpenTrench geology binding when entering the Kelp Forest.
    /// </summary>
    public sealed class KelpForestTrenchIntegrationBridge : MonoBehaviour, IBiomeMatrixEventListener
    {
        private const uint BiomeKelpTrenchHash = 0x64E62B68u; // Match MacroEcosystemMath.BiomeKelpTrench
        private const string ArchetypeOpenTrenchLabel = "OpenTrench";

        private WorldGenerativeGeologyBinding _activeTrenchBinding;
        private bool _isKelpBiomeActive;

        private void OnEnable()
        {
            BiomeMatrixEvents.Register(this);
        }

        private void OnDisable()
        {
            BiomeMatrixEvents.Unregister(this);
            ClearTrenchBinding();
        }

        public void OnMatrixBiomeChanged(HectonBiomeMatrixProfile profile)
        {
            if (profile == null)
            {
                SetKelpBiomeState(false);
                return;
            }

            // Using string match since profile.biomeName is exposed
            // If the name matches Kelp Forest, trigger.
            bool isKelp = profile.biomeName.Contains("Kelp", System.StringComparison.OrdinalIgnoreCase);
            SetKelpBiomeState(isKelp);
        }

        public void OnDepthTierChanged(int depthTier, float depthMeters)
        {
            // Update placement depth if needed
            if (_isKelpBiomeActive && _activeTrenchBinding != null)
            {
                Transform playerTransform = GlobalRegistry.Player?.PlayerTransform;
                if (playerTransform != null)
                {
                    // Snap the binding to player with offset
                    _activeTrenchBinding.transform.position = playerTransform.position + playerTransform.forward * 40f - Vector3.up * 10f;
                }
            }
        }

        private void SetKelpBiomeState(bool isActive)
        {
            if (_isKelpBiomeActive == isActive)
                return;

            _isKelpBiomeActive = isActive;

            if (_isKelpBiomeActive)
            {
                SpawnTrenchBinding();
            }
            else
            {
                ClearTrenchBinding();
            }
        }

        private void SpawnTrenchBinding()
        {
            if (_activeTrenchBinding != null)
                return;

            GameObject go = new GameObject("RuntimeKelpTrenchBinding");
            go.transform.SetParent(transform, false);
            
            Transform playerTransform = GlobalRegistry.Player?.PlayerTransform;
            if (playerTransform != null)
            {
                go.transform.position = playerTransform.position + playerTransform.forward * 40f - Vector3.up * 10f;
                go.transform.rotation = Quaternion.LookRotation(playerTransform.forward, Vector3.up);
            }

            go.transform.localScale = new Vector3(100f, 12f, 100f);

            _activeTrenchBinding = go.AddComponent<WorldGenerativeGeologyBinding>();
            // Use runtime key derived from position so it's stable if the player leaves and returns near same spot
            long key = unchecked((long)BiomeKelpTrenchHash + Mathf.RoundToInt(go.transform.position.x));
            if (key == 0) key = 1;

            _activeTrenchBinding.InjectDynamicState(key, ArchetypeOpenTrenchLabel);
        }

        private void ClearTrenchBinding()
        {
            if (_activeTrenchBinding != null)
            {
                Destroy(_activeTrenchBinding.gameObject);
                _activeTrenchBinding = null;
            }
        }
    }
}
