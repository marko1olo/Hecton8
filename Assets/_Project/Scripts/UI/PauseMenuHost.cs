using UnityEngine;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Pause Menu Host")]
    public sealed class PauseMenuHost : MonoBehaviour
    {
        [SerializeField] private string rootName = "PauseMenu_Root";
        private void Awake()
        {
            RectTransform parent = transform as RectTransform;
            if (parent == null)
                return;

            Transform existing = parent.Find(rootName);
            RectTransform root = existing as RectTransform;
            if (root == null)
            {
                // Player-build construction path: no authored/bootstrap instance reachable.
                // Must construct in player builds when bootstrap reorders or skips registration.
                GameObject go = new GameObject(rootName, typeof(RectTransform));
                go.layer = gameObject.layer;
                go.TryGetComponent(out root);
                root.SetParent(parent, false);
                root.localScale = Vector3.one;
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.offsetMin = Vector2.zero;
                root.offsetMax = Vector2.zero;
            }

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Must construct in player builds when bootstrap reorders or skips registration.
            root.TryGetComponent(out PauseMenuController controller);
            if (controller == null)
                controller = root.gameObject.AddComponent<PauseMenuController>();
        }
    }
}
