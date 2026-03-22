using System;
using UnityEngine;
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
    public sealed class ModalWindow : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // SINGLETON
        // ──────────────────────────────────────────────
        private static ModalWindow _instance;

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

            if (btnConfirm != null)
            {
                btnConfirm.onClick.RemoveAllListeners();
                btnConfirm.onClick.AddListener(OnConfirmClicked);
            }

            if (btnCancel != null)
            {
                btnCancel.onClick.RemoveAllListeners();
                btnCancel.onClick.AddListener(OnCancelClicked);
            }

            Hide();
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
            RefreshButtonLabels();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        // ══════════════════════════════════════════════
        // LOCALIZATION
        // ══════════════════════════════════════════════

        private void OnLanguageChanged(GameLanguage newLanguage)
        {
            RefreshButtonLabels();
        }

        private void RefreshButtonLabels()
        {
            LocalizationManager loc = LocalizationManager.Instance;
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

            _instance.ShowInternal(title, message, onConfirm, onCancel);
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
            Action onCancel)
        {
            if (titleText   != null) titleText.SetText(title);
            if (messageText != null) messageText.SetText(message);

            _cachedOnConfirm = onConfirm;
            _cachedOnCancel  = onCancel;

            // Refresh button labels in case language changed since last show
            RefreshButtonLabels();

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