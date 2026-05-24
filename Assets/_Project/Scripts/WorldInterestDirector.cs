using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Gameplay;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4075)]
    public sealed class WorldInterestDirector : MonoBehaviour, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private const string NoneLabel = "None";
        private const string KindResourceFieldLabel = "ResourceField";
        private const string KindFabricationLabel = "Fabrication";
        private const string KindToolRangeLabel = "ToolRange";
        private const string KindConstructionLabel = "Construction";
        private const string KindPowerLabel = "Power";
        private const string KindServiceLabel = "Service";
        private const string KindProgressionHubLabel = "ProgressionHub";

        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private ScatterBudgetController scatterBudgetController;
        [SerializeField] private WorldSliceDirector worldSliceDirector;

        [Header("Runtime Auto Resolve")]
        [SerializeField, Min(0f)] private float autoResolveRetryInterval = 1f;

        [Header("Defaults")]
        [SerializeField] private float idleScavengeRadiusScale = 1f;
        [SerializeField] private float idleSpawnScale = 1f;
        [SerializeField] private float idleColliderRadiusScale = 1f;
        [SerializeField] private float idleColliderOpsScale = 1f;
        [SerializeField] private float idleSliceNearScale = 1f;
        [SerializeField] private float idleSliceMidScale = 1f;

        // Inspector-only live diagnostics for tuning the streaming stack.
#pragma warning disable CS0414
        [Header("Diagnostics")]
        [SerializeField] private string _debugDominantAnchor = "None";
        [SerializeField] private string _debugDominantKind = "None";
        [SerializeField] private float _debugDominantInfluence;
        [SerializeField] private int _debugAnchorCount;
        [SerializeField] private bool _debugApplied;
