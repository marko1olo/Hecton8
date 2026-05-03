using System.Collections.Generic;
using Hecton.UI;
using Hecton.UI.MainMenu;
using Hecton8.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hecton8.Tools
{
    internal static class VerificationRuntimeProbe
    {
        // COLD ALLOC: List<GameObject>[512] - loaded-scene root traversal scratch for runtime verification probes - owner: VerificationRuntimeProbe
        private static readonly List<GameObject> _sceneRootScratch = new List<GameObject>(512);

        public static T FindSceneObjectIncludingInactive<T>() where T : Component
        {
            _sceneRootScratch.Clear();

            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                scene.GetRootGameObjects(_sceneRootScratch);
                for (int rootIndex = 0; rootIndex < _sceneRootScratch.Count; rootIndex++)
                {
                    GameObject root = _sceneRootScratch[rootIndex];
                    if (root == null)
                        continue;

                    T candidate = FindComponentInChildrenIncludingInactive<T>(root.transform);
                    if (candidate != null)
                    {
                        _sceneRootScratch.Clear();
                        return candidate;
                    }
                }

                _sceneRootScratch.Clear();
            }

            return null;
        }

        public static PauseMenuController ResolvePauseMenu()
        {
            PauseMenuController runtimePauseMenu = PauseMenuController.ActiveRuntimeInstance;
            if (runtimePauseMenu != null)
                return runtimePauseMenu;

            PauseMenuController pauseMenu = FindSceneObjectIncludingInactive<PauseMenuController>();
            if (pauseMenu != null)
                return pauseMenu;

            PauseMenuHost host = FindSceneObjectIncludingInactive<PauseMenuHost>();
            if (host != null)
                return host.GetComponent<PauseMenuController>();

            return null;
        }

        public static MainMenuController ResolveMainMenuController()
        {
            return FindSceneObjectIncludingInactive<MainMenuController>();
        }

        public static Button ResolvePauseExitToMenuButton(PauseMenuController pauseMenu)
        {
            if (pauseMenu == null)
                return null;

            return FindChildComponentByName<Button>(pauseMenu.transform, "MainButton_4");
        }

        public static bool IsPauseMenuVisible(PauseMenuController pauseMenu)
        {
            if (pauseMenu == null || !pauseMenu.IsOpen)
                return false;

            GameObject pauseObject = pauseMenu.gameObject;
            if (pauseObject == null || !pauseObject.activeInHierarchy)
                return false;

            CanvasGroup canvasGroup = pauseObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                return true;

            return canvasGroup.alpha > 0.001f && canvasGroup.blocksRaycasts && canvasGroup.interactable;
        }

        public static bool HasPauseSelection(PauseMenuController pauseMenu)
        {
            if (pauseMenu == null)
                return false;

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            GameObject selectedObject = eventSystem.currentSelectedGameObject;
            if (selectedObject == null)
                return false;

            Transform selectedTransform = selectedObject.transform;
            Transform pauseTransform = pauseMenu.transform;
            return selectedTransform != null && pauseTransform != null && selectedTransform.IsChildOf(pauseTransform);
        }

        private static T FindChildComponentByName<T>(Transform root, string childName) where T : Component
        {
            if (root == null || string.IsNullOrEmpty(childName))
                return null;

            if (string.Equals(root.name, childName, System.StringComparison.Ordinal))
                return root.GetComponent<T>();

            for (int i = 0; i < root.childCount; i++)
            {
                T match = FindChildComponentByName<T>(root.GetChild(i), childName);
                if (match != null)
                    return match;
            }

            return null;
        }

        private static T FindComponentInChildrenIncludingInactive<T>(Transform root) where T : Component
        {
            if (root == null)
                return null;

            if (root.TryGetComponent(out T candidate))
                return candidate;

            for (int i = 0; i < root.childCount; i++)
            {
                T match = FindComponentInChildrenIncludingInactive<T>(root.GetChild(i));
                if (match != null)
                    return match;
            }

            return null;
        }
    }
}
