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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                GameObject go = new GameObject(rootName, typeof(RectTransform));
                go.layer = gameObject.layer;
                go.TryGetComponent(out root);
                root.SetParent(parent, false);
                root.localScale = Vector3.one;
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.offsetMin = Vector2.zero;
                root.offsetMax = Vector2.zero;
#else
                return;
#endif
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            root.TryGetComponent(out PauseMenuController controller);
            if (controller == null)
                controller = root.gameObject.AddComponent<PauseMenuController>();
#else
            if (!root.TryGetComponent(out PauseMenuController _))
                return;
#endif
        }
    }
}
