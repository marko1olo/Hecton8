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
        public static T FindSceneObjectIncludingInactive<T>() where T : Component
        {
            T[] all = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < all.Length; i++)
            {
                T candidate = all[i];
                if (candidate == null)
                    continue;

                GameObject go = candidate.gameObject;
                if (go == null)
                    continue;

                Scene scene = go.scene;
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                return candidate;
            }

            return null;
        }

        public static PauseMenuController ResolvePauseMenu()
        {
            PauseMenuController pauseMenu = FindSceneObjectIncludingInactive<PauseMenuController>();
            if (pauseMenu != null)
                return pauseMenu;

            PauseMenuHost host = FindSceneObjectIncludingInactive<PauseMenuHost>();
            if (host != null)
                return host.GetComponentInChildren<PauseMenuController>(true);

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
    }
}
