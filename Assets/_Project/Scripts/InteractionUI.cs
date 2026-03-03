namespace Hecton.Interaction
{
    using UnityEngine;
    using TMPro;

    /// <summary>
    /// Минимальный UI: показывает "[E] Забрать титан" по центру-снизу экрана.
    /// Подписывается на события <see cref="PlayerInteraction"/>.
    /// </summary>
    public class InteractionUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInteraction playerInteraction;
        [SerializeField] private GameObject        promptRoot;
        [SerializeField] private TextMeshProUGUI   promptLabel;

        [Header("Format")]
        [SerializeField] private string promptFormat = "<b>[E]</b>  {0}";

        // ═════════════════════════════════════════════════════════
        private void Awake()
        {
            if (promptRoot != null)
                promptRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (playerInteraction == null)
            {
                Debug.LogError("[InteractionUI] PlayerInteraction не назначен!", this);
                return;
            }
            playerInteraction.OnTargetFound += HandleTargetFound;
            playerInteraction.OnTargetLost  += HandleTargetLost;
        }

        private void OnDisable()
        {
            if (playerInteraction == null) return;
            playerInteraction.OnTargetFound -= HandleTargetFound;
            playerInteraction.OnTargetLost  -= HandleTargetLost;
        }

        // ─────────────────────── Handlers ────────────────────────
        private void HandleTargetFound(string interactText)
        {
            if (promptLabel != null)
                promptLabel.text = string.Format(promptFormat, interactText);

            if (promptRoot != null)
                promptRoot.SetActive(true);
        }

        private void HandleTargetLost()
        {
            if (promptRoot != null)
                promptRoot.SetActive(false);
        }
    }
}