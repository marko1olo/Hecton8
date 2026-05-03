using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using Hecton.Localization;

namespace Hecton.UI.MainMenu
{
    /// <summary>
    /// Universal modal popup with confirm/cancel actions.
    /// Singleton within scene, static access via Show().
    /// Button labels auto-update on language change.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModalWindow : MonoBehaviour, ILocalizationLanguageChangedListener
    {
        // ──────────────────────────────────────────────
        // SINGLETON
        // ──────────────────────────────────────────────
        private static ModalWindow _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        // ──────────────────────────────────────────────
        // INSPECTOR
        // ──────────────────────────────────────────────
        [Header("=== UI REFERENCES ===")]
        [SerializeField] private CanvasGroup modalGroup;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;

        [Header("=== BUTTONS ===")]
        [SerializeField] private Button btnConfirm;
        [SerializeField] private Button btnCancel;

        [Header("=== BUTTON LABELS ===")]
        [SerializeField] private TMP_Text confirmButtonLabel;
        [SerializeField] private TMP_Text cancelButtonLabel;

        // Cached delegates
        private Action _cachedOnConfirm;
        private Action _cachedOnCancel;
        private UnityAction _confirmClickAction;
        private UnityAction _cancelClickAction;

        // ══════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[ModalWindow] Duplicate detected. Destroying extra instance.");
#endif
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _confirmClickAction = OnConfirmClicked; // COLD ALLOC: UnityAction[1] - cached modal confirm listener - owner: ModalWindow
            _cancelClickAction = OnCancelClicked; // COLD ALLOC: UnityAction[1] - cached modal cancel listener - owner: ModalWindow
            AutoWireSceneReferences();

            if (btnConfirm != null)
            {
                btnConfirm.onClick.RemoveAllListeners();
                btnConfirm.onClick.AddListener(_confirmClickAction);
            }

            if (btnCancel != null)
            {
                btnCancel.onClick.RemoveAllListeners();
                btnCancel.onClick.AddListener(_cancelClickAction);
            }

            Hide();
        }

        private void AutoWireSceneReferences()
        {
            Transform root = transform;

            modalGroup = ResolveCanvasGroup(modalGroup, root, "Panel_ModalConfirm");
            titleText = ResolveText(titleText, root, "TXT_Title");
            messageText = ResolveText(messageText, root, "TXT_Message");
            btnConfirm = ResolveButton(btnConfirm, root, "yes");
            btnCancel = ResolveButton(btnCancel, root, "no");
            confirmButtonLabel = ResolveButtonLabel(confirmButtonLabel, btnConfirm);
            cancelButtonLabel = ResolveButtonLabel(cancelButtonLabel, btnCancel);
        }

        private static CanvasGroup ResolveCanvasGroup(CanvasGroup current, Transform root, string objectName)
        {
            if (current != null)
                return current;

            Transform target = FindDeepChild(root, objectName);
            if (target == null)
                return null;

            if (target.TryGetComponent(out CanvasGroup group))
                return group;

            return target.gameObject.AddComponent<CanvasGroup>();
        }

        private static TMP_Text ResolveText(TMP_Text current, Transform root, string objectName)
        {
            if (current != null)
                return current;

            Transform target = FindDeepChild(root, objectName);
            if (target == null)
                return null;

            target.TryGetComponent(out TMP_Text text);
            return text;
        }

        private static Button ResolveButton(Button current, Transform root, string objectName)
        {
            if (current != null)
                return current;

            Transform target = FindDeepChild(root, objectName);
            if (target == null)
                return null;

            target.TryGetComponent(out Button button);
            return button;
        }

        private static TMP_Text ResolveButtonLabel(TMP_Text current, Button button)
        {
            if (current != null)
                return current;

            if (button == null)
                return null;

            return Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<TMP_Text>(button.transform);
        }

        private static Transform FindDeepChild(Transform parent, string childName)
        {
            if (parent == null)
                return null;

            if (parent.name == childName)
                return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindDeepChild(parent.GetChild(i), childName);
                if (result != null)
                    return result;
            }

