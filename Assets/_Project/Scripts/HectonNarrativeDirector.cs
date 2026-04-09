// ============================================================================
// HECTON-8 - HectonNarrativeDirector.cs
// Stateless narrative tracker for discoveries and depth-tier milestones.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using Hecton8.Systems.AI;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-150)]
    public sealed class HectonNarrativeDirector : MonoBehaviour, ISaveable, ISlowTickable
    {
        private static HectonNarrativeDirector _instance;

        [Header("Settings")]
        [SerializeField, UnityEngine.Range(1, 10)] private int slowTickRate = 2;

        [Header("State")]
        [SerializeField, ReadOnly] private List<string> discoveredIds = new List<string>();
        [SerializeField, ReadOnly] private int currentDepthTier;

        private Transform _playerTransform;
        private bool _registered;
        private bool _rareDiscoveryRequested;

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

            if (data.narrativeDiscoveryIds != null)
            {
                int count = Mathf.Min(data.narrativeDiscoveryCount, data.narrativeDiscoveryIds.Length);
                for (int i = 0; i < count; i++)
                {
                    if (!string.IsNullOrEmpty(data.narrativeDiscoveryIds[i]))
                    {
                        discoveredIds.Add(data.narrativeDiscoveryIds[i]);
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

            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                _playerTransform = player.transform;
        }

        private void OnEnable()
        {
            if (GameTickManager.Instance != null && !_registered)
            {
                GameTickManager.Instance.Register(this);
                _registered = true;
            }

            if (SaveManager.Instance != null)
                SaveManager.Instance.Register(this);

            NarrativeEvents.OnDiscoveryMade += HandleDiscovery;
            HectonDirectorAI.OnRequestRareDiscovery += HandleRareDiscoveryRequest;
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance != null && _registered)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }

            if (SaveManager.Instance != null)
                SaveManager.Instance.Unregister(this);
            NarrativeEvents.OnDiscoveryMade -= HandleDiscovery;
            HectonDirectorAI.OnRequestRareDiscovery -= HandleRareDiscoveryRequest;
            NarrativeEvents.OnDiscoveryMade -= HandleDiscovery;
        }

        public void SlowTick()
        {
            if (_playerTransform == null)
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
            return discoveredIds.Contains(id);
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
            if (discoveredIds.Contains(id))
                return;

            discoveredIds.Add(id);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Narrative] Discovery Logged: {id}");
#endif
        }

        private void HandleRareDiscoveryRequest(Vector3 hintPosition)
        {
            _rareDiscoveryRequested = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Narrative] Director requested Rare Discovery near {hintPosition}. " +
                      $"Signal received by Atlas-6 Narrative Bridge.");
#endif
            // FUTURE IMPLEMENTATION: Select a discovery from a pool or signal the spawning system
            // to place a procedural narrative objective near 'hintPosition'.
        }
    }
}
