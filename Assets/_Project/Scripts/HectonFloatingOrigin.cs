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

        [Tooltip("Object to follow (Player or Camera). If null, uses Camera.main.")]
        [SerializeField] private Transform _anchor;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private bool _isRegistered;
        private readonly List<GameObject> _cachedRootObjects = new List<GameObject>(256);

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

            // Ensure anchor is assigned
            if (_anchor == null && Camera.main != null)
            {
                _anchor = Camera.main.transform;
            }
        }

        private void OnEnable()
        {
            if (GameTickManager.Instance != null && !_isRegistered)
            {
                GameTickManager.Instance.Register(this);
                _isRegistered = true;
            }
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance != null && _isRegistered)
            {
                GameTickManager.Instance.Unregister(this);
                _isRegistered = false;
            }
        }

        private void OnDestroy()
        {
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
                if (Camera.main != null) _anchor = Camera.main.transform;
                else return;
            }

            // Check distance from origin (Zero-GC)
            Vector3 pos = _anchor.position;
            if (pos.magnitude > _threshold)
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

            Debug.Log($"[FloatingOrigin] World shifted by {offset}. Anchor returned to approx (0,0,0).");
        }
    }
}
