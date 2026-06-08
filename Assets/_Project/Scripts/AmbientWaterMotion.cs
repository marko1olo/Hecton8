// ============================================================================
// HECTON-8 - AmbientWaterMotion.cs
// Cheap visual-only bob/sway for decorative props. No Rigidbody required.
// Updated by AmbientWaterMotionManager with distance LOD.
// ============================================================================

using Hecton8.Core;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Physics
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Physics/Ambient Water Motion")]
    public sealed class AmbientWaterMotion : MonoBehaviour, IGlobalRegistryHotSwapListener
    {
        private const uint PhaseHashMultiplier = 747796405u;
        private const uint PhaseHashIncrement = 2891336453u;
        private const float PhaseStepRadians = 0.006135923151542565f;

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
        private AbsoluteUniversePosition _restAup;
        private bool _hasRestAup;
        private float _phase;
        private byte _managerDistanceLodBand;
        private AmbientWaterMotionManager _registeredManager;
        private bool _hotSwapRegistered;

        public Transform CachedTransform => _cachedTransform;
        public Vector3 RestLocalPosition => _restLocalPosition;
        public Quaternion RestLocalRotation => _restLocalRotation;
        public AbsoluteUniversePosition RestAup => _restAup;
        public bool HasRestAup => _hasRestAup;
        public float VerticalAmplitude => verticalAmplitude;
        public Vector3 PositionalAmplitude => positionalAmplitude;
        public Vector3 AngularAmplitude => angularAmplitude;
        public float BaseFrequency => baseFrequency;
        public float CurrentCoupling => currentCoupling;
        public bool AllowDistanceLod => allowDistanceLod;
        public float LodBias => lodBias;
        public float Phase => _phase;
        public AmbientWaterMotionProfile Profile => profile;
        internal byte ManagerDistanceLodBand
        {
            get => _managerDistanceLodBand;
            set => _managerDistanceLodBand = value;
        }

        private void Awake()
        {
            ApplyProfileIfNeeded();
            SanitizeTuning();
            _cachedTransform = transform;
            CaptureRestPose();

            uint seed = unchecked((uint)EntityId.ToULong(GetEntityId()));
            seed = unchecked((seed * PhaseHashMultiplier) + PhaseHashIncrement);
            _phase = (seed & 1023u) * PhaseStepRadians;
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            RebindManager(GlobalRegistry.AmbientWaterMotion);
        }

        private void OnDisable()
        {
            UnregisterFromManager();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            UnregisterFromManager();
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.AmbientWaterMotionRuntime)
                return;

            if (ReferenceEquals(_registeredManager, previousService))
                UnregisterFromManager();

            RebindManager(currentService as AmbientWaterMotionManager);
        }

        public void CaptureRestPose()
        {
            _cachedTransform ??= transform;
            Vector3 localPosition = _cachedTransform.localPosition;
            Quaternion localRotation = _cachedTransform.localRotation;
            Vector3 worldPosition = _cachedTransform.position;

            _restLocalPosition = IsFinite(localPosition) ? localPosition : Vector3.zero;
            _restLocalRotation = IsFinite(localRotation) ? localRotation : Quaternion.identity;
            if (!IsFinite(worldPosition))
            {
                _restAup = default;
                _hasRestAup = false;
                return;
            }

            _restAup = AbsoluteUniversePosition.FromRuntimePosition(worldPosition);
            _hasRestAup = _restAup.IsFinite();
        }

        public void ApplyProfile()
        {
            if (profile == null)
                return;

            verticalAmplitude = AmbientWaterMotionProfile.ResolveAmplitude(profile.verticalAmplitude);
            positionalAmplitude = AmbientWaterMotionProfile.ResolvePositionalAmplitude(profile.positionalAmplitude);
            angularAmplitude = AmbientWaterMotionProfile.ResolveAngularAmplitude(profile.angularAmplitude);
            baseFrequency = AmbientWaterMotionProfile.ResolveFrequency(profile.baseFrequency);
            currentCoupling = AmbientWaterMotionProfile.ResolveCurrentCoupling(profile.currentCoupling);
            allowDistanceLod = profile.allowDistanceLod;
            lodBias = AmbientWaterMotionProfile.ResolveLodBias(profile.lodBias);
        }

        private void ApplyProfileIfNeeded()
        {
            if (autoApplyProfile && profile != null)
                ApplyProfile();
        }

        private void RebindManager(AmbientWaterMotionManager manager)
        {
            if (ReferenceEquals(_registeredManager, manager))
                return;

            UnregisterFromManager();
            if (!isActiveAndEnabled || manager == null)
                return;

            if (manager.Register(this))
                _registeredManager = manager;
        }

        private void UnregisterFromManager()
        {
            AmbientWaterMotionManager manager = _registeredManager;
            if (manager != null)
                manager.Unregister(this);

            _registeredManager = null;
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

        private void SanitizeTuning()
        {
            verticalAmplitude = AmbientWaterMotionProfile.ResolveAmplitude(verticalAmplitude);
            positionalAmplitude = AmbientWaterMotionProfile.ResolvePositionalAmplitude(positionalAmplitude);
            angularAmplitude = AmbientWaterMotionProfile.ResolveAngularAmplitude(angularAmplitude);
            baseFrequency = AmbientWaterMotionProfile.ResolveFrequency(baseFrequency);
            currentCoupling = AmbientWaterMotionProfile.ResolveCurrentCoupling(currentCoupling);
            lodBias = AmbientWaterMotionProfile.ResolveLodBias(lodBias);
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion rotation)
        {
            return float.IsFinite(rotation.x) &&
                   float.IsFinite(rotation.y) &&
                   float.IsFinite(rotation.z) &&
                   float.IsFinite(rotation.w);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyProfileIfNeeded();
            SanitizeTuning();
            if (!Application.isPlaying)
                CaptureRestPose();
        }
#endif
    }
}
