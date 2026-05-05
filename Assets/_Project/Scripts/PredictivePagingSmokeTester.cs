#if UNITY_EDITOR || DEVELOPMENT_BUILD
// ============================================================================
// HECTON-8 - PredictivePagingSmokeTester.cs
// Dev-only smoke for 20s velocity-projected indexed-sector paging math.
// ============================================================================

using Hecton8.SaveSystem;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Predictive Paging Smoke Tester")]
    public sealed class PredictivePagingSmokeTester : MonoBehaviour
    {
        [Header("Execution")]
        [SerializeField]
        private bool runOnStart;

        [Header("Math Inputs")]
        [SerializeField]
        private Vector3 currentRuntimePosition = new Vector3(990f, -25f, 0f);

        [SerializeField]
        private Vector3 currentWorldVelocity = new Vector3(2f, 0f, 0f);

        [SerializeField, Min(0.01f)]
        private float lookaheadSeconds = 20f;

        [SerializeField, Min(1)]
        private int chunkSizeMeters = 64;

#pragma warning disable CS0414
        [Header("Debug")]
        [SerializeField] private bool _debugLastPass;
        [SerializeField] private long _debugCurrentSectorHash;
        [SerializeField] private long _debugProjectedSectorHash;
        [SerializeField] private int3 _debugCurrentChunkId;
        [SerializeField] private int3 _debugProjectedChunkId;
#pragma warning restore CS0414

        private void Start()
        {
            if (runOnStart)
                RunPredictivePagingSmokePass();
        }

        [ContextMenu("Run Predictive Paging Smoke Pass")]
        public void RunPredictivePagingSmokePass()
        {
            _debugLastPass = false;
            if (!SaveManager.TryComputePredictiveIndexedPagingSectorHash(
                    currentRuntimePosition,
                    currentWorldVelocity,
                    lookaheadSeconds,
                    chunkSizeMeters,
                    out long currentSectorHash,
                    out long projectedSectorHash,
                    out int3 currentChunkId,
                    out int3 projectedChunkId))
            {
                Debug.LogError("[PredictivePagingSmokeTester] Predictive paging math rejected valid finite inputs.");
                return;
            }

            _debugCurrentSectorHash = currentSectorHash;
            _debugProjectedSectorHash = projectedSectorHash;
            _debugCurrentChunkId = currentChunkId;
            _debugProjectedChunkId = projectedChunkId;

            if (currentSectorHash == projectedSectorHash)
            {
                Debug.LogError("[PredictivePagingSmokeTester] 20s projection did not cross the expected paged sector boundary.");
                return;
            }

            if (math.all(currentChunkId == projectedChunkId))
            {
                Debug.LogError("[PredictivePagingSmokeTester] 20s projection did not cross the expected chunk boundary.");
                return;
            }

            if (SaveManager.TryComputePredictiveIndexedPagingSectorHash(
                    new Vector3(float.NaN, 0f, 0f),
                    currentWorldVelocity,
                    lookaheadSeconds,
                    chunkSizeMeters,
                    out _,
                    out _,
                    out _,
                    out _))
            {
                Debug.LogError("[PredictivePagingSmokeTester] Predictive paging math accepted NaN input.");
                return;
            }

            _debugLastPass = true;
            Debug.Log("[PredictivePagingSmokeTester] PASS: 20s velocity projection resolves next indexed sector and rejects NaN input.");
        }
    }
}
#endif
