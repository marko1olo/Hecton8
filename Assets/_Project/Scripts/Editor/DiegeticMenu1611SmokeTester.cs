#if UNITY_EDITOR
using System;
using System.IO;
using Hecton8.UI;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hecton8.Editor
{
    internal static class DiegeticMenu1611SmokeTester
    {
        private const string ScenePath = "Assets/_Project/Scenes/01_MAIN_MENU.unity";
        private const string MainMenuPath = "Assets/_Project/Scripts/MainMenuController.cs";
        private const string PauseMenuPath = "Assets/_Project/Scripts/UI/PauseMenuController.cs";
        private const string RaycasterPath = "Assets/_Project/Scripts/UI/DiegeticMenuRaycastReceiver.cs";
        private const string CameraControllerPath = "Assets/_Project/Scripts/UI/MenuCameraController.cs";
        private const string PanelControllerPath = "Assets/_Project/Scripts/UI/DiegeticPanelController.cs";
        private const string TerminalKeyboardPath = "Assets/_Project/Scripts/UI/PhysicalTerminalKeyboard.cs";
        private const string PhysicalButtonPath = "Assets/_Project/Scripts/UI/PhysicalPanelButton.cs";
        private const string PhysicalDialPath = "Assets/_Project/Scripts/UI/PhysicalPanelDial.cs";
        private const string PdaControllerPath = "Assets/_Project/Scripts/UI/DiegeticPDAController.cs";
        private const string ArchitectEyeConsolePath = "Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyePdaCommandConsole.cs";
        private const string FabricatorActuatorPath = "Assets/_Project/Scripts/FabricatorPhysicalActuator.cs";
        private const string KinematicTerminalBridgePath = "Assets/_Project/Scripts/Interaction/KinematicTerminalInteractionBridge.cs";
        private const string SceneRuntimePath = "Assets/_Project/Scripts/Core/SceneRuntimeService.cs";
        private const string MainMenuValidatorPath = "Assets/_Project/Scripts/Editor/MainMenuValidator.cs";
        private const int SplineIterations = 1000;
        private const int RaycastIterations = 10000;
        private const float PositionTolerance = 0.0001f;
        private const float RotationToleranceDegrees = 0.0001f;
        private const int RecorderCapacity = 1;
        private static readonly string[] GcAllocCounters = { "GC Allocated In Frame" };

        [MenuItem("Hecton8/1611/Validate Diegetic Menu Smoke")]
        private static void ValidateFromMenu()
        {
            ValidateOrThrow();
            Debug.Log("1611 diegetic menu smoke validation passed.");
        }

        internal static void ValidateOrThrow()
        {
            ValidateSplineDrift();
            ValidateRaycasterHotPathNoManagedGc();
            ValidateRaycasterCanvasGroupOverflowFailClosed();
            ValidateRaycasterCanvasGroupBlocksRaycastsFailClosed();
            ValidateRaycasterCanvasGroupIgnoreParentGroups();
            ValidateRaycasterIgnoreParentGroupsStopsCacheOverflow();
            ValidateRaycasterFlushSelectionRechecksEligibility();
            ValidateRaycasterNonFiniteHitPointFailsClosed();
            ValidateRaycasterRebuildClearsPressedState();
            ValidateRaycasterCombinedBitmaskReleaseFailsClosed();
            ValidateRaycasterToggleAndSliderControls();
            ValidateRaycasterTopmostDrawOrder();
            ValidateRaycasterGraphicBlockerStopsLowerControl();
            ValidateRaycasterCanvasGroupBlockerStopsLowerControl();
            ValidateMainMenuCanvasYaml();
            ValidateMainMenuValidatorPolicy();
            ValidateNewGameRoutesToOrbitPrologue();
            ValidateHotPathSourceText();
            ValidateApexIntegratorProtocol();
        }

        private static void ValidateSplineDrift()
        {
            GameObject root = new GameObject("DiegeticMenu1611SplineSmoke", typeof(Camera), typeof(MenuCameraController));
            try
            {
                Transform rootTransform = root.transform;
                rootTransform.SetPositionAndRotation(
                    new Vector3(1.065f, 1.741f, -2.093f),
                    Quaternion.Euler(0f, -120.158f, 0f));

                if (!root.TryGetComponent(out Camera camera) ||
                    !root.TryGetComponent(out MenuCameraController controller))
                {
                    throw new InvalidOperationException("1611 spline smoke failed to create required camera/controller components.");
                }

                controller.Configure(camera);

                MenuCameraController.MenuCameraRoute[] routes =
                {
                    MenuCameraController.MenuCameraRoute.Main,
                    MenuCameraController.MenuCameraRoute.Saves,
                    MenuCameraController.MenuCameraRoute.Settings,
                    MenuCameraController.MenuCameraRoute.Loading,
                    MenuCameraController.MenuCameraRoute.Handoff
                };

                Vector3[] targetPositions = new Vector3[routes.Length];
                Quaternion[] targetRotations = new Quaternion[routes.Length];

                for (int i = 0; i < routes.Length; i++)
                {
                    controller.BeginRoute(routes[i], 0.25f);
                    controller.Advance(1f);
                    targetPositions[i] = rootTransform.position;
                    targetRotations[i] = rootTransform.rotation;
                }

                for (int i = 0; i < SplineIterations; i++)
                {
                    int routeIndex = i % routes.Length;
                    controller.BeginRoute(routes[routeIndex], 0.25f);
                    controller.Advance(1f);

                    float positionError = Vector3.Distance(rootTransform.position, targetPositions[routeIndex]);
                    float rotationError = Quaternion.Angle(rootTransform.rotation, targetRotations[routeIndex]);
                    if (positionError > PositionTolerance || rotationError > RotationToleranceDegrees)
                    {
                        throw new InvalidOperationException(
                            "1611 spline drift detected at iteration " + i +
                            ": positionError=" + positionError +
                            ", rotationError=" + rotationError);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateRaycasterHotPathNoManagedGc()
        {
            GameObject root = new GameObject("DiegeticMenu1611RaycastSmoke", typeof(RectTransform), typeof(Canvas), typeof(DiegeticMenuRaycastReceiver));
            try
            {
                RectTransform rootRect = root.transform as RectTransform;
                rootRect.sizeDelta = new Vector2(
                    DiegeticMenuCanvasUtility.ReferenceWidth,
                    DiegeticMenuCanvasUtility.ReferenceHeight);

                GameObject buttonObject = new GameObject("Button", typeof(RectTransform), typeof(Button));
                buttonObject.transform.SetParent(root.transform, false);
                RectTransform buttonRect = buttonObject.transform as RectTransform;
                buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.sizeDelta = new Vector2(960f, 540f);

                if (!root.TryGetComponent(out DiegeticMenuRaycastReceiver receiver))
                    throw new InvalidOperationException("1611 raycaster smoke failed to create receiver component.");

                receiver.Configure(rootRect, null, 0x31363131u);

                DiegeticPanelInputEvent inputEvent = default;
                inputEvent.EventType = DiegeticPanelInputEventType.Hover;
                inputEvent.CanvasHitPoint = new float2(960f, 540f);
                receiver.ReceiveCanvasInput(in inputEvent);

                ProfilerRecorder recorder = StartRecorderCold();
                long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
                long afterBytes;
                try
                {
                    uint state = 2166136261u;
                    for (int i = 0; i < RaycastIterations; i++)
                    {
                        state = (state ^ (uint)i) * 16777619u;
                        float x = 480f + ((state & 1023u) * (960f / 1023f));
                        state = (state ^ 0x9E3779B9u) * 16777619u;
                        float y = 270f + ((state & 1023u) * (540f / 1023f));
                        inputEvent.CanvasHitPoint = new float2(x, y);
                        receiver.ReceiveCanvasInput(in inputEvent);
                    }

                    afterBytes = GC.GetAllocatedBytesForCurrentThread();
                }
                finally
                {
                    if (recorder.Valid)
                        recorder.Dispose();
                }

                if (afterBytes != beforeBytes)
                    throw new InvalidOperationException("1611 raycaster hot path allocated " + (afterBytes - beforeBytes) + " managed bytes.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateRaycasterCanvasGroupOverflowFailClosed()
        {
            GameObject root = new GameObject("DiegeticMenu1611RaycastOverflowSmoke", typeof(RectTransform), typeof(Canvas), typeof(DiegeticMenuRaycastReceiver));
            try
            {
                RectTransform rootRect = root.transform as RectTransform;
                rootRect.sizeDelta = new Vector2(
                    DiegeticMenuCanvasUtility.ReferenceWidth,
                    DiegeticMenuCanvasUtility.ReferenceHeight);

                Transform parent = root.transform;
                for (int i = 0; i < 9; i++)
                {
                    GameObject groupObject = new GameObject("Group" + i, typeof(RectTransform), typeof(CanvasGroup));
                    groupObject.transform.SetParent(parent, false);
                    parent = groupObject.transform;
                }

                GameObject buttonObject = new GameObject("OverflowButton", typeof(RectTransform), typeof(Button));
                buttonObject.transform.SetParent(parent, false);
                RectTransform buttonRect = buttonObject.transform as RectTransform;
                buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.sizeDelta = new Vector2(960f, 540f);

                bool clicked = false;
                if (!buttonObject.TryGetComponent(out Button button))
                    throw new InvalidOperationException("1611 overflow smoke failed to create button component.");

                button.onClick.AddListener(() => clicked = true);

                if (!root.TryGetComponent(out DiegeticMenuRaycastReceiver receiver))
                    throw new InvalidOperationException("1611 overflow smoke failed to create receiver component.");

                receiver.Configure(rootRect, null, 0x31363131u);

                DiegeticPanelInputEvent inputEvent = default;
                inputEvent.CanvasHitPoint = new float2(960f, 540f);
                inputEvent.EventType = DiegeticPanelInputEventType.Down;
                receiver.ReceiveCanvasInput(in inputEvent);
                inputEvent.EventType = DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);

                if (clicked)
                    throw new InvalidOperationException("1611 raycaster CanvasGroup overflow did not fail closed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateRaycasterCanvasGroupBlocksRaycastsFailClosed()
        {
            GameObject root = new GameObject("DiegeticMenu1611RaycastBlocksSmoke", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(DiegeticMenuRaycastReceiver));
            try
            {
                RectTransform rootRect = root.transform as RectTransform;
                rootRect.sizeDelta = new Vector2(
                    DiegeticMenuCanvasUtility.ReferenceWidth,
                    DiegeticMenuCanvasUtility.ReferenceHeight);

                if (!root.TryGetComponent(out CanvasGroup group))
                    throw new InvalidOperationException("1611 blocksRaycasts smoke failed to create CanvasGroup.");

                group.blocksRaycasts = false;

                GameObject buttonObject = new GameObject("BlockedButton", typeof(RectTransform), typeof(Button));
                buttonObject.transform.SetParent(root.transform, false);
                RectTransform buttonRect = buttonObject.transform as RectTransform;
                buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.sizeDelta = new Vector2(960f, 540f);

                int clicks = 0;
                if (!buttonObject.TryGetComponent(out Button button))
                    throw new InvalidOperationException("1611 blocksRaycasts smoke failed to create button component.");

                button.onClick.AddListener(() => clicks++);

                if (!root.TryGetComponent(out DiegeticMenuRaycastReceiver receiver))
                    throw new InvalidOperationException("1611 blocksRaycasts smoke failed to create receiver component.");

                receiver.Configure(rootRect, null, 0x31363131u);

                DiegeticPanelInputEvent inputEvent = default;
                inputEvent.CanvasHitPoint = new float2(960f, 540f);
                inputEvent.EventType = DiegeticPanelInputEventType.Down;
                receiver.ReceiveCanvasInput(in inputEvent);
                inputEvent.EventType = DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);
                if (clicks != 0)
                    throw new InvalidOperationException("1611 raycaster ignored CanvasGroup.blocksRaycasts=false.");

                group.blocksRaycasts = true;
                receiver.RebuildButtonCache();
                inputEvent.EventType = DiegeticPanelInputEventType.Down;
                receiver.ReceiveCanvasInput(in inputEvent);
                inputEvent.EventType = DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);
                if (clicks != 1)
                    throw new InvalidOperationException("1611 raycaster blocksRaycasts smoke broke normal click recovery.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateRaycasterCanvasGroupIgnoreParentGroups()
        {
            GameObject root = new GameObject("DiegeticMenu1611RaycastIgnoreParentSmoke", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(DiegeticMenuRaycastReceiver));
            try
            {
                RectTransform rootRect = root.transform as RectTransform;
                rootRect.sizeDelta = new Vector2(
                    DiegeticMenuCanvasUtility.ReferenceWidth,
                    DiegeticMenuCanvasUtility.ReferenceHeight);

                if (!root.TryGetComponent(out CanvasGroup rootGroup))
                    throw new InvalidOperationException("1611 ignoreParentGroups smoke failed to create root CanvasGroup.");

                rootGroup.interactable = false;
                rootGroup.blocksRaycasts = false;

                GameObject childGroupObject = new GameObject("IgnoredParentGroup", typeof(RectTransform), typeof(CanvasGroup));
                childGroupObject.transform.SetParent(root.transform, false);
                RectTransform childGroupRect = childGroupObject.transform as RectTransform;
                childGroupRect.sizeDelta = new Vector2(
                    DiegeticMenuCanvasUtility.ReferenceWidth,
                    DiegeticMenuCanvasUtility.ReferenceHeight);

                if (!childGroupObject.TryGetComponent(out CanvasGroup childGroup))
                    throw new InvalidOperationException("1611 ignoreParentGroups smoke failed to create child CanvasGroup.");

                childGroup.ignoreParentGroups = true;
                childGroup.interactable = true;
                childGroup.blocksRaycasts = true;

                GameObject buttonObject = new GameObject("IgnoredParentButton", typeof(RectTransform), typeof(Button));
                buttonObject.transform.SetParent(childGroupObject.transform, false);
                RectTransform buttonRect = buttonObject.transform as RectTransform;
                buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.sizeDelta = new Vector2(960f, 540f);

                int clicks = 0;
                if (!buttonObject.TryGetComponent(out Button button))
                    throw new InvalidOperationException("1611 ignoreParentGroups smoke failed to create button component.");

                button.onClick.AddListener(() => clicks++);

                if (!root.TryGetComponent(out DiegeticMenuRaycastReceiver receiver))
                    throw new InvalidOperationException("1611 ignoreParentGroups smoke failed to create receiver component.");

                receiver.Configure(rootRect, null, 0x31363131u);

                DiegeticPanelInputEvent inputEvent = default;
                inputEvent.CanvasHitPoint = new float2(960f, 540f);
                inputEvent.EventType = DiegeticPanelInputEventType.Down;
                receiver.ReceiveCanvasInput(in inputEvent);
                inputEvent.EventType = DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);
                if (clicks != 1)
                    throw new InvalidOperationException("1611 raycaster ignored CanvasGroup.ignoreParentGroups=true.");

                childGroup.ignoreParentGroups = false;
                receiver.RebuildButtonCache();
                inputEvent.EventType = DiegeticPanelInputEventType.Down;
                receiver.ReceiveCanvasInput(in inputEvent);
                inputEvent.EventType = DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);
                if (clicks != 1)
                    throw new InvalidOperationException("1611 raycaster leaked through parent CanvasGroup after ignoreParentGroups=false.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateRaycasterIgnoreParentGroupsStopsCacheOverflow()
        {
            GameObject root = new GameObject("DiegeticMenu1611RaycastIgnoreParentOverflowSmoke", typeof(RectTransform), typeof(Canvas), typeof(DiegeticMenuRaycastReceiver));
            try
            {
                RectTransform rootRect = root.transform as RectTransform;
                rootRect.sizeDelta = new Vector2(
                    DiegeticMenuCanvasUtility.ReferenceWidth,
                    DiegeticMenuCanvasUtility.ReferenceHeight);

                Transform parent = root.transform;
                for (int i = 0; i < 9; i++)
                {
                    GameObject groupObject = new GameObject("IgnoredAncestor" + i, typeof(RectTransform), typeof(CanvasGroup));
                    groupObject.transform.SetParent(parent, false);
                    if (!groupObject.TryGetComponent(out CanvasGroup parentGroup))
                        throw new InvalidOperationException("1611 ignoreParentGroups overflow smoke failed to create parent CanvasGroup.");

                    parentGroup.interactable = false;
                    parentGroup.blocksRaycasts = false;
                    parent = groupObject.transform;
                }

                GameObject childGroupObject = new GameObject("IgnoreParentBoundary", typeof(RectTransform), typeof(CanvasGroup));
                childGroupObject.transform.SetParent(parent, false);
                RectTransform childGroupRect = childGroupObject.transform as RectTransform;
                childGroupRect.sizeDelta = new Vector2(
                    DiegeticMenuCanvasUtility.ReferenceWidth,
                    DiegeticMenuCanvasUtility.ReferenceHeight);

                if (!childGroupObject.TryGetComponent(out CanvasGroup childGroup))
                    throw new InvalidOperationException("1611 ignoreParentGroups overflow smoke failed to create child CanvasGroup.");

                childGroup.ignoreParentGroups = true;
                childGroup.interactable = true;
                childGroup.blocksRaycasts = true;

                GameObject buttonObject = new GameObject("IgnoreOverflowButton", typeof(RectTransform), typeof(Button));
                buttonObject.transform.SetParent(childGroupObject.transform, false);
                RectTransform buttonRect = buttonObject.transform as RectTransform;
                buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.sizeDelta = new Vector2(960f, 540f);

                int clicks = 0;
                if (!buttonObject.TryGetComponent(out Button button))
                    throw new InvalidOperationException("1611 ignoreParentGroups overflow smoke failed to create button component.");

                button.onClick.AddListener(() => clicks++);

                if (!root.TryGetComponent(out DiegeticMenuRaycastReceiver receiver))
                    throw new InvalidOperationException("1611 ignoreParentGroups overflow smoke failed to create receiver component.");

                receiver.Configure(rootRect, null, 0x31363131u);

                DiegeticPanelInputEvent inputEvent = default;
                inputEvent.CanvasHitPoint = new float2(960f, 540f);
                inputEvent.EventType = DiegeticPanelInputEventType.Down;
                receiver.ReceiveCanvasInput(in inputEvent);
                inputEvent.EventType = DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);
                if (clicks != 1)
                    throw new InvalidOperationException("1611 raycaster cache overflow ignored CanvasGroup.ignoreParentGroups boundary.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateRaycasterFlushSelectionRechecksEligibility()
        {
            GameObject eventSystemRoot = new GameObject("DiegeticMenu1611SelectionEventSystem", typeof(EventSystem));
            GameObject root = new GameObject("DiegeticMenu1611SelectionFlushSmoke", typeof(RectTransform), typeof(Canvas), typeof(DiegeticMenuRaycastReceiver));
            try
            {
                if (!eventSystemRoot.TryGetComponent(out EventSystem eventSystem))
                    throw new InvalidOperationException("1611 selection flush smoke failed to create EventSystem.");

                RectTransform rootRect = root.transform as RectTransform;
                rootRect.sizeDelta = new Vector2(
                    DiegeticMenuCanvasUtility.ReferenceWidth,
                    DiegeticMenuCanvasUtility.ReferenceHeight);

                GameObject buttonObject = new GameObject("SelectionButton", typeof(RectTransform), typeof(Button));
                buttonObject.transform.SetParent(root.transform, false);
                RectTransform buttonRect = buttonObject.transform as RectTransform;
                buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.sizeDelta = new Vector2(960f, 540f);

                if (!buttonObject.TryGetComponent(out Button button))
                    throw new InvalidOperationException("1611 selection flush smoke failed to create button component.");

                if (!root.TryGetComponent(out DiegeticMenuRaycastReceiver receiver))
                    throw new InvalidOperationException("1611 selection flush smoke failed to create receiver component.");

                receiver.Configure(rootRect, eventSystem, 0x31363131u);

                DiegeticPanelInputEvent inputEvent = default;
                inputEvent.CanvasHitPoint = new float2(960f, 540f);
                inputEvent.EventType = DiegeticPanelInputEventType.Hover;
                receiver.ReceiveCanvasInput(in inputEvent);
                button.interactable = false;
                receiver.FlushPendingSelection();

                if (eventSystem.currentSelectedGameObject != null)
                    throw new InvalidOperationException("1611 raycaster selected a control disabled before LateFrame flush.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(eventSystemRoot);
            }
        }

        private static void ValidateRaycasterNonFiniteHitPointFailsClosed()
        {
            GameObject root = new GameObject("DiegeticMenu1611RaycastFiniteSmoke", typeof(RectTransform), typeof(Canvas), typeof(DiegeticMenuRaycastReceiver));
            try
            {
                RectTransform rootRect = root.transform as RectTransform;
                rootRect.sizeDelta = new Vector2(
                    DiegeticMenuCanvasUtility.ReferenceWidth,
                    DiegeticMenuCanvasUtility.ReferenceHeight);

                GameObject buttonObject = new GameObject("Button", typeof(RectTransform), typeof(Button));
                buttonObject.transform.SetParent(root.transform, false);
                RectTransform buttonRect = buttonObject.transform as RectTransform;
                buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.anchoredPosition = new Vector2(-360f, 0f);
                buttonRect.sizeDelta = new Vector2(320f, 180f);

                int clicks = 0;
                if (!buttonObject.TryGetComponent(out Button button))
                    throw new InvalidOperationException("1611 non-finite smoke failed to create button component.");

                button.onClick.AddListener(() => clicks++);

                GameObject sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
                sliderObject.transform.SetParent(root.transform, false);
                RectTransform sliderRect = sliderObject.transform as RectTransform;
                sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
                sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
                sliderRect.pivot = new Vector2(0.5f, 0.5f);
                sliderRect.anchoredPosition = new Vector2(360f, 0f);
                sliderRect.sizeDelta = new Vector2(400f, 100f);

                if (!sliderObject.TryGetComponent(out Slider slider))
                    throw new InvalidOperationException("1611 non-finite smoke failed to create slider component.");

                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.value = 0.25f;
                slider.direction = Slider.Direction.LeftToRight;

                if (!root.TryGetComponent(out DiegeticMenuRaycastReceiver receiver))
                    throw new InvalidOperationException("1611 non-finite smoke failed to create receiver component.");

                receiver.Configure(rootRect, null, 0x31363131u);

                DiegeticPanelInputEvent inputEvent = default;
                inputEvent.CanvasHitPoint = new float2(float.NaN, 540f);
                inputEvent.EventType = DiegeticPanelInputEventType.Down;
                receiver.ReceiveCanvasInput(in inputEvent);
                inputEvent.CanvasHitPoint = new float2(600f, float.PositiveInfinity);
                inputEvent.EventType = DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);
                if (clicks != 0)
                    throw new InvalidOperationException("1611 raycaster accepted a non-finite button hit point.");

                inputEvent.CanvasHitPoint = new float2(1320f, 540f);
                inputEvent.EventType = DiegeticPanelInputEventType.Down;
                receiver.ReceiveCanvasInput(in inputEvent);
                inputEvent.CanvasHitPoint = new float2(float.NegativeInfinity, float.NaN);
                inputEvent.EventType = DiegeticPanelInputEventType.Hold;
                receiver.ReceiveCanvasInput(in inputEvent);
                inputEvent.CanvasHitPoint = new float2(float.NaN, float.NegativeInfinity);
                inputEvent.EventType = DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);
                if (!math.isfinite(slider.value))
                    throw new InvalidOperationException("1611 raycaster propagated non-finite slider value.");

                inputEvent.CanvasHitPoint = new float2(1320f, 540f);
                inputEvent.EventType = DiegeticPanelInputEventType.Down;
                receiver.ReceiveCanvasInput(in inputEvent);
                float sliderPressedValue = slider.value;
                inputEvent.CanvasHitPoint = new float2(DiegeticMenuCanvasUtility.ReferenceWidth + 64f, 540f);
                inputEvent.EventType = DiegeticPanelInputEventType.Hold;
                receiver.ReceiveCanvasInput(in inputEvent);
                inputEvent.EventType = DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);
                if (!Mathf.Approximately(slider.value, sliderPressedValue))
                    throw new InvalidOperationException("1611 raycaster accepted an out-of-reference slider hit point.");

                inputEvent.CanvasHitPoint = new float2(600f, 540f);
                inputEvent.EventType = DiegeticPanelInputEventType.Down;
                receiver.ReceiveCanvasInput(in inputEvent);
                inputEvent.EventType = DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);
                if (clicks != 1)
                    throw new InvalidOperationException("1611 raycaster non-finite guard broke normal button recovery.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateRaycasterRebuildClearsPressedState()
        {
            GameObject root = new GameObject("DiegeticMenu1611RaycastRebuildSmoke", typeof(RectTransform), typeof(Canvas), typeof(DiegeticMenuRaycastReceiver));
            try
            {
                RectTransform rootRect = root.transform as RectTransform;
                rootRect.sizeDelta = new Vector2(
                    DiegeticMenuCanvasUtility.ReferenceWidth,
                    DiegeticMenuCanvasUtility.ReferenceHeight);

                GameObject buttonObject = new GameObject("Button", typeof(RectTransform), typeof(Button));
                buttonObject.transform.SetParent(root.transform, false);
                RectTransform buttonRect = buttonObject.transform as RectTransform;
                buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.sizeDelta = new Vector2(960f, 540f);

                int clicks = 0;
                if (!buttonObject.TryGetComponent(out Button button))
                    throw new InvalidOperationException("1611 rebuild smoke failed to create button component.");

                button.onClick.AddListener(() => clicks++);

                if (!root.TryGetComponent(out DiegeticMenuRaycastReceiver receiver))
                    throw new InvalidOperationException("1611 rebuild smoke failed to create receiver component.");

                receiver.Configure(rootRect, null, 0x31363131u);

                DiegeticPanelInputEvent inputEvent = default;
                inputEvent.CanvasHitPoint = new float2(960f, 540f);
                inputEvent.EventType = DiegeticPanelInputEventType.Down;
                receiver.ReceiveCanvasInput(in inputEvent);

                receiver.RebuildButtonCache();

                inputEvent.EventType = DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);
                if (clicks != 0)
                    throw new InvalidOperationException("1611 raycaster rebuild retained pressed state.");

                inputEvent.EventType = DiegeticPanelInputEventType.Down;
                receiver.ReceiveCanvasInput(in inputEvent);
                inputEvent.EventType = DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);
                if (clicks != 1)
                    throw new InvalidOperationException("1611 raycaster rebuild broke normal click state.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateRaycasterCombinedBitmaskReleaseFailsClosed()
        {
            GameObject root = new GameObject("DiegeticMenu1611RaycastBitmaskSmoke", typeof(RectTransform), typeof(Canvas), typeof(DiegeticMenuRaycastReceiver));
            try
            {
                RectTransform rootRect = root.transform as RectTransform;
                rootRect.sizeDelta = new Vector2(
                    DiegeticMenuCanvasUtility.ReferenceWidth,
                    DiegeticMenuCanvasUtility.ReferenceHeight);

                GameObject buttonObject = new GameObject("Button", typeof(RectTransform), typeof(Button));
                buttonObject.transform.SetParent(root.transform, false);
                RectTransform buttonRect = buttonObject.transform as RectTransform;
                buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.sizeDelta = new Vector2(960f, 540f);

                int clicks = 0;
                if (!buttonObject.TryGetComponent(out Button button))
                    throw new InvalidOperationException("1611 bitmask smoke failed to create button component.");

                button.onClick.AddListener(() => clicks++);

                if (!root.TryGetComponent(out DiegeticMenuRaycastReceiver receiver))
                    throw new InvalidOperationException("1611 bitmask smoke failed to create receiver component.");

                receiver.Configure(rootRect, null, 0x31363131u);

                DiegeticPanelInputEvent inputEvent = default;
                inputEvent.CanvasHitPoint = new float2(960f, 540f);
                inputEvent.EventType = DiegeticPanelInputEventType.Down;
                receiver.ReceiveCanvasInput(in inputEvent);
                inputEvent.EventType = DiegeticPanelInputEventType.Hold | DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);
                if (clicks != 1)
                    throw new InvalidOperationException("1611 raycaster did not release on combined Hold|Up bitmask.");

                inputEvent.EventType = DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);
                if (clicks != 1)
                    throw new InvalidOperationException("1611 raycaster retained pressed state after combined Hold|Up bitmask.");

                inputEvent.EventType = DiegeticPanelInputEventType.Down | DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);
                if (clicks != 1)
                    throw new InvalidOperationException("1611 raycaster accepted ambiguous Down|Up bitmask as a click.");

                inputEvent.EventType = DiegeticPanelInputEventType.Down;
                receiver.ReceiveCanvasInput(in inputEvent);
                inputEvent.EventType = DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);
                if (clicks != 2)
                    throw new InvalidOperationException("1611 raycaster combined-bitmask guard broke normal click recovery.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateRaycasterToggleAndSliderControls()
        {
            GameObject root = new GameObject("DiegeticMenu1611RaycastSelectableSmoke", typeof(RectTransform), typeof(Canvas), typeof(DiegeticMenuRaycastReceiver));
            try
            {
                RectTransform rootRect = root.transform as RectTransform;
                rootRect.sizeDelta = new Vector2(
                    DiegeticMenuCanvasUtility.ReferenceWidth,
                    DiegeticMenuCanvasUtility.ReferenceHeight);

                GameObject toggleObject = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle));
                toggleObject.transform.SetParent(root.transform, false);
                RectTransform toggleRect = toggleObject.transform as RectTransform;
                toggleRect.anchorMin = new Vector2(0.5f, 0.5f);
                toggleRect.anchorMax = new Vector2(0.5f, 0.5f);
                toggleRect.pivot = new Vector2(0.5f, 0.5f);
                toggleRect.anchoredPosition = new Vector2(-480f, 0f);
                toggleRect.sizeDelta = new Vector2(320f, 200f);

                if (!toggleObject.TryGetComponent(out Toggle toggle))
                    throw new InvalidOperationException("1611 selectable smoke failed to create toggle component.");

                toggle.isOn = false;

                GameObject sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
                sliderObject.transform.SetParent(root.transform, false);
                RectTransform sliderRect = sliderObject.transform as RectTransform;
                sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
                sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
                sliderRect.pivot = new Vector2(0.5f, 0.5f);
                sliderRect.anchoredPosition = new Vector2(480f, 0f);
                sliderRect.sizeDelta = new Vector2(400f, 100f);

                if (!sliderObject.TryGetComponent(out Slider slider))
                    throw new InvalidOperationException("1611 selectable smoke failed to create slider component.");

                slider.minValue = 0f;
                slider.maxValue = 100f;
                slider.value = 0f;
                slider.direction = Slider.Direction.LeftToRight;

                if (!root.TryGetComponent(out DiegeticMenuRaycastReceiver receiver))
                    throw new InvalidOperationException("1611 selectable smoke failed to create receiver component.");

                receiver.Configure(rootRect, null, 0x31363131u);

                DiegeticPanelInputEvent inputEvent = default;
                inputEvent.CanvasHitPoint = new float2(480f, 540f);
                inputEvent.EventType = DiegeticPanelInputEventType.Down;
                receiver.ReceiveCanvasInput(in inputEvent);
                inputEvent.EventType = DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);
                if (!toggle.isOn)
                    throw new InvalidOperationException("1611 raycaster did not toggle a physical settings toggle.");

                inputEvent.CanvasHitPoint = new float2(1240f, 540f);
                inputEvent.EventType = DiegeticPanelInputEventType.Down;
                receiver.ReceiveCanvasInput(in inputEvent);
                inputEvent.CanvasHitPoint = new float2(1640f, 540f);
                inputEvent.EventType = DiegeticPanelInputEventType.Hold;
                receiver.ReceiveCanvasInput(in inputEvent);
                inputEvent.EventType = DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);
                if (slider.value < 99f)
                    throw new InvalidOperationException("1611 raycaster did not drag a physical settings slider.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateRaycasterTopmostDrawOrder()
        {
            GameObject root = new GameObject("DiegeticMenu1611RaycastDrawOrderSmoke", typeof(RectTransform), typeof(Canvas), typeof(DiegeticMenuRaycastReceiver));
            try
            {
                RectTransform rootRect = root.transform as RectTransform;
                rootRect.sizeDelta = new Vector2(
                    DiegeticMenuCanvasUtility.ReferenceWidth,
                    DiegeticMenuCanvasUtility.ReferenceHeight);

                GameObject backObject = new GameObject("BackButton", typeof(RectTransform), typeof(Button));
                backObject.transform.SetParent(root.transform, false);
                RectTransform backRect = backObject.transform as RectTransform;
                backRect.anchorMin = new Vector2(0.5f, 0.5f);
                backRect.anchorMax = new Vector2(0.5f, 0.5f);
                backRect.pivot = new Vector2(0.5f, 0.5f);
                backRect.anchoredPosition = Vector2.zero;
                backRect.sizeDelta = new Vector2(300f, 180f);

                GameObject frontObject = new GameObject("FrontButton", typeof(RectTransform), typeof(Button));
                frontObject.transform.SetParent(root.transform, false);
                RectTransform frontRect = frontObject.transform as RectTransform;
                frontRect.anchorMin = new Vector2(0.5f, 0.5f);
                frontRect.anchorMax = new Vector2(0.5f, 0.5f);
                frontRect.pivot = new Vector2(0.5f, 0.5f);
                frontRect.anchoredPosition = Vector2.zero;
                frontRect.sizeDelta = new Vector2(300f, 180f);

                if (!backObject.TryGetComponent(out Button backButton) ||
                    !frontObject.TryGetComponent(out Button frontButton) ||
                    !root.TryGetComponent(out DiegeticMenuRaycastReceiver receiver))
                {
                    throw new InvalidOperationException("1611 draw-order smoke failed to create required components.");
                }

                int backClicks = 0;
                int frontClicks = 0;
                backButton.onClick.AddListener(() => backClicks++);
                frontButton.onClick.AddListener(() => frontClicks++);
                receiver.Configure(rootRect, null, 0x31363131u);

                DiegeticPanelInputEvent inputEvent = default;
                inputEvent.CanvasHitPoint = new float2(960f, 540f);
                inputEvent.EventType = DiegeticPanelInputEventType.Down;
                receiver.ReceiveCanvasInput(in inputEvent);
                inputEvent.EventType = DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);

                if (frontClicks != 1 || backClicks != 0)
                    throw new InvalidOperationException("1611 raycaster selected the lower visual control during overlap.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateRaycasterGraphicBlockerStopsLowerControl()
        {
            GameObject root = new GameObject("DiegeticMenu1611RaycastBlockerSmoke", typeof(RectTransform), typeof(Canvas), typeof(DiegeticMenuRaycastReceiver));
            try
            {
                RectTransform rootRect = root.transform as RectTransform;
                rootRect.sizeDelta = new Vector2(
                    DiegeticMenuCanvasUtility.ReferenceWidth,
                    DiegeticMenuCanvasUtility.ReferenceHeight);

                GameObject lowerObject = new GameObject("LowerButton", typeof(RectTransform), typeof(Button));
                lowerObject.transform.SetParent(root.transform, false);
                RectTransform lowerRect = lowerObject.transform as RectTransform;
                lowerRect.anchorMin = new Vector2(0.5f, 0.5f);
                lowerRect.anchorMax = new Vector2(0.5f, 0.5f);
                lowerRect.pivot = new Vector2(0.5f, 0.5f);
                lowerRect.anchoredPosition = Vector2.zero;
                lowerRect.sizeDelta = new Vector2(300f, 180f);

                GameObject blockerObject = new GameObject("ModalBackdrop", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                blockerObject.transform.SetParent(root.transform, false);
                RectTransform blockerRect = blockerObject.transform as RectTransform;
                blockerRect.anchorMin = new Vector2(0.5f, 0.5f);
                blockerRect.anchorMax = new Vector2(0.5f, 0.5f);
                blockerRect.pivot = new Vector2(0.5f, 0.5f);
                blockerRect.anchoredPosition = Vector2.zero;
                blockerRect.sizeDelta = new Vector2(900f, 600f);

                if (!lowerObject.TryGetComponent(out Button lowerButton) ||
                    !blockerObject.TryGetComponent(out CanvasGroup blockerGroup) ||
                    !blockerObject.TryGetComponent(out Image blockerImage) ||
                    !root.TryGetComponent(out DiegeticMenuRaycastReceiver receiver))
                {
                    throw new InvalidOperationException("1611 blocker smoke failed to create required components.");
                }

                blockerImage.raycastTarget = true;
                blockerGroup.alpha = 1f;
                blockerGroup.interactable = false;
                blockerGroup.blocksRaycasts = true;

                int clicks = 0;
                lowerButton.onClick.AddListener(() => clicks++);
                receiver.Configure(rootRect, null, 0x31363131u);

                DiegeticPanelInputEvent inputEvent = default;
                inputEvent.CanvasHitPoint = new float2(960f, 540f);
                inputEvent.EventType = DiegeticPanelInputEventType.Down;
                receiver.ReceiveCanvasInput(in inputEvent);
                inputEvent.EventType = DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);

                if (clicks != 0)
                    throw new InvalidOperationException("1611 raycaster clicked through an active modal graphic blocker.");

                blockerGroup.blocksRaycasts = false;
                receiver.RebuildButtonCache();

                inputEvent.EventType = DiegeticPanelInputEventType.Down;
                receiver.ReceiveCanvasInput(in inputEvent);
                inputEvent.EventType = DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);

                if (clicks != 1)
                    throw new InvalidOperationException("1611 raycaster blocker recovery broke lower control input.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateRaycasterCanvasGroupBlockerStopsLowerControl()
        {
            GameObject root = new GameObject("DiegeticMenu1611RaycastGroupBlockerSmoke", typeof(RectTransform), typeof(Canvas), typeof(DiegeticMenuRaycastReceiver));
            try
            {
                RectTransform rootRect = root.transform as RectTransform;
                rootRect.sizeDelta = new Vector2(
                    DiegeticMenuCanvasUtility.ReferenceWidth,
                    DiegeticMenuCanvasUtility.ReferenceHeight);

                GameObject lowerObject = new GameObject("LowerButton", typeof(RectTransform), typeof(Button));
                lowerObject.transform.SetParent(root.transform, false);
                RectTransform lowerRect = lowerObject.transform as RectTransform;
                lowerRect.anchorMin = new Vector2(0.5f, 0.5f);
                lowerRect.anchorMax = new Vector2(0.5f, 0.5f);
                lowerRect.pivot = new Vector2(0.5f, 0.5f);
                lowerRect.anchoredPosition = Vector2.zero;
                lowerRect.sizeDelta = new Vector2(300f, 180f);

                GameObject blockerObject = new GameObject("ModalGroupOnly", typeof(RectTransform), typeof(CanvasGroup));
                blockerObject.transform.SetParent(root.transform, false);
                RectTransform blockerRect = blockerObject.transform as RectTransform;
                blockerRect.anchorMin = new Vector2(0.5f, 0.5f);
                blockerRect.anchorMax = new Vector2(0.5f, 0.5f);
                blockerRect.pivot = new Vector2(0.5f, 0.5f);
                blockerRect.anchoredPosition = Vector2.zero;
                blockerRect.sizeDelta = new Vector2(900f, 600f);

                if (!lowerObject.TryGetComponent(out Button lowerButton) ||
                    !blockerObject.TryGetComponent(out CanvasGroup blockerGroup) ||
                    !root.TryGetComponent(out DiegeticMenuRaycastReceiver receiver))
                {
                    throw new InvalidOperationException("1611 CanvasGroup blocker smoke failed to create required components.");
                }

                blockerGroup.alpha = 1f;
                blockerGroup.interactable = false;
                blockerGroup.blocksRaycasts = true;

                int clicks = 0;
                lowerButton.onClick.AddListener(() => clicks++);
                receiver.Configure(rootRect, null, 0x31363131u);

                DiegeticPanelInputEvent inputEvent = default;
                inputEvent.CanvasHitPoint = new float2(960f, 540f);
                inputEvent.EventType = DiegeticPanelInputEventType.Down;
                receiver.ReceiveCanvasInput(in inputEvent);
                inputEvent.EventType = DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);

                if (clicks != 0)
                    throw new InvalidOperationException("1611 raycaster clicked through an active CanvasGroup blocker.");

                blockerGroup.blocksRaycasts = false;
                receiver.RebuildButtonCache();

                inputEvent.EventType = DiegeticPanelInputEventType.Down;
                receiver.ReceiveCanvasInput(in inputEvent);
                inputEvent.EventType = DiegeticPanelInputEventType.Up;
                receiver.ReceiveCanvasInput(in inputEvent);

                if (clicks != 1)
                    throw new InvalidOperationException("1611 raycaster CanvasGroup blocker recovery broke lower control input.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static ProfilerRecorder StartRecorderCold()
        {
            for (int i = 0; i < GcAllocCounters.Length; i++)
            {
                try
                {
                    ProfilerRecorder recorder = ProfilerRecorder.StartNew(
                        ProfilerCategory.Memory,
                        GcAllocCounters[i],
                        RecorderCapacity,
                        ProfilerRecorderOptions.Default);
                    if (recorder.Valid)
                        return recorder;
                }
                catch (ArgumentException)
                {
                }
            }

            return default;
        }

        private static void ValidateMainMenuCanvasYaml()
        {
            string absoluteScenePath = Path.GetFullPath(ScenePath);
            string sceneText = File.ReadAllText(absoluteScenePath);
            string[] documents = sceneText.Split(new[] { "--- !u!" }, StringSplitOptions.None);
            int canvasCount = 0;
            int badCanvasCount = 0;
            int enabledGraphicRaycasterCount = 0;
            for (int i = 0; i < documents.Length; i++)
            {
                string document = documents[i];
                if (document.IndexOf("Canvas:", StringComparison.Ordinal) >= 0)
                {
                    canvasCount++;
                    if (document.IndexOf("m_RenderMode: 2", StringComparison.Ordinal) < 0)
                        badCanvasCount++;
                }

                if (document.IndexOf("GraphicRaycaster:", StringComparison.Ordinal) >= 0 &&
                    document.IndexOf("m_Enabled: 0", StringComparison.Ordinal) < 0)
                {
                    enabledGraphicRaycasterCount++;
                }
            }

            if (canvasCount != 1 || badCanvasCount != 0 || enabledGraphicRaycasterCount != 0)
                throw new InvalidOperationException("1611 main menu YAML diegetic canvas audit failed.");
        }

        private static void ValidateNewGameRoutesToOrbitPrologue()
        {
            string mainMenu = File.ReadAllText(Path.GetFullPath(MainMenuPath));
            string sceneText = File.ReadAllText(Path.GetFullPath(ScenePath));
            if (mainMenu.IndexOf("newGameTargetSceneName = \"01_ORBIT\"", StringComparison.Ordinal) < 0 ||
                sceneText.IndexOf("newGameTargetSceneName: 01_ORBIT", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 orbital handoff violation: new game route is not serialized to 01_ORBIT.");
            }
        }

        private static void ValidateMainMenuValidatorPolicy()
        {
            string validator = File.ReadAllText(Path.GetFullPath(MainMenuValidatorPath));
            if (validator.IndexOf("DIEGETIC INPUT OWNERSHIP", StringComparison.Ordinal) < 0 ||
                validator.IndexOf("FindComponentInScene<EventSystem>", StringComparison.Ordinal) < 0 ||
                validator.IndexOf("canvas.renderMode == RenderMode.WorldSpace", StringComparison.Ordinal) < 0 ||
                validator.IndexOf("GraphicRaycaster enabled", StringComparison.Ordinal) < 0 ||
                validator.IndexOf("EventSystem/GraphicRaycaster found", StringComparison.Ordinal) >= 0 ||
                validator.IndexOf("using TMPro;", StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException("1611 main menu validator policy regressed from diegetic input ownership.");
            }
        }

        private static void ValidateHotPathSourceText()
        {
            string raycaster = File.ReadAllText(Path.GetFullPath(RaycasterPath));
            string cameraController = File.ReadAllText(Path.GetFullPath(CameraControllerPath));
            AssertAbsent(raycaster, "GetComponents");
            AssertAbsent(raycaster, "FindObject");
            AssertAbsent(raycaster, "GameObject.Find");
            AssertAbsent(raycaster, "foreach");
            AssertAbsent(raycaster, "string.Format");
            AssertAbsent(cameraController, "foreach");
            AssertAbsent(cameraController, "string.Format");
            AssertAbsent(cameraController, "GameObject.Find");
            AssertAbsent(cameraController, "Get" + "Component<Camera>");
        }

        private static void ValidateApexIntegratorProtocol()
        {
            string mainMenu = File.ReadAllText(Path.GetFullPath(MainMenuPath));
            string pauseMenu = File.ReadAllText(Path.GetFullPath(PauseMenuPath));
            string raycaster = File.ReadAllText(Path.GetFullPath(RaycasterPath));
            string cameraController = File.ReadAllText(Path.GetFullPath(CameraControllerPath));
            string panelController = File.ReadAllText(Path.GetFullPath(PanelControllerPath));
            string terminalKeyboard = File.ReadAllText(Path.GetFullPath(TerminalKeyboardPath));
            string physicalButton = File.ReadAllText(Path.GetFullPath(PhysicalButtonPath));
            string physicalDial = File.ReadAllText(Path.GetFullPath(PhysicalDialPath));
            string pdaController = File.ReadAllText(Path.GetFullPath(PdaControllerPath));
            string architectEyeConsole = File.ReadAllText(Path.GetFullPath(ArchitectEyeConsolePath));
            string fabricatorActuator = File.ReadAllText(Path.GetFullPath(FabricatorActuatorPath));
            string kinematicTerminalBridge = File.ReadAllText(Path.GetFullPath(KinematicTerminalBridgePath));
            string sceneRuntime = File.ReadAllText(Path.GetFullPath(SceneRuntimePath));

            AssertNoGlobalRegistryGet(mainMenu, MainMenuPath);
            AssertNoGlobalRegistryGet(pauseMenu, PauseMenuPath);
            AssertNoGlobalRegistryGet(raycaster, RaycasterPath);
            AssertNoGlobalRegistryGet(cameraController, CameraControllerPath);
            AssertNoGlobalRegistryGet(panelController, PanelControllerPath);
            AssertNoGlobalRegistryGet(terminalKeyboard, TerminalKeyboardPath);
            AssertNoGlobalRegistryGet(physicalButton, PhysicalButtonPath);
            AssertNoGlobalRegistryGet(physicalDial, PhysicalDialPath);
            AssertNoGlobalRegistryGet(pdaController, PdaControllerPath);
            AssertNoGlobalRegistryGet(architectEyeConsole, ArchitectEyeConsolePath);
            AssertNoGlobalRegistryGet(fabricatorActuator, FabricatorActuatorPath);
            AssertNoGlobalRegistryGet(kinematicTerminalBridge, KinematicTerminalBridgePath);
            AssertNoGlobalRegistryGet(sceneRuntime, SceneRuntimePath);

            AssertHotMethodClean(mainMenu, "Tick", MainMenuPath);
            AssertHotMethodClean(mainMenu, "LateFrameTick", MainMenuPath);
            AssertHotMethodClean(mainMenu, "EnsureMenuInputRoutingReady", MainMenuPath);
            AssertHotMethodClean(mainMenu, "RefreshMenuInputRoutingReadyFromCache", MainMenuPath);
            AssertHotMethodClean(mainMenu, "RefreshSelectionIfNeeded", MainMenuPath);
            AssertHotMethodClean(mainMenu, "IsDefaultSelectionTargetEligible", MainMenuPath);
            AssertHotMethodClean(mainMenu, "UpdatePanelTransition", MainMenuPath);
            AssertHotMethodClean(mainMenu, "HandleCancelInput", MainMenuPath);
            AssertHotMethodClean(mainMenu, "GetUnscaledDeltaTime", MainMenuPath);
            AssertHotMethodClean(mainMenu, "ResolveCurrentUnscaledTimeSeconds", MainMenuPath);
            AssertHotMethodClean(mainMenu, "SyncVisualStyleLateFrame", MainMenuPath);
            AssertHotMethodClean(mainMenu, "SyncVisualConceptLateFrame", MainMenuPath);
            AssertMainMenuTimeSourcesFinite(mainMenu);

            AssertHotMethodClean(pauseMenu, "UnscaledFastTick", PauseMenuPath);
            AssertHotMethodClean(pauseMenu, "LateFrameTick", PauseMenuPath);
            AssertHotMethodClean(pauseMenu, "ResolveCurrentUnscaledTimeSeconds", PauseMenuPath);
            AssertHotMethodClean(pauseMenu, "AdvancePauseInputState", PauseMenuPath);
            AssertHotMethodClean(pauseMenu, "ProcessPendingPauseMenuCommands", PauseMenuPath);
            AssertHotMethodClean(pauseMenu, "RefreshPauseSectionInteractionGate", PauseMenuPath);
            AssertHotMethodClean(pauseMenu, "ApplyPauseSectionInteractionGate", PauseMenuPath);
            AssertHotMethodClean(pauseMenu, "FlushPendingDefaultSelection", PauseMenuPath);
            AssertHotMethodClean(pauseMenu, "FlushPendingPauseSelectionClear", PauseMenuPath);
            AssertHotMethodClean(pauseMenu, "ClearPauseSelection", PauseMenuPath);
            AssertHotMethodClean(pauseMenu, "SelectDefaultButtonForSection", PauseMenuPath);
            AssertHotMethodClean(pauseMenu, "IsDefaultSelectionTargetEligible", PauseMenuPath);
            AssertHotMethodClean(pauseMenu, "ResolveSectionGroup", PauseMenuPath);
            AssertHotMethodClean(pauseMenu, "SyncVisualStyleLateFrame", PauseMenuPath);
            AssertHotMethodClean(pauseMenu, "SyncVisualConceptLateFrame", PauseMenuPath);
            AssertPauseMenuTimeSourcesFinite(pauseMenu);

            AssertRaycasterHotGraphClean(raycaster);
            AssertRaycasterMissDownFailsClosed(raycaster);
            AssertRaycasterEmptyCacheClearsState(raycaster);
            AssertRaycasterNonFiniteHitPointGuard(raycaster);
            AssertRaycasterCombinedBitmaskReleasePriority(raycaster);
            AssertRaycasterSliderWritesFiniteOnly(raycaster);
            AssertPanelReceiverPointerNormalization(terminalKeyboard, pdaController, architectEyeConsole, fabricatorActuator);
            AssertPhysicalKeyboardSanitizesAuthoring(terminalKeyboard);
            AssertArchitectEyeConsoleSanitizesAuthoring(architectEyeConsole);
            AssertPhysicalButtonDispatchFailsClosed(physicalButton);
            AssertPhysicalDialSanitizesAuthoring(physicalDial);
            AssertPdaRejectsForeignPanelId(pdaController);
            AssertPdaRejectsNonFiniteHitPoint(pdaController);
            AssertPdaDownCancelsExistingPress(pdaController);
            AssertPdaPointerCanvasGroupStackCached(pdaController);
            AssertRaycasterTopmostHitOrder(raycaster);
            AssertRaycasterGraphicBlockers(raycaster);
            AssertRaycasterSelectionFlushLate(raycaster, mainMenu, pauseMenu);
            AssertMainMenuSelectionFailsClosedDuringTransition(mainMenu);
            AssertMainMenuUsesCinematicHandoffBeforeBootstrapFallback(mainMenu);
            AssertColdMethodClean(raycaster, "ClearInteractionState", RaycasterPath);
            AssertPdaHotGraphClean(pdaController);
            AssertHotMethodClean(terminalKeyboard, "ReceiveCanvasInput", TerminalKeyboardPath);
            AssertHotMethodClean(terminalKeyboard, "TryResolveButtonSnap", TerminalKeyboardPath);
            AssertHotMethodClean(terminalKeyboard, "ResolveKeyIndex", TerminalKeyboardPath);
            AssertHotMethodClean(terminalKeyboard, "CacheLayout", TerminalKeyboardPath);
            AssertHotMethodClean(terminalKeyboard, "QueuePressAudio", TerminalKeyboardPath);
            AssertHotMethodClean(terminalKeyboard, "ResolveSafeKeyboardMin", TerminalKeyboardPath);
            AssertHotMethodClean(terminalKeyboard, "ResolveSafeKeyboardSize", TerminalKeyboardPath);
            AssertHotMethodClean(terminalKeyboard, "ResolveSafePressAudioVolume", TerminalKeyboardPath);
            AssertHotMethodClean(terminalKeyboard, "ResolveSafePressAudioPitch", TerminalKeyboardPath);
            AssertHotMethodClean(physicalButton, "AdvanceButtonPresentation", PhysicalButtonPath);
            AssertHotMethodClean(physicalButton, "LateFrameTick", PhysicalButtonPath);
            AssertHotMethodClean(physicalButton, "ApplyInteractionSignal", PhysicalButtonPath);
            AssertHotMethodClean(physicalButton, "DispatchPanelEvent", PhysicalButtonPath);
            AssertHotMethodClean(architectEyeConsole, "ReceiveCanvasInput", ArchitectEyeConsolePath);
            AssertHotMethodClean(architectEyeConsole, "ResolveKeyIndex", ArchitectEyeConsolePath);
            AssertHotMethodClean(architectEyeConsole, "CacheLayout", ArchitectEyeConsolePath);
            AssertHotMethodClean(architectEyeConsole, "ResolveSafeKeyboardMin", ArchitectEyeConsolePath);
            AssertHotMethodClean(architectEyeConsole, "ResolveSafeKeyboardSize", ArchitectEyeConsolePath);
            AssertHotMethodClean(kinematicTerminalBridge, "Tick", KinematicTerminalBridgePath);
            AssertHotMethodClean(kinematicTerminalBridge, "RunTerminalTick", KinematicTerminalBridgePath);
            AssertHotMethodClean(kinematicTerminalBridge, "LateFrameTick", KinematicTerminalBridgePath);
            AssertHotMethodClean(kinematicTerminalBridge, "ResolveTerminalActionFlags", KinematicTerminalBridgePath);
            AssertHotMethodClean(kinematicTerminalBridge, "HandleProjectionLost", KinematicTerminalBridgePath);
            AssertHotMethodClean(kinematicTerminalBridge, "ResolveSafeTimestamp", KinematicTerminalBridgePath);
            AssertHotMethodClean(cameraController, "Advance", CameraControllerPath);
            AssertHotMethodClean(cameraController, "ResolveSafePosition", CameraControllerPath);
            AssertHotMethodClean(cameraController, "ResolveSafeRotation", CameraControllerPath);
            AssertMenuCameraSanitizesDeltaTime(cameraController);
            AssertMenuCameraSnapsToTargetOnCompletion(cameraController);
            AssertMenuCameraUsesBezierAndSlerp(cameraController);
            AssertMenuCameraSanitizesRoutePose(cameraController);
            AssertHotMethodClean(panelController, "ResolvePrimaryPointerAction", PanelControllerPath);
            AssertHotMethodClean(panelController, "LateFrameTick", PanelControllerPath);
            AssertHotMethodClean(panelController, "QueueInputEventsFromInputState", PanelControllerPath);
            AssertHotMethodClean(panelController, "TryProjectCanvasPointToWorld", PanelControllerPath);
            AssertHotMethodClean(panelController, "TryProjectRayToPanel", PanelControllerPath);
            AssertHotMethodClean(panelController, "TryProjectLocalHitToCanvas", PanelControllerPath);
            AssertHotMethodClean(panelController, "IsPanelProjectionDataFinite", PanelControllerPath);
            AssertHotMethodClean(panelController, "IsCanvasPointInsideReference", PanelControllerPath);
            AssertHotMethodClean(panelController, "ClearHoverState", PanelControllerPath);
            AssertHotMethodClean(panelController, "DispatchReleaseBeforeClear", PanelControllerPath);
            AssertHotMethodClean(physicalDial, "ReceiveCanvasInput", PhysicalDialPath);
            AssertHotMethodClean(physicalDial, "IsInsideDialHotZone", PhysicalDialPath);
            AssertHotMethodClean(physicalDial, "ApplyRotation", PhysicalDialPath);
            AssertHotMethodClean(physicalDial, "QueueScrollAudio", PhysicalDialPath);
            AssertHotMethodClean(physicalDial, "ClampDialDegrees", PhysicalDialPath);
            AssertHotMethodClean(physicalDial, "ResolveSafeDialBounds", PhysicalDialPath);
            AssertHotMethodClean(sceneRuntime, "Tick", SceneRuntimePath);

            AssertPhaseRoute(mainMenu, "Tick", "LateFrameTick", "_menuCameraController?.Advance", MainMenuPath);
            AssertPhaseRoute(mainMenu, "Tick", "LateFrameTick", "UpdatePanelTransition", MainMenuPath);
            AssertPhaseRoute(mainMenu, "Tick", "LateFrameTick", "RefreshSelectionIfNeeded", MainMenuPath);
            AssertMainMenuDropsBlockedCancel(mainMenu);
            AssertPhaseRoute(pauseMenu, "UnscaledFastTick", "LateFrameTick", "_pauseMenuCameraController?.Advance", PauseMenuPath);
            AssertPauseMenuUsesUnscaledPresentationDelta(pauseMenu);
            AssertPauseMenuLocksSectionInteractionDuringCameraRoute(pauseMenu);
            AssertPanelLateFrameFlagUsesFinally(panelController);
            AssertPanelClearDropsQueuedEventsBeforeRelease(panelController);
            AssertPanelProjectionFailsClosed(panelController);
            AssertPanelAnalogDeltaFailsClosed(panelController);
            AssertTransitionOverlayWorldSpace(sceneRuntime);
            AssertTransitionVisualOverkillDamping(sceneRuntime);
            AssertDataVaultWriteLocksFlattened(mainMenu, MainMenuPath);
            AssertDataVaultWriteLocksFlattened(pauseMenu, PauseMenuPath);
            AssertDataVaultWriteLocksFlattened(raycaster, RaycasterPath);
            AssertDataVaultWriteLocksFlattened(cameraController, CameraControllerPath);
            AssertDataVaultWriteLocksFlattened(panelController, PanelControllerPath);
            AssertKinematicBridgeTimestampsFinite(kinematicTerminalBridge);
            AssertDataVaultWriteLocksFlattened(sceneRuntime, SceneRuntimePath);
        }

        private static void AssertNoGlobalRegistryGet(string text, string path)
        {
            if (text.IndexOf("GlobalRegistry.Get<", StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("1611 APEX violation: GlobalRegistry.Get<T> in " + path);
        }

        private static void AssertMainMenuTimeSourcesFinite(string mainMenu)
        {
            string helperBody = ExtractMethodBody(mainMenu, "ResolveCurrentUnscaledTimeSeconds");
            string startBody = ExtractMethodBody(mainMenu, "Start");
            string deltaBody = ExtractMethodBody(mainMenu, "GetUnscaledDeltaTime");
            string retryBody = ExtractMethodBody(mainMenu, "EnsureMenuInputRoutingReady");
            string cancelBody = ExtractMethodBody(mainMenu, "HandleCancelInput");
            string blockBody = ExtractMethodBody(mainMenu, "BlockCancelInputBriefly");
            string styleBody = ExtractMethodBody(mainMenu, "SyncVisualStyleLateFrame");
            string conceptBody = ExtractMethodBody(mainMenu, "SyncVisualConceptLateFrame");
            if (helperBody == null || startBody == null || deltaBody == null || retryBody == null || cancelBody == null ||
                blockBody == null || styleBody == null || conceptBody == null)
            {
                throw new InvalidOperationException("1611 APEX main-menu time violation: time methods missing.");
            }

            if (helperBody.IndexOf("(float)SystemDispatcher.CurrentUnscaledTimeSeconds", StringComparison.Ordinal) < 0 ||
                helperBody.IndexOf("math.isfinite(currentTime)", StringComparison.Ordinal) < 0 ||
                helperBody.IndexOf("math.isfinite(fallback)", StringComparison.Ordinal) < 0 ||
                startBody.IndexOf("_lastUnscaledTickTime = ResolveCurrentUnscaledTimeSeconds(0f)", StringComparison.Ordinal) < 0 ||
                deltaBody.IndexOf("ResolveCurrentUnscaledTimeSeconds(_lastUnscaledTickTime)", StringComparison.Ordinal) < 0 ||
                deltaBody.IndexOf("MaxMenuPresentationDeltaSeconds", StringComparison.Ordinal) < 0 ||
                deltaBody.IndexOf("math.isfinite(delta)", StringComparison.Ordinal) < 0 ||
                retryBody.IndexOf("ResolveCurrentUnscaledTimeSeconds(_nextInputRoutingRetryTime)", StringComparison.Ordinal) < 0 ||
                cancelBody.IndexOf("math.isfinite(_cancelInputBlockedUntil)", StringComparison.Ordinal) < 0 ||
                blockBody.IndexOf("ResolveCurrentUnscaledTimeSeconds(0f) + CancelInputDebounceSeconds", StringComparison.Ordinal) < 0 ||
                styleBody.IndexOf("ResolveCurrentUnscaledTimeSeconds(0f)", StringComparison.Ordinal) < 0 ||
                conceptBody.IndexOf("ResolveCurrentUnscaledTimeSeconds(0f)", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX main-menu time violation: non-finite dispatcher time can poison presentation state.");
            }

            if (mainMenu.IndexOf("_lastUnscaledTickTime = (float)SystemDispatcher.CurrentUnscaledTimeSeconds", StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("1611 APEX main-menu time violation: direct dispatcher seed bypasses finite guard.");
        }

        private static void AssertPauseMenuTimeSourcesFinite(string pauseMenu)
        {
            string helperBody = ExtractMethodBody(pauseMenu, "ResolveCurrentUnscaledTimeSeconds");
            string styleBody = ExtractMethodBody(pauseMenu, "SyncVisualStyleLateFrame");
            string conceptBody = ExtractMethodBody(pauseMenu, "SyncVisualConceptLateFrame");
            if (helperBody == null || styleBody == null || conceptBody == null)
                throw new InvalidOperationException("1611 APEX pause-menu time violation: time methods missing.");

            if (helperBody.IndexOf("(float)SystemDispatcher.CurrentUnscaledTimeSeconds", StringComparison.Ordinal) < 0 ||
                helperBody.IndexOf("math.isfinite(currentTime)", StringComparison.Ordinal) < 0 ||
                styleBody.IndexOf("ResolveCurrentUnscaledTimeSeconds()", StringComparison.Ordinal) < 0 ||
                conceptBody.IndexOf("ResolveCurrentUnscaledTimeSeconds()", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX pause-menu time violation: non-finite dispatcher time can poison visual style state.");
            }
        }

        private static void AssertHotMethodClean(string text, string methodName, string path)
        {
            string body = ExtractMethodBody(text, methodName);
            if (body == null)
                throw new InvalidOperationException("1611 APEX violation: missing hot method " + path + "::" + methodName);

            AssertHotTokenAbsent(body, "GlobalRegistry.Get<", path, methodName);
            AssertHotTokenAbsent(body, "GetComponent(", path, methodName);
            AssertHotTokenAbsent(body, "TryGetComponent(", path, methodName);
            AssertHotTokenAbsent(body, "GetComponents", path, methodName);
            AssertHotTokenAbsent(body, "FindObject", path, methodName);
            AssertHotTokenAbsent(body, "GameObject.Find", path, methodName);
            AssertHotTokenAbsent(body, "EventSystem.current", path, methodName);
            AssertHotTokenAbsent(body, ".Complete(", path, methodName);
            AssertHotTokenAbsent(body, "foreach", path, methodName);
            AssertHotTokenAbsent(body, "Enumerable.", path, methodName);
            AssertHotTokenAbsent(body, "string.Format", path, methodName);
            AssertHotTokenAbsent(body, "new List<", path, methodName);
            AssertHotTokenAbsent(body, "new Dictionary<", path, methodName);
        }

        private static void AssertRaycasterHotGraphClean(string raycaster)
        {
            AssertHotMethodClean(raycaster, "ReceiveCanvasInput", RaycasterPath);
            AssertHotMethodClean(raycaster, "ResolvePrimaryPointerAction", RaycasterPath);
            AssertHotMethodClean(raycaster, "ResolveControlIndex", RaycasterPath);
            AssertHotMethodClean(raycaster, "CanvasPointToWorld", RaycasterPath);
            AssertHotMethodClean(raycaster, "IsRaycastItemEligible", RaycasterPath);
            AssertHotMethodClean(raycaster, "IsRaycastBlockerEligible", RaycasterPath);
            AssertHotMethodClean(raycaster, "IsControlEligible", RaycasterPath);
            AssertHotMethodClean(raycaster, "IsSliderEligible", RaycasterPath);
            AssertHotMethodClean(raycaster, "TryApplySliderValue", RaycasterPath);
            AssertHotMethodClean(raycaster, "IsCanvasHitPointInsideReference", RaycasterPath);
            AssertHotMethodClean(raycaster, "InvokeControl", RaycasterPath);
            AssertHotMethodClean(raycaster, "UpdateHover", RaycasterPath);
            AssertHotMethodClean(raycaster, "ClearInteractionState", RaycasterPath);
            AssertHotMethodClean(raycaster, "PublishHaptic", RaycasterPath);
            AssertHotMethodClean(raycaster, "PublishAcoustic", RaycasterPath);
            AssertHotMethodClean(raycaster, "FlushPendingSelection", RaycasterPath);
        }

        private static void AssertRaycasterMissDownFailsClosed(string raycaster)
        {
            string body = ExtractMethodBody(raycaster, "ReceiveCanvasInput");
            if (body == null)
                throw new InvalidOperationException("1611 APEX raycaster violation: ReceiveCanvasInput missing.");

            int downIndex = body.IndexOf("if (pointerAction == DiegeticPanelInputEventType.Down)", StringComparison.Ordinal);
            int missGuardIndex = downIndex >= 0 ? body.IndexOf("if (targetIndex < 0)", downIndex, StringComparison.Ordinal) : -1;
            int missReturnIndex = missGuardIndex >= 0 ? body.IndexOf("return;", missGuardIndex, StringComparison.Ordinal) : -1;
            int hapticIndex = downIndex >= 0 ? body.IndexOf("PublishHaptic(0.08f", downIndex, StringComparison.Ordinal) : -1;
            if (downIndex < 0 || missGuardIndex < downIndex || missReturnIndex < missGuardIndex || hapticIndex < 0 || missReturnIndex > hapticIndex)
                throw new InvalidOperationException("1611 APEX raycaster violation: empty-panel Down can publish haptic feedback.");
        }

        private static void AssertRaycasterEmptyCacheClearsState(string raycaster)
        {
            string body = ExtractMethodBody(raycaster, "ReceiveCanvasInput");
            if (body == null)
                throw new InvalidOperationException("1611 APEX raycaster violation: ReceiveCanvasInput missing.");

            int guardIndex = body.IndexOf("if (_canvasRoot == null || _controlCount <= 0)", StringComparison.Ordinal);
            int clearIndex = guardIndex >= 0 ? body.IndexOf("ClearInteractionState();", guardIndex, StringComparison.Ordinal) : -1;
            int returnIndex = guardIndex >= 0 ? body.IndexOf("return;", guardIndex, StringComparison.Ordinal) : -1;
            if (guardIndex < 0 || clearIndex < guardIndex || returnIndex < clearIndex)
                throw new InvalidOperationException("1611 APEX raycaster violation: missing canvas/cache guard does not clear stale interaction state.");
        }

        private static void AssertRaycasterNonFiniteHitPointGuard(string raycaster)
        {
            string resolveBody = ExtractMethodBody(raycaster, "ResolveControlIndex");
            string sliderBody = ExtractMethodBody(raycaster, "TryApplySliderValue");
            string boundsBody = ExtractMethodBody(raycaster, "IsCanvasHitPointInsideReference");
            string receiveBody = ExtractMethodBody(raycaster, "ReceiveCanvasInput");
            if (resolveBody == null || sliderBody == null || boundsBody == null || receiveBody == null)
                throw new InvalidOperationException("1611 APEX raycaster violation: hit-point guard methods missing.");

            int resolveBoundsIndex = resolveBody.IndexOf("IsCanvasHitPointInsideReference(canvasHitPoint)", StringComparison.Ordinal);
            int resolveWorldIndex = resolveBody.IndexOf("CanvasPointToWorld(canvasHitPoint)", StringComparison.Ordinal);
            int sliderBoundsIndex = sliderBody.IndexOf("IsCanvasHitPointInsideReference(canvasHitPoint)", StringComparison.Ordinal);
            int sliderWorldIndex = sliderBody.IndexOf("CanvasPointToWorld(canvasHitPoint)", StringComparison.Ordinal);
            int boundsFiniteIndex = boundsBody.IndexOf("math.all(math.isfinite(canvasHitPoint))", StringComparison.Ordinal);
            int boundsMinXIndex = boundsBody.IndexOf("canvasHitPoint.x >= 0f", StringComparison.Ordinal);
            int boundsMinYIndex = boundsBody.IndexOf("canvasHitPoint.y >= 0f", StringComparison.Ordinal);
            int boundsMaxXIndex = boundsBody.IndexOf("canvasHitPoint.x <= _referenceWidth", StringComparison.Ordinal);
            int boundsMaxYIndex = boundsBody.IndexOf("canvasHitPoint.y <= _referenceHeight", StringComparison.Ordinal);
            int downBranchIndex = receiveBody.IndexOf("if (pointerAction == DiegeticPanelInputEventType.Down)", StringComparison.Ordinal);
            int downSliderIndex = downBranchIndex >= 0 ? receiveBody.IndexOf("bool targetIsSlider = IsSliderEligible(targetIndex)", downBranchIndex, StringComparison.Ordinal) : -1;
            int downGuardIndex = downSliderIndex >= 0 ? receiveBody.IndexOf("if (targetIsSlider && !TryApplySliderValue(targetIndex, inputEvent.CanvasHitPoint))", downSliderIndex, StringComparison.Ordinal) : -1;
            int downPressedIndex = downBranchIndex >= 0 ? receiveBody.IndexOf("_pressedControlIndex = targetIndex", downBranchIndex, StringComparison.Ordinal) : -1;
            int downHapticIndex = downBranchIndex >= 0 ? receiveBody.IndexOf("PublishHaptic(0.08f", downBranchIndex, StringComparison.Ordinal) : -1;
            int upBranchIndex = receiveBody.IndexOf("if (pointerAction == DiegeticPanelInputEventType.Up)", StringComparison.Ordinal);
            int releaseIndex = upBranchIndex >= 0 ? receiveBody.IndexOf("if (IsSliderEligible(pressedIndex))", upBranchIndex, StringComparison.Ordinal) : -1;
            int releaseGuardIndex = releaseIndex >= 0 ? receiveBody.IndexOf("if (!TryApplySliderValue(pressedIndex, inputEvent.CanvasHitPoint))", releaseIndex, StringComparison.Ordinal) : -1;
            int releaseReturnIndex = releaseGuardIndex >= 0 ? receiveBody.IndexOf("return;", releaseGuardIndex, StringComparison.Ordinal) : -1;
            int releaseHapticIndex = releaseIndex >= 0 ? receiveBody.IndexOf("PublishHaptic(0.10f", releaseIndex, StringComparison.Ordinal) : -1;
            if (resolveBoundsIndex < 0 ||
                resolveWorldIndex < 0 ||
                resolveBoundsIndex > resolveWorldIndex ||
                sliderBoundsIndex < 0 ||
                sliderWorldIndex < 0 ||
                sliderBoundsIndex > sliderWorldIndex ||
                boundsFiniteIndex < 0 ||
                boundsMinXIndex < boundsFiniteIndex ||
                boundsMinYIndex < boundsMinXIndex ||
                boundsMaxXIndex < boundsMinYIndex ||
                boundsMaxYIndex < boundsMaxXIndex ||
                downSliderIndex < downBranchIndex ||
                downGuardIndex < downSliderIndex ||
                downPressedIndex < downGuardIndex ||
                downHapticIndex < downPressedIndex ||
                releaseGuardIndex < releaseIndex ||
                releaseReturnIndex < releaseGuardIndex ||
                releaseHapticIndex < 0 ||
                releaseReturnIndex > releaseHapticIndex)
            {
                throw new InvalidOperationException("1611 APEX raycaster violation: invalid hit points can enter world-space transform math.");
            }
        }

        private static void AssertRaycasterCombinedBitmaskReleasePriority(string raycaster)
        {
            string receiveBody = ExtractMethodBody(raycaster, "ReceiveCanvasInput");
            string resolveBody = ExtractMethodBody(raycaster, "ResolvePrimaryPointerAction");
            if (receiveBody == null || resolveBody == null)
                throw new InvalidOperationException("1611 APEX raycaster violation: combined bitmask action resolver missing.");

            int resolverCallIndex = receiveBody.IndexOf("ResolvePrimaryPointerAction(eventType)", StringComparison.Ordinal);
            int targetIndex = receiveBody.IndexOf("ResolveControlIndex(inputEvent.CanvasHitPoint)", StringComparison.Ordinal);
            int receiveUpIndex = receiveBody.IndexOf("if (pointerAction == DiegeticPanelInputEventType.Up)", StringComparison.Ordinal);
            int receiveDownIndex = receiveBody.IndexOf("if (pointerAction == DiegeticPanelInputEventType.Down)", StringComparison.Ordinal);
            int receiveHoldIndex = receiveBody.IndexOf("if (pointerAction == DiegeticPanelInputEventType.Hold)", StringComparison.Ordinal);
            int resolveUpIndex = resolveBody.IndexOf("DiegeticPanelInputEventType.Up", StringComparison.Ordinal);
            int resolveDownIndex = resolveBody.IndexOf("DiegeticPanelInputEventType.Down", StringComparison.Ordinal);
            int resolveHoldIndex = resolveBody.IndexOf("DiegeticPanelInputEventType.Hold", StringComparison.Ordinal);
            if (resolverCallIndex < 0 ||
                targetIndex < resolverCallIndex ||
                receiveUpIndex < targetIndex ||
                receiveDownIndex < receiveUpIndex ||
                receiveHoldIndex < receiveDownIndex ||
                resolveUpIndex < 0 ||
                resolveDownIndex < resolveUpIndex ||
                resolveHoldIndex < resolveDownIndex)
            {
                throw new InvalidOperationException("1611 APEX raycaster violation: combined Hold|Up can bypass release cleanup.");
            }
        }

        private static void AssertRaycasterSliderWritesFiniteOnly(string raycaster)
        {
            string receiveBody = ExtractMethodBody(raycaster, "ReceiveCanvasInput");
            string sliderBody = ExtractMethodBody(raycaster, "TryApplySliderValue");
            if (receiveBody == null || sliderBody == null)
                throw new InvalidOperationException("1611 APEX raycaster violation: slider input methods missing.");

            if (receiveBody.IndexOf("targetIsSlider && !TryApplySliderValue", StringComparison.Ordinal) < 0 ||
                receiveBody.IndexOf("_pressedControlIndex = -1", StringComparison.Ordinal) < 0 ||
                sliderBody.IndexOf("math.isfinite(new float2(localPoint.x, localPoint.y))", StringComparison.Ordinal) < 0 ||
                sliderBody.IndexOf("math.isfinite(normalized)", StringComparison.Ordinal) < 0 ||
                sliderBody.IndexOf("slider.normalizedValue = math.saturate(normalized)", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX raycaster violation: slider writes are not finite-only fail-closed writes.");
            }
        }

        private static void AssertPanelReceiverPointerNormalization(
            string terminalKeyboard,
            string pdaController,
            string architectEyeConsole,
            string fabricatorActuator)
        {
            AssertPanelReceiverUsesPrimaryPointerAction(terminalKeyboard, TerminalKeyboardPath);
            AssertPanelReceiverUsesPrimaryPointerAction(pdaController, PdaControllerPath);
            AssertPanelReceiverUsesPrimaryPointerAction(architectEyeConsole, ArchitectEyeConsolePath);
            AssertPanelReceiverUsesPrimaryPointerAction(fabricatorActuator, FabricatorActuatorPath);
        }

        private static void AssertPanelReceiverUsesPrimaryPointerAction(string source, string path)
        {
            string body = ExtractMethodBody(source, "ReceiveCanvasInput");
            if (body == null)
                throw new InvalidOperationException("1611 APEX pointer-normalization violation: ReceiveCanvasInput missing in " + path);

            if (body.IndexOf("ResolvePrimaryPointerAction(inputEvent.EventType)", StringComparison.Ordinal) < 0 ||
                body.IndexOf("inputEvent.EventType & DiegeticPanelInputEventType.Down", StringComparison.Ordinal) >= 0 ||
                body.IndexOf("inputEvent.EventType == DiegeticPanelInputEventType.", StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException("1611 APEX pointer-normalization violation: flagged panel input can bypass primary action resolution in " + path);
            }
        }

        private static void AssertPhysicalKeyboardSanitizesAuthoring(string terminalKeyboard)
        {
            string keyBody = ExtractMethodBody(terminalKeyboard, "ResolveKeyIndex");
            string snapBody = ExtractMethodBody(terminalKeyboard, "TryResolveButtonSnap");
            string layoutBody = ExtractMethodBody(terminalKeyboard, "CacheLayout");
            string audioBody = ExtractMethodBody(terminalKeyboard, "QueuePressAudio");
            string referenceBody = ExtractMethodBody(terminalKeyboard, "ResolveSafeReferenceResolution");
            string minBody = ExtractMethodBody(terminalKeyboard, "ResolveSafeKeyboardMin");
            string sizeBody = ExtractMethodBody(terminalKeyboard, "ResolveSafeKeyboardSize");
            string volumeBody = ExtractMethodBody(terminalKeyboard, "ResolveSafePressAudioVolume");
            string pitchBody = ExtractMethodBody(terminalKeyboard, "ResolveSafePressAudioPitch");
            if (keyBody == null || snapBody == null || layoutBody == null || audioBody == null ||
                referenceBody == null || minBody == null || sizeBody == null || volumeBody == null || pitchBody == null)
            {
                throw new InvalidOperationException("1611 APEX keyboard violation: finite-sanitize helpers missing.");
            }

            if (keyBody.IndexOf("math.all(math.isfinite(canvasPosition))", StringComparison.Ordinal) < 0 ||
                keyBody.IndexOf("ResolveSafeKeyboardMin()", StringComparison.Ordinal) < 0 ||
                keyBody.IndexOf("ResolveSafeKeyboardSize()", StringComparison.Ordinal) < 0 ||
                snapBody.IndexOf("ResolveSafeKeyboardMin()", StringComparison.Ordinal) < 0 ||
                layoutBody.IndexOf("ResolveSafeKeyboardSize()", StringComparison.Ordinal) < 0 ||
                audioBody.IndexOf("ResolveSafePressAudioVolume()", StringComparison.Ordinal) < 0 ||
                audioBody.IndexOf("ResolveSafePressAudioPitch()", StringComparison.Ordinal) < 0 ||
                referenceBody.IndexOf("SanitizeFinite(referenceResolution.x", StringComparison.Ordinal) < 0 ||
                minBody.IndexOf("SanitizeFinite(keyboardMin.x", StringComparison.Ordinal) < 0 ||
                sizeBody.IndexOf("SanitizeFinite(keyboardSize.x", StringComparison.Ordinal) < 0 ||
                volumeBody.IndexOf("SanitizeFinite(pressAudioVolume", StringComparison.Ordinal) < 0 ||
                pitchBody.IndexOf("SanitizeFinite(pressAudioPitch", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX keyboard violation: non-finite authored values can poison key/audio routing.");
            }
        }

        private static void AssertArchitectEyeConsoleSanitizesAuthoring(string architectEyeConsole)
        {
            string keyBody = ExtractMethodBody(architectEyeConsole, "ResolveKeyIndex");
            string layoutBody = ExtractMethodBody(architectEyeConsole, "CacheLayout");
            string minBody = ExtractMethodBody(architectEyeConsole, "ResolveSafeKeyboardMin");
            string sizeBody = ExtractMethodBody(architectEyeConsole, "ResolveSafeKeyboardSize");
            if (keyBody == null || layoutBody == null || minBody == null || sizeBody == null)
                throw new InvalidOperationException("1611 APEX architect-eye console violation: finite-sanitize helpers missing.");

            if (keyBody.IndexOf("math.all(math.isfinite(canvasPosition))", StringComparison.Ordinal) < 0 ||
                keyBody.IndexOf("ResolveSafeKeyboardMin()", StringComparison.Ordinal) < 0 ||
                keyBody.IndexOf("ResolveSafeKeyboardSize()", StringComparison.Ordinal) < 0 ||
                layoutBody.IndexOf("ResolveSafeKeyboardSize()", StringComparison.Ordinal) < 0 ||
                minBody.IndexOf("SanitizeFinite(keyboardMin.x", StringComparison.Ordinal) < 0 ||
                sizeBody.IndexOf("SanitizeFinite(keyboardSize.x", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX architect-eye console violation: non-finite authored values can poison command key routing.");
            }
        }

        private static void AssertKinematicBridgeTimestampsFinite(string kinematicTerminalBridge)
        {
            string runBody = ExtractMethodBody(kinematicTerminalBridge, "RunTerminalTick");
            string lostBody = ExtractMethodBody(kinematicTerminalBridge, "HandleProjectionLost");
            string timestampBody = ExtractMethodBody(kinematicTerminalBridge, "ResolveSafeTimestamp");
            if (runBody == null || lostBody == null || timestampBody == null)
                throw new InvalidOperationException("1611 APEX kinematic terminal bridge violation: timestamp methods missing.");

            if (kinematicTerminalBridge.IndexOf("Timestamp = (float)SystemDispatcher.CurrentUnscaledTimeSeconds", StringComparison.Ordinal) >= 0 ||
                runBody.IndexOf("Timestamp = ResolveSafeTimestamp(SystemDispatcher.CurrentUnscaledTimeSeconds)", StringComparison.Ordinal) < 0 ||
                lostBody.IndexOf("Timestamp = ResolveSafeTimestamp(SystemDispatcher.CurrentUnscaledTimeSeconds)", StringComparison.Ordinal) < 0 ||
                timestampBody.IndexOf("double.IsNaN(timestampSeconds)", StringComparison.Ordinal) < 0 ||
                timestampBody.IndexOf("double.IsInfinity(timestampSeconds)", StringComparison.Ordinal) < 0 ||
                timestampBody.IndexOf("timestampSeconds >= float.MaxValue", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX kinematic terminal bridge violation: panel event timestamps can propagate non-finite time.");
            }
        }

        private static void AssertPhysicalButtonDispatchFailsClosed(string physicalButton)
        {
            string applyBody = ExtractMethodBody(physicalButton, "ApplyInteractionSignal");
            string dispatchBody = ExtractMethodBody(physicalButton, "DispatchPanelEvent");
            if (applyBody == null || dispatchBody == null)
                throw new InvalidOperationException("1611 APEX physical button violation: dispatch methods missing.");

            if (physicalButton.IndexOf("private bool DispatchPanelEvent", StringComparison.Ordinal) < 0 ||
                dispatchBody.IndexOf("if (_panelInteractable == null)", StringComparison.Ordinal) < 0 ||
                dispatchBody.IndexOf("return false", StringComparison.Ordinal) < 0 ||
                dispatchBody.IndexOf("float2 panelHitPoint = new float2(canvasHitPoint.x, canvasHitPoint.y)", StringComparison.Ordinal) < 0 ||
                dispatchBody.IndexOf("eventType != DiegeticPanelInputEventType.Up", StringComparison.Ordinal) < 0 ||
                dispatchBody.IndexOf("math.all(math.isfinite(panelHitPoint))", StringComparison.Ordinal) < 0 ||
                dispatchBody.IndexOf("CanvasHitPoint = panelHitPoint", StringComparison.Ordinal) < 0 ||
                dispatchBody.IndexOf("return true", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX physical button violation: panel event dispatch does not fail closed.");
            }

            int dispatchDownIndex = applyBody.IndexOf("DispatchPanelEvent(DiegeticPanelInputEventType.Down)", StringComparison.Ordinal);
            int falseReturnIndex = dispatchDownIndex >= 0 ? applyBody.IndexOf("return;", dispatchDownIndex, StringComparison.Ordinal) : -1;
            int clickIndex = applyBody.IndexOf("QueueDiegeticClick(runtimeHitPoint)", StringComparison.Ordinal);
            int pressIndex = applyBody.IndexOf("_pressDispatched = true", StringComparison.Ordinal);
            if (dispatchDownIndex < 0 ||
                falseReturnIndex < dispatchDownIndex ||
                clickIndex < falseReturnIndex ||
                pressIndex < clickIndex)
            {
                throw new InvalidOperationException("1611 APEX physical button violation: failed Down dispatch can still latch pressed state or click audio.");
            }
        }

        private static void AssertPhysicalDialSanitizesAuthoring(string physicalDial)
        {
            string receiveBody = ExtractMethodBody(physicalDial, "ReceiveCanvasInput");
            string hotZoneBody = ExtractMethodBody(physicalDial, "IsInsideDialHotZone");
            string rotationBody = ExtractMethodBody(physicalDial, "ApplyRotation");
            string audioBody = ExtractMethodBody(physicalDial, "QueueScrollAudio");
            string clampBody = ExtractMethodBody(physicalDial, "ClampDialDegrees");
            string boundsBody = ExtractMethodBody(physicalDial, "ResolveSafeDialBounds");
            string scrollBody = ExtractMethodBody(physicalDial, "ResolveSafeScrollScale");
            string volumeBody = ExtractMethodBody(physicalDial, "ResolveSafeAudioVolume");
            string pitchBody = ExtractMethodBody(physicalDial, "ResolveSafeAudioPitch");
            if (receiveBody == null || hotZoneBody == null || rotationBody == null || audioBody == null ||
                clampBody == null || boundsBody == null || scrollBody == null || volumeBody == null || pitchBody == null)
            {
                throw new InvalidOperationException("1611 APEX physical dial violation: finite-sanitize helpers missing.");
            }

            if (receiveBody.IndexOf("float scrollDegrees = scrollY * ResolveSafeScrollScale()", StringComparison.Ordinal) < 0 ||
                receiveBody.IndexOf("_currentDegrees = ClampDialDegrees(_currentDegrees + scrollDegrees)", StringComparison.Ordinal) < 0 ||
                receiveBody.IndexOf("math.all(math.isfinite(inputEvent.AnalogDelta))", StringComparison.Ordinal) < 0 ||
                hotZoneBody.IndexOf("math.all(math.isfinite(canvasPosition))", StringComparison.Ordinal) < 0 ||
                hotZoneBody.IndexOf("SanitizeFinite(dialCenter.x", StringComparison.Ordinal) < 0 ||
                hotZoneBody.IndexOf("SanitizeFinite(dialHalfExtents.x", StringComparison.Ordinal) < 0 ||
                rotationBody.IndexOf("_currentDegrees = ClampDialDegrees(_currentDegrees)", StringComparison.Ordinal) < 0 ||
                audioBody.IndexOf("ResolveSafeAudioVolume()", StringComparison.Ordinal) < 0 ||
                audioBody.IndexOf("ResolveSafeAudioPitch()", StringComparison.Ordinal) < 0 ||
                clampBody.IndexOf("SanitizeFinite(degrees, 0f)", StringComparison.Ordinal) < 0 ||
                boundsBody.IndexOf("SanitizeFinite(minimumDegrees", StringComparison.Ordinal) < 0 ||
                boundsBody.IndexOf("if (maximum < minimum)", StringComparison.Ordinal) < 0 ||
                scrollBody.IndexOf("SanitizeFinite(degreesPerScrollUnit", StringComparison.Ordinal) < 0 ||
                volumeBody.IndexOf("SanitizeFinite(scrollAudioVolume", StringComparison.Ordinal) < 0 ||
                pitchBody.IndexOf("SanitizeFinite(scrollAudioPitch", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX physical dial violation: non-finite authored values can poison rotation/audio.");
            }
        }

        private static void AssertPdaRejectsForeignPanelId(string pdaController)
        {
            string body = ExtractMethodBody(pdaController, "ReceiveCanvasInput");
            if (body == null)
                throw new InvalidOperationException("1611 APEX PDA identity violation: ReceiveCanvasInput missing.");

            int panelGuardIndex = body.IndexOf("inputEvent.PanelId != _acceptedPanelId", StringComparison.Ordinal);
            int openGuardIndex = body.IndexOf("PlayerPDA.IsOpen", StringComparison.Ordinal);
            int hitTargetIndex = body.IndexOf("ResolvePanelHitTarget(inputEvent.CanvasHitPoint)", StringComparison.Ordinal);
            if (panelGuardIndex < 0 ||
                openGuardIndex < panelGuardIndex ||
                hitTargetIndex < openGuardIndex)
            {
                throw new InvalidOperationException("1611 APEX PDA identity violation: foreign panel input can reach PDA pointer mutation.");
            }
        }

        private static void AssertPdaRejectsNonFiniteHitPoint(string pdaController)
        {
            string receiveBody = ExtractMethodBody(pdaController, "ReceiveCanvasInput");
            string worldBody = ExtractMethodBody(pdaController, "TryCanvasHitPointToRootWorld");
            string boundedBody = ExtractMethodBody(pdaController, "TryResolveBoundedCanvasHit");
            if (receiveBody == null || worldBody == null || boundedBody == null)
                throw new InvalidOperationException("1611 APEX PDA finite-hit violation: PDA hit methods missing.");

            int receiveFiniteIndex = receiveBody.IndexOf("math.all(math.isfinite(inputEvent.CanvasHitPoint))", StringComparison.Ordinal);
            int receiveBoundedIndex = receiveBody.IndexOf("TryResolveBoundedCanvasHit(inputEvent.CanvasHitPoint", StringComparison.Ordinal);
            int receiveHitIndex = receiveBody.IndexOf("ResolvePanelHitTarget(inputEvent.CanvasHitPoint)", StringComparison.Ordinal);
            int releaseIndex = receiveBody.IndexOf("HandlePointerUp(null)", StringComparison.Ordinal);
            int worldBoundedIndex = worldBody.IndexOf("TryResolveBoundedCanvasHit(canvasHitPoint", StringComparison.Ordinal);
            int worldUvIndex = worldBody.IndexOf("canvasHitPoint.x / safeReferenceWidth", StringComparison.Ordinal);
            int boundedFiniteIndex = boundedBody.IndexOf("math.all(math.isfinite(canvasHitPoint))", StringComparison.Ordinal);
            int boundedBoundsIndex = boundedBody.IndexOf("canvasHitPoint.x < 0f", StringComparison.Ordinal);
            int boundedReturnTrueIndex = boundedBody.IndexOf("return true", StringComparison.Ordinal);
            if (receiveFiniteIndex < 0 ||
                receiveBoundedIndex < receiveFiniteIndex ||
                receiveHitIndex < receiveBoundedIndex ||
                releaseIndex < receiveFiniteIndex ||
                worldBoundedIndex < 0 ||
                worldUvIndex < worldBoundedIndex ||
                boundedFiniteIndex < 0 ||
                boundedBoundsIndex < boundedFiniteIndex ||
                boundedReturnTrueIndex < boundedBoundsIndex)
            {
                throw new InvalidOperationException("1611 APEX PDA finite-hit violation: invalid canvas coordinates can enter PDA pointer hit math.");
            }
        }

        private static void AssertPdaHotGraphClean(string pdaController)
        {
            AssertHotMethodClean(pdaController, "ReceiveCanvasInput", PdaControllerPath);
            AssertHotMethodClean(pdaController, "ResolvePanelHitTarget", PdaControllerPath);
            AssertHotMethodClean(pdaController, "TryResolveCachedPointerTarget", PdaControllerPath);
            AssertHotMethodClean(pdaController, "IsCachedPointerTargetEnabled", PdaControllerPath);
            AssertHotMethodClean(pdaController, "TryCanvasHitPointToRootWorld", PdaControllerPath);
            AssertHotMethodClean(pdaController, "TryResolveBoundedCanvasHit", PdaControllerPath);
            AssertHotMethodClean(pdaController, "HandlePointerDown", PdaControllerPath);
        }

        private static void AssertPdaDownCancelsExistingPress(string pdaController)
        {
            string body = ExtractMethodBody(pdaController, "HandlePointerDown");
            if (body == null)
                throw new InvalidOperationException("1611 APEX PDA pointer-state violation: HandlePointerDown missing.");

            int cancelIndex = body.IndexOf("CancelActivePointerGesture()", StringComparison.Ordinal);
            int assignIndex = body.IndexOf("_pressedTarget = hitTarget", StringComparison.Ordinal);
            if (cancelIndex < 0 || assignIndex < 0 || cancelIndex > assignIndex)
            {
                throw new InvalidOperationException("1611 APEX PDA pointer-state violation: repeated Down can overwrite active press without cleanup.");
            }
        }

        private static void AssertPdaPointerCanvasGroupStackCached(string pdaController)
        {
            string addBody = ExtractMethodBody(pdaController, "AddPointerTarget");
            string cacheBody = ExtractMethodBody(pdaController, "CachePointerTargetCanvasGroups");
            string enabledBody = ExtractMethodBody(pdaController, "IsCachedPointerTargetEnabled");
            string clearBody = ExtractMethodBody(pdaController, "ClearPointerTargetCache");
            if (addBody == null || cacheBody == null || enabledBody == null || clearBody == null)
                throw new InvalidOperationException("1611 APEX PDA CanvasGroup violation: pointer CanvasGroup stack methods missing.");

            int cacheCallIndex = addBody.IndexOf("CachePointerTargetCanvasGroups(_pointerTargetCount, targetRect)", StringComparison.Ordinal);
            int incrementIndex = addBody.IndexOf("_pointerTargetCount++", StringComparison.Ordinal);
            if (pdaController.IndexOf("ResolveNearestParentComponent<CanvasGroup>(targetRect)", StringComparison.Ordinal) >= 0 ||
                pdaController.IndexOf("new CanvasGroup[PointerTargetCapacity * MaxPointerCanvasGroupsPerTarget]", StringComparison.Ordinal) < 0 ||
                pdaController.IndexOf("private readonly byte[] _pointerTargetCanvasGroupCounts", StringComparison.Ordinal) < 0 ||
                cacheCallIndex < 0 ||
                incrementIndex < cacheCallIndex)
            {
                throw new InvalidOperationException("1611 APEX PDA CanvasGroup violation: PDA target cache stores only nearest CanvasGroup.");
            }

            if (cacheBody.IndexOf("current.TryGetComponent(out CanvasGroup group)", StringComparison.Ordinal) < 0 ||
                cacheBody.IndexOf("PointerCanvasGroupCacheOverflow", StringComparison.Ordinal) < 0 ||
                enabledBody.IndexOf("groupCount == PointerCanvasGroupCacheOverflow", StringComparison.Ordinal) < 0 ||
                enabledBody.IndexOf("for (int i = 0; i < groupCount; i++)", StringComparison.Ordinal) < 0 ||
                enabledBody.IndexOf("_pointerTargetCanvasGroups[baseIndex + i]", StringComparison.Ordinal) < 0 ||
                enabledBody.IndexOf("!canvasGroup.blocksRaycasts", StringComparison.Ordinal) < 0 ||
                clearBody.IndexOf("for (int groupIndex = 0; groupIndex < MaxPointerCanvasGroupsPerTarget; groupIndex++)", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX PDA CanvasGroup violation: hidden ancestor CanvasGroups can leak PDA pointer targets.");
            }
        }

        private static void AssertRaycasterTopmostHitOrder(string raycaster)
        {
            string body = ExtractMethodBody(raycaster, "ResolveControlIndex");
            if (body == null)
                throw new InvalidOperationException("1611 APEX raycaster violation: ResolveControlIndex missing.");

            if (body.IndexOf("for (int i = _raycastItemCount - 1; i >= 0; i--", StringComparison.Ordinal) < 0 ||
                body.IndexOf("RaycastItemKind.Control", StringComparison.Ordinal) < 0 ||
                body.IndexOf(": -1", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("1611 APEX raycaster violation: overlapping controls are not resolved in visual draw order.");
        }

        private static void AssertRaycasterGraphicBlockers(string raycaster)
        {
            string cacheBody = ExtractMethodBody(raycaster, "CacheGraphicBlocker");
            string resolveBody = ExtractMethodBody(raycaster, "ResolveControlIndex");
            string eligibleBody = ExtractMethodBody(raycaster, "IsRaycastBlockerEligible");
            if (cacheBody == null || resolveBody == null || eligibleBody == null)
                throw new InvalidOperationException("1611 APEX raycaster violation: modal graphic blocker methods missing.");

            if (cacheBody.IndexOf("Graphic graphic", StringComparison.Ordinal) < 0 ||
                cacheBody.IndexOf("graphic.raycastTarget", StringComparison.Ordinal) < 0 ||
                cacheBody.IndexOf("RaycastItemKind.GraphicBlocker", StringComparison.Ordinal) < 0 ||
                cacheBody.IndexOf("HasInteractiveAncestor", StringComparison.Ordinal) < 0 ||
                resolveBody.IndexOf("RaycastItemKind.Control", StringComparison.Ordinal) < 0 ||
                eligibleBody.IndexOf("!graphic.isActiveAndEnabled", StringComparison.Ordinal) < 0 ||
                eligibleBody.IndexOf("!group.blocksRaycasts", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX raycaster violation: modal graphic blockers do not stop lower controls.");
            }

            string groupBody = ExtractMethodBody(raycaster, "CacheCanvasGroupBlocker");
            if (groupBody == null ||
                groupBody.IndexOf("CanvasGroup group", StringComparison.Ordinal) < 0 ||
                groupBody.IndexOf("RaycastItemKind.CanvasGroupBlocker", StringComparison.Ordinal) < 0 ||
                eligibleBody.IndexOf("activeInHierarchy", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX raycaster violation: CanvasGroup blockers do not stop lower controls.");
            }

            if (eligibleBody.IndexOf("!group.interactable", StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("1611 APEX raycaster violation: non-interactive CanvasGroup blockers leak lower controls.");

            string controlBody = ExtractMethodBody(raycaster, "IsControlEligible");
            string cacheControlBody = ExtractMethodBody(raycaster, "CacheCanvasGroups");
            string cacheBlockerBody = ExtractMethodBody(raycaster, "CacheRaycastItemCanvasGroups");
            if (controlBody == null ||
                cacheControlBody == null ||
                cacheBlockerBody == null ||
                controlBody.IndexOf("group.ignoreParentGroups", StringComparison.Ordinal) < 0 ||
                eligibleBody.IndexOf("group.ignoreParentGroups", StringComparison.Ordinal) < 0 ||
                cacheControlBody.IndexOf("group.ignoreParentGroups", StringComparison.Ordinal) < 0 ||
                cacheBlockerBody.IndexOf("group.ignoreParentGroups", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX raycaster violation: cached CanvasGroup ignoreParentGroups is not honored.");
            }
        }

        private static void AssertRaycasterSelectionFlushLate(string raycaster, string mainMenu, string pauseMenu)
        {
            string updateHoverBody = ExtractMethodBody(raycaster, "UpdateHover");
            string clearBody = ExtractMethodBody(raycaster, "ClearInteractionState");
            string flushBody = ExtractMethodBody(raycaster, "FlushPendingSelection");
            string mainLateBody = ExtractMethodBody(mainMenu, "LateFrameTick");
            string pauseLateBody = ExtractMethodBody(pauseMenu, "LateFrameTick");
            if (updateHoverBody == null || clearBody == null || flushBody == null || mainLateBody == null || pauseLateBody == null)
                throw new InvalidOperationException("1611 APEX selection violation: selection flush methods missing.");

            if (updateHoverBody.IndexOf("SetSelectedGameObject", StringComparison.Ordinal) >= 0 ||
                updateHoverBody.IndexOf("_pendingSelectionControlIndex = targetIndex", StringComparison.Ordinal) < 0 ||
                clearBody.IndexOf("SetSelectedGameObject", StringComparison.Ordinal) >= 0 ||
                clearBody.IndexOf("_pendingSelectionControlIndex = -1", StringComparison.Ordinal) < 0 ||
                flushBody.IndexOf("SetSelectedGameObject", StringComparison.Ordinal) < 0 ||
                flushBody.IndexOf("eventSystem.currentSelectedGameObject == targetObject", StringComparison.Ordinal) < 0 ||
                flushBody.IndexOf("IsControlEligible(targetIndex)", StringComparison.Ordinal) < 0 ||
                mainLateBody.IndexOf("FlushPendingSelection", StringComparison.Ordinal) < 0 ||
                pauseLateBody.IndexOf("FlushPendingSelection", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX selection violation: diegetic hover selection is not late-frame flushed.");
            }
        }

        private static void AssertColdMethodClean(string text, string methodName, string path)
        {
            string body = ExtractMethodBody(text, methodName);
            if (body == null)
                throw new InvalidOperationException("1611 APEX violation: missing cold method " + path + "::" + methodName);

            AssertHotTokenAbsent(body, "GlobalRegistry.Get<", path, methodName);
            AssertHotTokenAbsent(body, "GetComponent(", path, methodName);
            AssertHotTokenAbsent(body, "TryGetComponent(", path, methodName);
            AssertHotTokenAbsent(body, "GetComponents", path, methodName);
            AssertHotTokenAbsent(body, "FindObject", path, methodName);
            AssertHotTokenAbsent(body, "GameObject.Find", path, methodName);
            AssertHotTokenAbsent(body, ".Complete(", path, methodName);
            AssertHotTokenAbsent(body, "foreach", path, methodName);
            AssertHotTokenAbsent(body, "Enumerable.", path, methodName);
            AssertHotTokenAbsent(body, "string.Format", path, methodName);
            AssertHotTokenAbsent(body, "new List<", path, methodName);
            AssertHotTokenAbsent(body, "new Dictionary<", path, methodName);
        }

        private static void AssertMenuCameraSanitizesDeltaTime(string cameraController)
        {
            string advanceBody = ExtractMethodBody(cameraController, "Advance");
            if (advanceBody == null)
                throw new InvalidOperationException("1611 APEX camera violation: MenuCameraController.Advance missing.");

            if (advanceBody.IndexOf("math.isfinite(unscaledDeltaTime)", StringComparison.Ordinal) < 0 ||
                advanceBody.IndexOf("safeDeltaTime", StringComparison.Ordinal) < 0 ||
                advanceBody.IndexOf("math.isfinite(_elapsed)", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX camera violation: MenuCameraController.Advance does not sanitize delta/elapsed.");
            }
        }

        private static void AssertMenuCameraSnapsToTargetOnCompletion(string cameraController)
        {
            string advanceBody = ExtractMethodBody(cameraController, "Advance");
            if (advanceBody == null)
                throw new InvalidOperationException("1611 APEX camera violation: MenuCameraController.Advance missing.");

            int completeIndex = advanceBody.IndexOf("if (t >= 1f)", StringComparison.Ordinal);
            int snapIndex = advanceBody.IndexOf("SetPositionAndRotation(_targetPosition, _targetRotation)", StringComparison.Ordinal);
            int inactiveIndex = advanceBody.IndexOf("_active = false", StringComparison.Ordinal);
            if (completeIndex < 0 || snapIndex < completeIndex || inactiveIndex < snapIndex)
                throw new InvalidOperationException("1611 APEX camera violation: camera route does not snap exactly to target on completion.");
        }

        private static void AssertMenuCameraUsesBezierAndSlerp(string cameraController)
        {
            string advanceBody = ExtractMethodBody(cameraController, "Advance");
            string slerpBody = ExtractMethodBody(cameraController, "ResolveSlerp");
            if (advanceBody == null || slerpBody == null)
                throw new InvalidOperationException("1611 APEX camera violation: cinematic interpolation methods missing.");

            if (advanceBody.IndexOf("ResolveBezier", StringComparison.Ordinal) < 0 ||
                advanceBody.IndexOf("ResolveSlerp", StringComparison.Ordinal) < 0 ||
                cameraController.IndexOf("ResolveNlerp", StringComparison.Ordinal) >= 0 ||
                slerpBody.IndexOf("math.acos(dot)", StringComparison.Ordinal) < 0 ||
                slerpBody.IndexOf("math.sin(theta)", StringComparison.Ordinal) < 0 ||
                slerpBody.IndexOf("dot > 0.9995f", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX camera violation: menu camera route is not using Bezier position plus bounded slerp rotation.");
            }
        }

        private static void AssertMenuCameraSanitizesRoutePose(string cameraController)
        {
            string configureBody = ExtractMethodBody(cameraController, "Configure");
            string beginBody = ExtractMethodBody(cameraController, "BeginRoute");
            string advanceBody = ExtractMethodBody(cameraController, "Advance");
            string safePositionBody = ExtractMethodBody(cameraController, "ResolveSafePosition");
            string safeRotationBody = ExtractMethodBody(cameraController, "ResolveSafeRotation");
            if (configureBody == null || beginBody == null || advanceBody == null ||
                safePositionBody == null || safeRotationBody == null)
            {
                throw new InvalidOperationException("1611 APEX camera violation: finite pose sanitizer methods missing.");
            }

            if (configureBody.IndexOf("ResolveSafePosition(_cameraTransform.position", StringComparison.Ordinal) < 0 ||
                configureBody.IndexOf("ResolveSafeRotation(_cameraTransform.rotation", StringComparison.Ordinal) < 0 ||
                configureBody.IndexOf("SetPositionAndRotation(safePosition, safeRotation)", StringComparison.Ordinal) < 0 ||
                beginBody.IndexOf("ResolveSafePosition(_cameraTransform.position, _basePosition)", StringComparison.Ordinal) < 0 ||
                beginBody.IndexOf("ResolveSafeRotation(_cameraTransform.rotation, _baseRotation)", StringComparison.Ordinal) < 0 ||
                beginBody.IndexOf("ResolveSafePosition(targetPosition, _basePosition)", StringComparison.Ordinal) < 0 ||
                beginBody.IndexOf("ResolveSafeRotation(targetRotation, _baseRotation)", StringComparison.Ordinal) < 0 ||
                advanceBody.IndexOf("!math.isfinite(_duration)", StringComparison.Ordinal) < 0 ||
                advanceBody.IndexOf("ResolveSafePosition(position, _targetPosition)", StringComparison.Ordinal) < 0 ||
                advanceBody.IndexOf("ResolveSafeRotation(rotation, _targetRotation)", StringComparison.Ordinal) < 0 ||
                safePositionBody.IndexOf("math.all(math.isfinite(v))", StringComparison.Ordinal) < 0 ||
                safeRotationBody.IndexOf("Quaternion.identity", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX camera violation: menu camera can propagate non-finite route poses.");
            }
        }

        private static void AssertHotTokenAbsent(string body, string token, string path, string methodName)
        {
            if (body.IndexOf(token, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException(
                    "1611 APEX hot-path violation: " + token + " in " + path + "::" + methodName);
        }

        private static void AssertPhaseRoute(string text, string simulationMethod, string presentationMethod, string token, string path)
        {
            string simulationBody = ExtractMethodBody(text, simulationMethod);
            string presentationBody = ExtractMethodBody(text, presentationMethod);
            if (simulationBody == null || presentationBody == null)
                throw new InvalidOperationException("1611 APEX phase route missing in " + path);

            if (simulationBody.IndexOf(token, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("1611 APEX phase violation: " + token + " found in " + path + "::" + simulationMethod);

            if (presentationBody.IndexOf(token, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("1611 APEX phase violation: " + token + " missing from " + path + "::" + presentationMethod);
        }

        private static void AssertPauseMenuUsesUnscaledPresentationDelta(string pauseMenu)
        {
            string unscaledBody = ExtractMethodBody(pauseMenu, "UnscaledFastTick");
            string lateBody = ExtractMethodBody(pauseMenu, "LateFrameTick");
            if (unscaledBody == null || lateBody == null)
                throw new InvalidOperationException("1611 APEX phase violation: pause menu unscaled presentation methods missing.");

            if (lateBody.IndexOf("CurrentFrameDeltaTime", StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("1611 APEX phase violation: pause menu camera uses scaled delta in LateFrameTick.");

            if (unscaledBody.IndexOf("_pauseMenuPresentationDeltaTime", StringComparison.Ordinal) < 0 ||
                lateBody.IndexOf("_pauseMenuPresentationDeltaTime", StringComparison.Ordinal) < 0 ||
                lateBody.IndexOf("ResolveCurrentUnscaledFrameDeltaTime", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX phase violation: pause menu unscaled delta transfer is not wired.");
            }
        }

        private static void AssertMainMenuDropsBlockedCancel(string mainMenu)
        {
            string body = ExtractMethodBody(mainMenu, "HandleCancelInput");
            if (body == null)
                throw new InvalidOperationException("1611 APEX cancel violation: MainMenuController.HandleCancelInput missing.");

            int requestedGuardIndex = body.IndexOf("if (!_cancelRequested)", StringComparison.Ordinal);
            int blockedGuardIndex = body.IndexOf("if (_isTransitioning", StringComparison.Ordinal);
            int dropIndex = blockedGuardIndex >= 0
                ? body.IndexOf("_cancelRequested = false;", blockedGuardIndex, StringComparison.Ordinal)
                : -1;
            int blockedReturnIndex = dropIndex >= 0
                ? body.IndexOf("return;", dropIndex, StringComparison.Ordinal)
                : -1;
            int activeUseIndex = blockedReturnIndex >= 0
                ? body.IndexOf("if (_currentPanel == settingsGroup)", blockedReturnIndex, StringComparison.Ordinal)
                : -1;
            if (requestedGuardIndex < 0 ||
                blockedGuardIndex < requestedGuardIndex ||
                dropIndex < blockedGuardIndex ||
                blockedReturnIndex < dropIndex ||
                activeUseIndex < blockedReturnIndex)
            {
                throw new InvalidOperationException("1611 APEX cancel violation: blocked cancel is not consumed before panel routing.");
            }
        }

        private static void AssertMainMenuSelectionFailsClosedDuringTransition(string mainMenu)
        {
            string refreshBody = ExtractMethodBody(mainMenu, "RefreshSelectionIfNeeded");
            string eligibleBody = ExtractMethodBody(mainMenu, "IsDefaultSelectionTargetEligible");
            if (refreshBody == null || eligibleBody == null)
                throw new InvalidOperationException("1611 APEX selection violation: main menu selection eligibility methods missing.");

            if (refreshBody.IndexOf("IsDefaultSelectionTargetEligible(target)", StringComparison.Ordinal) < 0 ||
                eligibleBody.IndexOf("!currentPanel.interactable", StringComparison.Ordinal) < 0 ||
                eligibleBody.IndexOf("!currentPanel.blocksRaycasts", StringComparison.Ordinal) < 0 ||
                eligibleBody.IndexOf("currentPanel.alpha < 0.999f", StringComparison.Ordinal) < 0 ||
                eligibleBody.IndexOf("targetTransform.IsChildOf(panelTransform)", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX selection violation: main menu default focus can select a locked or foreign panel target.");
            }
        }

        private static void AssertMainMenuUsesCinematicHandoffBeforeBootstrapFallback(string mainMenu)
        {
            string startBody = ExtractMethodBody(mainMenu, "StartGame");
            if (startBody == null)
                throw new InvalidOperationException("1611 APEX handoff violation: MainMenuController.StartGame missing.");

            int sceneServiceIndex = startBody.IndexOf("ISceneService sceneService", StringComparison.Ordinal);
            int configureIndex = startBody.IndexOf("ConfigureMainMenuCinematic(mainMenuCamera, cinematicPanel)", StringComparison.Ordinal);
            int loadIndex = startBody.IndexOf("sceneService.LoadScene(sceneName)", StringComparison.Ordinal);
            int fallbackIndex = startBody.IndexOf("TryRecoverBootstrapRouteForStart(sceneName)", StringComparison.Ordinal);
            if (sceneServiceIndex < 0 ||
                configureIndex < sceneServiceIndex ||
                loadIndex < configureIndex ||
                fallbackIndex >= 0 && fallbackIndex < sceneServiceIndex)
            {
                throw new InvalidOperationException("1611 APEX handoff violation: bootstrap fallback can bypass the main-menu cinematic handoff.");
            }
        }

        private static void AssertPauseMenuLocksSectionInteractionDuringCameraRoute(string pauseMenu)
        {
            string showBody = ExtractMethodBody(pauseMenu, "ShowSection");
            string lateBody = ExtractMethodBody(pauseMenu, "LateFrameTick");
            string refreshBody = ExtractMethodBody(pauseMenu, "RefreshPauseSectionInteractionGate");
            string applyBody = ExtractMethodBody(pauseMenu, "ApplyPauseSectionInteractionGate");
            string queueSelectionBody = ExtractMethodBody(pauseMenu, "QueueDefaultSelectionForSection");
            string flushSelectionBody = ExtractMethodBody(pauseMenu, "FlushPendingDefaultSelection");
            string flushClearBody = ExtractMethodBody(pauseMenu, "FlushPendingPauseSelectionClear");
            string clearSelectionBody = ExtractMethodBody(pauseMenu, "ClearPauseSelection");
            string selectBody = ExtractMethodBody(pauseMenu, "SelectDefaultButtonForSection");
            if (showBody == null || lateBody == null || refreshBody == null || applyBody == null ||
                queueSelectionBody == null || flushSelectionBody == null || flushClearBody == null ||
                clearSelectionBody == null || selectBody == null)
                throw new InvalidOperationException("1611 APEX phase violation: pause section interaction gate methods missing.");

            if (showBody.IndexOf("ApplyPauseSectionInteractionGate(locked: true)", StringComparison.Ordinal) < 0 ||
                showBody.IndexOf("QueueDefaultSelectionForSection(section, gateInteraction)", StringComparison.Ordinal) < 0 ||
                lateBody.IndexOf("RefreshPauseSectionInteractionGate()", StringComparison.Ordinal) < 0 ||
                lateBody.IndexOf("FlushPendingPauseSelectionClear()", StringComparison.Ordinal) < 0 ||
                lateBody.IndexOf("FlushPendingDefaultSelection()", StringComparison.Ordinal) < 0 ||
                refreshBody.IndexOf("_pauseMenuCameraController.IsActive", StringComparison.Ordinal) < 0 ||
                refreshBody.IndexOf("FlushPendingDefaultSelection()", StringComparison.Ordinal) >= 0 ||
                applyBody.IndexOf("group.interactable = !locked", StringComparison.Ordinal) < 0 ||
                applyBody.IndexOf("group.blocksRaycasts = !locked", StringComparison.Ordinal) < 0 ||
                queueSelectionBody.IndexOf("_hasPendingDefaultSelection = true", StringComparison.Ordinal) < 0 ||
                queueSelectionBody.IndexOf("SelectDefaultButtonForSection(section)", StringComparison.Ordinal) >= 0 ||
                flushSelectionBody.IndexOf("_pauseSectionInteractionGateActive", StringComparison.Ordinal) < 0 ||
                flushSelectionBody.IndexOf("section != _activeSection", StringComparison.Ordinal) < 0 ||
                clearSelectionBody.IndexOf("SetSelectedGameObject", StringComparison.Ordinal) >= 0 ||
                clearSelectionBody.IndexOf("_hasPendingDefaultSelection = false", StringComparison.Ordinal) < 0 ||
                clearSelectionBody.IndexOf("_hasPendingPauseSelectionClear = true", StringComparison.Ordinal) < 0 ||
                clearSelectionBody.IndexOf("FlushPendingPauseSelectionClear()", StringComparison.Ordinal) < 0 ||
                flushClearBody.IndexOf("SetSelectedGameObject(null)", StringComparison.Ordinal) < 0 ||
                flushClearBody.IndexOf("_hasPendingPauseSelectionClear = false", StringComparison.Ordinal) < 0 ||
                pauseMenu.IndexOf("QueueDefaultSelectionForSection(PauseSection.Saves, gateInteraction: false)", StringComparison.Ordinal) < 0 ||
                selectBody.IndexOf("IsDefaultSelectionTargetEligible(section, target)", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX phase violation: pause section interaction/default selection is not locked during camera route.");
            }

            string eligibleBody = ExtractMethodBody(pauseMenu, "IsDefaultSelectionTargetEligible");
            if (eligibleBody == null ||
                eligibleBody.IndexOf("!sectionGroup.interactable", StringComparison.Ordinal) < 0 ||
                eligibleBody.IndexOf("!sectionGroup.blocksRaycasts", StringComparison.Ordinal) < 0 ||
                eligibleBody.IndexOf("sectionGroup.alpha < 0.999f", StringComparison.Ordinal) < 0 ||
                eligibleBody.IndexOf("targetTransform.IsChildOf(groupTransform)", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX phase violation: pause default focus can select a locked or foreign section target.");
            }
        }

        private static void AssertPanelLateFrameFlagUsesFinally(string panelController)
        {
            string lateBody = ExtractMethodBody(panelController, "LateFrameTick");
            if (lateBody == null)
                throw new InvalidOperationException("1611 APEX phase violation: panel LateFrameTick missing.");

            if (lateBody.IndexOf("_applyingLateFramePresentation = true", StringComparison.Ordinal) < 0 ||
                lateBody.IndexOf("finally", StringComparison.Ordinal) < 0 ||
                lateBody.IndexOf("_applyingLateFramePresentation = false", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX phase violation: panel late-frame presentation flag is not protected by finally.");
            }

            int flagIndex = lateBody.IndexOf("_applyingLateFramePresentation = true", StringComparison.Ordinal);
            int advanceIndex = lateBody.IndexOf("AdvancePanelInteractionPresentation", StringComparison.Ordinal);
            if (advanceIndex < 0 || flagIndex > advanceIndex)
                throw new InvalidOperationException("1611 APEX phase violation: panel late-frame presentation flag does not cover the presentation advance.");
        }

        private static void AssertPanelClearDropsQueuedEventsBeforeRelease(string panelController)
        {
            string clearBody = ExtractMethodBody(panelController, "ClearHoverState");
            string releaseBody = ExtractMethodBody(panelController, "DispatchReleaseBeforeClear");
            if (clearBody == null || releaseBody == null)
                throw new InvalidOperationException("1611 APEX panel-clear violation: clear/release methods missing.");

            if (releaseBody.IndexOf("DispatchInputEvents(_inputEventCount)", StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("1611 APEX panel-clear violation: stale queued events are flushed during panel clear.");

            int shouldReleaseIndex = releaseBody.IndexOf("bool shouldRelease = _wasPressedLastFrame || _fingerPressedLastFrame", StringComparison.Ordinal);
            int clearHeadIndex = releaseBody.IndexOf("_inputEventHead = 0", StringComparison.Ordinal);
            int clearTailIndex = releaseBody.IndexOf("_inputEventTail = 0", StringComparison.Ordinal);
            int clearCountIndex = releaseBody.IndexOf("_inputEventCount = 0", StringComparison.Ordinal);
            int nullGuardIndex = releaseBody.IndexOf("if (_panelInteractable == null)", StringComparison.Ordinal);
            int releaseGuardIndex = releaseBody.IndexOf("if (!shouldRelease)", StringComparison.Ordinal);
            int receiveIndex = releaseBody.IndexOf("_panelInteractable.ReceiveCanvasInput(in releaseEvent)", StringComparison.Ordinal);
            if (shouldReleaseIndex < 0 ||
                clearHeadIndex < shouldReleaseIndex ||
                clearTailIndex < clearHeadIndex ||
                clearCountIndex < clearTailIndex ||
                nullGuardIndex < clearCountIndex ||
                releaseGuardIndex < nullGuardIndex ||
                receiveIndex < releaseGuardIndex)
            {
                throw new InvalidOperationException("1611 APEX panel-clear violation: clear-state release is not fail-closed and ordered.");
            }

            int dispatchIndex = clearBody.IndexOf("DispatchReleaseBeforeClear()", StringComparison.Ordinal);
            int clearWasPressedIndex = clearBody.IndexOf("_wasPressedLastFrame = false", StringComparison.Ordinal);
            int clearFingerPressedIndex = clearBody.IndexOf("_fingerPressedLastFrame = false", StringComparison.Ordinal);
            if (dispatchIndex < 0 ||
                clearWasPressedIndex < dispatchIndex ||
                clearFingerPressedIndex < clearWasPressedIndex)
            {
                throw new InvalidOperationException("1611 APEX panel-clear violation: release state is cleared before synthetic Up can be emitted.");
            }
        }

        private static void AssertPanelProjectionFailsClosed(string panelController)
        {
            string canvasBody = ExtractMethodBody(panelController, "TryProjectCanvasPointToWorld");
            string rayBody = ExtractMethodBody(panelController, "TryProjectRayToPanel");
            string localBody = ExtractMethodBody(panelController, "TryProjectLocalHitToCanvas");
            string projectionBody = ExtractMethodBody(panelController, "IsPanelProjectionDataFinite");
            string referenceBody = ExtractMethodBody(panelController, "IsCanvasPointInsideReference");
            if (canvasBody == null || rayBody == null || localBody == null || projectionBody == null || referenceBody == null)
                throw new InvalidOperationException("1611 APEX projection violation: panel projection guard methods missing.");

            if (canvasBody.IndexOf("worldPosition = default", StringComparison.Ordinal) < 0 ||
                canvasBody.IndexOf("!IsPanelProjectionDataFinite()", StringComparison.Ordinal) < 0 ||
                canvasBody.IndexOf("!IsCanvasPointInsideReference(canvasPosition)", StringComparison.Ordinal) < 0 ||
                canvasBody.IndexOf("math.clamp(canvasPosition", StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException("1611 APEX projection violation: canvas-to-world projection still repairs invalid points instead of failing closed.");
            }

            if (rayBody.IndexOf("!math.all(math.isfinite(rayOriginWs))", StringComparison.Ordinal) < 0 ||
                rayBody.IndexOf("!math.all(math.isfinite(rayDirectionWs))", StringComparison.Ordinal) < 0 ||
                rayBody.IndexOf("!math.isfinite(directionLengthSq)", StringComparison.Ordinal) < 0 ||
                rayBody.IndexOf("!math.isfinite(denom)", StringComparison.Ordinal) < 0 ||
                rayBody.IndexOf("!math.isfinite(planeDistance)", StringComparison.Ordinal) < 0 ||
                rayBody.IndexOf("!math.isfinite(travelDistanceSq)", StringComparison.Ordinal) < 0 ||
                rayBody.IndexOf("!math.all(math.isfinite(worldHit))", StringComparison.Ordinal) < 0 ||
                rayBody.IndexOf("!math.all(math.isfinite(localHit))", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX projection violation: ray projection can propagate non-finite state.");
            }

            if (localBody.IndexOf("!math.all(math.isfinite(localHit))", StringComparison.Ordinal) < 0 ||
                localBody.IndexOf("!math.all(math.isfinite(uv))", StringComparison.Ordinal) < 0 ||
                localBody.IndexOf("math.clamp(uv", StringComparison.Ordinal) >= 0 ||
                projectionBody.IndexOf("_panelData.ReferenceWidth > 0", StringComparison.Ordinal) < 0 ||
                referenceBody.IndexOf("canvasPosition.x >= 0f", StringComparison.Ordinal) < 0 ||
                referenceBody.IndexOf("canvasPosition.x <= _panelData.ReferenceWidth", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX projection violation: local/reference projection bounds are incomplete.");
            }
        }

        private static void AssertPanelAnalogDeltaFailsClosed(string panelController)
        {
            string body = ExtractMethodBody(panelController, "QueueInputEventsFromInputState");
            if (body == null)
                throw new InvalidOperationException("1611 APEX panel input violation: QueueInputEventsFromInputState missing.");

            int assignIndex = body.IndexOf("analogDelta = new float2(state.ScrollDelta.x, state.ScrollDelta.y)", StringComparison.Ordinal);
            int finiteIndex = body.IndexOf("!math.all(math.isfinite(analogDelta))", StringComparison.Ordinal);
            int zeroIndex = finiteIndex >= 0
                ? body.IndexOf("analogDelta = float2.zero", finiteIndex, StringComparison.Ordinal)
                : -1;
            int scrollIndex = body.IndexOf("eventType |= DiegeticPanelInputEventType.Scroll", StringComparison.Ordinal);
            if (assignIndex < 0 ||
                finiteIndex < assignIndex ||
                zeroIndex < finiteIndex ||
                scrollIndex < zeroIndex)
            {
                throw new InvalidOperationException("1611 APEX panel input violation: AnalogDelta can propagate non-finite scroll state.");
            }
        }

        private static void AssertTransitionOverlayWorldSpace(string sceneRuntime)
        {
            if (sceneRuntime.IndexOf("RenderMode.ScreenSpaceOverlay", StringComparison.Ordinal) >= 0 ||
                sceneRuntime.IndexOf("RenderMode.ScreenSpaceCamera", StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException("1611 APEX overlay violation: scene transition overlay still uses Screen Space canvas.");
            }

            string ensureBody = ExtractMethodBody(sceneRuntime, "EnsureTransitionOverlay");
            string placeBody = ExtractMethodBody(sceneRuntime, "PlaceTransitionOverlayInCameraView");
            string abortBody = ExtractMethodBody(sceneRuntime, "AbortTransitionOverlayCreation");
            string clearBody = ExtractMethodBody(sceneRuntime, "ClearTransitionOverlayObjectReferences");
            string endBody = ExtractMethodBody(sceneRuntime, "EndMainMenuCinematicTransition");
            if (ensureBody == null || placeBody == null || abortBody == null || clearBody == null || endBody == null)
                throw new InvalidOperationException("1611 APEX overlay violation: transition overlay world-space methods missing.");

            if (ensureBody.IndexOf("canvas.renderMode = RenderMode.WorldSpace", StringComparison.Ordinal) < 0 ||
                ensureBody.IndexOf("PlaceTransitionOverlayInCameraView()", StringComparison.Ordinal) < 0 ||
                placeBody.IndexOf("SetPositionAndRotation", StringComparison.Ordinal) < 0 ||
                placeBody.IndexOf("worldCamera = overlayCamera", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX overlay violation: transition overlay is not camera-anchored world-space.");
            }

            if (ensureBody.IndexOf("AbortTransitionOverlayCreation(root)", StringComparison.Ordinal) < 0 ||
                abortBody.IndexOf("DestroyTransitionOverlayRoot(root)", StringComparison.Ordinal) < 0 ||
                abortBody.IndexOf("ClearTransitionOverlayObjectReferences()", StringComparison.Ordinal) < 0 ||
                abortBody.IndexOf("DestroyTransitionDitherMaterial()", StringComparison.Ordinal) < 0 ||
                endBody.IndexOf("ClearTransitionOverlayObjectReferences()", StringComparison.Ordinal) < 0 ||
                clearBody.IndexOf("_transitionOverlayRoot = null", StringComparison.Ordinal) < 0 ||
                clearBody.IndexOf("_terminalBootText = null", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX overlay violation: transition overlay partial creation is not fail-closed.");
            }
        }

        private static void AssertTransitionVisualOverkillDamping(string sceneRuntime)
        {
            string tickBody = ExtractMethodBody(sceneRuntime, "AdvanceMainMenuCinematicTransitionState");
            string dissolveBody = ExtractMethodBody(sceneRuntime, "DissolveTransitionOverlayAsync");
            string lateBody = ExtractMethodBody(sceneRuntime, "LateFrameTick");
            string applyBody = ExtractMethodBody(sceneRuntime, "ApplyQueuedMainMenuCinematicPresentation");
            string queueBody = ExtractMethodBody(sceneRuntime, "QueueMainMenuCinematicPresentation");
            string updateBody = ExtractMethodBody(sceneRuntime, "UpdateTransitionVisualOverkill01");
            string resolveBody = ExtractMethodBody(sceneRuntime, "ResolveGlobalQualityWeight01");
            string cameraBody = ExtractMethodBody(sceneRuntime, "ApplyCinematicCameraPose");
            string ditherBody = ExtractMethodBody(sceneRuntime, "SetTransitionDitherCoverage");
            string smoothBody = ExtractMethodBody(sceneRuntime, "SmoothStep01");
            string registerLateBody = ExtractMethodBody(sceneRuntime, "TryRegisterLateFrameTickable");
            string unregisterLateBody = ExtractMethodBody(sceneRuntime, "TryUnregisterLateFrameTickable");
            string clearPresentationBody = ExtractMethodBody(sceneRuntime, "ClearTransitionPresentationState");
            if (tickBody == null || dissolveBody == null || lateBody == null || applyBody == null || queueBody == null ||
                updateBody == null || resolveBody == null || cameraBody == null || ditherBody == null || smoothBody == null ||
                registerLateBody == null || unregisterLateBody == null || clearPresentationBody == null)
                throw new InvalidOperationException("1611 APEX quality violation: transition visual-overkill damping methods missing.");

            if (resolveBody.IndexOf("HomeostasisBrain.GlobalQualityWeight", StringComparison.Ordinal) < 0 ||
                resolveBody.IndexOf("math.isfinite", StringComparison.Ordinal) < 0 ||
                updateBody.IndexOf("math.lerp(1f, targetQuality", StringComparison.Ordinal) < 0 ||
                updateBody.IndexOf("math.min(_transitionVisualOverkill01, desiredVisualOverkill01)", StringComparison.Ordinal) < 0 ||
                updateBody.IndexOf("_transitionVisualOverkill01", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX quality violation: transition does not monotonically damp toward GlobalQualityWeight.");
            }

            if (queueBody.IndexOf("math.isfinite(elapsedSeconds)", StringComparison.Ordinal) < 0 ||
                smoothBody.IndexOf("math.isfinite(value)", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX quality violation: transition scalar transfer can propagate non-finite values.");
            }

            if (tickBody.IndexOf("UpdateTransitionVisualOverkill01(normalized)", StringComparison.Ordinal) < 0 ||
                tickBody.IndexOf("QueueMainMenuCinematicPresentation", StringComparison.Ordinal) < 0 ||
                dissolveBody.IndexOf("UpdateTransitionVisualOverkill01(normalized)", StringComparison.Ordinal) < 0 ||
                dissolveBody.IndexOf("QueueMainMenuCinematicPresentation", StringComparison.Ordinal) < 0 ||
                queueBody.IndexOf("_transitionPresentationDirty = true", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX quality violation: transition damping is not applied during both menu dive and dissolve.");
            }

            if (tickBody.IndexOf("ApplyCinematicCameraPose", StringComparison.Ordinal) >= 0 ||
                tickBody.IndexOf("SetTransitionDitherCoverage", StringComparison.Ordinal) >= 0 ||
                dissolveBody.IndexOf("ApplyCinematicCameraPose", StringComparison.Ordinal) >= 0 ||
                dissolveBody.IndexOf("SetTransitionDitherCoverage", StringComparison.Ordinal) >= 0 ||
                lateBody.IndexOf("ApplyQueuedMainMenuCinematicPresentation()", StringComparison.Ordinal) < 0 ||
                applyBody.IndexOf("if (!_cinematicTransitionActive)", StringComparison.Ordinal) < 0 ||
                applyBody.IndexOf("ApplyCinematicCameraPose", StringComparison.Ordinal) < 0 ||
                applyBody.IndexOf("SetTransitionDitherCoverage", StringComparison.Ordinal) < 0 ||
                applyBody.IndexOf("UpdateWorldDroneCrossfade", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX phase violation: scene transition presentation is not isolated to LateFrameTick.");
            }

            if (sceneRuntime.IndexOf("ILateFrameTickable", StringComparison.Ordinal) < 0 ||
                registerLateBody.IndexOf("SystemDispatcher.Register((ILateFrameTickable)this", StringComparison.Ordinal) < 0 ||
                unregisterLateBody.IndexOf("SystemDispatcher.Unregister((ILateFrameTickable)this", StringComparison.Ordinal) < 0 ||
                clearPresentationBody.IndexOf("_transitionPresentationDirty = false", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX phase violation: scene transition late-frame registration or cleanup missing.");
            }

            if (cameraBody.IndexOf("safeVisualOverkill", StringComparison.Ordinal) < 0 ||
                cameraBody.IndexOf("CinematicHeaveAmplitude", StringComparison.Ordinal) < 0 ||
                ditherBody.IndexOf("MinimumTransitionDitherCoverageScale", StringComparison.Ordinal) < 0 ||
                ditherBody.IndexOf("qualityCoverageScale", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX quality violation: transition damping does not scale heave and dither coverage.");
            }
        }

        private static void AssertDataVaultWriteLocksFlattened(string text, string path)
        {
            int acquireIndex = text.IndexOf("AcquireWriteLock", StringComparison.Ordinal);
            if (acquireIndex < 0)
                acquireIndex = text.IndexOf("TryAcquireWriteLock", StringComparison.Ordinal);
            if (acquireIndex < 0)
                return;

            string body = ExtractContainingBody(text, acquireIndex);
            if (body == null ||
                body.IndexOf("try", StringComparison.Ordinal) < 0 ||
                body.IndexOf("finally", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("1611 APEX lock violation: write lock without try/finally in " + path);
            }

            int first = body.IndexOf("AcquireWriteLock", StringComparison.Ordinal);
            int second = first >= 0 ? body.IndexOf("AcquireWriteLock", first + 1, StringComparison.Ordinal) : -1;
            int firstTry = body.IndexOf("TryAcquireWriteLock", StringComparison.Ordinal);
            int secondTry = firstTry >= 0 ? body.IndexOf("TryAcquireWriteLock", firstTry + 1, StringComparison.Ordinal) : -1;
            if (second >= 0 || secondTry >= 0)
                throw new InvalidOperationException("1611 APEX lock violation: nested write lock candidate in " + path);
        }

        private static string ExtractMethodBody(string text, string methodName)
        {
            string marker = methodName + "(";
            int markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
            while (markerIndex >= 0)
            {
                if (IsMethodMarker(text, markerIndex, methodName))
                {
                    int braceIndex = text.IndexOf('{', markerIndex);
                    int semicolonIndex = text.IndexOf(';', markerIndex);
                    if (braceIndex >= 0 && (semicolonIndex < 0 || braceIndex < semicolonIndex))
                        return ExtractBalancedBody(text, braceIndex);
                }

                markerIndex = text.IndexOf(marker, markerIndex + marker.Length, StringComparison.Ordinal);
            }

            return null;
        }

        private static bool IsMethodMarker(string text, int markerIndex, string methodName)
        {
            if (markerIndex > 0)
            {
                char previous = text[markerIndex - 1];
                if (char.IsLetterOrDigit(previous) || previous == '_')
                    return false;
            }

            int lineStart = text.LastIndexOf('\n', math.max(0, markerIndex - 1));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            string declarationPrefix = text.Substring(lineStart, markerIndex - lineStart);
            if (declarationPrefix.IndexOf("public ", StringComparison.Ordinal) < 0 &&
                declarationPrefix.IndexOf("private ", StringComparison.Ordinal) < 0 &&
                declarationPrefix.IndexOf("internal ", StringComparison.Ordinal) < 0 &&
                declarationPrefix.IndexOf("protected ", StringComparison.Ordinal) < 0)
            {
                return false;
            }

            int parenthesisIndex = markerIndex + methodName.Length;
            return parenthesisIndex < text.Length && text[parenthesisIndex] == '(';
        }

        private static string ExtractContainingBody(string text, int index)
        {
            int braceIndex = index;
            while (braceIndex >= 0 && text[braceIndex] != '{')
                braceIndex--;
            if (braceIndex < 0)
                return null;

            return ExtractBalancedBody(text, braceIndex);
        }

        private static string ExtractBalancedBody(string text, int braceIndex)
        {
            int depth = 0;
            for (int i = braceIndex; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return text.Substring(braceIndex, i - braceIndex + 1);
                }
            }

            return null;
        }

        private static void AssertAbsent(string text, string token)
        {
            if (text.IndexOf(token, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("1611 hot-path source contains forbidden token: " + token);
        }

    }
}
#endif
