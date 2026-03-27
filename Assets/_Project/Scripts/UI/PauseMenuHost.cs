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
            RectTransform root;
            if (existing != null)
            {
                root = existing as RectTransform;
            }
            else
            {
                GameObject go = new GameObject(rootName, typeof(RectTransform));
                go.layer = gameObject.layer;
                root = go.GetComponent<RectTransform>();
                root.SetParent(parent, false);
                root.localScale = Vector3.one;
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.offsetMin = Vector2.zero;
                root.offsetMax = Vector2.zero;
            }

            PauseMenuController controller = root.GetComponent<PauseMenuController>();
            if (controller == null)
                controller = root.gameObject.AddComponent<PauseMenuController>();

        }
    }
}
