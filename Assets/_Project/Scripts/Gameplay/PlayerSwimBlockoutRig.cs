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
    public sealed partial class PlayerSwimBlockoutRig : MonoBehaviour, ITickable, IUpdatable
    {
        private const string LeftShoulderName = "Swim_LeftShoulder";
        private const string RightShoulderName = "Swim_RightShoulder";
        private const string LeftUpperArmName = "Swim_LeftUpperArm";
        private const string RightUpperArmName = "Swim_RightUpperArm";
        private const string LeftForearmName = "Swim_LeftForearm";
        private const string RightForearmName = "Swim_RightForearm";
        private const string LeftGloveName = "Swim_LeftGlove";
        private const string RightGloveName = "Swim_RightGlove";
        private const string LeftShoulderAttachmentName = "Swim_LeftShoulderAttachment";
        private const string RightShoulderAttachmentName = "Swim_RightShoulderAttachment";
        private const string LeftUpperArmAttachmentName = "Swim_LeftUpperArmAttachment";
        private const string RightUpperArmAttachmentName = "Swim_RightUpperArmAttachment";
        private const string LeftForearmAttachmentName = "Swim_LeftForearmAttachment";
        private const string RightForearmAttachmentName = "Swim_RightForearmAttachment";
        private const string LeftHandAttachmentName = "Swim_LeftHandAttachment";
        private const string RightHandAttachmentName = "Swim_RightHandAttachment";
        private const string ViewmodelRootName = "Swim_ViewmodelRoot";
        private const int MaxHierarchyTraversalDepth = 64;
        private const int MaxHierarchyTraversalNodes = 512;
        private const int FirstPersonToolsLayerIndex = 18;

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

        [Header("── Attachment API ───────────────────────")]
        [Tooltip("If false, debug blockout cubes are hidden while all rig transforms and attachment points continue updating.")]
        [SerializeField] private bool showDebugCubes = true;

        [Tooltip("Stable left shoulder attachment for future authored art.")]
        [SerializeField] private Transform leftShoulderAttachment;

        [Tooltip("Stable right shoulder attachment for future authored art.")]
        [SerializeField] private Transform rightShoulderAttachment;

        [Tooltip("Stable left upper-arm attachment for future authored art.")]
        [SerializeField] private Transform leftUpperArmAttachment;

        [Tooltip("Stable right upper-arm attachment for future authored art.")]
        [SerializeField] private Transform rightUpperArmAttachment;

        [Tooltip("Stable left forearm attachment for future authored art.")]
        [SerializeField] private Transform leftForearmAttachment;

        [Tooltip("Stable right forearm attachment for future authored art.")]
        [SerializeField] private Transform rightForearmAttachment;

        [Tooltip("Stable left hand attachment for future authored art.")]
        [SerializeField] private Transform leftHandAttachment;

        [Tooltip("Stable right hand attachment for future authored art.")]
        [SerializeField] private Transform rightHandAttachment;

        [Tooltip("Optional explicit swim viewmodel root. If missing, auto-resolved by name.")]
        [SerializeField] private Transform viewmodelRoot;

        [Header("── Visibility ─────────────────────────")]
        [Tooltip("How quickly blockout visibility follows swim presentation.")]
        [SerializeField, Range(1f, 20f)] private float visibilityBlendSpeed = 9f;

        [Tooltip("When visual weight falls below this, renderers are disabled entirely.")]
        [SerializeField, Range(0f, 0.2f)] private float rendererDisableThreshold = 0.035f;

        [Tooltip("Extra visibility multiplier for shallow wade presentation.")]
        [SerializeField, Range(0f, 1f)] private float shallowWadeVisibility = 0.16f;

        [Tooltip("Extra visibility multiplier for surface swim to preserve horizon readability.")]
        [SerializeField, Range(0f, 1f)] private float surfaceVisibility = 0.72f;

        [Tooltip("Minimum visibility floor for surface tread so blockout arms do not disappear entirely while the player is still floating at the top band.")]
        [SerializeField, Range(0f, 0.5f)] private float surfaceTreadVisibilityFloor = 0.14f;

        [Tooltip("Minimum visibility floor for surface stroke so near-camera swim mass stays visible during flatter surface locomotion.")]
        [SerializeField, Range(0f, 0.5f)] private float surfaceStrokeVisibilityFloor = 0.12f;

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

        [Header("â”€â”€ Body Connection â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Minimum shoulder visibility contribution while any swim presentation is active. Keeps the arm visually connected to the torso.")]
        [SerializeField, Range(0f, 0.5f)] private float shoulderConnectionVisibilityFloor = 0.16f;

        [Tooltip("Minimum upper-arm visibility contribution while any swim presentation is active. Prevents forearms from reading as detached cubes.")]
        [SerializeField, Range(0f, 0.5f)] private float upperArmConnectionVisibilityFloor = 0.12f;

        [Tooltip("How much shoulder X movement follows the hand-guide pose delta. Keeps the arm chain connected when framing pushes hands outward.")]
        [SerializeField, Range(0f, 0.6f)] private float shoulderGuideHorizontalFollow = 0.24f;

        [Tooltip("How much shoulder Y movement follows the hand-guide pose delta. Keeps shoulders from feeling nailed in place.")]
        [SerializeField, Range(0f, 0.6f)] private float shoulderGuideVerticalFollow = 0.18f;

        [Tooltip("How much shoulder Z movement follows the hand-guide pose delta. Helps the whole arm sit back into the torso.")]
        [SerializeField, Range(0f, 0.6f)] private float shoulderGuideDepthFollow = 0.14f;


        [Header("── Diagnostics ────────────────────────")]
#if UNITY_EDITOR
        [SerializeField] private float _debugVisualWeight;
        [SerializeField] private float _debugLeftVisualWeight;
        [SerializeField] private float _debugRightVisualWeight;
        [SerializeField] private float _debugSuitScale = 1f;
        [SerializeField] private bool _debugRenderersVisible;
        [SerializeField] private bool _debugAttachmentsResolved;
        [SerializeField] private int _debugLastDrivenFrame = -1;
#endif

        /// <summary>Whether debug blockout cubes remain visible.</summary>
        public bool ShowDebugCubes => showDebugCubes;

        /// <summary>Enables or disables debug blockout mesh rendering while keeping the rig and attachments alive.</summary>
        public void SetDebugCubesVisible(bool visible)
        {
            showDebugCubes = visible;
            ForceSyncAttachmentPoints();
        }

        /// <summary>Forces all attachment points to snap to the current animated rig, even when debug cubes are hidden.</summary>
        public void ForceSyncAttachmentPoints()
        {
            ApplyAttachmentPose(leftShoulderAttachment, leftShoulder);
            ApplyAttachmentPose(rightShoulderAttachment, rightShoulder);
            ApplyAttachmentPose(leftUpperArmAttachment, leftUpperArm);
            ApplyAttachmentPose(rightUpperArmAttachment, rightUpperArm);
            ApplyAttachmentPose(leftForearmAttachment, leftForearm);
            ApplyAttachmentPose(rightForearmAttachment, rightForearm);
            ApplyAttachmentPose(leftHandAttachment, leftGlove);
            ApplyAttachmentPose(rightHandAttachment, rightGlove);
            ForceSyncFullBodyAttachmentPoints();
            RefreshAttachmentDebugState();
        }

        /// <summary>Stable left shoulder attachment for future authored art.</summary>
        public Transform LeftShoulderAttachment => leftShoulderAttachment != null ? leftShoulderAttachment : leftShoulder;

        /// <summary>Stable right shoulder attachment for future authored art.</summary>
        public Transform RightShoulderAttachment => rightShoulderAttachment != null ? rightShoulderAttachment : rightShoulder;

        /// <summary>Stable left upper-arm attachment for future authored art.</summary>
        public Transform LeftUpperArmAttachment => leftUpperArmAttachment != null ? leftUpperArmAttachment : leftUpperArm;

        /// <summary>Stable right upper-arm attachment for future authored art.</summary>
        public Transform RightUpperArmAttachment => rightUpperArmAttachment != null ? rightUpperArmAttachment : rightUpperArm;

        /// <summary>Stable left forearm attachment for future authored art.</summary>
        public Transform LeftForearmAttachment => leftForearmAttachment != null ? leftForearmAttachment : leftForearm;

        /// <summary>Stable right forearm attachment for future authored art.</summary>
        public Transform RightForearmAttachment => rightForearmAttachment != null ? rightForearmAttachment : rightForearm;

        /// <summary>Stable left hand attachment for future authored art.</summary>
        public Transform LeftHandAttachment => leftHandAttachment != null ? leftHandAttachment : leftGlove;

        /// <summary>Stable right hand attachment for future authored art.</summary>
        public Transform RightHandAttachment => rightHandAttachment != null ? rightHandAttachment : rightGlove;

        private bool _registered;
        private int _firstPersonToolsLayer = -1;
        private float _visualWeight;
        private float _leftVisualWeight;
        private float _rightVisualWeight;
        private bool _hasInitializedVisibleState;
        private Vector3 _leftShoulderBaseLocalPosition;
        private Vector3 _rightShoulderBaseLocalPosition;
        private Vector3 _leftShoulderBaseScale = Vector3.one;
        private Vector3 _rightShoulderBaseScale = Vector3.one;
        private Vector3 _leftUpperArmBaseScale = Vector3.one;
        private Vector3 _rightUpperArmBaseScale = Vector3.one;
        private Vector3 _leftForearmBaseScale = Vector3.one;
        private Vector3 _rightForearmBaseScale = Vector3.one;
        private Vector3 _leftGloveBaseScale = Vector3.one;
        private Vector3 _rightGloveBaseScale = Vector3.one;
        private int _lastDrivenFrame = -1;

        private void Awake()
        {
            _firstPersonToolsLayer = FirstPersonToolsLayerIndex;
            AutoResolveReferences();
            CacheBaseScales();
            RefreshAttachmentDebugState();
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void Start()
        {
            if (_firstPersonToolsLayer < 0)
                _firstPersonToolsLayer = FirstPersonToolsLayerIndex;

            AutoResolveReferences();
            CacheBaseScales();
            RefreshAttachmentDebugState();
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
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            _firstPersonToolsLayer = FirstPersonToolsLayerIndex;
            AutoResolveReferences();
            CacheBaseScales();
            RefreshAttachmentDebugState();
        }
#endif

        /// <inheritdoc />
        public void Tick(float dt)
        {
            SyncFromPresentation(dt);
        }

        /// <summary>Applies the current swim presentation frame to the blockout rig. Safe to call from the presentation owner.</summary>
        public void SyncFromPresentation(float dt, bool forceFrame = false)
        {
            int frame = Time.frameCount;
            if (!forceFrame && _lastDrivenFrame == frame)
                return;

            _lastDrivenFrame = frame;
#if UNITY_EDITOR
            _debugLastDrivenFrame = frame;
#endif

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
            if (!_hasInitializedVisibleState && (targetWeight > 0f || targetLeftWeight > 0f || targetRightWeight > 0f))
            {
                _visualWeight = targetWeight;
                _leftVisualWeight = targetLeftWeight;
                _rightVisualWeight = targetRightWeight;
                _hasInitializedVisibleState = true;
            }

            float t = 1f - math.exp(-visibilityBlendSpeed * dt);
            _visualWeight = math.lerp(_visualWeight, targetWeight, t);
            _leftVisualWeight = math.lerp(_leftVisualWeight, targetLeftWeight, t);
            _rightVisualWeight = math.lerp(_rightVisualWeight, targetRightWeight, t);
            float leftShoulderWeight = ResolveConnectionWeight(_leftVisualWeight, _visualWeight, shoulderConnectionVisibilityFloor);
            float rightShoulderWeight = ResolveConnectionWeight(_rightVisualWeight, _visualWeight, shoulderConnectionVisibilityFloor);
            float leftUpperArmWeight = ResolveConnectionWeight(_leftVisualWeight, _visualWeight, upperArmConnectionVisibilityFloor);
            float rightUpperArmWeight = ResolveConnectionWeight(_rightVisualWeight, _visualWeight, upperArmConnectionVisibilityFloor);
            ApplyShoulderPose(
                leftShoulder,
                _leftShoulderBaseLocalPosition,
                swimPresentationController.CurrentLeftGuideLocalPosition,
                profile != null ? profile.LeftGuideBaseLocalPosition : Vector3.zero,
                leftShoulderWeight);
            ApplyShoulderPose(
                rightShoulder,
                _rightShoulderBaseLocalPosition,
                swimPresentationController.CurrentRightGuideLocalPosition,
                profile != null ? profile.RightGuideBaseLocalPosition : Vector3.zero,
                rightShoulderWeight);

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
                leftShoulderWeight,
                suitScale * shoulderThicknessScale,
                sprintBoost,
                verticalCompression,
                1.15f);
            ApplyPart(
                rightShoulder,
                rightShoulderRenderer,
                _rightShoulderBaseScale,
                rightShoulderWeight,
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
                leftUpperArmWeight,
                suitScale * upperArmThicknessScale,
                sprintBoost,
                verticalCompression);
            ApplyUpperArm(
                rightUpperArm,
                rightUpperArmRenderer,
                rightShoulder,
                rightForearm,
                _rightUpperArmBaseScale,
                rightUpperArmWeight,
                suitScale * upperArmThicknessScale,
                sprintBoost,
                verticalCompression);
            ApplyFullBodyPose(mode, profile, suitScale, sprintBoost, verticalCompression, dt);

            ForceSyncAttachmentPoints();

#if UNITY_EDITOR
            _debugVisualWeight = _visualWeight;
            _debugLeftVisualWeight = _leftVisualWeight;
            _debugRightVisualWeight = _rightVisualWeight;
            _debugSuitScale = suitScale;
            _debugRenderersVisible = renderersVisible && showDebugCubes;
#endif
            RefreshAttachmentDebugState();
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registered = false;
        }

        private void AutoResolveReferences()
        {
            if (swimPresentationController == null)
                gameObject.TryGetComponent(out swimPresentationController);

            Transform root = swimPresentationController != null
                ? swimPresentationController.transform
                : transform;

            if (viewmodelRoot == null)
                viewmodelRoot = FindTransformRecursive(root, ViewmodelRootName);

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

            if (leftShoulderAttachment == null)
                leftShoulderAttachment = FindTransformRecursive(root, LeftShoulderAttachmentName);

            if (rightShoulderAttachment == null)
                rightShoulderAttachment = FindTransformRecursive(root, RightShoulderAttachmentName);

            if (leftUpperArmAttachment == null)
                leftUpperArmAttachment = FindTransformRecursive(root, LeftUpperArmAttachmentName);

            if (rightUpperArmAttachment == null)
                rightUpperArmAttachment = FindTransformRecursive(root, RightUpperArmAttachmentName);

            if (leftForearmAttachment == null)
                leftForearmAttachment = FindTransformRecursive(root, LeftForearmAttachmentName);

            if (rightForearmAttachment == null)
                rightForearmAttachment = FindTransformRecursive(root, RightForearmAttachmentName);

            if (leftHandAttachment == null)
                leftHandAttachment = FindTransformRecursive(root, LeftHandAttachmentName);

            if (rightHandAttachment == null)
                rightHandAttachment = FindTransformRecursive(root, RightHandAttachmentName);

            AutoResolveFullBodyReferences(root);

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

            EnforceViewmodelLayer();
            RefreshAttachmentDebugState();
        }

        private void EnforceViewmodelLayer()
        {
            if (_firstPersonToolsLayer < 0)
                return;

            Transform root = viewmodelRoot;
            if (root == null)
                root = leftForearm != null ? leftForearm.parent != null ? leftForearm.parent.parent : null : null;
            if (root == null)
                return;

            ApplyLayerRecursive(root, _firstPersonToolsLayer);
        }

        private static void ApplyLayerRecursive(Transform root, int layer)
        {
            int visitedNodeCount = 0;
            ApplyLayerRecursive(root, layer, 0, ref visitedNodeCount);
        }

        private static void ApplyLayerRecursive(Transform root, int layer, int depth, ref int visitedNodeCount)
        {
            if (root == null || depth > MaxHierarchyTraversalDepth || visitedNodeCount >= MaxHierarchyTraversalNodes)
                return;

            visitedNodeCount++;
            root.gameObject.layer = layer;
            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
            {
                ApplyLayerRecursive(root.GetChild(i), layer, depth + 1, ref visitedNodeCount);
                if (visitedNodeCount >= MaxHierarchyTraversalNodes)
                    break;
            }
        }

        private void RefreshAttachmentDebugState()
        {
#if UNITY_EDITOR
            _debugAttachmentsResolved =
                leftShoulderAttachment != null &&
                rightShoulderAttachment != null &&
                leftUpperArmAttachment != null &&
                rightUpperArmAttachment != null &&
                leftForearmAttachment != null &&
                rightForearmAttachment != null &&
                leftHandAttachment != null &&
                rightHandAttachment != null &&
                AreFullBodyAttachmentsResolved();
#endif
        }

        private void CacheBaseScales()
        {
            if (leftShoulder != null)
                _leftShoulderBaseLocalPosition = leftShoulder.localPosition;

            if (rightShoulder != null)
                _rightShoulderBaseLocalPosition = rightShoulder.localPosition;

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

            CacheFullBodyBaseScales();
        }

        private float ResolveTargetWeight(PlayerSwimPresentationMode mode, float guideWeight)
        {
            switch (mode)
            {
                case PlayerSwimPresentationMode.ShallowWade:
                    return math.saturate(guideWeight) * shallowWadeVisibility;

                case PlayerSwimPresentationMode.SurfaceTread:
                    return math.max(math.saturate(guideWeight) * surfaceVisibility, surfaceTreadVisibilityFloor);

                case PlayerSwimPresentationMode.SurfaceStroke:
                    return math.max(math.saturate(guideWeight) * surfaceVisibility, surfaceStrokeVisibilityFloor);

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

        private static float ResolveConnectionWeight(float limbWeight, float overallWeight, float floor)
        {
            float visibility = math.saturate(limbWeight);
            float connectionFloor = math.saturate(overallWeight) * floor;
            return math.max(visibility, connectionFloor);
        }

        private void ApplyShoulderPose(
            Transform shoulder,
            Vector3 baseLocalPosition,
            Vector3 guideLocalPosition,
            Vector3 guideBaseLocalPosition,
            float visibilityWeight)
        {
            if (shoulder == null)
                return;

            float visibility = math.saturate(visibilityWeight);
            Vector3 guideDelta = guideLocalPosition - guideBaseLocalPosition;
            Vector3 targetLocalPosition = baseLocalPosition;
            targetLocalPosition.x += guideDelta.x * shoulderGuideHorizontalFollow;
            targetLocalPosition.y += guideDelta.y * shoulderGuideVerticalFollow;
            targetLocalPosition.z += guideDelta.z * shoulderGuideDepthFollow;
            shoulder.localPosition = Vector3.Lerp(baseLocalPosition, targetLocalPosition, visibility);
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

            bool rendererVisible = showDebugCubes && visibility > rendererDisableThreshold;
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
            bool rendererVisible = showDebugCubes && visibility > rendererDisableThreshold;
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

        private static void ApplyAttachmentPose(Transform attachment, Transform source)
        {
            if (attachment == null || source == null)
                return;

            attachment.SetPositionAndRotation(source.position, source.rotation);
            if (attachment.localScale != Vector3.one)
                attachment.localScale = Vector3.one;
        }

        private static Transform FindTransformRecursive(Transform parent, string transformName)
        {
            int visitedNodeCount = 0;
            return FindTransformRecursive(parent, transformName, 0, ref visitedNodeCount);
        }

        private static Transform FindTransformRecursive(Transform parent, string transformName, int depth, ref int visitedNodeCount)
        {
            if (parent == null || depth > MaxHierarchyTraversalDepth || visitedNodeCount >= MaxHierarchyTraversalNodes)
                return null;

            visitedNodeCount++;
            if (parent.name == transformName)
                return parent;

            int childCount = parent.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform match = FindTransformRecursive(parent.GetChild(i), transformName, depth + 1, ref visitedNodeCount);
                if (match != null)
                    return match;

                if (visitedNodeCount >= MaxHierarchyTraversalNodes)
                    break;
            }

            return null;
        }
    }
}
