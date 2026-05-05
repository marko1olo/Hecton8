using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Power;
using Hecton8.Tools;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Physical tool dock that recharges the active modular tool state through the base power grid.
    /// Removable battery-cell charging remains owned by <see cref="Hecton8.Gameplay.BatteryCharger"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PowerNode))]
    [AddComponentMenu("Hecton8/Construction/Battery Charger Module")]
    public sealed class BatteryChargerModule : MonoBehaviour, IUpdatable, IPowerComponent, IInteractable, IPoolable
    {
        private const string EmptyPrompt = "Dock Tool";
        private const string ChargingPrompt = "Charging Tool";
        private const string ReadyPrompt = "Retrieve Tool";

        [Header("Dock")]
        [SerializeField] private Transform toolSocket;
        [SerializeField, Min(0.01f)] private float chargeRateNormalizedPerSecond = 0.08f;
        [SerializeField, Min(0f)] private float standbyPowerDrawWatts = 3f;
        [SerializeField, Min(0f)] private float activePowerDrawWatts = 65f;
        [SerializeField, Range(0, 100)] private int powerPriority = 45;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugHasPower = true;
        [SerializeField] private bool _debugHasTool;
        [SerializeField] private float _debugBattery01;

        private PowerNode _powerNode;
        private PlayerTool _slottedTool;
        private Transform _slottedToolTransform;
        private Transform _originalParent;
        private Vector3 _originalLocalPosition;
        private Quaternion _originalLocalRotation;
        private Vector3 _originalLocalScale;
        private PlayerToolManager _owningToolManager;
        private bool _registered;
        private bool _hasPower = true;
        private bool _isCharging;

        public float PowerRating
        {
            get
            {
                if (_slottedTool == null)
                    return 0f;

                float draw = standbyPowerDrawWatts;
                if (_isCharging)
                    draw += activePowerDrawWatts;

                return -draw;
            }
        }

        public int PowerPriority => powerPriority;
        public bool HasPower => _hasPower;

        private void Awake()
        {
            _powerNode = GetComponent<PowerNode>();
        }

        private void OnEnable()
        {
            TryRegister();
            RefreshDiagnostics();
        }

        private void OnDisable()
        {
            TryRestoreToolPose();
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryRestoreToolPose();
            TryUnregister();
        }

        public void OnSpawn()
        {
            _hasPower = true;
            _isCharging = false;
            _slottedTool = null;
            _slottedToolTransform = null;
            TryRegister();
            RefreshDiagnostics();
        }

        public void OnDespawn()
        {
            TryRestoreToolPose();
            _hasPower = true;
            _isCharging = false;
            TryUnregister();
            RefreshDiagnostics();
        }

        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            RefreshDiagnostics();
        }

        public void Tick(float deltaTime)
        {
            if (_slottedTool == null || !_hasPower || GlobalRegistry.ModularEquipment == null)
            {
                _isCharging = false;
                RefreshDiagnostics();
                return;
            }

            IModularEquipmentService equipment = GlobalRegistry.ModularEquipment;
            uint toolId = _slottedTool.RuntimeToolId;
            float currentBattery = equipment.GetBatteryNormalized(toolId, _slottedTool.ResolveModularBatteryNormalized());
            if (currentBattery >= 0.999f)
            {
                _isCharging = false;
                _debugBattery01 = 1f;
                return;
            }

            _isCharging = true;
            float nextBattery = Mathf.Clamp01(currentBattery + chargeRateNormalizedPerSecond * Mathf.Max(0f, deltaTime));
            equipment.SetBattery(toolId, nextBattery);
            _debugBattery01 = nextBattery;

            if (_powerNode != null && _powerNode.Grid != null)
                _powerNode.Grid.MarkDirty();
        }

        public void OnHoverStart() { }

        public void OnHoverEnd() { }

        public void Interact(Transform interactor)
        {
            if (_slottedTool != null)
            {
                TryRestoreToolPose();
                RefreshDiagnostics();
                return;
            }

            PlayerToolManager toolManager = ResolveToolManager(interactor);
            if (toolManager == null || toolManager.CurrentTool == null)
                return;

            TryDockTool(toolManager.CurrentTool, toolManager);
        }

        public string GetInteractText()
        {
            if (_slottedTool == null)
                return EmptyPrompt;

            return _isCharging ? ChargingPrompt : ReadyPrompt;
        }

        public bool TryDockTool(PlayerTool tool)
        {
            return TryDockTool(tool, ResolveToolManager(null));
        }

        private bool TryDockTool(PlayerTool tool, PlayerToolManager toolManager)
        {
            if (tool == null || toolManager == null || _slottedTool != null)
                return false;

            if (!toolManager.TryBeginExternalToolDock(tool))
                return false;

            _slottedTool = tool;
            _owningToolManager = toolManager;
            _slottedToolTransform = tool.transform;
            _originalParent = _slottedToolTransform.parent;
            _originalLocalPosition = _slottedToolTransform.localPosition;
            _originalLocalRotation = _slottedToolTransform.localRotation;
            _originalLocalScale = _slottedToolTransform.localScale;

            Transform socket = toolSocket != null ? toolSocket : transform;
            _slottedToolTransform.SetParent(socket, false);
            _slottedToolTransform.localPosition = Vector3.zero;
            _slottedToolTransform.localRotation = Quaternion.identity;
            _slottedToolTransform.localScale = Vector3.one;

            if (_powerNode != null && _powerNode.Grid != null)
                _powerNode.Grid.MarkDirty();

            RefreshDiagnostics();
            return true;
        }

        private void TryRestoreToolPose()
        {
            PlayerTool restoredTool = _slottedTool;
            if (_slottedToolTransform != null)
            {
                _slottedToolTransform.SetParent(_originalParent, false);
                _slottedToolTransform.localPosition = _originalLocalPosition;
                _slottedToolTransform.localRotation = _originalLocalRotation;
                _slottedToolTransform.localScale = _originalLocalScale;
            }

            _slottedTool = null;
            _slottedToolTransform = null;
            _originalParent = null;
            _isCharging = false;
            if (_owningToolManager != null)
                _owningToolManager.EndExternalToolDock(restoredTool);

            _owningToolManager = null;

            if (_powerNode != null && _powerNode.Grid != null)
                _powerNode.Grid.MarkDirty();
        }

        private static PlayerToolManager ResolveToolManager(Transform interactor)
        {
            if (interactor != null)
            {
                PlayerToolManager toolManager = interactor.GetComponentInParent<PlayerToolManager>();
                if (toolManager != null)
                    return toolManager;
            }

            IPlayerRuntimeContext player = GlobalRegistry.Player;
            return player != null ? player.ToolManager : null;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registered = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void RefreshDiagnostics()
        {
            _debugHasPower = _hasPower;
            _debugHasTool = _slottedTool != null;
        }
    }
}
