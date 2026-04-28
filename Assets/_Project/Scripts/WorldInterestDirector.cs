using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4075)]
    public sealed class WorldInterestDirector : MonoBehaviour, ISlowTickable
    {
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

        private readonly List<WorldInterestAnchor> _anchors = new List<WorldInterestAnchor>(24);
        private bool _registeredToTickManager;
        private float _nextAutoResolveAttemptTime = float.NegativeInfinity;

        private void Awake()
        {
            ResolveReferences();
            RefreshAnchors();
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void Start()
        {
            TryRegister();

            ApplyInterest(forceRefresh: true);
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void TryRegister()
        {
            if (_registeredToTickManager)
                return;


            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTickManager = true;
        }

        private void TryUnregister()
        {
            if (!_registeredToTickManager)
                return;

                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registeredToTickManager = false;
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

            if (playerTransform == null || scatterBudgetController == null)
            {
                _debugApplied = false;
                return;
            }

            Vector3 playerPosition = playerTransform.position;
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

                float influence = anchor.EvaluateInfluence(playerPosition);
                if (influence <= 0.001f)
                    continue;

                scavengeScale = Mathf.Max(scavengeScale, Mathf.Lerp(1f, anchor.ScavengeRadiusScale, influence));
                spawnScale = Mathf.Max(spawnScale, Mathf.Lerp(1f, anchor.SpawnScale, influence));
                colliderRadiusScale = Mathf.Max(colliderRadiusScale, Mathf.Lerp(1f, anchor.ColliderRadiusScale, influence));
                colliderOpsScale = Mathf.Max(colliderOpsScale, Mathf.Lerp(1f, anchor.ColliderOpsScale, influence));
                sliceNearScale = Mathf.Max(sliceNearScale, Mathf.Lerp(1f, anchor.SliceNearScale, influence));
                sliceMidScale = Mathf.Max(sliceMidScale, Mathf.Lerp(1f, anchor.SliceMidScale, influence));

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

            _debugDominantAnchor = bestAnchor != null ? bestAnchor.InterestLabel : "None";
            _debugDominantKind = bestAnchor != null ? bestAnchor.Kind.ToString() : "None";
            _debugDominantInfluence = bestInfluence;
            _debugAnchorCount = _anchors.Count;
            _debugApplied = true;
        }

        private void ResolveReferences()
        {
            if (playerTransform != null &&
                scatterBudgetController != null &&
                worldSliceDirector != null)
                return;

            float now = Time.realtimeSinceStartup;
            if (now < _nextAutoResolveAttemptTime)
                return;

            _nextAutoResolveAttemptTime = now + Mathf.Max(0f, autoResolveRetryInterval);

            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);
            WorldRuntimeReferenceUtility.TryResolveScatterBudgetController(ref scatterBudgetController);
            WorldRuntimeReferenceUtility.TryResolveWorldSliceDirector(ref worldSliceDirector);
        }
    }
}
