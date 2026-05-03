using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Power;
using Hecton8.SaveSystem;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// High-draw seabed drill that periodically extracts heavy raw materials into a connected logistics sorter.
    /// Placement is validated by authored seabed rules, while runtime extraction stays on SlowTick.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PowerNode))]
    [AddComponentMenu("Hecton8/Construction/Deep Drill Module")]
    public sealed class DeepDrillModule : MonoBehaviour, ISlowTickable, IPoolable, IPowerComponent, IBuildPlacementRule
    {
        private const float SlowTickDeltaTime = 0.5f;
        private const string DefaultPlacementBlockedReason = "SEABED FOOTING REQUIRED";
        private const string DefaultSlopeBlockedReason = "SEABED TOO STEEP";
        private static readonly int _placementProbeToolId = Animator.StringToHash("deep_drill_placement_probe");

        private static readonly List<DeepDrillModule> s_ActiveModules = new List<DeepDrillModule>(8);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_ActiveModules.Clear();
        }

        [System.Serializable]
        private struct DrillYieldEntry
        {
            [Tooltip("Item extracted by this deep drill cycle.")]
            public ItemData item;

            [Tooltip("Units generated when this entry is selected.")]
            [Min(1)] public int amount;

            [Tooltip("Relative weight used for deterministic selection among authored outputs.")]
            [Min(0.01f)] public float weight;

            public DrillYieldEntry(ItemData item, int amount, float weight)
            {
                this.item = item;
                this.amount = amount;
                this.weight = weight;
            }
        }

        [Header("── Extraction ─────────────────────────────")]
        [Tooltip("Seconds between extraction cycles while the drill has power and free output capacity.")]
        [SerializeField, Range(10f, 600f)] private float extractionCycleSeconds = 120f;

        [Tooltip("Local sorter that receives freshly drilled output before the base network routes it into storage.")]
        [SerializeField] private LogisticsSorterModule linkedSorter;

        [Tooltip("Fallback output used when no weighted extraction table is authored.")]
        [SerializeField] private ItemData fallbackOutputItem;

        [Tooltip("Fallback amount produced when no weighted extraction table is authored.")]
        [SerializeField, Range(1, 8)] private int fallbackOutputAmount = 1;

        [Tooltip("Weighted extraction table for deep-core output.")]
        [SerializeField] private DrillYieldEntry[] extractionTable;

        [Tooltip("Maximum buffered units held inside the drill when the sorter is offline or full.")]
        [SerializeField, Range(1, 32)] private int maxBufferedUnits = 8;

        [Header("── Placement ──────────────────────────────")]
        [Tooltip("Layers considered valid seabed for drill anchoring.")]
        [SerializeField] private LayerMask seabedMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Tooltip("Vertical probe height above the candidate placement point.")]
        [SerializeField, Range(0.25f, 8f)] private float placementProbeHeight = 2.5f;

        [Tooltip("Maximum downward probe distance used to find the seabed under the drill footprint.")]
        [SerializeField, Range(1f, 20f)] private float placementProbeDistance = 6f;

        [Tooltip("Minimum upward normal component required for stable seabed anchoring.")]
        [SerializeField, Range(0.1f, 1f)] private float minimumSeabedNormalY = 0.82f;

        [Header("── Power ──────────────────────────────────")]
        [Tooltip("Continuous grid draw while the drill is actively cutting into the seabed.")]
        [SerializeField, Range(0f, 500f)] private float activePowerDraw = 240f;

        [Tooltip("Priority used when the power grid starts shedding non-critical industrial loads.")]
        [SerializeField, Range(0, 100)] private int powerPriority = 32;

        [Header("── Diagnostics ───────────────────────────")]
        [SerializeField] private bool _debugHasPower = true;
        [SerializeField] private bool _debugIsOperating;
        [SerializeField] private float _debugCycleTimer;
        [SerializeField] private string _debugBufferedItemId;
        [SerializeField] private int _debugBufferedUnits;
        [SerializeField] private int _debugCompletedCycleCount;

        private PowerNode _powerNode;
        private bool _registered;
        private bool _hasPower = true;
        private bool _isOperating;
        private float _cycleTimer;
        private ItemData _bufferedItem;
        private int _bufferedUnits;
        private int _completedCycleCount;
        private ulong _placementRayRequesterId;

        internal static int ActiveModuleCount => s_ActiveModules.Count;
        internal bool IsOperating => _isOperating;
        internal int CompletedCycleCount => _completedCycleCount;

        public float PowerRating => _isOperating ? -activePowerDraw : 0f;
        public int PowerPriority => powerPriority;
        public bool HasPower => _hasPower;

        internal static DeepDrillModule GetActiveModuleAt(int index)
        {
            return index >= 0 && index < s_ActiveModules.Count ? s_ActiveModules[index] : null;
        }

        private void Awake()
        {
            _powerNode = GetComponent<PowerNode>();
        }

        private void OnEnable()
        {
            RegisterModuleInstance();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            UnregisterModuleInstance();
        }

        private void OnDestroy()
        {
            TryUnregister();
            UnregisterModuleInstance();
        }

        public void OnSpawn()
        {
            _hasPower = true;
            _debugHasPower = true;
            _cycleTimer = 0f;
            _isOperating = false;
            _debugIsOperating = false;
            _completedCycleCount = 0;
            _debugCompletedCycleCount = 0;
            _bufferedItem = null;
            _bufferedUnits = 0;
            _debugBufferedItemId = string.Empty;
            _debugBufferedUnits = 0;
            TryRegister();
            RegisterModuleInstance();
        }

        public void OnDespawn()
        {
            TryUnregister();
            UnregisterModuleInstance();
            _hasPower = true;
            _debugHasPower = true;
            _isOperating = false;
            _debugIsOperating = false;
            _cycleTimer = 0f;
            ClearBufferedOutputState();
        }

        public void SlowTick()
        {
            TryFlushBufferedOutput();

            bool nextOperating = _hasPower && CanAccumulateMoreOutput();
            if (_isOperating != nextOperating)
            {
                _isOperating = nextOperating;
                _debugIsOperating = nextOperating;
                NotifyGridBalanceChanged();
            }

            if (!_isOperating)
            {
                _debugCycleTimer = _cycleTimer;
                return;
            }

            _cycleTimer += SlowTickDeltaTime;
            _debugCycleTimer = _cycleTimer;
            if (_cycleTimer < extractionCycleSeconds)
                return;

            _cycleTimer = 0f;
            ProduceExtractionBatch();
            TryFlushBufferedOutput();
        }

        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            _debugHasPower = hasPower;

            if (!hasPower && _isOperating)
            {
                _isOperating = false;
                _debugIsOperating = false;
                NotifyGridBalanceChanged();
            }
        }

        internal void PopulateSaveData(ref ModuleDTO dto)
        {
            dto.drillCycleTimerSeconds = Mathf.Max(0f, _cycleTimer);
            if (_bufferedItem == null || _bufferedUnits <= 0)
                return;

            dto.drillBufferedItemId = _bufferedItem.PersistentId;
            dto.drillBufferedAmount = _bufferedUnits;
        }

        internal void RestoreFromSaveData(ModuleDTO dto, ItemCatalog itemCatalog)
        {
            ClearBufferedOutputState();
            _cycleTimer = Mathf.Clamp(dto.drillCycleTimerSeconds, 0f, Mathf.Max(extractionCycleSeconds, SlowTickDeltaTime));
            _debugCycleTimer = _cycleTimer;

            if (itemCatalog == null || string.IsNullOrWhiteSpace(dto.drillBufferedItemId) || dto.drillBufferedAmount <= 0)
                return;

            ItemData item = itemCatalog.FindById(dto.drillBufferedItemId);
            if (item == null)
                return;

            _bufferedItem = item;
            _bufferedUnits = Mathf.Clamp(dto.drillBufferedAmount, 1, maxBufferedUnits);
            _debugBufferedItemId = item.PersistentId;
            _debugBufferedUnits = _bufferedUnits;
        }

        internal void EjectBufferedOutput(BaseModule owner, PlayerInventory inventory, ObjectPoolManager pool, ref Vector3 dropPosition)
        {
            if (owner == null || _bufferedItem == null || _bufferedUnits <= 0)
                return;

            int itemHashId = Hecton.Localization.LocHash.Compute(_bufferedItem.PersistentId);
            if (itemHashId != 0)
                owner.DropItemQuantityToInventoryOrWorld(itemHashId, _bufferedUnits, inventory, pool, ref dropPosition);

            ClearBufferedOutputState();
        }

        public bool ValidatePlacement(Vector3 position, Quaternion rotation, out string blockReason)
        {
            Vector3 origin = position + Vector3.up * placementProbeHeight;
            if (_placementRayRequesterId == 0UL)
                _placementRayRequesterId = EntityId.ToULong(gameObject.GetEntityId()) ^ 0x4452494C4C504C41UL;

            IInteractionSignalService interactionService = GlobalRegistry.InteractionSignals;
            if (interactionService == null || !interactionService.IsInitialized)
            {
                blockReason = DefaultPlacementBlockedReason;
                return false;
            }

            InteractionPacket packet = new InteractionPacket(
                unchecked((uint)_placementProbeToolId),
                new Unity.Mathematics.float3(origin.x, origin.y, origin.z),
                new Unity.Mathematics.float3(0f, -1f, 0f),
                1f,
                placementProbeHeight + placementProbeDistance,
                (byte)ToolActionMode.Primary,
                (byte)ToolStateBits.Active,
                unchecked((uint)Time.frameCount));

            if (!interactionService.TryRaycastPrimary(
                    _placementRayRequesterId,
                    in packet,
                    seabedMask.value,
                    QueryTriggerInteraction.Ignore,
                    out RaycastHit hit))
            {
                blockReason = DefaultPlacementBlockedReason;
                return false;
            }

            if (hit.normal.y < minimumSeabedNormalY)
            {
                blockReason = DefaultSlopeBlockedReason;
                return false;
            }

            blockReason = string.Empty;
            return true;
        }

        private void RegisterModuleInstance()
        {
            for (int i = 0; i < s_ActiveModules.Count; i++)
            {
                if (ReferenceEquals(s_ActiveModules[i], this))
                    return;
            }

            s_ActiveModules.Add(this);
        }

        private void UnregisterModuleInstance()
        {
            for (int i = s_ActiveModules.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(s_ActiveModules[i], this))
                    s_ActiveModules.RemoveAt(i);
            }
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private bool CanAccumulateMoreOutput()
        {
            if (_bufferedItem == null)
                return true;

            return _bufferedUnits < maxBufferedUnits;
        }

        private void ProduceExtractionBatch()
        {
            ItemData outputItem;
            int outputAmount;
            if (!TryResolveExtractionOutput(out outputItem, out outputAmount))
                return;

            if (_bufferedItem != null && !ReferenceEquals(_bufferedItem, outputItem))
                return;

            int nextBufferedUnits = _bufferedUnits + outputAmount;
            if (nextBufferedUnits > maxBufferedUnits)
                return;

            _bufferedItem = outputItem;
            _bufferedUnits = nextBufferedUnits;
            _completedCycleCount++;
            _debugCompletedCycleCount = _completedCycleCount;
            _debugBufferedItemId = outputItem != null ? outputItem.PersistentId : string.Empty;
            _debugBufferedUnits = _bufferedUnits;
        }

        private void TryFlushBufferedOutput()
        {
            if (_bufferedItem == null || _bufferedUnits <= 0 || linkedSorter == null)
                return;

            int acceptedUnits = linkedSorter.AcceptInbound(_bufferedItem, _bufferedUnits);
            if (acceptedUnits <= 0)
                return;

            _bufferedUnits -= acceptedUnits;
            if (_bufferedUnits <= 0)
            {
                _bufferedUnits = 0;
                _bufferedItem = null;
                _debugBufferedItemId = string.Empty;
            }

            _debugBufferedUnits = _bufferedUnits;
        }

        private bool TryResolveExtractionOutput(out ItemData item, out int amount)
        {
            item = fallbackOutputItem;
            amount = fallbackOutputAmount;

            if (extractionTable == null || extractionTable.Length == 0)
                return item != null && amount > 0;

            float totalWeight = 0f;
            for (int i = 0; i < extractionTable.Length; i++)
            {
                DrillYieldEntry entry = extractionTable[i];
                if (entry.item == null || entry.amount <= 0 || entry.weight <= 0f)
                    continue;

                totalWeight += entry.weight;
            }

            if (totalWeight <= 0f)
                return item != null && amount > 0;

            float pick = Random.value * totalWeight;
            for (int i = 0; i < extractionTable.Length; i++)
            {
                DrillYieldEntry entry = extractionTable[i];
                if (entry.item == null || entry.amount <= 0 || entry.weight <= 0f)
                    continue;

                if (pick <= entry.weight)
                {
                    item = entry.item;
                    amount = entry.amount;
                    return true;
                }

                pick -= entry.weight;
            }

            return item != null && amount > 0;
        }

        private void NotifyGridBalanceChanged()
        {
            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            if (grid != null)
                grid.MarkDirty();
        }

        private void ClearBufferedOutputState()
        {
            _bufferedItem = null;
            _bufferedUnits = 0;
            _debugBufferedItemId = string.Empty;
            _debugBufferedUnits = 0;
        }
    }
}