#pragma warning restore CS0414

        // COLD ALLOC: List<WorldInterestAnchor>[24] - slow-tick world-interest anchor scratch - owner: WorldInterestDirector
        private readonly List<WorldInterestAnchor> _anchors = new List<WorldInterestAnchor>(24);
        private IPlayerRuntimeContext _playerRuntimeContext;
        private HectonPlayerMovement _playerMovement;
        private bool _registeredToTickManager;
        private bool _hotSwapRegistered;
        private float _nextAutoResolveAttemptTime = float.NegativeInfinity;

        private void Awake()
        {
            CacheRuntimeDependencies();
            ResolveReferences();
            RefreshAnchors();
        }

        private void OnEnable()
        {
            CacheRuntimeDependencies();
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void Start()
        {
            CacheRuntimeDependencies();
            TryRegister();

            ApplyInterest(forceRefresh: true);
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            ClearRuntimeDependencies();
        }

        private void TryRegister()
        {
            if (_registeredToTickManager || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTickManager = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registeredToTickManager = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Player)
                return;

            _playerRuntimeContext = currentService as IPlayerRuntimeContext;
            ApplyPlayerRuntimeContext();
        }

        private void CacheRuntimeDependencies()
        {
            _playerRuntimeContext = Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext;
            ApplyPlayerRuntimeContext();
        }

        private void ClearRuntimeDependencies()
        {
            _playerRuntimeContext = null;
            _playerMovement = null;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        public void SlowTick()
        {
            ApplyInterest(forceRefresh: false);
        }

        public void RefreshAnchors()
        {
            WorldInterestAnchor.CopyActiveAnchorsTo(_anchors);
            _debugAnchorCount = _anchors.Count;
        }

        private void ApplyInterest(bool forceRefresh)
        {
            ResolveReferences();

            if (forceRefresh || _anchors.Count == 0)
                RefreshAnchors();

            if (scatterBudgetController == null)
            {
                _debugApplied = false;
                return;
            }

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            {
                _debugApplied = false;
                return;
            }

            float bestInfluence = 0f;
            WorldInterestAnchor bestAnchor = null;

            float scavengeScale = idleScavengeRadiusScale;
            float spawnScale = idleSpawnScale;
            float colliderRadiusScale = idleColliderRadiusScale;
            float colliderOpsScale = idleColliderOpsScale;
            float sliceNearScale = idleSliceNearScale;
            float sliceMidScale = idleSliceMidScale;

            for (int i = 0; i < _anchors.Count; i++)
            {
                WorldInterestAnchor anchor = _anchors[i];
                if (anchor == null)
                    continue;

                float influence = anchor.EvaluateInfluence(in playerAup);
                if (influence <= 0.001f)
                    continue;

                scavengeScale = math.max(scavengeScale, math.lerp(1f, anchor.ScavengeRadiusScale, influence));
                spawnScale = math.max(spawnScale, math.lerp(1f, anchor.SpawnScale, influence));
                colliderRadiusScale = math.max(colliderRadiusScale, math.lerp(1f, anchor.ColliderRadiusScale, influence));
                colliderOpsScale = math.max(colliderOpsScale, math.lerp(1f, anchor.ColliderOpsScale, influence));
                sliceNearScale = math.max(sliceNearScale, math.lerp(1f, anchor.SliceNearScale, influence));
                sliceMidScale = math.max(sliceMidScale, math.lerp(1f, anchor.SliceMidScale, influence));

                if (influence > bestInfluence)
                {
                    bestInfluence = influence;
                    bestAnchor = anchor;
                }
            }

            scatterBudgetController.SetInterestScales(
                scavengeScale,
                spawnScale,
                colliderRadiusScale,
                colliderOpsScale);

            if (worldSliceDirector != null)
                worldSliceDirector.SetInterestScales(sliceNearScale, sliceMidScale);

            _debugDominantAnchor = bestAnchor != null ? bestAnchor.InterestLabel : NoneLabel;
            _debugDominantKind = bestAnchor != null ? ResolveInterestKindLabel(bestAnchor.Kind) : NoneLabel;
            _debugDominantInfluence = bestInfluence;
            _debugAnchorCount = _anchors.Count;
            _debugApplied = true;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            if (_playerMovement == null)
            {
                IPlayerRuntimeContext playerContext = _playerRuntimeContext;
                if (playerContext != null && playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
                {
                    playerAup = snapshot.Aup;
                    return playerAup.IsFinite();
                }

                if (playerContext != null)
                    _playerMovement = playerContext.PlayerMovement;
            }

            if (_playerMovement != null)
            {
                playerAup = _playerMovement.CurrentAup;
                return true;
            }

            playerAup = default;
            return false;
        }

        private void ResolveReferences()
        {
            if ((playerTransform != null || _playerMovement != null) &&
                scatterBudgetController != null &&
                worldSliceDirector != null)
                return;

            float now = Time.realtimeSinceStartup;
            if (now < _nextAutoResolveAttemptTime)
                return;

            _nextAutoResolveAttemptTime = now + Mathf.Max(0f, autoResolveRetryInterval);

            ApplyPlayerRuntimeContext();

            if (playerTransform != null && _playerMovement == null)
                playerTransform.TryGetComponent(out _playerMovement);

#if UNITY_EDITOR
            if (playerTransform == null && !Application.isPlaying)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);
#endif
            WorldRuntimeReferenceUtility.TryResolveScatterBudgetController(ref scatterBudgetController);
            WorldRuntimeReferenceUtility.TryResolveWorldSliceDirector(ref worldSliceDirector);
        }

        private void ApplyPlayerRuntimeContext()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext == null)
                return;

            if (playerTransform == null)
                playerTransform = playerContext.PlayerTransform;

            _playerMovement = playerContext.PlayerMovement;
        }

        private static string ResolveInterestKindLabel(WorldInterestAnchor.InterestKind kind)
        {
            return kind switch
            {
                WorldInterestAnchor.InterestKind.ResourceField => KindResourceFieldLabel,
                WorldInterestAnchor.InterestKind.Fabrication => KindFabricationLabel,
                WorldInterestAnchor.InterestKind.ToolRange => KindToolRangeLabel,
                WorldInterestAnchor.InterestKind.Construction => KindConstructionLabel,
                WorldInterestAnchor.InterestKind.Power => KindPowerLabel,
                WorldInterestAnchor.InterestKind.Service => KindServiceLabel,
                _ => KindProgressionHubLabel
            };
        }
    }
}
