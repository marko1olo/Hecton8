// ============================================================================
// HECTON-8 — HectonItem.cs
// Подбираемый предмет в мире. Реализует IInteractable.
// Использует Data-Driven подход: вся информация — в ItemData.
//
// ИЗМЕНЕНИЕ v2:
//   Добавлен public метод SetItemData(ItemData, int) для программной
//   инициализации при спавне из BaseModule.Deconstruct().
//   Позволяет переиспользовать один worldItemPrefab для любых ресурсов.
// ============================================================================

using Hecton8.Core;
using Hecton8.Interaction;
using Hecton8.Physics;

namespace Hecton8.Items
{
    using UnityEngine;

    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(InteractionHighlighter))]
    [DisallowMultipleComponent]
    public class HectonItem : MonoBehaviour, IInteractable
    {
        // ─────────────────────── Data ────────────────────────────
        [Header("Item Configuration")]
        [SerializeField] private ItemData itemData;
        [SerializeField] private int      quantity = 1;

        // ─────────────────────── Cached ──────────────────────────
        private InteractionHighlighter _highlighter;
        private Rigidbody _rb;
        private BuoyancyObject _buoyancy;

        // ═════════════════════════════════════════════════════════
        private void Awake()
        {
            _highlighter = GetComponent<InteractionHighlighter>();
            _rb = GetComponent<Rigidbody>();
            _buoyancy = GetComponent<BuoyancyObject>();
            ConfigureWaterDynamicsFromData();

            if (itemData == null)
                Debug.LogError($"[HectonItem] ItemData не назначен на {gameObject.name}!", this);
        }
                // ─────────────────────── Physics Sleep (v3.0) ────────────
        // When loot is spawned (scattered from ResourceNode), it has
        // Rigidbody with impulse force. After settling on cave floor,
        // we force-sleep the Rigidbody to prevent perpetual micro-physics
        // updates on uneven voxel mesh surfaces.
        //
        // Uses Unity 6 Awaitable — pooled, zero GC, auto-cancelled
        // via destroyCancellationToken when object is despawned/destroyed.

        private void OnEnable()
        {
            if (_rb != null)
            {
                // Wake up in case it was sleeping from previous pool cycle
                _rb.WakeUp();

                // Fire and forget — auto-cancelled if despawned before 2s
                _ = SettleAndSleepAsync();
            }
        }

        /// <summary>
        /// Waits 2 seconds, then force-sleeps Rigidbody if velocity is near zero.
        /// If the object is still moving (player kicked it, water current), retries once.
        ///
        /// Awaitable is pooled by Unity 6 runtime — zero heap allocation.
        /// destroyCancellationToken auto-cancels if GameObject is destroyed/despawned.
        /// </summary>
        private async Awaitable SettleAndSleepAsync()
        {
            try
            {
                // Wait for initial scatter impulse to settle
                await Awaitable.WaitForSecondsAsync(2f, destroyCancellationToken);

                if (_rb == null) return;

                if (_rb.linearVelocity.sqrMagnitude < 0.01f)
                {
                    _rb.Sleep();
                    return;
                }

                // Still moving — wait one more second and try again
                await Awaitable.WaitForSecondsAsync(1f, destroyCancellationToken);

                if (_rb != null && _rb.linearVelocity.sqrMagnitude < 0.01f)
                {
                    _rb.Sleep();
                }
            }
            catch (System.OperationCanceledException)
            {
                // Object was despawned/destroyed before settling — normal, ignore
            }
        }
        // ─────────────────────── Public API ──────────────────────

        /// <summary>
        /// Программная инициализация данных предмета.
        /// Вызывается при спавне из BaseModule.Deconstruct()
        /// для установки конкретного ресурса на generic worldItemPrefab.
        ///
        /// Безопасно вызывать повторно (перезаписывает данные).
        /// </summary>
        /// <param name="data">Данные предмета (ItemData ScriptableObject).</param>
        /// <param name="qty">Количество единиц.</param>
        public void SetItemData(ItemData data, int qty)
        {
            itemData = data;
            quantity = qty > 0 ? qty : 1;
            ConfigureWaterDynamicsFromData();
        }

        /// <summary>Текущие данные предмета (read-only).</summary>
        public ItemData Data => itemData;

        /// <summary>Текущее количество (read-only).</summary>
        public int Quantity => quantity;

        private void ConfigureWaterDynamicsFromData()
        {
            if (itemData == null || itemData.worldBuoyancyProfile == null || _rb == null)
                return;

            if (_buoyancy == null)
                _buoyancy = GetComponent<BuoyancyObject>() ?? gameObject.AddComponent<BuoyancyObject>();

            _buoyancy.SetProfile(itemData.worldBuoyancyProfile);
        }

        // ─────────────────────── IInteractable ───────────────────
        public void OnHoverStart()
        {
            _highlighter.SetHighlight(true);
        }

        public void OnHoverEnd()
        {
            _highlighter.SetHighlight(false);
        }

        public void Interact(Transform interactor)
        {
            if (itemData == null) return;

            InteractionEvents.RaiseItemCollected(itemData, quantity, interactor);

            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Despawn(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public string GetInteractText()
        {
            if (itemData == null) return "???";

            string qtyStr = quantity > 1 ? $" ×{quantity}" : "";
            return $"{itemData.interactVerb} {itemData.itemName}{qtyStr}";
        }

        // ─────────────────────── Editor ──────────────────────────
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (quantity < 1) quantity = 1;

            if (itemData != null && !Application.isPlaying)
                gameObject.name = $"Item_{itemData.itemName}";

            if (!Application.isPlaying)
            {
                _rb = GetComponent<Rigidbody>();
                _buoyancy = GetComponent<BuoyancyObject>();
                ConfigureWaterDynamicsFromData();
            }
        }
#endif
    }
}
