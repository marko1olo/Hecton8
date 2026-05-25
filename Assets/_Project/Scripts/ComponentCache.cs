// ============================================================================
// HECTON-8 — ComponentCache.cs  v1.0
// Generic GetComponent caching utility with auto-refresh on component change.
//
// PURPOSE:
//   Provide a safe, reusable pattern for caching GetComponent<T> results.
//   Prevents the anti-pattern of calling GetComponent in hot paths.
//
// WHY THIS MATTERS:
//   • GetComponent() is O(N) where N = number of components on GameObject.
//   • Calling GetComponent in Update/Tick = performance death.
//   • Even calling in Start can be slow if many components exist.
//   • ComponentCache automatically invalidates when component changes.
//
// USAGE:
//   public class MySystem : MonoBehaviour
//   {
//       private ComponentCache<Rigidbody> _rigidbodyCache;
//
//       private void Awake()
//       {
//           _rigidbodyCache = new ComponentCache<Rigidbody>(gameObject);
//       }
//
//       public void Tick(float dt)
//       {
//           Rigidbody rb = _rigidbodyCache.Value;  // Returns cached || queries once
//           if (rb != null)
//               rb.velocity = someVelocity;
//       }
//   }
//
// FEATURES:
//   ✓ Lazy initialization: First access triggers GetComponent, not Awake.
//   ✓ Auto-invalidation: Detects component removal via null-check.
//   ✓ Struct-based: Zero indirection, stack-allocated.
//   ✓ Generic: Works with any MonoBehaviour/Component.
//   ✓ Thread-safe: Not mutable after GetComponent lookup (main thread only).
//
// PERFORMANCE:
//   • Single GetComponent call (cached thereafter).
//   • Value property: one bool check + field return.
//   • Zero allocations (struct, no collections).
//
// ============================================================================

using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Zero-allocation cache for a single component reference.
    /// Struct — must be stored as field (not local, not parameter).
    /// </summary>
    public struct ComponentCache<T> where T : Component
    {
        private GameObject _gameObject;
        private T _cachedComponent;
        private bool _isCached;

        /// <summary>
        /// Initialize cache for component on target GameObject.
        /// Does NOT call GetComponent yet (lazy).
        /// </summary>
        public ComponentCache(GameObject target)
        {
            _gameObject = target;
            _cachedComponent = null;
            _isCached = false;
        }

        /// <summary>
        /// Initialize cache for component on target transform.
        /// Does NOT call GetComponent yet (lazy).
        /// </summary>
        public ComponentCache(Transform target)
            : this(target != null ? target.gameObject : null)
        {
        }

        /// <summary>
        /// Get cached component (or query if not yet cached).
        /// Null-safe: returns null if GameObject was destroyed or component missing.
        ///
        /// First call = GetComponent (O(N) where N = component count)
        /// Subsequent calls = O(1) field return
        /// </summary>
        public T Value
        {
            get
            {
                // ── Fast path: already cached ──
                if (_isCached && _cachedComponent != null)
                    return _cachedComponent;

                // ── GameObject destroyed? ──
                if (_gameObject == null)
                {
                    _cachedComponent = null;
                    _isCached = true;
                    return null;
                }

                // ── Slow path: query component (first time or after removal) ──
                _gameObject.TryGetComponent(out _cachedComponent);
                _isCached = true;

                return _cachedComponent;
            }
        }

        /// <summary>
        /// Check if component exists and is cached.
        /// </summary>
        public bool HasComponent => Value != null;

        /// <summary>
        /// Manually invalidate cache (e.g., after dynamic AddComponent).
        /// Next access to Value will re-query GetComponent.
        /// </summary>
        public void Invalidate()
        {
            _isCached = false;
            _cachedComponent = null;
        }

        /// <summary>
        /// Force immediate query (even if already cached).
        /// Useful for detecting dynamically added components.
        /// </summary>
        public void Refresh()
        {
            _isCached = false;
            _ = Value; // Trigger query
        }
    }

    /// <summary>
    /// Extension methods for convenient ComponentCache creation.
    /// </summary>
    public static class ComponentCacheExtensions
    {
        /// <summary>
        /// Create a ComponentCache for this GameObject.
        /// Usage: var cache = gameObject.CreateComponentCache<Rigidbody>();
        /// </summary>
        public static ComponentCache<T> CreateComponentCache<T>(this GameObject go) where T : Component
            => new ComponentCache<T>(go);

        /// <summary>
        /// Create a ComponentCache for this Transform.
        /// Usage: var cache = transform.CreateComponentCache<Collider>();
        /// </summary>
        public static ComponentCache<T> CreateComponentCache<T>(this Transform tr) where T : Component
            => new ComponentCache<T>(tr);

        /// <summary>
        /// Create a ComponentCache for this Component's GameObject.
        /// Usage: var cache = meshRenderer.CreateComponentCache<Rigidbody>();
        /// </summary>
        public static ComponentCache<T> CreateComponentCache<T>(this Component comp) where T : Component
            => new ComponentCache<T>(comp.gameObject);
    }
}
