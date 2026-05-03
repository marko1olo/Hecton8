// ============================================================================
// HECTON-8 — AmbientWaterMotion.cs
// Cheap visual-only bob/sway for decorative props. No Rigidbody required.
// Updated by AmbientWaterMotionManager with distance LOD.
// ============================================================================

using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Physics
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Physics/Ambient Water Motion")]
    public sealed class AmbientWaterMotion : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField] private AmbientWaterMotionProfile profile;
        [SerializeField] private bool autoApplyProfile = true;
        [Header("Offsets")]
        [SerializeField] private float verticalAmplitude = 0.06f;
        [SerializeField] private Vector3 positionalAmplitude = new Vector3(0.04f, 0f, 0.04f);

        [Header("Rotation")]
        [SerializeField] private Vector3 angularAmplitude = new Vector3(3f, 1.5f, 4f);

        [Header("Timing")]
        [SerializeField] private float baseFrequency = 0.45f;
        [SerializeField] private float currentCoupling = 0.6f;
        [SerializeField] private bool allowDistanceLod = true;
        [SerializeField] private float lodBias = 1f;

        private Transform _cachedTransform;
        private Vector3 _restLocalPosition;
        private Quaternion _restLocalRotation;
        private float _phase;

        public Transform CachedTransform => _cachedTransform;
        public Vector3 RestLocalPosition => _restLocalPosition;
        public Quaternion RestLocalRotation => _restLocalRotation;
        public float VerticalAmplitude => verticalAmplitude;
        public Vector3 PositionalAmplitude => positionalAmplitude;
        public Vector3 AngularAmplitude => angularAmplitude;
        public float BaseFrequency => baseFrequency;
        public float CurrentCoupling => currentCoupling;
        public bool AllowDistanceLod => allowDistanceLod;
        public float LodBias => lodBias;
        public float Phase => _phase;
        public AmbientWaterMotionProfile Profile => profile;

        private void Awake()
        {
            ApplyProfileIfNeeded();
            _cachedTransform = transform;
            CaptureRestPose();

            int id = unchecked((int)EntityId.ToULong(GetEntityId()));
            float seed = id < 0 ? -id : id;
            _phase = seed * 0.173f;
        }

        private void OnEnable()
        {
            AmbientWaterMotionManager manager = GlobalRegistry.AmbientWaterMotion;
            if (manager != null)
                manager.Register(this);
        }

        private void OnDisable()
        {
            AmbientWaterMotionManager manager = GlobalRegistry.AmbientWaterMotion;
            if (manager != null)
                manager.Unregister(this);
        }

        public void CaptureRestPose()
        {
            _cachedTransform ??= transform;
            _restLocalPosition = _cachedTransform.localPosition;
            _restLocalRotation = _cachedTransform.localRotation;
        }

        public void ApplyProfile()
        {
            if (profile == null)
                return;

            verticalAmplitude = profile.verticalAmplitude;
            positionalAmplitude = profile.positionalAmplitude;
            angularAmplitude = profile.angularAmplitude;
            baseFrequency = profile.baseFrequency;
            currentCoupling = profile.currentCoupling;
            allowDistanceLod = profile.allowDistanceLod;
            lodBias = profile.lodBias;
        }

        private void ApplyProfileIfNeeded()
        {
            if (autoApplyProfile && profile != null)
                ApplyProfile();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyProfileIfNeeded();
            if (baseFrequency < 0f) baseFrequency = 0f;
            if (lodBias < 0.1f) lodBias = 0.1f;
            if (!Application.isPlaying)
                CaptureRestPose();
        }
#endif
    }
}
