using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4100)]
    public sealed class WorldSliceDirector : MonoBehaviour, ISlowTickable
    {
        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private WorldChunkStreamingProfile chunkStreamingProfile;

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

        private void Awake()
        {
            ResolvePlayer();
            RefreshChunkProfileScales();
            RefreshAnchors();
            UpdateDiagnostics(false);
        }

        private void OnEnable()
        {
            if (GameTickManager.Instance != null && !_registeredToTickManager)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredToTickManager = true;
            }
        }

        private void Start()
        {
            if (!_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredToTickManager = true;
            }

            ApplySlices(forceRefresh: true);
        }

        private void OnDisable()
        {
            if (_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister((ISlowTickable)this);
                _registeredToTickManager = false;
            }
        }

        public void SlowTick()
        {
            ApplySlices(forceRefresh: false);
        }

        public void RefreshAnchors()
        {
            _anchors.Clear();

            WorldSliceAnchor[] anchors = Resources.FindObjectsOfTypeAll<WorldSliceAnchor>();
            for (int i = 0; i < anchors.Length; i++)
            {
                WorldSliceAnchor anchor = anchors[i];
                if (anchor == null)
                    continue;

                GameObject go = anchor.gameObject;
                if (go == null || !go.scene.IsValid())
                    continue;

                _anchors.Add(anchor);
            }

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

            if (playerTransform == null)
            {
                UpdateDiagnostics(false);
                return;
            }

            Vector3 playerPosition = playerTransform.position;
            for (int i = 0; i < _anchors.Count; i++)
            {
                WorldSliceAnchor anchor = _anchors[i];
                if (anchor == null)
                    continue;

                Vector3 delta = anchor.transform.position - playerPosition;
                delta.y = 0f;
                anchor.ApplyForDistance(
                    delta.magnitude,
                    _profileNearDistanceScale * nearDistanceScale * interestNearDistanceScale * zoneNearDistanceScale,
                    _profileMidDistanceScale * midDistanceScale * interestMidDistanceScale * zoneMidDistanceScale);
            }

            UpdateDiagnostics(true);
        }

        private void ResolvePlayer()
        {
            if (playerTransform != null)
                return;

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
                player = GameObject.Find("Player");

            if (player != null)
                playerTransform = player.transform;
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
    }
}
