// ============================================================================
// HECTON-8 - HectonNarrativeDirector.cs
// Stateless narrative tracker for discoveries and depth-tier milestones.
// ============================================================================

using System.Collections.Generic;
using Hecton8.AtlasSignal;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using Hecton8.Systems.AI;
using Hecton8.Interaction;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-150)]
    public sealed class HectonNarrativeDirector : MonoBehaviour, ISaveable, ISlowTickable
    {
        private static HectonNarrativeDirector _instance;

        [Header("Settings")]
#pragma warning disable CS0414 // slowTickRate reserved for future SlowTick throttling
        [SerializeField, UnityEngine.Range(1, 10)] private int slowTickRate = 2;
#pragma warning restore CS0414

        [Header("State")]
        [SerializeField, ReadOnly] private List<string> discoveredIds = new List<string>(); // COLD ALLOC: 64 for [N] discoveries (reasonable number of narrative discoveries)
        [SerializeField, ReadOnly] private int currentDepthTier;

        // Runtime-only collection for active POIs in the world.
        // COLD ALLOC: 64 for initial capacity (reasonable number of POIs per scene).
        private readonly List<NarrativeDiscovery> _activePOIs = new List<NarrativeDiscovery>(64);
        private readonly HashSet<string> _discoveredIdLookup = new HashSet<string>(64, System.StringComparer.Ordinal);

        private Transform _playerTransform;
        private bool _registered;

        // Флаг: Director запросил редкую находку в текущем тике
        // Читается в диагностике и будущей системе спавна
#pragma warning disable CS0414
        private bool _rareDiscoveryRequested;
#pragma warning restore CS0414

        public static HectonNarrativeDirector Instance => _instance;

        public int SavePriority => 5;
        public int LoadPriority => 5;


        public void PopulateSaveData(SaveData data)
        {
            data.narrativeDepthTier = currentDepthTier;
            data.narrativeDiscoveryCount = Mathf.Min(discoveredIds.Count, SaveData.MaxNarrativeDiscoveries);
            
            if (data.narrativeDiscoveryIds == null || data.narrativeDiscoveryIds.Length != SaveData.MaxNarrativeDiscoveries)
            {
                data.narrativeDiscoveryIds = new string[SaveData.MaxNarrativeDiscoveries];
            }

            for (int i = 0; i < data.narrativeDiscoveryCount; i++)
            {
                data.narrativeDiscoveryIds[i] = discoveredIds[i];
            }
        }


        public void LoadFromSaveData(SaveData data)
        {
            currentDepthTier = data.narrativeDepthTier;
            discoveredIds.Clear();
            _discoveredIdLookup.Clear();

            if (data.narrativeDiscoveryIds != null)
            {
                int count = Mathf.Min(data.narrativeDiscoveryCount, data.narrativeDiscoveryIds.Length);
                for (int i = 0; i < count; i++)
                {
                    string discoveryId = data.narrativeDiscoveryIds[i];
                    if (!string.IsNullOrWhiteSpace(discoveryId) &&
                        _discoveredIdLookup.Add(discoveryId))
                    {
                        discoveredIds.Add(discoveryId);
                    }
                }
            }
            
            // Re-sync world state from loaded data
            NarrativeEvents.RaiseDepthTierReached(currentDepthTier);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            RebuildDiscoveryLookup();
        }

        private void OnEnable()
        {
            TryRegister();

            if (SaveManager.Instance != null)
                SaveManager.Instance.Register(this);

            NarrativeEvents.OnDiscoveryMade += HandleDiscovery;
            NarrativeEvents.OnNarrativePOIRegistered += HandlePOIRegistered;
            NarrativeEvents.OnNarrativePOIDisposed += HandlePOIDisposed;
            HectonDirectorAI.OnRequestRareDiscovery += HandleRareDiscoveryRequest;

            ResolvePlayerTransform();
        }

        private void OnDisable()
        {
            TryUnregister();

            if (SaveManager.Instance != null)
                SaveManager.Instance.Unregister(this);

            NarrativeEvents.OnDiscoveryMade -= HandleDiscovery;
            NarrativeEvents.OnNarrativePOIRegistered -= HandlePOIRegistered;
            NarrativeEvents.OnNarrativePOIDisposed -= HandlePOIDisposed;
            HectonDirectorAI.OnRequestRareDiscovery -= HandleRareDiscoveryRequest;
        }

        private void OnDestroy()
        {
            TryUnregister();

            if (_instance == this)
                _instance = null;
        }

        private void TryRegister()
        {
            if (_registered)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Core);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _registered = false;
        }

        public NarrativeDiscovery GetNearestUndiscoveredPOI(Vector3 center, float maxDistance)
        {
            NarrativeDiscovery nearest = null;
            float minSqrDist = maxDistance * maxDistance;

            for (int i = 0; i < _activePOIs.Count; i++)
            {
                var poi = _activePOIs[i];
                if (poi == null) continue;

                // Check if already discovered
                if (HasDiscovery(poi.DiscoveryId)) continue;

                float sqrDist = (poi.transform.position - center).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    nearest = poi;
                }
            }

            return nearest;
        }

        public void SlowTick()
        {
            if (_playerTransform == null && !ResolvePlayerTransform())
                return;

            float depth = -_playerTransform.position.y;
            int newTier = CalculateDepthTier(depth);

            if (newTier <= currentDepthTier)
                return;

            currentDepthTier = newTier;
            NarrativeEvents.RaiseDepthTierReached(currentDepthTier);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Narrative] New Depth Tier Reached: {currentDepthTier} (Depth: {depth:F1}m)");
