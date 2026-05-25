using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.UI;

namespace Hecton.UI.MainMenu
{
    /// <summary>
    /// Universal modal popup with confirm/cancel actions.
    /// Scene-owned registry service, static access via Show().
    /// Button labels auto-update on language change.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModalWindow : MonoBehaviour, ILocalizationLanguageChangedListener, Hecton8.Core.IModalWindowService, IGlobalRegistryHotSwapListener
    {
        // ──────────────────────────────────────────────
        // SINGLETON
        // ──────────────────────────────────────────────
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
        private bool _runtimeBindingsReady;
        private ILocalizationTextReadModel _localization;
        private bool _hotSwapRegistered;

        // ══════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════

        private void Awake()
        {
            if (!TryClaimInstance())
                return;

            EnsureRuntimeBindings(hideAfterBinding: true);
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
            if (!TryClaimInstance())
                return;

            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            EnsureRuntimeBindings(hideAfterBinding: !_runtimeBindingsReady);
            LocalizationEvents.RegisterLanguageListener(this);
            RefreshButtonLabels();
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            TryUnregisterHotSwapListener();
            ReleaseServiceIfOwner();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            if (btnConfirm != null)
                btnConfirm.onClick.RemoveListener(_confirmClickAction);

            if (btnCancel != null)
                btnCancel.onClick.RemoveListener(_cancelClickAction);

            ReleaseServiceIfOwner();
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
            ILocalizationTextReadModel loc = _localization;
            if (loc == null) return;

            if (confirmButtonLabel != null)
                TmpTextNoAlloc.Set(confirmButtonLabel, ResolveLocalizedSpan(loc, LocalizationKeys.MODAL_CONFIRM, "CONFIRM"));

            if (cancelButtonLabel != null)
                TmpTextNoAlloc.Set(cancelButtonLabel, ResolveLocalizedSpan(loc, LocalizationKeys.MODAL_CANCEL, "CANCEL"));
        }

        private static ReadOnlySpan<char> ResolveLocalizedSpan(ILocalizationTextReadModel manager, string key, ReadOnlySpan<char> fallback)
        {
            return manager != null
                ? manager.GetRawSpanOrFallback(LocHash.Compute(key.AsSpan()), fallback)
                : fallback;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.LocalizationRuntime)
                return;

            _localization = currentService as ILocalizationTextReadModel;
            RefreshButtonLabels();
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
            if (!TryResolveService(out Hecton8.Core.IModalWindowService service))
                return;

            service.ShowModal(title, message, onConfirm, onCancel, null, null);
        }

        public static void Show(
            string title,
            char[] messageBuffer,
            int messageLength,
            Action onConfirm,
            Action onCancel = null)
        {
            if (!TryResolveService(out Hecton8.Core.IModalWindowService service))
                return;

            service.ShowModal(title, messageBuffer, messageLength, onConfirm, onCancel, null, null);
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
            if (!TryResolveService(out Hecton8.Core.IModalWindowService service))
                return;

            service.ShowModal(title, message, onConfirm, onCancel, confirmLabel, cancelLabel);
        }

        public static void ShowWithCustomLabels(
            string title,
            char[] messageBuffer,
            int messageLength,
            Action onConfirm,
            Action onCancel,
            string confirmLabel,
            string cancelLabel)
        {
            if (!TryResolveService(out Hecton8.Core.IModalWindowService service))
                return;

            service.ShowModal(title, messageBuffer, messageLength, onConfirm, onCancel, confirmLabel, cancelLabel);
        }

        /// <summary>
        /// Statically closes the modal window.
        /// </summary>
        public static void Close()
        {
            Hecton8.Core.IModalWindowService service = Hecton8.Core.GlobalRegistry.ModalWindow;
            if (service != null)
                service.CloseModal();
        }

        // ══════════════════════════════════════════════
        // INTERNAL
        // ══════════════════════════════════════════════

        private static bool TryResolveService(out Hecton8.Core.IModalWindowService service)
        {
            service = Hecton8.Core.GlobalRegistry.ModalWindow;
            if (service != null)
                return true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[ModalWindow] No ModalWindow instance found in scene. Add ModalWindow component to your Canvas.");
#endif
            return false;
        }

        private bool TryClaimInstance()
        {
            Hecton8.Core.IModalWindowService existing = Hecton8.Core.GlobalRegistry.ModalWindow;
            if (existing != null && !ReferenceEquals(existing, this))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[ModalWindow] Duplicate detected. Destroying extra instance.");
#endif
                Destroy(gameObject);
                return false;
            }

