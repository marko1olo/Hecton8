using System.Collections.Generic;
using Hecton.UI.MainMenu;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hecton8.Editor
{
    internal static class CodexRuntimeRouteDiagnostics1428
    {
        private const string ProbeMenuPath = "Hecton8/Codex/1428/Probe Main Menu UI Route";
        private const string ClickMenuPath = "Hecton8/Codex/1428/Invoke Start Button Click";
        private const string StartMenuPath = "Hecton8/Codex/1428/Invoke Main Menu New Game";
        private static readonly List<RaycastResult> RaycastResults = new List<RaycastResult>(32);

        [MenuItem(ProbeMenuPath, priority = 14280)]
        private static void ProbeMainMenuUiRoute()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.Log("[Codex1428] probe skipped: editor is not in Play Mode.");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            EventSystem eventSystem = EventSystem.current;
            InputSystemUIInputModule inputModule = null;
            if (eventSystem != null)
                eventSystem.TryGetComponent(out inputModule);

            MainMenuController controller = Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            Button startButton = ResolveStartButton();
            Vector2 pointerPosition = ResolvePointerPosition(startButton);
            RaycastResults.Clear();
            if (eventSystem != null)
            {
                PointerEventData data = new PointerEventData(eventSystem)
                {
                    position = pointerPosition,
                    button = PointerEventData.InputButton.Left
                };
                eventSystem.RaycastAll(data, RaycastResults);
            }

            Debug.Log(
                "[Codex1428] menu_probe " +
                "scene=" + activeScene.name +
                " eventSystem=" + FormatObject(eventSystem) +
                " inputModule=" + FormatObject(inputModule) +
                " actionsBound=" + HasUsableUiModuleActions(inputModule) +
                " pointEnabled=" + FormatActionEnabled(inputModule != null ? inputModule.point : null) +
                " clickEnabled=" + FormatActionEnabled(inputModule != null ? inputModule.leftClick : null) +
                " selected=" + FormatObject(eventSystem != null ? eventSystem.currentSelectedGameObject : null) +
                " controller=" + FormatObject(controller) +
                " startButton=" + FormatObject(startButton) +
                " pointer=" + pointerPosition +
                " hits=" + RaycastResults.Count);

            int limit = RaycastResults.Count < 8 ? RaycastResults.Count : 8;
            for (int i = 0; i < limit; i++)
            {
                RaycastResult hit = RaycastResults[i];
                Button hitButton = hit.gameObject != null ? hit.gameObject.GetComponentInParent<Button>() : null;
                Graphic graphic = hit.gameObject != null ? hit.gameObject.GetComponent<Graphic>() : null;
                Debug.Log(
                    "[Codex1428] menu_probe_hit[" + i + "] object=" + FormatObject(hit.gameObject) +
                    " button=" + FormatObject(hitButton) +
                    " graphicRaycast=" + (graphic != null && graphic.raycastTarget) +
                    " module=" + FormatObject(hit.module));
            }

            RaycastResults.Clear();
        }

        [MenuItem(ClickMenuPath, priority = 14281)]
        private static void InvokeStartButtonClick()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.Log("[Codex1428] start click skipped: editor is not in Play Mode.");
                return;
            }

            Button startButton = ResolveStartButton();
            if (startButton == null)
            {
                Debug.LogError("[Codex1428] BTN_Start button not found.");
                return;
            }

            Debug.Log("[Codex1428] invoking BTN_Start.onClick.");
            startButton.onClick.Invoke();
        }

        [MenuItem(StartMenuPath, priority = 14282)]
        private static void InvokeMainMenuNewGame()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.Log("[Codex1428] new game skipped: editor is not in Play Mode.");
                return;
            }

            MainMenuController controller = Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogError("[Codex1428] MainMenuController not found.");
                return;
            }

            Debug.Log("[Codex1428] invoking MainMenuController.StartGame(NewGame).");
            controller.StartGame(string.Empty);
        }

        private static Button ResolveStartButton()
        {
            GameObject startRoot = GameObject.Find("BTN_Start");
            if (startRoot == null)
                return null;

            startRoot.TryGetComponent(out Button button);
            return button;
        }

        private static Vector2 ResolvePointerPosition(Button button)
        {
            if (button != null && button.TryGetComponent(out RectTransform rectTransform))
            {
                Canvas canvas = button.GetComponentInParent<Canvas>();
                Camera camera = canvas != null ? canvas.worldCamera : null;
                Vector3 worldCenter = rectTransform.TransformPoint(rectTransform.rect.center);
                return RectTransformUtility.WorldToScreenPoint(camera, worldCenter);
            }

            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();

            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        private static string FormatObject(Object target)
        {
            return target != null ? target.name : "null";
        }

        private static string FormatActionEnabled(InputActionReference reference)
        {
            return reference != null && reference.action != null
                ? reference.action.enabled.ToString()
                : "null";
        }

        private static bool HasUsableUiModuleActions(InputSystemUIInputModule inputModule)
        {
            return inputModule != null &&
                   inputModule.actionsAsset != null &&
                   HasUsableActionReference(inputModule.point) &&
                   HasUsableActionReference(inputModule.leftClick) &&
                   HasUsableActionReference(inputModule.move) &&
                   HasUsableActionReference(inputModule.submit) &&
                   HasUsableActionReference(inputModule.cancel);
        }

        private static bool HasUsableActionReference(InputActionReference reference)
        {
            return reference != null &&
                   reference.action != null &&
                   reference.action.bindings.Count > 0;
        }
    }
}