#endif
        }

        public bool HasDiscovery(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && _discoveredIdLookup.Contains(id);
        }

        private int CalculateDepthTier(float depth)
        {
            if (depth >= 1000f)
                return 4;

            if (depth >= 300f)
                return 3;

            if (depth >= 100f)
                return 2;

            if (depth >= 0f)
                return 1;

            return 0;
        }

        private void HandleDiscovery(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !_discoveredIdLookup.Add(id))
                return;

            discoveredIds.Add(id);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Narrative] Discovery Logged: {id}");
#endif
        }

        private void HandleRareDiscoveryRequest(Vector3 hintPosition)
        {
            _rareDiscoveryRequested = true;

            FirstHourDirector firstHourDirector = FirstHourDirector.Instance;
            if (firstHourDirector != null &&
                !firstHourDirector.IsMilestoneComplete(FirstHourMilestone.FirstModule))
            {
                return;
            }

            AtlasSignalSystem atlasSignalSystem = AtlasSignalSystem.Instance;
            bool rebroadcastedPulse = CanRebroadcastAtlasRareDiscoveryPulse(atlasSignalSystem);
            if (rebroadcastedPulse)
                AtlasSignalEvents.RaisePulse(atlasSignalSystem.CurrentStrength);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Narrative] Rare Discovery requested near {hintPosition}. " +
                      $"Atlas-6 signal {(rebroadcastedPulse ? "pulse triggered" : "pulse skipped")} (strength: " +
                      $"{(AtlasSignalSystem.Instance != null ? AtlasSignalSystem.Instance.CurrentStrength : 0f):F2}).");
#endif
        }

        private static bool CanRebroadcastAtlasRareDiscoveryPulse(AtlasSignalSystem atlasSignalSystem)
        {
            return atlasSignalSystem != null &&
                   atlasSignalSystem.CurrentRevealStage >= 3 &&
                   atlasSignalSystem.CurrentStrength > 0f;
        }

        private bool ResolvePlayerTransform()
        {
            if (_playerTransform != null)
                return true;

            if (!SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            _playerTransform = playerTransform;
            return true;
        }

        private void HandlePOIRegistered(NarrativeDiscovery poi)
        {
            if (poi == null) return;
            if (!_activePOIs.Contains(poi))
            {
                _activePOIs.Add(poi);
            }
        }

        private void HandlePOIDisposed(NarrativeDiscovery poi)
        {
            if (poi == null) return;
            _activePOIs.Remove(poi);
        }

        private void RebuildDiscoveryLookup()
        {
            _discoveredIdLookup.Clear();
            if (discoveredIds == null)
                return;

            int writeIndex = 0;
            for (int i = 0; i < discoveredIds.Count; i++)
            {
                string discoveryId = discoveredIds[i];
                if (string.IsNullOrWhiteSpace(discoveryId) || !_discoveredIdLookup.Add(discoveryId))
                    continue;

                if (writeIndex != i)
                    discoveredIds[writeIndex] = discoveryId;

                writeIndex++;
            }

            if (writeIndex < discoveredIds.Count)
                discoveredIds.RemoveRange(writeIndex, discoveredIds.Count - writeIndex);
        }
    }
}
