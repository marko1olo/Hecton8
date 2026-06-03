using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Hazards/Light Culling Proxy")]
    public sealed class LightCullingProxy : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const float PropertyWriteEpsilon = 0.0001f;

        [SerializeField] private Light targetLight;
        [SerializeField] private DecalProjector targetDecalProjector;
        [SerializeField] private float maxDistanceMeters = 32f;
        [SerializeField] private float hysteresisMeters = 4f;
        [SerializeField] private float minimumQualityWeight = 0.05f;
        [SerializeField] private bool disableDecalWithLight = true;
        [SerializeField] private bool managePresentationScalars = true;

        private IPlayerRuntimeContext _playerRuntime;
        private Transform _cachedTransform;
        private bool _lateFrameRegistered;
        private bool _hotSwapRegistered;
        private bool _visible = true;
        private float _baseIntensity = 1f;
        private float _baseRange = 8f;
        private float _baseDecalFade = 1f;
        private float _lastAppliedIntensity = -1f;
        private float _lastAppliedRange = -1f;
        private float _lastAppliedDecalFade = -1f;

        public Light TargetLight => targetLight;
        public DecalProjector TargetDecalProjector => targetDecalProjector;
        public bool HasValidFactoryConfiguration => targetLight != null && targetDecalProjector != null;
        public bool ManagesPresentationScalars => managePresentationScalars;

        public void ConfigureForEditor(
            Light light,
            DecalProjector decalProjector,
            float cullDistanceMeters,
            float hysteresisDistanceMeters,
            float minimumQuality,
            bool manageScalars = true)
        {
            targetLight = light;
            targetDecalProjector = decalProjector;
            maxDistanceMeters = Mathf.Max(1f, SanitizeNonNegative(cullDistanceMeters, 32f));
            hysteresisMeters = Mathf.Clamp(SanitizeNonNegative(hysteresisDistanceMeters, 4f), 0.5f, maxDistanceMeters);
            minimumQualityWeight = Mathf.Clamp01(SanitizeNonNegative(minimumQuality, 0.05f));
            disableDecalWithLight = true;
            managePresentationScalars = manageScalars;
            CacheBasePresentationState();
        }

        private void Awake()
        {
            _cachedTransform = transform;
            if (targetLight == null)
                TryGetComponent(out targetLight);
            if (targetDecalProjector == null)
                TryGetComponent(out targetDecalProjector);

            EnforceNoShadows();
            CacheBasePresentationState();
            RefreshColdRegistryReferences();
        }

        private void OnEnable()
        {
            EnforceNoShadows();
            RefreshColdRegistryReferences();
            TryRegisterLateFrame();
            TryRegisterHotSwapListener();
        }

        private void OnDisable()
        {
            TryUnregisterLateFrame();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            TryUnregisterLateFrame();
            TryUnregisterHotSwapListener();
        }

        public void LateFrameTick()
        {
            Light light = targetLight;
            if (light == null)
            {
                TryUnregisterLateFrame();
                return;
            }

            float quality = Sanitize01(HomeostasisBrain.GlobalQualityWeight);
            bool hasPlayer = TryResolvePlayerPosition(out Vector3 playerPosition);
            bool shouldEnable = quality > minimumQualityWeight && hasPlayer;
            if (!shouldEnable)
                _visible = false;

            if (shouldEnable)
            {
                Transform self = _cachedTransform;
                Vector3 hazardPosition = self != null ? self.position : Vector3.zero;
                Vector3 delta = playerPosition - hazardPosition;
                float distanceSq = delta.sqrMagnitude;
                float qualityDistanceScale = Mathf.Lerp(0.65f, 1.35f, quality);
                float cullDistance = Mathf.Max(1f, maxDistanceMeters * qualityDistanceScale);
                float offDistance = cullDistance + hysteresisMeters;
                float onDistance = Mathf.Max(0.5f, cullDistance - hysteresisMeters);

                if (_visible)
                    _visible = distanceSq <= offDistance * offDistance;
                else
                    _visible = distanceSq <= onDistance * onDistance;

                shouldEnable = _visible;
            }

            ApplyPresentation(light, shouldEnable, quality);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
                _playerRuntime = currentService as IPlayerRuntimeContext;
        }

        private void ApplyPresentation(Light light, bool shouldEnable, float quality)
        {
            if (light.shadows != LightShadows.None)
                light.shadows = LightShadows.None;

            DecalProjector decal = targetDecalProjector;
            if (!shouldEnable)
            {
                if (light.enabled)
                    light.enabled = false;

                if (decal != null && disableDecalWithLight && decal.enabled)
                    decal.enabled = false;
                return;
            }

            float curve = quality * quality * (3f - (2f * quality));
            if (!light.enabled)
                light.enabled = true;

            if (!managePresentationScalars)
            {
                if (decal != null && disableDecalWithLight && !decal.enabled)
                    decal.enabled = true;
                return;
            }

            float targetIntensity = _baseIntensity * curve;
            float targetRange = Mathf.Max(0.5f, _baseRange * Mathf.Lerp(0.7f, 1.15f, curve));
            if (ShouldApplyProperty(_lastAppliedIntensity, targetIntensity))
            {
                light.intensity = targetIntensity;
                _lastAppliedIntensity = targetIntensity;
            }
            if (ShouldApplyProperty(_lastAppliedRange, targetRange))
            {
                light.range = targetRange;
                _lastAppliedRange = targetRange;
            }

            if (decal == null || !disableDecalWithLight)
                return;

            if (!decal.enabled)
                decal.enabled = true;
            float targetFade = _baseDecalFade * curve;
            if (ShouldApplyProperty(_lastAppliedDecalFade, targetFade))
            {
                decal.fadeFactor = targetFade;
                _lastAppliedDecalFade = targetFade;
            }
        }

        private bool TryResolvePlayerPosition(out Vector3 position)
        {
            IPlayerRuntimeContext playerRuntime = _playerRuntime;
            if (playerRuntime != null &&
                playerRuntime.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                math.all(math.isfinite(snapshot.RuntimePosition)))
            {
                position = new Vector3(snapshot.RuntimePosition.x, snapshot.RuntimePosition.y, snapshot.RuntimePosition.z);
                return true;
            }
            position = Vector3.zero;
            return false;
        }

        private void CacheBasePresentationState()
        {
            if (targetLight != null)
            {
                _baseIntensity = Mathf.Max(0f, targetLight.intensity);
                _baseRange = Mathf.Max(0.5f, targetLight.range);
            }

            if (targetDecalProjector != null)
                _baseDecalFade = Mathf.Clamp01(targetDecalProjector.fadeFactor);

            _lastAppliedIntensity = -1f;
            _lastAppliedRange = -1f;
            _lastAppliedDecalFade = -1f;
        }

        private void EnforceNoShadows()
        {
            if (targetLight != null)
                targetLight.shadows = LightShadows.None;
        }

        private void RefreshColdRegistryReferences()
        {
            _playerRuntime = GlobalRegistry.Player;
        }

        private void TryRegisterLateFrame()
        {
            if (_lateFrameRegistered || !Application.isPlaying || targetLight == null)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _lateFrameRegistered = false;
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

        private void OnValidate()
        {
            maxDistanceMeters = Mathf.Max(1f, SanitizeNonNegative(maxDistanceMeters, 32f));
            hysteresisMeters = Mathf.Clamp(SanitizeNonNegative(hysteresisMeters, 4f), 0.5f, maxDistanceMeters);
            minimumQualityWeight = Mathf.Clamp01(SanitizeNonNegative(minimumQualityWeight, 0.05f));
            EnforceNoShadows();
        }

        private static float Sanitize01(float value)
        {
            return IsFinite(value) ? Mathf.Clamp01(value) : 0f;
        }

        private static float SanitizeNonNegative(float value, float fallback)
        {
            return IsFinite(value) ? Mathf.Max(0f, value) : fallback;
        }

        private static bool ShouldApplyProperty(float previous, float next)
        {
            return !IsFinite(previous) || Mathf.Abs(previous - next) > PropertyWriteEpsilon;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
