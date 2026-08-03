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
    public sealed partial class PlayerSwimBlockoutRig : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
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
        private const float Pi = 3.14159265359f;
        private const float TwoPi = 6.28318530718f;
        private const float HalfPi = 1.57079632679f;
        private const float DegreesToRadians = 0.01745329252f;

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

        private bool _registeredLateFrame;
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
        private bool _leftForearmVisible;
        private bool _rightForearmVisible;
        private bool _leftShoulderVisible;
        private bool _rightShoulderVisible;
        private bool _leftUpperArmVisible;
        private bool _rightUpperArmVisible;
        private bool _leftGloveVisible;
        private bool _rightGloveVisible;
        private bool _leftForearmVisibleDirty;
        private bool _rightForearmVisibleDirty;
        private bool _leftShoulderVisibleDirty;
        private bool _rightShoulderVisibleDirty;
        private bool _leftUpperArmVisibleDirty;
        private bool _rightUpperArmVisibleDirty;
        private bool _leftGloveVisibleDirty;
        private bool _rightGloveVisibleDirty;

        private void Awake()
        {
            _firstPersonToolsLayer = FirstPersonToolsLayerIndex;
            AutoResolveReferences();
            CacheBaseScales();
            RefreshAttachmentDebugState();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
                GlobalRegistry.TryRegisterHotSwapListener(this);

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
            GlobalRegistry.TryUnregisterHotSwapListener(this);
        }

        private void OnDestroy()
        {
            TryUnregister();
            GlobalRegistry.TryUnregisterHotSwapListener(this);
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

        public void LateFrameTick()
        {
            FlushQueuedRendererVisibility();
        }

        /// <summary>Applies the current swim presentation frame to the blockout rig. Safe to call from the presentation owner.</summary>
        public void SyncFromPresentation(float dt, bool forceFrame = false)
        {
            // L19 hop2 LIVE: ApplyUpperArm → ResolveLookRotationNoTrig ACCESS_VIOLATION
            // under -batchmode. Presentation-only bone path; hop probes only need locomotion intent.
            if (Application.isBatchMode)
                return;

            int frame = SystemDispatcher.CurrentFrameIndex;
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

            float t = ResolveDecayBlend(visibilityBlendSpeed, dt);
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
            if (!Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregister()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrame = false;
            }

        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregister();
            if (currentService != null && isActiveAndEnabled)
                TryRegister();
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

        private static float ResolveDecayBlend(float speed, float deltaTime)
        {
            float x = math.max(0f, speed) * math.max(0f, deltaTime);
            return math.saturate(x / (1f + x));
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
            shoulder.localPosition = ApproximateVectorLerp(baseLocalPosition, targetLocalPosition, visibility);
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
            QueueRendererVisibility(partRenderer, rendererVisible);
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
            QueueRendererVisibility(upperArmRenderer, rendererVisible);

            if (!rendererVisible)
                return;

            Vector3 shoulderPosition = shoulder.position;
            Vector3 forearmPosition = forearm.position;
            Vector3 direction = forearmPosition - shoulderPosition;
            float distanceSq = direction.sqrMagnitude;
            if (distanceSq <= 0.00000001f)
                return;

            float inverseDistance = math.rsqrt(distanceSq);
            float distance = distanceSq * inverseDistance;
            Vector3 midpoint = shoulderPosition + direction * 0.5f;
            Quaternion rotation = ResolveLookRotationNoTrig(direction * inverseDistance, transform.up);
            upperArm.SetPositionAndRotation(midpoint, rotation);

            float bulkScale = suitScale + sprintBoost;
            Vector3 scaled = baseScale;
            scaled.x *= bulkScale * visibility;
            scaled.y *= bulkScale * visibility * verticalCompression;
            scaled.z = distance;
            upperArm.localScale = scaled;
        }

        private void QueueRendererVisibility(Renderer renderer, bool visible)
        {
            if (renderer == null)
                return;

            if (ReferenceEquals(renderer, leftForearmRenderer))
            {
                _leftForearmVisible = visible;
                _leftForearmVisibleDirty = true;
            }
            else if (ReferenceEquals(renderer, rightForearmRenderer))
            {
                _rightForearmVisible = visible;
                _rightForearmVisibleDirty = true;
            }
            else if (ReferenceEquals(renderer, leftShoulderRenderer))
            {
                _leftShoulderVisible = visible;
                _leftShoulderVisibleDirty = true;
            }
            else if (ReferenceEquals(renderer, rightShoulderRenderer))
            {
                _rightShoulderVisible = visible;
                _rightShoulderVisibleDirty = true;
            }
            else if (ReferenceEquals(renderer, leftUpperArmRenderer))
            {
                _leftUpperArmVisible = visible;
                _leftUpperArmVisibleDirty = true;
            }
            else if (ReferenceEquals(renderer, rightUpperArmRenderer))
            {
                _rightUpperArmVisible = visible;
                _rightUpperArmVisibleDirty = true;
            }
            else if (ReferenceEquals(renderer, leftGloveRenderer))
            {
                _leftGloveVisible = visible;
                _leftGloveVisibleDirty = true;
            }
            else if (ReferenceEquals(renderer, rightGloveRenderer))
            {
                _rightGloveVisible = visible;
                _rightGloveVisibleDirty = true;
            }
            else
            {
                TryQueueBodyRendererVisibility(renderer, visible);
            }
        }

        private void FlushQueuedRendererVisibility()
        {
            FlushRendererVisibility(leftForearmRenderer, ref _leftForearmVisibleDirty, _leftForearmVisible);
            FlushRendererVisibility(rightForearmRenderer, ref _rightForearmVisibleDirty, _rightForearmVisible);
            FlushRendererVisibility(leftShoulderRenderer, ref _leftShoulderVisibleDirty, _leftShoulderVisible);
            FlushRendererVisibility(rightShoulderRenderer, ref _rightShoulderVisibleDirty, _rightShoulderVisible);
            FlushRendererVisibility(leftUpperArmRenderer, ref _leftUpperArmVisibleDirty, _leftUpperArmVisible);
            FlushRendererVisibility(rightUpperArmRenderer, ref _rightUpperArmVisibleDirty, _rightUpperArmVisible);
            FlushRendererVisibility(leftGloveRenderer, ref _leftGloveVisibleDirty, _leftGloveVisible);
            FlushRendererVisibility(rightGloveRenderer, ref _rightGloveVisibleDirty, _rightGloveVisible);
            FlushQueuedBodyRendererVisibility();
        }

        private static void FlushRendererVisibility(Renderer renderer, ref bool dirty, bool visible)
        {
            if (!dirty)
                return;

            dirty = false;
            if (renderer != null && renderer.enabled != visible)
                renderer.enabled = visible;
        }

        private static void ApplyAttachmentPose(Transform attachment, Transform source)
        {
            if (attachment == null || source == null)
                return;

            attachment.SetPositionAndRotation(source.position, source.rotation);
            if (attachment.localScale != Vector3.one)
                attachment.localScale = Vector3.one;
        }

        private static float ApproximateSinCycle01(float cycle01)
        {
            ApproximateSinCosFullNoTrig(cycle01 * TwoPi, out float sin, out _);
            return sin;
        }

        private static float ApproximateCosCycle01(float cycle01)
        {
            ApproximateSinCosFullNoTrig(cycle01 * TwoPi, out _, out float cos);
            return cos;
        }

        private static Vector3 ApproximateVectorLerp(Vector3 from, Vector3 to, float blend01)
        {
            float t = math.saturate(blend01);
            return new Vector3(
                math.lerp(from.x, to.x, t),
                math.lerp(from.y, to.y, t),
                math.lerp(from.z, to.z, t));
        }

        private static Quaternion ApproximateNlerpNoSqrt(Quaternion fromRotation, Quaternion toRotation, float blend01)
        {
            float4 from = new float4(fromRotation.x, fromRotation.y, fromRotation.z, fromRotation.w);
            float4 to = new float4(toRotation.x, toRotation.y, toRotation.z, toRotation.w);
            if (math.dot(from, to) < 0f)
                to = -to;

            float4 blended = math.lerp(from, to, math.saturate(blend01));
            float lengthSq = math.max(math.dot(blended, blended), 0.000001f);
            blended *= math.rsqrt(lengthSq);
            return new Quaternion(blended.x, blended.y, blended.z, blended.w);
        }

        private static Quaternion ResolveEulerRotationNoTrig(Vector3 eulerDegrees)
        {
            ApproximateSinCosFullNoTrig(eulerDegrees.x * DegreesToRadians * 0.5f, out float sx, out float cx);
            ApproximateSinCosFullNoTrig(eulerDegrees.y * DegreesToRadians * 0.5f, out float sy, out float cy);
            ApproximateSinCosFullNoTrig(eulerDegrees.z * DegreesToRadians * 0.5f, out float sz, out float cz);

            float4 pitch = new float4(sx, 0f, 0f, cx);
            float4 yaw = new float4(0f, sy, 0f, cy);
            float4 roll = new float4(0f, 0f, sz, cz);
            return ToQuaternion(NormalizeQuaternionNoSqrt(MulQuaternionNoSqrt(MulQuaternionNoSqrt(yaw, pitch), roll)));
        }

        private static Quaternion ResolveLookRotationNoTrig(Vector3 forward, Vector3 up)
        {
            Vector3 f = NormalizeVectorRsqrt(forward, Vector3.forward);
            Vector3 u = NormalizeVectorRsqrt(up, Vector3.up);
            if (math.abs(math.dot((float3)f, (float3)u)) > 0.94f)
                u = math.abs(f.y) < 0.94f ? Vector3.up : Vector3.right;

            Vector3 r = NormalizeVectorRsqrt(CrossVector(u, f), Vector3.right);
            u = NormalizeVectorRsqrt(CrossVector(f, r), Vector3.up);

            float m00 = r.x;
            float m01 = u.x;
            float m02 = f.x;
            float m10 = r.y;
            float m11 = u.y;
            float m12 = f.y;
            float m20 = r.z;
            float m21 = u.z;
            float m22 = f.z;
            float trace = m00 + m11 + m22;

            float4 q;
            if (trace > 0f)
                q = new float4(m21 - m12, m02 - m20, m10 - m01, 1f + trace);
            else if (m00 >= m11 && m00 >= m22)
                q = new float4(1f + m00 - m11 - m22, m01 + m10, m02 + m20, m21 - m12);
            else if (m11 > m22)
                q = new float4(m01 + m10, 1f + m11 - m00 - m22, m12 + m21, m02 - m20);
            else
                q = new float4(m02 + m20, m12 + m21, 1f + m22 - m00 - m11, m10 - m01);

            return ToQuaternion(NormalizeQuaternionNoSqrt(q));
        }

        private static void ApproximateSinCosFullNoTrig(float radians, out float sin, out float cos)
        {
            float x = radians - (TwoPi * math.round(radians / TwoPi));
            float cosSign = 1f;
            if (x > HalfPi)
            {
                x = Pi - x;
                cosSign = -1f;
            }
            else if (x < -HalfPi)
            {
                x = -Pi - x;
                cosSign = -1f;
            }

            float x2 = x * x;
            sin = x * (1f - (x2 * (0.16666667f - (x2 * 0.008333333f))));
            cos = cosSign * (1f - (x2 * (0.5f - (x2 * 0.041666667f))));
        }

        private static Vector3 NormalizeVectorRsqrt(Vector3 value, Vector3 fallback)
        {
            float lengthSq = value.sqrMagnitude;
            if (lengthSq <= 0.000001f || !math.all(math.isfinite((float3)value)))
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

        private static Vector3 CrossVector(Vector3 a, Vector3 b)
        {
            return new Vector3(
                a.y * b.z - a.z * b.y,
                a.z * b.x - a.x * b.z,
                a.x * b.y - a.y * b.x);
        }

        private static float4 MulQuaternionNoSqrt(float4 lhs, float4 rhs)
        {
            return new float4(
                lhs.w * rhs.x + lhs.x * rhs.w + lhs.y * rhs.z - lhs.z * rhs.y,
                lhs.w * rhs.y - lhs.x * rhs.z + lhs.y * rhs.w + lhs.z * rhs.x,
                lhs.w * rhs.z + lhs.x * rhs.y - lhs.y * rhs.x + lhs.z * rhs.w,
                lhs.w * rhs.w - lhs.x * rhs.x - lhs.y * rhs.y - lhs.z * rhs.z);
        }

        private static float4 NormalizeQuaternionNoSqrt(float4 value)
        {
            float lengthSq = math.max(math.dot(value, value), 0.000001f);
            return value * math.rsqrt(lengthSq);
        }

        private static Quaternion ToQuaternion(float4 value)
        {
            return new Quaternion(value.x, value.y, value.z, value.w);
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
