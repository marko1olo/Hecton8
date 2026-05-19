using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Inventory;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.EditorTools
{
    public sealed class InventoryRoutingNetworkTunerWindow : EditorWindow
    {
        private const int HeatmapSlots = 256;
        private InventoryRoutingBufferHandles _handles;
        private int _slotCapacity = InventoryRoutingNetwork.DefaultSlotCapacity;
        private float _qualityWeight = 1f;
        private float _radiusMeters = 400f;
        private Label _statusLabel;
        private Label _layoutLabel;
        private Label _fragmentationLabel;
        private VisualElement _heatmap;

        [MenuItem("Hecton/Inventory/SOA Routing Tuner")]
        public static void Open()
        {
            GetWindow<InventoryRoutingNetworkTunerWindow>("SOA Inventory");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _layoutLabel = new Label();
            _statusLabel = new Label();
            _fragmentationLabel = new Label();
            root.Add(_layoutLabel);
            root.Add(_statusLabel);
            root.Add(_fragmentationLabel);

            IntegerField capacityField = new IntegerField("Slot Capacity") { value = _slotCapacity };
            capacityField.RegisterValueChangedCallback(evt => _slotCapacity = math.clamp(evt.newValue, 1, InventoryRoutingNetwork.DefaultSlotCapacity * 4));
            root.Add(capacityField);

            Slider qualitySlider = new Slider("Global Quality Weight", 0f, 1f) { value = _qualityWeight };
            qualitySlider.RegisterValueChangedCallback(evt => _qualityWeight = math.saturate(evt.newValue));
            root.Add(qualitySlider);

            Slider radiusSlider = new Slider("AUP Query Radius", 0f, 5000f) { value = _radiusMeters };
            radiusSlider.RegisterValueChangedCallback(evt => _radiusMeters = math.max(0f, evt.newValue));
            root.Add(radiusSlider);

            VisualElement buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.marginTop = 6;
            root.Add(buttons);
            buttons.Add(new Button(EnsureBuffers) { text = "Allocate Vault Buffers" });
            buttons.Add(new Button(GenerateMockNetwork) { text = "Generate 100k Mock" });
            buttons.Add(new Button(RefreshHeatmap) { text = "Refresh Heatmap" });
            buttons.Add(new Button(DumpTelemetry) { text = "Dump Telemetry" });

            _heatmap = new VisualElement();
            _heatmap.style.flexDirection = FlexDirection.Row;
            _heatmap.style.flexWrap = Wrap.Wrap;
            _heatmap.style.marginTop = 8;
            _heatmap.style.height = 144;
            root.Add(_heatmap);

            UpdateLayoutLabel();
            RefreshHeatmap();
        }

        private void EnsureBuffers()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                SetStatus("GlobalDataVault unavailable.");
                return;
            }

            _handles = InventoryRoutingNetwork.EnsureBuffers(vault, _slotCapacity);
            if (!InventoryRoutingNetwork.TryResolveBuffers(vault, ref _handles, out InventoryRoutingBuffers buffers))
            {
                SetStatus("Vault buffers unresolved.");
                return;
            }

            if (buffers.Tuning.IsCreated && buffers.Tuning.Length > 0)
            {
                buffers.Tuning[0] = new InventoryRoutingTuningDTO
                {
                    GlobalQualityWeight = _qualityWeight,
                    SliceCount = InventoryRoutingNetwork.ResolveTimeSliceBatchSize(_qualityWeight, buffers.Slots.Length),
                    ActiveSlotCount = buffers.ActiveSlotCount.IsCreated && buffers.ActiveSlotCount.Length > 0 ? buffers.ActiveSlotCount[0] : 0,
                    MaxQueryRadiusMeters = _radiusMeters,
                    MaxTransactionCASRetries = 1
                };
            }

            SetStatus("Vault buffers ready.");
            RefreshHeatmap();
        }

        private void GenerateMockNetwork()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                SetStatus("GlobalDataVault unavailable.");
                return;
            }

            if (!InventoryRoutingNetwork.TryResolveBuffers(vault, ref _handles, out InventoryRoutingBuffers buffers))
                _handles = InventoryRoutingNetwork.EnsureBuffers(vault, _slotCapacity);
            if (!InventoryRoutingNetwork.TryResolveBuffers(vault, ref _handles, out buffers))
            {
                SetStatus("Vault buffers unresolved.");
                return;
            }

            int count = math.min(_slotCapacity, buffers.Slots.Length);
            JobHandle handle = new GenerateMockLogisticsNetworkJob
            {
                Slots = buffers.Slots,
                SlotCount = count,
                OriginAUP = double3.zero,
                Seed = 0x5348494Eu
            }.Schedule(count, 128);
            handle.Complete();

            new CompactInventoryArrayJob
            {
                Slots = buffers.Slots,
                ActiveSlotCount = buffers.ActiveSlotCount,
                SlotLimit = count
            }.Run();

            SetStatus("Mock slots generated.");
            RefreshHeatmap();
        }

        private void RefreshHeatmap()
        {
            UpdateLayoutLabel();
            _heatmap?.Clear();

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || !InventoryRoutingNetwork.TryResolveBuffers(vault, ref _handles, out InventoryRoutingBuffers buffers))
            {
                SetStatus("No resolved inventory routing buffers.");
                return;
            }

            int count = math.min(HeatmapSlots, buffers.Slots.Length);
            int activeHint = buffers.ActiveSlotCount.IsCreated && buffers.ActiveSlotCount.Length > 0 ? buffers.ActiveSlotCount[0] : count;
            float fragmentation = InventoryRoutingNetwork.ComputeFragmentation01(buffers.Slots, activeHint);
            _fragmentationLabel.text = $"Fragmentation: {fragmentation:0.000} | Slice: {InventoryRoutingNetwork.ResolveTimeSliceBatchSize(_qualityWeight, buffers.Slots.Length)}";

            for (int i = 0; i < count; i++)
            {
                InventorySlotDTO slot = buffers.Slots[i];
                VisualElement cell = new VisualElement();
                cell.style.width = 10;
                cell.style.height = 10;
                cell.style.marginRight = 1;
                cell.style.marginBottom = 1;
                cell.tooltip = $"Slot {i} Hash {slot.ItemHashID} Qty {slot.Quantity} Lock {slot.ReservedLock}";
                cell.style.backgroundColor = ResolveCellColor(slot);
                _heatmap.Add(cell);
            }
        }

        private void DumpTelemetry()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || !InventoryRoutingNetwork.TryResolveBuffers(vault, ref _handles, out InventoryRoutingBuffers buffers))
            {
                SetStatus("Telemetry unavailable.");
                return;
            }

            InventoryRoutingNetwork.WriteTelemetryDump(InventoryRoutingNetwork.DumpPath, buffers.TelemetryRing, buffers.TelemetryCursor);
            SetStatus("Telemetry dump written.");
        }

        private void UpdateLayoutLabel()
        {
            _layoutLabel.text = InventoryRoutingNetwork.RuntimeLayoutValid()
                ? "InventorySlotDTO layout: 32 bytes, offsets 0/4/8/16/20."
                : "InventorySlotDTO layout mismatch.";
        }

        private void SetStatus(string value)
        {
            if (_statusLabel != null)
                _statusLabel.text = value;
        }

        private static Color ResolveCellColor(InventorySlotDTO slot)
        {
            if (slot.ItemHashID == 0u && slot.Quantity == 0u)
                return new Color(0.12f, 0.12f, 0.12f, 1f);
            if (slot.ItemHashID == 0u || slot.Quantity == 0u)
                return new Color(0.8f, 0.05f, 0.05f, 1f);
            if (slot.ReservedLock != 0u)
                return new Color(0.9f, 0.55f, 0.05f, 1f);
            if ((slot.ConditionFlags & InventoryRoutingNetwork.ConditionDegraded) != 0u)
                return new Color(0.5f, 0.15f, 0.7f, 1f);
            return new Color(0.05f, 0.65f, 0.3f, 1f);
        }
    }
}