            Hecton8.Core.GlobalRegistry.RegisterModalWindowService(this);
            return true;
        }

        private void CacheRegistryServicesCold()
        {
            _localization = Hecton8.Core.GlobalRegistry.LocalizationText;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = Hecton8.Core.GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            Hecton8.Core.GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void ReleaseServiceIfOwner()
        {
            Hecton8.Core.IModalWindowService existing = Hecton8.Core.GlobalRegistry.ModalWindow;
            if (ReferenceEquals(existing, this))
                Hecton8.Core.GlobalRegistry.UnregisterModalWindowService(this);
        }

        void Hecton8.Core.IModalWindowService.ShowModal(
            string title,
            string message,
            Action onConfirm,
            Action onCancel,
            string confirmLabel,
            string cancelLabel)
        {
            EnsureRuntimeBindings(hideAfterBinding: false);
            ShowInternal(title, message, onConfirm, onCancel, confirmLabel, cancelLabel);
        }

        void Hecton8.Core.IModalWindowService.ShowModal(
            string title,
            char[] messageBuffer,
            int messageLength,
            Action onConfirm,
            Action onCancel,
            string confirmLabel,
            string cancelLabel)
        {
            EnsureRuntimeBindings(hideAfterBinding: false);
            ShowInternal(title, messageBuffer, messageLength, onConfirm, onCancel, confirmLabel, cancelLabel);
        }

        void Hecton8.Core.IModalWindowService.CloseModal()
        {
            Hide();
        }

        private void EnsureRuntimeBindings(bool hideAfterBinding)
        {
            AutoWireSceneReferences();
            EnsureModalRootActive();

            if (_confirmClickAction == null)
                _confirmClickAction = OnConfirmClicked; // COLD ALLOC: UnityAction[1] - cached modal confirm listener - owner: ModalWindow
            if (_cancelClickAction == null)
                _cancelClickAction = OnCancelClicked; // COLD ALLOC: UnityAction[1] - cached modal cancel listener - owner: ModalWindow

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

            _runtimeBindingsReady = true;

            if (hideAfterBinding)
                Hide();
        }

        private void EnsureModalRootActive()
        {
            if (modalGroup == null)
                return;

            GameObject modalRoot = modalGroup.gameObject;
            if (!modalRoot.activeSelf)
                modalRoot.SetActive(true);
        }

        private void ShowInternal(
            string title,
            string message,
            Action onConfirm,
            Action onCancel,
            string customConfirmLabel,
            string customCancelLabel)
        {
            if (titleText   != null) TmpTextNoAlloc.Set(titleText, title);
            if (messageText != null) TmpTextNoAlloc.Set(messageText, message);

            _cachedOnConfirm = onConfirm;
            _cachedOnCancel  = onCancel;

            RefreshButtonLabels();

            if (!string.IsNullOrEmpty(customConfirmLabel) && confirmButtonLabel != null)
                TmpTextNoAlloc.Set(confirmButtonLabel, customConfirmLabel);

            if (!string.IsNullOrEmpty(customCancelLabel) && cancelButtonLabel != null)
                TmpTextNoAlloc.Set(cancelButtonLabel, customCancelLabel);

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

        private void ShowInternal(
            string title,
            char[] messageBuffer,
            int messageLength,
            Action onConfirm,
            Action onCancel,
            string customConfirmLabel,
            string customCancelLabel)
        {
            if (titleText != null) TmpTextNoAlloc.Set(titleText, title);
            if (messageText != null)
            {
                int safeLength = messageBuffer == null ? 0 : Mathf.Clamp(messageLength, 0, messageBuffer.Length);
                if (safeLength > 0)
                    messageText.SetCharArray(messageBuffer, 0, safeLength);
                else
                    messageText.SetCharArray(Array.Empty<char>(), 0, 0);
            }

            _cachedOnConfirm = onConfirm;
            _cachedOnCancel  = onCancel;

            RefreshButtonLabels();

            if (!string.IsNullOrEmpty(customConfirmLabel) && confirmButtonLabel != null)
                TmpTextNoAlloc.Set(confirmButtonLabel, customConfirmLabel);

            if (!string.IsNullOrEmpty(customCancelLabel) && cancelButtonLabel != null)
                TmpTextNoAlloc.Set(cancelButtonLabel, customCancelLabel);

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
