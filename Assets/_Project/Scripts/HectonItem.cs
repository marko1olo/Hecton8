// ============================================================================
// HECTON-8 — HectonItem.cs
// Подбираемый предмет в мире. Реализует IInteractable.
// Использует Data-Driven подход: вся информация — в ItemData.
//
// ИЗМЕНЕНИЕ v2:
//   Добавлен public метод SetItemData(ItemData, int) для программной
//   инициализации при спавне из BaseModule.Deconstruct().
//   Позволяет переиспользовать один worldItemPrefab для любых ресурсов.
//
// ИЗМЕНЕНИЕ v3.1 (POOL-SAFE SETTLE):
//   Убран async Awaitable SettleAndSleepAsync — destroyCancellationToken
//   НЕ срабатывает при SetActive(false) (пулинг). Заменён на ITickable
//   с конечным автоматом и таймером. Полностью Zero GC.
//   Сброс состояния в OnDisable() гарантирует корректность при пулинге.
// ============================================================================

using Hecton8.Core;
using Hecton8.Inventory;
using Hecton8.Interaction;
using Hecton8.Modding;
using Hecton8.Physics;
using Hecton8.SaveSystem;
using Hecton8.World;
using Hecton.Localization;

namespace Hecton8.Items
{
    using UnityEngine;

    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(InteractionHighlighter))]
    [DisallowMultipleComponent]
    public class HectonItem : MonoBehaviour, IInteractable, ITickable, IUpdatable, IInventoryPickupSource, IInteractionVulnerabilitySource, IPhysicsImpactMaterialProvider
    {
        private const float OverflowScatterImpulse = 2.5f;
        private const float OverflowScatterLiftImpulse = 1.2f;
        private const float OverflowScatterTorqueImpulse = 0.35f;
        // ─────────────────────── Data ────────────────────────────
        [Header("Item Configuration")]
        [SerializeField] private ItemData itemData;
        [SerializeField] private int      quantity = 1;

        // ─────────────────────── Settle Config ───────────────────
        // Время ожидания перед первой попыткой усыпить Rigidbody (сек).
        private const float SettleDelay       = 2.0f;
        // Время ожидания перед повторной попыткой (сек).
        private const float SettleRetryDelay  = 1.0f;
        // Порог скорости для засыпания (sqrMagnitude).
        private const float SleepVelocitySqr  = 0.01f;

        // ─────────────────────── Settle State ────────────────────
        /// <summary>
        /// Фазы конечного автомата засыпания Rigidbody.
        /// Idle     — не тикаемся, ждать нечего.
        /// Waiting  — первичное ожидание (SettleDelay).
        /// Retrying — повторное ожидание (SettleRetryDelay).
        /// Done     — Rigidbody усыплён или отказ, тикание остановлено.
        /// </summary>
        private enum SettlePhase : byte
        {
            Idle,
            Waiting,
            Retrying,
            Done
        }

        private SettlePhase _settlePhase;
        private float       _settleTimer;
        private bool        _isTickRegistered;

        // ─────────────────────── Cached ──────────────────────────
        private InteractionHighlighter _highlighter;
        private Rigidbody _rb;
        private BuoyancyObject _buoyancy;
        private Collider _collider;
        private PhysicMaterial _defaultColliderMaterial;
        private string _cachedInteractText = "???";
        private int _cachedItemHashId;
        private PersistentWorldRegistry _persistentWorldRegistry;
        private int _persistentWorldRecordIndex = -1;

        // ═════════════════════════════════════════════════════════
        private void Awake()
        {
            _highlighter = GetComponent<InteractionHighlighter>();
            _rb = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
            _buoyancy = GetComponent<BuoyancyObject>();
            _defaultColliderMaterial = _collider != null ? _collider.sharedMaterial : null;
            ApplyPhysicalMetadata();
            ConfigureWaterDynamicsFromData();
            RefreshCachedItemHash();

            if (itemData == null)
                Debug.LogError($"[HectonItem] ItemData не назначен на {gameObject.name}!", this);
        }

        // ─────────────────────── Pool-Safe Settle (v3.1) ─────────
        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            RebuildInteractTextCache();

            if (_rb != null)
            {
                _rb.WakeUp();
                BeginSettle();
            }
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;

            // Гарантированная отписка при деактивации (пулинг).
            // Сбрасываем фазу — при следующем OnEnable начнём заново.
            StopSettle();
            ClearPersistentWorldRecord();
        }

        // ─────────────────────── ITickable ───────────────────────
        public void Tick(float deltaTime)
        {
            switch (_settlePhase)
            {
                case SettlePhase.Waiting:
                    _settleTimer -= deltaTime;
                    if (_settleTimer <= 0f)
                    {
                        if (TrySleepRigidbody())
                        {
                            FinishSettle();
                        }
                        else
                        {
                            // Ещё движется — одна повторная попытка
                            _settlePhase = SettlePhase.Retrying;
                            _settleTimer = SettleRetryDelay;
                        }
                    }
                    break;

                case SettlePhase.Retrying:
                    _settleTimer -= deltaTime;
                    if (_settleTimer <= 0f)
                    {
                        TrySleepRigidbody(); // Пытаемся, результат неважен
                        FinishSettle();
                    }
                    break;

                default:
                    // Idle или Done — не должны тикаться, но на всякий случай
                    StopSettle();
                    break;
            }
        }

        /// <summary>
        /// Пытается усыпить Rigidbody если скорость достаточно мала.
        /// Возвращает true если усыпил или rb == null.
        /// </summary>
        private bool TrySleepRigidbody()
        {
            if (_rb == null) return true;

            if (_rb.linearVelocity.sqrMagnitude < SleepVelocitySqr)
            {
                _rb.Sleep();
                return true;
            }

            return false;
        }

        private void BeginSettle()
        {
            _settlePhase = SettlePhase.Waiting;
            _settleTimer = SettleDelay;
            StartTicking();
        }

        private void FinishSettle()
        {
            _settlePhase = SettlePhase.Done;
            _settleTimer = 0f;
            StopTicking();
        }

        private void StopSettle()
        {
            _settlePhase = SettlePhase.Idle;
            _settleTimer = 0f;
            StopTicking();
        }

        private void StartTicking()
        {
            if (_isTickRegistered) return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null) return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _isTickRegistered = true;
        }

        private void StopTicking()
        {
            if (!_isTickRegistered) return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _isTickRegistered = false;
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
            RefreshCachedItemHash();
            ApplyPhysicalMetadata();
            ConfigureWaterDynamicsFromData();
            RebuildInteractTextCache();
        }

        public bool SetItemByHash(ItemCatalog catalog, int itemHashId, int qty)
        {
            if (catalog == null || itemHashId == 0)
                return false;

            ItemData resolvedItem = catalog.FindByHash(itemHashId);
            if (resolvedItem == null)
                return false;

            SetItemData(resolvedItem, qty);
            return true;
        }

        /// <summary>Текущие данные предмета (read-only).</summary>
        public ItemData Data => itemData;

        /// <summary>Текущее количество (read-only).</summary>
        public int Quantity => quantity;
        public int ItemHashId => _cachedItemHashId;
        public uint VulnerabilityMask => itemData != null ? itemData.VulnerabilityMask : 0u;
        public byte ImpactAudioMaterialId => itemData != null ? itemData.AudioMaterialByte : (byte)ItemAudioMaterialId.Organic;

        internal void BindPersistentWorldRecord(PersistentWorldRegistry registry, int recordIndex)
        {
            _persistentWorldRegistry = registry;
            _persistentWorldRecordIndex = recordIndex;
        }

        internal void ClearPersistentWorldRecord()
        {
            _persistentWorldRegistry = null;
            _persistentWorldRecordIndex = -1;
        }

        private void ConfigureWaterDynamicsFromData()
        {
            if (itemData == null || itemData.worldBuoyancyProfile == null || _rb == null)
                return;

            if (_buoyancy == null)
                _buoyancy = GetComponent<BuoyancyObject>() ?? gameObject.AddComponent<BuoyancyObject>();

            _buoyancy.SetProfile(itemData.worldBuoyancyProfile);
        }

        private void ApplyPhysicalMetadata()
        {
            if (_rb != null && itemData != null)
                _rb.mass = itemData.MassKg;

            if (_collider == null)
                return;

            _collider.sharedMaterial = itemData != null && itemData.WorldPhysicMaterial != null
                ? itemData.WorldPhysicMaterial
                : _defaultColliderMaterial;
        }

        private void RefreshCachedItemHash()
        {
            _cachedItemHashId = itemData != null
                ? LocHash.Compute(itemData.PersistentId)
                : 0;
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
            TryHandleInventoryPickup(PlayerInventory.Instance, interactor);
        }

        public bool TryHandleInventoryPickup(PlayerInventory inventory, Transform interactor)
        {
            if (itemData == null || quantity <= 0 || _cachedItemHashId == 0)
                return false;

            if (inventory == null)
            {
                DropOverflow(interactor);
                return true;
            }

            PlayerInventory.ScavengeAttemptResult attempt = inventory.ScavengeAttempt(_cachedItemHashId, quantity, interactor);
            if (!attempt.AnyAdded)
            {
                DropOverflow(interactor);
                return true;
            }

            InteractionEvents.RaiseItemCollected(itemData, attempt.AddedQuantity, interactor);
            HectonEventBus.Publish(new ItemCollectedEvent(itemData, _cachedItemHashId, attempt.AddedQuantity, interactor));

            quantity = attempt.RejectedQuantity;
            if (quantity > 0)
            {
                RebuildInteractTextCache();
                DropOverflow(interactor);
                return true;
            }

            _persistentWorldRegistry?.MarkRecordCollected(_persistentWorldRecordIndex);
            ConsumeWorldProxy();
            return true;
        }

        public string GetInteractText()
        {
            return _cachedInteractText;
        }

        private void RebuildInteractTextCache()
        {
            if (itemData == null)
            {
                _cachedInteractText = "???";
                return;
            }

            string baseText = itemData.GetInteractText();
            if (quantity > 1)
            {
                _cachedInteractText = baseText + " x" + quantity;
                return;
            }

            _cachedInteractText = baseText;
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildInteractTextCache();
        }

        private void ConsumeWorldProxy()
        {
            if (_highlighter != null)
                _highlighter.SetHighlight(false);

            if (ObjectPoolManager.Instance != null && TryGetComponent(out ObjectPoolManager.PoolItemMarker _))
            {
                ObjectPoolManager.Instance.Despawn(gameObject);
                return;
            }

            gameObject.SetActive(false);
        }

        private void DropOverflow(Transform interactor)
        {
            if (_rb == null || _rb.isKinematic)
                return;

            Vector3 scatterDirection = ResolveScatterDirection(interactor);
            Vector3 impulse = scatterDirection * OverflowScatterImpulse;
            impulse.y += OverflowScatterLiftImpulse;

            if (!IsFiniteVector(impulse))
                return;

            _rb.WakeUp();
            PhysicsForceRouter.QueueForce(_rb, impulse, ForceMode.Impulse);

            Vector3 torqueAxis = Vector3.Cross(Vector3.up, scatterDirection);
            if (torqueAxis.sqrMagnitude <= 0.0001f)
                torqueAxis = Vector3.right;

            Vector3 torque = torqueAxis.normalized * OverflowScatterTorqueImpulse;
            if (IsFiniteVector(torque))
                PhysicsForceRouter.QueueTorque(_rb, torque, ForceMode.Impulse);
        }

        private Vector3 ResolveScatterDirection(Transform interactor)
        {
            if (interactor != null)
            {
                Vector3 scatterDirection = transform.position - interactor.position;
                scatterDirection.y = 0f;
                if (scatterDirection.sqrMagnitude > 0.0001f)
                    return scatterDirection.normalized;

                Vector3 fallbackForward = -interactor.forward;
                fallbackForward.y = 0f;
                if (fallbackForward.sqrMagnitude > 0.0001f)
                    return fallbackForward.normalized;
            }

            return Vector3.forward;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
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
                RebuildInteractTextCache();
            }
        }
#endif
    }
}
