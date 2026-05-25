using UnityEngine;

namespace Hecton8.AI
{
    public enum FaunaPOIType
    {
        SafeZone,
        HuntingPoint,
        EscapePoint
    }

    // ============================================================================
    // HECTON-8 - FaunaPOI.cs
    // Static Point of Interest target for Fauna AI. Requires a trigger collider
    // allowing AI to sample nearby POIs using OverlapSphereNonAlloc without GC.
    // ============================================================================
    [RequireComponent(typeof(SphereCollider))]
    public class FaunaPOI : MonoBehaviour
    {
        private const float MinPoiRadius = 0.01f;
#if UNITY_EDITOR
        private const int MaxEditorValidateDepth = 4;
        private static int _editorValidateDepth;
#endif

        [Header("Configuration")]
        [Tooltip("Defines how creatures interpret this POI.")]
        public FaunaPOIType poiType = FaunaPOIType.SafeZone;

        [Tooltip("Radius of the POI influence (syncs to SphereCollider radius automatically).")]
        public float radius = 10f;

        private SphereCollider _collider;

        private void Awake()
        {
            TryGetComponent(out _collider);
            _collider.isTrigger = true;
            radius = GetSafeRadius();
            _collider.radius = radius;
            
            // Set layer to POI if available (must be configured by project owner to match sensor's poiMask)
            // gameObject.layer = LayerMask.NameToLayer("FaunaPOI"); 
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _editorValidateDepth++;
            if (_editorValidateDepth > MaxEditorValidateDepth)
            {
                _editorValidateDepth--;
                Debug.LogError("FaunaPOI editor validation watchdog tripped.", this);
                return;
            }

            try
            {
                radius = GetSafeRadius();
                if (_collider == null)
                    TryGetComponent(out _collider);

                if (_collider != null)
                {
                    _collider.isTrigger = true;
                    _collider.radius = radius;
                }
            }
            finally
            {
                _editorValidateDepth--;
            }
        }

        private void OnDrawGizmosSelected()
        {
            float safeRadius = GetSafeRadius();
            if (safeRadius <= 0f)
                return;

            Gizmos.color = poiType switch
            {
                FaunaPOIType.SafeZone => new Color(0f, 1f, 0f, 0.3f),
                FaunaPOIType.HuntingPoint => new Color(1f, 0f, 0f, 0.3f),
                FaunaPOIType.EscapePoint => new Color(0f, 0f, 1f, 0.3f),
                _ => new Color(1f, 1f, 1f, 0.3f)
            };
            Gizmos.DrawWireSphere(transform.position, safeRadius);
        }
#endif

        private float GetSafeRadius()
        {
            if (!float.IsFinite(radius))
                return MinPoiRadius;

            return radius >= MinPoiRadius ? radius : MinPoiRadius;
        }
    }
}
