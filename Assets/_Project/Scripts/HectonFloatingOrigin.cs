// ============================================================================
// HECTON-8 — HectonFloatingOrigin.cs
// Centralized system to resolve floating-point precision issues in large worlds.
//
// DESIGN:
//   - Monitors anchor (Camera/Player) distance from (0,0,0).
//   - When threshold is exceeded, shifts ALL root GameObjects by -offset.
//   - Notifies subscribers via OnWorldShift event.
//
// ZERO GC:
//   - Distance check in Tick() is allocation-free.
//   - Shift logic uses pre-allocated List for root objects.
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton8.Bootstrap;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hecton8.Core;

namespace Hecton8.Core
{
    /// <summary>
    /// Manages the world origin shift to maintain 1:1 precision within a 1km radius.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public sealed class HectonFloatingOrigin : MonoBehaviour, ITickable
    {
        // ══════════════════════════════════════════════════════════
        //  SINGLETON & EVENTS
        // ══════════════════════════════════════════════════════════

        private static HectonFloatingOrigin _instance;
        public static HectonFloatingOrigin Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            OnWorldShift = null;
        }

        /// <summary>
        /// Fired immediately after the world has shifted.
        /// Parameter: the world-space offset applied to all objects.
        /// </summary>
        public static event Action<Vector3> OnWorldShift;

        /// <summary>
        /// Cumulative offset applied to the world origin since startup.
        /// </summary>
        public Vector3 TotalOffset { get; private set; }

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Settings ────────────────────────────────")]
        [Tooltip("Distance from (0,0,0) that triggers a shift.")]
        [SerializeField] private float _threshold = 1000f;

        [Tooltip("Object to follow (normally Player). If null, resolves via SceneBootstrap.")]
        [SerializeField] private Transform _anchor;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private bool _isRegistered;
        private readonly List<GameObject> _cachedRootObjects = new List<GameObject>(256);
        private float _thresholdSqr;
        private float _anchorResolveTimer;
        private const float AnchorResolveCooldown = 1f;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            RefreshThresholdCache();
            TryResolveAnchor(force: true);
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();

            if (_instance == this)
            {
                _instance = null;
                OnWorldShift = null; // Clear static event on teardown
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable — Distance Monitoring
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            // Fail-safe anchor acquisition
            if (_anchor == null)
            {
                _anchorResolveTimer -= deltaTime;
                if (_anchorResolveTimer > 0f)
                    return;

                TryResolveAnchor(force: false);
                if (_anchor == null)
                    return;
            }

            // Check distance from origin (Zero-GC)
            Vector3 pos = _anchor.position;
            if (pos.sqrMagnitude > _thresholdSqr)
            {
                ShiftWorld(pos);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  SHIFT LOGIC
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Moves all root objects in all loaded scenes by -offset.
        /// </summary>
        private void ShiftWorld(Vector3 offset)
        {
            // 1. Iterate all active loaded scenes
            int sceneCount = SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                // 2. Get root objects (Zero-GC with cached list)
                _cachedRootObjects.Clear();
                scene.GetRootGameObjects(_cachedRootObjects);

                // 3. Subtract offset from all root transforms
                for (int j = 0; j < _cachedRootObjects.Count; j++)
                {
                    GameObject go = _cachedRootObjects[j];
                    if (go == null) continue;

                    // Note: We shift everything. Components relying on world-space
                    // coordinates must subscribe to OnWorldShift to compensate.
                    go.transform.position -= offset;
                }
            }

            // 4. Update internal Unity state
            TotalOffset += offset;
            UnityEngine.Physics.SyncTransforms();

            // 5. Notify specialized systems (Voxel, VFX, Sound emitters)
            OnWorldShift?.Invoke(offset);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[FloatingOrigin] World shifted by {offset}. Anchor returned to approx (0,0,0).");
#endif
        }

        private void TryResolveAnchor(bool force)
        {
            if (_anchor != null)
                return;

            if (!force && _anchorResolveTimer > 0f)
                return;

            _anchorResolveTimer = AnchorResolveCooldown;

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
                _anchor = playerTransform;
        }

        private void RefreshThresholdCache()
        {
            if (_threshold < 1f)
                _threshold = 1f;

            _thresholdSqr = _threshold * _threshold;
        }

        private void TryRegister()
        {
            if (_isRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register(this);
            _isRegistered = true;
        }

        private void TryUnregister()
        {
            if (!_isRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister(this);

            _isRegistered = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RefreshThresholdCache();
        }
#endif
    }
}
