using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Low-cost near-camera swim blockout rig driven by swim presentation truth.
    /// Keeps visible forearm/glove mass in sync with stroke cadence without owning locomotion or camera offsets.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Player Swim Blockout Rig")]
    public sealed class PlayerSwimBlockoutRig : MonoBehaviour, ITickable
    {
        private const string LeftShoulderName = "Swim_LeftShoulder";
        private const string RightShoulderName = "Swim_RightShoulder";
        private const string LeftUpperArmName = "Swim_LeftUpperArm";
        private const string RightUpperArmName = "Swim_RightUpperArm";
        private const string LeftForearmName = "Swim_LeftForearm";
        private const string RightForearmName = "Swim_RightForearm";
        private const string LeftGloveName = "Swim_LeftGlove";
        private const string RightGloveName = "Swim_RightGlove";

        [Header("── References ─────────────────────────")]
        [Tooltip("Primary swim presentation owner publishing guide pose truth.")]
        [SerializeField] private PlayerSwimPresentationController swimPresentationController;

        [Tooltip("Optional explicit left forearm blockout transform.")]
        [SerializeField] private Transform leftForearm;

        [Tooltip("Optional explicit right forearm blockout transform.")]
        [SerializeField] private Transform rightForearm;

        [Tooltip("Optional explicit left shoulder blockout transform.")]
        [SerializeField] private Transform leftShoulder;

        [Tooltip("Optional explicit right shoulder blockout transform.")]
        [SerializeField] private Transform rightShoulder;

        [Tooltip("Optional explicit left upper-arm blockout transform.")]
        [SerializeField] private Transform leftUpperArm;

        [Tooltip("Optional explicit right upper-arm blockout transform.")]
        [SerializeField] private Transform rightUpperArm;

        [Tooltip("Optional explicit left glove blockout transform.")]
        [SerializeField] private Transform leftGlove;

        [Tooltip("Optional explicit right glove blockout transform.")]
        [SerializeField] private Transform rightGlove;

        [Tooltip("Optional explicit left forearm renderer.")]
        [SerializeField] private Renderer leftForearmRenderer;

        [Tooltip("Optional explicit right forearm renderer.")]
        [SerializeField] private Renderer rightForearmRenderer;

        [Tooltip("Optional explicit left shoulder renderer.")]
        [SerializeField] private Renderer leftShoulderRenderer;

        [Tooltip("Optional explicit right shoulder renderer.")]
        [SerializeField] private Renderer rightShoulderRenderer;

        [Tooltip("Optional explicit left upper-arm renderer.")]
        [SerializeField] private Renderer leftUpperArmRenderer;

        [Tooltip("Optional explicit right upper-arm renderer.")]
        [SerializeField] private Renderer rightUpperArmRenderer;

        [Tooltip("Optional explicit left glove renderer.")]
        [SerializeField] private Renderer leftGloveRenderer;

        [Tooltip("Optional explicit right glove renderer.")]
        [SerializeField] private Renderer rightGloveRenderer;

        [Header("── Visibility ─────────────────────────")]
        [Tooltip("How quickly blockout visibility follows swim presentation.")]
        [SerializeField, Range(1f, 20f)] private float visibilityBlendSpeed = 9f;

        [Tooltip("When visual weight falls below this, renderers are disabled entirely.")]
        [SerializeField, Range(0f, 0.2f)] private float rendererDisableThreshold = 0.035f;

        [Tooltip("Extra visibility multiplier for shallow wade presentation.")]
        [SerializeField, Range(0f, 1f)] private float shallowWadeVisibility = 0.16f;

        [Tooltip("Extra visibility multiplier for surface swim to preserve horizon readability.")]
        [SerializeField, Range(0f, 1f)] private float surfaceVisibility = 0.72f;

        [Header("── Mass Feel ───────────────────────────")]
        [Tooltip("Scale multiplier for light expedition swim blockout.")]
        [SerializeField, Range(0.7f, 1.3f)] private float lightSuitScale = 0.92f;

        [Tooltip("Scale multiplier for utility swim blockout.")]
        [SerializeField, Range(0.7f, 1.3f)] private float utilitySuitScale = 1f;

        [Tooltip("Scale multiplier for heavy industrial swim blockout.")]
        [SerializeField, Range(0.7f, 1.5f)] private float heavySuitScale = 1.14f;

        [Tooltip("Scale multiplier for powered-assist swim blockout.")]
        [SerializeField, Range(0.7f, 1.3f)] private float poweredAssistScale = 0.96f;

        [Tooltip("How much sprint presentation thickens the rig silhouette.")]
        [SerializeField, Range(0f, 0.25f)] private float sprintBulkBoost = 0.08f;

        [Tooltip("How much surface presentation flattens the blockout vertically.")]
        [SerializeField, Range(0f, 0.4f)] private float surfaceVerticalCompression = 0.12f;

        [Tooltip("How much upper-arm thickness grows beyond the authored shoulder blockout.")]
        [SerializeField, Range(0.8f, 1.6f)] private float upperArmThicknessScale = 1.08f;

        [Tooltip("How much shoulder blockout thickness grows beyond the forearm silhouette.")]
        [SerializeField, Range(0.8f, 1.8f)] private float shoulderThicknessScale = 1.18f;

        [Header("── Diagnostics ────────────────────────")]
        [SerializeField] private float _debugVisualWeight;
        [SerializeField] private float _debugLeftVisualWeight;
        [SerializeField] private float _debugRightVisualWeight;
        [SerializeField] private float _debugSuitScale = 1f;
        [SerializeField] private bool _debugRenderersVisible;

        private bool _registered;
        private float _visualWeight;
        private float _leftVisualWeight;
        private float _rightVisualWeight;
        private Vector3 _leftShoulderBaseScale = Vector3.one;
        private Vector3 _rightShoulderBaseScale = Vector3.one;
        private Vector3 _leftUpperArmBaseScale = Vector3.one;
        private Vector3 _rightUpperArmBaseScale = Vector3.one;
        private Vector3 _leftForearmBaseScale = Vector3.one;
        private Vector3 _rightForearmBaseScale = Vector3.one;
        private Vector3 _leftGloveBaseScale = Vector3.one;
        private Vector3 _rightGloveBaseScale = Vector3.one;

        private void Awake()
        {
            AutoResolveReferences();
            CacheBaseScales();
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void Start()
        {
            AutoResolveReferences();
            CacheBaseScales();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            AutoResolveReferences();
            CacheBaseScales();
        }
#endif

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (dt <= 0f)
                return;

            if (swimPresentationController == null)
            {
                AutoResolveReferences();
                if (swimPresentationController == null)
                    return;
            }

            SwimPresentationProfile profile = swimPresentationController.CurrentProfile;
            PlayerSwimPresentationMode mode = swimPresentationController.CurrentMode;
            float guideWeight = swimPresentationController.CurrentGuideWeight;
            float leftGuideWeight = swimPresentationController.CurrentLeftGuideWeight;
            float rightGuideWeight = swimPresentationController.CurrentRightGuideWeight;

            float targetWeight = ResolveTargetWeight(mode, guideWeight);
            float targetLeftWeight = ResolveTargetWeight(mode, leftGuideWeight);
            float targetRightWeight = ResolveTargetWeight(mode, rightGuideWeight);
            float t = 1f - math.exp(-visibilityBlendSpeed * dt);
            _visualWeight = math.lerp(_visualWeight, targetWeight, t);
            _leftVisualWeight = math.lerp(_leftVisualWeight, targetLeftWeight, t);
            _rightVisualWeight = math.lerp(_rightVisualWeight, targetRightWeight, t);

            float suitScale = ResolveSuitScale(profile);
            float sprintBoost = mode == PlayerSwimPresentationMode.UnderwaterSprint ? sprintBulkBoost : 0f;
            float verticalCompression = mode == PlayerSwimPresentationMode.SurfaceTread ||
                                        mode == PlayerSwimPresentationMode.SurfaceStroke
                ? 1f - surfaceVerticalCompression
                : 1f;

            bool renderersVisible = _visualWeight > rendererDisableThreshold;
            ApplyPart(
                leftShoulder,
                leftShoulderRenderer,
                _leftShoulderBaseScale,
                _leftVisualWeight,
                suitScale * shoulderThicknessScale,
                sprintBoost,
                verticalCompression,
                1.15f);
            ApplyPart(
                rightShoulder,
                rightShoulderRenderer,
                _rightShoulderBaseScale,
                _rightVisualWeight,
                suitScale * shoulderThicknessScale,
                sprintBoost,
                verticalCompression,
                1.15f);
            ApplyPart(
                leftForearm,
                leftForearmRenderer,
                _leftForearmBaseScale,
                _leftVisualWeight,
                suitScale,
                sprintBoost,
                verticalCompression,
                1f);
            ApplyPart(
                rightForearm,
                rightForearmRenderer,
                _rightForearmBaseScale,
                _rightVisualWeight,
                suitScale,
                sprintBoost,
                verticalCompression,
                1f);
            ApplyPart(
                leftGlove,
                leftGloveRenderer,
                _leftGloveBaseScale,
                _leftVisualWeight,
                suitScale * 1.02f,
                sprintBoost,
                verticalCompression,
                1.12f);
            ApplyPart(
                rightGlove,
                rightGloveRenderer,
                _rightGloveBaseScale,
                _rightVisualWeight,
                suitScale * 1.02f,
                sprintBoost,
                verticalCompression,
                1.12f);
            ApplyUpperArm(
                leftUpperArm,
                leftUpperArmRenderer,
                leftShoulder,
                leftForearm,
                _leftUpperArmBaseScale,
                _leftVisualWeight,
                suitScale * upperArmThicknessScale,
                sprintBoost,
                verticalCompression);
            ApplyUpperArm(
                rightUpperArm,
                rightUpperArmRenderer,
                rightShoulder,
                rightForearm,
                _rightUpperArmBaseScale,
                _rightVisualWeight,
                suitScale * upperArmThicknessScale,
                sprintBoost,
                verticalCompression);

            _debugVisualWeight = _visualWeight;
            _debugLeftVisualWeight = _leftVisualWeight;
            _debugRightVisualWeight = _rightVisualWeight;
            _debugSuitScale = suitScale;
            _debugRenderersVisible = renderersVisible;
        }

        private void TryRegister()
        {
            if (_registered)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager == null)
                return;

            gameTickManager.Register(this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GameTickManager gameTickManager = GameTickManager.Instance;
            if (gameTickManager != null)
                gameTickManager.Unregister(this);

            _registered = false;
        }

        private void AutoResolveReferences()
        {
            if (swimPresentationController == null)
                gameObject.TryGetComponent(out swimPresentationController);

            Transform root = swimPresentationController != null
                ? swimPresentationController.transform
                : transform;

            if (leftShoulder == null)
                leftShoulder = FindTransformRecursive(root, LeftShoulderName);

            if (rightShoulder == null)
                rightShoulder = FindTransformRecursive(root, RightShoulderName);

            if (leftUpperArm == null)
                leftUpperArm = FindTransformRecursive(root, LeftUpperArmName);

            if (rightUpperArm == null)
                rightUpperArm = FindTransformRecursive(root, RightUpperArmName);

            if (leftForearm == null)
                leftForearm = FindTransformRecursive(root, LeftForearmName);

            if (rightForearm == null)
                rightForearm = FindTransformRecursive(root, RightForearmName);

            if (leftGlove == null)
                leftGlove = FindTransformRecursive(root, LeftGloveName);

            if (rightGlove == null)
                rightGlove = FindTransformRecursive(root, RightGloveName);

            if (leftForearmRenderer == null && leftForearm != null)
                leftForearm.TryGetComponent(out leftForearmRenderer);

            if (rightForearmRenderer == null && rightForearm != null)
                rightForearm.TryGetComponent(out rightForearmRenderer);

            if (leftShoulderRenderer == null && leftShoulder != null)
                leftShoulder.TryGetComponent(out leftShoulderRenderer);

            if (rightShoulderRenderer == null && rightShoulder != null)
                rightShoulder.TryGetComponent(out rightShoulderRenderer);

            if (leftUpperArmRenderer == null && leftUpperArm != null)
                leftUpperArm.TryGetComponent(out leftUpperArmRenderer);

            if (rightUpperArmRenderer == null && rightUpperArm != null)
                rightUpperArm.TryGetComponent(out rightUpperArmRenderer);

            if (leftGloveRenderer == null && leftGlove != null)
                leftGlove.TryGetComponent(out leftGloveRenderer);

            if (rightGloveRenderer == null && rightGlove != null)
                rightGlove.TryGetComponent(out rightGloveRenderer);
        }

        private void CacheBaseScales()
        {
            if (leftShoulder != null)
                _leftShoulderBaseScale = leftShoulder.localScale;

            if (rightShoulder != null)
                _rightShoulderBaseScale = rightShoulder.localScale;

            if (leftUpperArm != null)
                _leftUpperArmBaseScale = leftUpperArm.localScale;

            if (rightUpperArm != null)
                _rightUpperArmBaseScale = rightUpperArm.localScale;

            if (leftForearm != null)
                _leftForearmBaseScale = leftForearm.localScale;

            if (rightForearm != null)
                _rightForearmBaseScale = rightForearm.localScale;

            if (leftGlove != null)
                _leftGloveBaseScale = leftGlove.localScale;

            if (rightGlove != null)
                _rightGloveBaseScale = rightGlove.localScale;
        }

        private float ResolveTargetWeight(PlayerSwimPresentationMode mode, float guideWeight)
        {
            switch (mode)
            {
                case PlayerSwimPresentationMode.ShallowWade:
                    return math.saturate(guideWeight) * shallowWadeVisibility;

                case PlayerSwimPresentationMode.SurfaceTread:
                case PlayerSwimPresentationMode.SurfaceStroke:
                    return math.saturate(guideWeight) * surfaceVisibility;

                case PlayerSwimPresentationMode.Dry:
                case PlayerSwimPresentationMode.None:
                    return 0f;

                default:
                    return math.saturate(guideWeight);
            }
        }

        private float ResolveSuitScale(SwimPresentationProfile profile)
        {
            if (profile == null)
                return utilitySuitScale;

            switch (profile.AuthoredStrokeStyle)
            {
                case SwimPresentationProfile.StrokeStyle.LightExpedition:
                    return lightSuitScale;

                case SwimPresentationProfile.StrokeStyle.HeavyIndustrial:
                    return heavySuitScale;

                case SwimPresentationProfile.StrokeStyle.PoweredAssist:
                    return poweredAssistScale;

                default:
                    return utilitySuitScale;
            }
        }

        private void ApplyPart(
            Transform part,
            Renderer partRenderer,
            Vector3 baseScale,
            float visibilityWeight,
            float suitScale,
            float sprintBoost,
            float verticalCompression,
            float gloveThicknessBoost)
        {
            if (part == null)
                return;

            float visibility = math.saturate(visibilityWeight);
            Vector3 scaled = baseScale;
            float bulkScale = suitScale + sprintBoost;

            scaled.x *= bulkScale * visibility * gloveThicknessBoost;
            scaled.y *= bulkScale * visibility * verticalCompression;
            scaled.z *= bulkScale * visibility;
            part.localScale = scaled;

            bool rendererVisible = visibility > rendererDisableThreshold;
            if (partRenderer != null && partRenderer.enabled != rendererVisible)
                partRenderer.enabled = rendererVisible;
        }

        private void ApplyUpperArm(
            Transform upperArm,
            Renderer upperArmRenderer,
            Transform shoulder,
            Transform forearm,
            Vector3 baseScale,
            float visibilityWeight,
            float suitScale,
            float sprintBoost,
            float verticalCompression)
        {
            if (upperArm == null || shoulder == null || forearm == null)
                return;

            float visibility = math.saturate(visibilityWeight);
            bool rendererVisible = visibility > rendererDisableThreshold;
            if (upperArmRenderer != null && upperArmRenderer.enabled != rendererVisible)
                upperArmRenderer.enabled = rendererVisible;

            if (!rendererVisible)
                return;

            Vector3 shoulderPosition = shoulder.position;
            Vector3 forearmPosition = forearm.position;
            Vector3 direction = forearmPosition - shoulderPosition;
            float distance = direction.magnitude;
            if (distance <= 0.0001f)
                return;

            Vector3 midpoint = shoulderPosition + direction * 0.5f;
            Quaternion rotation = Quaternion.LookRotation(direction / distance, transform.up);
            upperArm.SetPositionAndRotation(midpoint, rotation);

            float bulkScale = suitScale + sprintBoost;
            Vector3 scaled = baseScale;
            scaled.x *= bulkScale * visibility;
            scaled.y *= bulkScale * visibility * verticalCompression;
            scaled.z = distance;
            upperArm.localScale = scaled;
        }

        private static Transform FindTransformRecursive(Transform parent, string transformName)
        {
            if (parent == null)
                return null;

            if (parent.name == transformName)
                return parent;

            int childCount = parent.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform match = FindTransformRecursive(parent.GetChild(i), transformName);
                if (match != null)
                    return match;
            }

            return null;
        }
    }
}