            return null;
        }

        private void OnEnable()
        {
            LocalizationEvents.RegisterLanguageListener(this);
            RefreshButtonLabels();
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
        }

        private void OnDestroy()
        {
            if (btnConfirm != null)
                btnConfirm.onClick.RemoveListener(_confirmClickAction);

            if (btnCancel != null)
                btnCancel.onClick.RemoveListener(_cancelClickAction);

            if (_instance == this)
            {
                _instance = null;
            }
        }

        // ══════════════════════════════════════════════
        // LOCALIZATION
        // ══════════════════════════════════════════════

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            OnLanguageChanged((GameLanguage)payload.Language);

        }


        private void OnLanguageChanged(GameLanguage newLanguage)
        {
            RefreshButtonLabels();
        }

        private void RefreshButtonLabels()
        {
            LocalizationManager loc = Hecton8.Core.GlobalRegistry.Localization;
            if (loc == null) return;

            if (confirmButtonLabel != null)
                confirmButtonLabel.SetText(loc.Get(LocalizationKeys.MODAL_CONFIRM));

            if (cancelButtonLabel != null)
                cancelButtonLabel.SetText(loc.Get(LocalizationKeys.MODAL_CANCEL));
        }

        // ══════════════════════════════════════════════
        // STATIC API
        // ══════════════════════════════════════════════

        /// <summary>
        /// Shows the modal window with localized button labels.
        /// Title and message should already be localized by the caller.
        /// </summary>
        /// <param name="title">Window title (pre-localized).</param>
        /// <param name="message">Message body (pre-localized).</param>
        /// <param name="onConfirm">Callback on confirm button.</param>
        /// <param name="onCancel">
        /// Callback on cancel button. If null, cancel simply closes the window.
        /// </param>
        public static void Show(
            string title,
            string message,
            Action onConfirm,
            Action onCancel = null)
        {
            if (_instance == null)
            {
#if UNITY_EDITOR
                Debug.LogError(
                    "[ModalWindow] No ModalWindow instance found in scene! " +
                    "Add ModalWindow component to your Canvas."
                );
#endif
                return;
            }

            _instance.ShowInternal(title, message, onConfirm, onCancel, null, null);
        }

        /// <summary>
        /// Shows the modal window with custom button labels.
        /// Title, message, and button labels should already be localized by the caller.
        /// </summary>
        /// <param name="title">Window title (pre-localized).</param>
        /// <param name="message">Message body (pre-localized).</param>
        /// <param name="onConfirm">Callback on confirm button.</param>
        /// <param name="onCancel">Callback on cancel button. If null, cancel simply closes the window.</param>
        /// <param name="confirmLabel">Custom confirm button label (e.g., "Retry"). If null, uses default localized label.</param>
        /// <param name="cancelLabel">Custom cancel button label (e.g., "Return to Menu"). If null, uses default localized label.</param>
        public static void ShowWithCustomLabels(
            string title,
            string message,
            Action onConfirm,
            Action onCancel,
            string confirmLabel,
            string cancelLabel)
        {
            if (_instance == null)
            {
#if UNITY_EDITOR
                Debug.LogError(
                    "[ModalWindow] No ModalWindow instance found in scene! " +
                    "Add ModalWindow component to your Canvas."
                );
#endif
                return;
            }

            _instance.ShowInternal(title, message, onConfirm, onCancel, confirmLabel, cancelLabel);
        }

        /// <summary>
        /// Statically closes the modal window.
        /// </summary>
        public static void Close()
        {
            if (_instance != null)
            {
                _instance.Hide();
            }
        }

        // ══════════════════════════════════════════════
        // INTERNAL
        // ══════════════════════════════════════════════

        private void ShowInternal(
            string title,
            string message,
            Action onConfirm,
            Action onCancel,
            string customConfirmLabel,
            string customCancelLabel)
        {
            if (titleText   != null) titleText.SetText(title);
            if (messageText != null) messageText.SetText(message);

            _cachedOnConfirm = onConfirm;
            _cachedOnCancel  = onCancel;

            // Use custom labels if provided, otherwise refresh with localized defaults
            if (!string.IsNullOrEmpty(customConfirmLabel) && confirmButtonLabel != null)
            {
                confirmButtonLabel.SetText(customConfirmLabel);
            }
            else if (!string.IsNullOrEmpty(customCancelLabel) && cancelButtonLabel != null)
            {
                cancelButtonLabel.SetText(customCancelLabel);
            }
            else
            {
                RefreshButtonLabels();
            }

            // Apply custom labels if provided
            if (!string.IsNullOrEmpty(customConfirmLabel) && confirmButtonLabel != null)
                confirmButtonLabel.SetText(customConfirmLabel);

            if (!string.IsNullOrEmpty(customCancelLabel) && cancelButtonLabel != null)
                cancelButtonLabel.SetText(customCancelLabel);

            if (btnCancel != null)
            {
                btnCancel.gameObject.SetActive(true);
            }

            if (modalGroup != null)
            {
                modalGroup.alpha          = 1f;
                modalGroup.interactable   = true;
                modalGroup.blocksRaycasts = true;
            }
        }

        public void Hide()
        {
            if (modalGroup != null)
            {
                modalGroup.alpha          = 0f;
                modalGroup.interactable   = false;
                modalGroup.blocksRaycasts = false;
            }

            _cachedOnConfirm = null;
            _cachedOnCancel  = null;
        }

        // ══════════════════════════════════════════════
        // BUTTON HANDLERS
        // ══════════════════════════════════════════════

        private void OnConfirmClicked()
        {
            Action callback = _cachedOnConfirm;
            Hide();
            callback?.Invoke();
        }

        private void OnCancelClicked()
        {
            Action callback = _cachedOnCancel;
            Hide();
            callback?.Invoke();
        }
    }
}
