using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4100)]
    public sealed class WorldSliceDirector : MonoBehaviour, ISlowTickable
    {
        internal static WorldSliceDirector ActiveRuntimeInstance { get; private set; }

        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private WorldChunkStreamingProfile chunkStreamingProfile;

        [Header("Runtime Auto Resolve")]
        [SerializeField, Min(0f)] private float autoResolveRetryInterval = 1f;

        [Header("Runtime Scales")]
        [SerializeField] private float nearDistanceScale = 1f;
        [SerializeField] private float midDistanceScale = 1f;
        [SerializeField] private float interestNearDistanceScale = 1f;
        [SerializeField] private float interestMidDistanceScale = 1f;
        [SerializeField] private float zoneNearDistanceScale = 1f;
        [SerializeField] private float zoneMidDistanceScale = 1f;

        [Header("Diagnostics")]
        [SerializeField] private int _debugSliceCount;
        [SerializeField] private bool _debugPlayerReady;
        [SerializeField] private bool _debugApplied;
        [SerializeField] private float _debugProfileNearScale = 1f;
        [SerializeField] private float _debugProfileMidScale = 1f;

        private readonly List<WorldSliceAnchor> _anchors = new List<WorldSliceAnchor>(32);
        private bool _registeredToTickManager;
        private float _profileNearDistanceScale = 1f;
        private float _profileMidDistanceScale = 1f;
        private float _nextAutoResolveAttemptTime = float.NegativeInfinity;

        private void Awake()
        {
            ActiveRuntimeInstance = this;
            ResolvePlayer();
            RefreshChunkProfileScales();
            RefreshAnchors();
            UpdateDiagnostics(false);
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void Start()
        {
            TryRegister();

            ApplySlices(forceRefresh: true);
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();

            if (ActiveRuntimeInstance == this)
                ActiveRuntimeInstance = null;
        }

        private void TryRegister()
        {
            if (_registeredToTickManager || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTickManager = GlobalRegistry.SlowTickables.Contains(this);
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
            ApplySlices(forceRefresh: false);
        }

        public void RefreshAnchors()
        {
            WorldSliceAnchor.CopyActiveAnchorsTo(_anchors);
            _debugSliceCount = _anchors.Count;
        }

        public void SetDistanceScales(float nearScale, float midScale)
        {
            nearDistanceScale = Mathf.Clamp(nearScale, 0.6f, 1.4f);
            midDistanceScale = Mathf.Clamp(midScale, 0.6f, 1.5f);
        }

        public void SetChunkStreamingProfile(WorldChunkStreamingProfile profile)
        {
            chunkStreamingProfile = profile;
            RefreshChunkProfileScales();
        }

        public void SetInterestScales(float nearScale, float midScale)
        {
            interestNearDistanceScale = Mathf.Clamp(nearScale, 0.75f, 1.4f);
            interestMidDistanceScale = Mathf.Clamp(midScale, 0.8f, 1.5f);
        }

        public void SetZoneScales(float nearScale, float midScale)
        {
            zoneNearDistanceScale = Mathf.Clamp(nearScale, 0.75f, 1.4f);
            zoneMidDistanceScale = Mathf.Clamp(midScale, 0.8f, 1.5f);
        }

        private void ApplySlices(bool forceRefresh)
        {
            ResolvePlayer();
            if (forceRefresh || _anchors.Count == 0)
                RefreshAnchors();

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            {
                UpdateDiagnostics(false);
                return;
            }

            float resolvedNearDistanceScale =
                _profileNearDistanceScale *
                nearDistanceScale *
                interestNearDistanceScale *
                zoneNearDistanceScale;
            float resolvedMidDistanceScale =
                _profileMidDistanceScale *
                midDistanceScale *
                interestMidDistanceScale *
                zoneMidDistanceScale;
            for (int i = 0; i < _anchors.Count; i++)
            {
                WorldSliceAnchor anchor = _anchors[i];
                if (anchor == null)
                    continue;

                float planarDistanceSq = anchor.GetPlanarDistanceSq(in playerAup);
                anchor.ApplyForDistanceSq(
                    planarDistanceSq,
                    resolvedNearDistanceScale,
                    resolvedMidDistanceScale);
            }

            UpdateDiagnostics(true);
        }

        private void ResolvePlayer()
        {
            if (playerTransform != null)
                return;

            float now = Time.realtimeSinceStartup;
            if (now < _nextAutoResolveAttemptTime)
                return;

            _nextAutoResolveAttemptTime = now + Mathf.Max(0f, autoResolveRetryInterval);
            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);
        }

        private void RefreshChunkProfileScales()
        {
            if (chunkStreamingProfile != null)
            {
                WorldChunkStreamingProfile.LayerProfile terrainLayer =
                    chunkStreamingProfile.GetLayerProfileOrDefault(WorldStreamingLayer.TerrainLod);
                _profileNearDistanceScale = Mathf.Clamp(terrainLayer.nearRadiusScale, 0.7f, 1.8f);
                _profileMidDistanceScale = Mathf.Clamp(terrainLayer.midRadiusScale, 0.7f, 1.8f);
            }
            else
            {
                _profileNearDistanceScale = 1f;
                _profileMidDistanceScale = 1f;
            }

            _debugProfileNearScale = _profileNearDistanceScale;
            _debugProfileMidScale = _profileMidDistanceScale;
        }

        private void UpdateDiagnostics(bool applied)
        {
            _debugSliceCount = _anchors.Count;
            _debugPlayerReady = playerTransform != null;
            _debugApplied = applied;
            _debugProfileNearScale = _profileNearDistanceScale;
            _debugProfileMidScale = _profileMidDistanceScale;
        }

        private static bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;

            IPlayerRuntimeContext playerContext = Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext;
            if (playerContext == null)
                return false;

            if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                playerAup = snapshot.Aup;
                return playerAup.IsFinite();
            }

            var playerMovement = playerContext.PlayerMovement;
            if (playerMovement == null)
                return false;

            playerAup = playerMovement.CurrentAup;
            return playerAup.IsFinite();
        }
    }
}
