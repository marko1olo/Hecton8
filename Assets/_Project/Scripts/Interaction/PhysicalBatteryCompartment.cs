using Hecton8.Items;
using Hecton8.Tools;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Interaction
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Interaction/Physical Battery Compartment")]
    public sealed class PhysicalBatteryCompartment : MonoBehaviour, IUpdatable
    {
        [Header("Battery Tool")]
        [SerializeField] private MonoBehaviour batteryToolOwner;
        [SerializeField] private ItemData acceptedBatteryItem;

        [Header("Door")]
        [SerializeField] private Transform batteryDoor;
        [SerializeField] private Vector3 doorLocalAxis = Vector3.right;
        [SerializeField] private float doorTravelDegrees = 72f;
        [SerializeField, Range(0f, 1f)] private float doorOpenThreshold01 = 0.85f;

        [Header("Cell Visual")]
        [SerializeField] private Transform batteryCellVisual;
        [SerializeField] private Transform batterySocket;
        [SerializeField, Min(0.01f)] private float batterySnapDurationSeconds = 0.2f;

        private Quaternion _doorClosedRotation;
        private float _doorOpen01;
        private Transform _snappingCell;
        private Rigidbody _snappingCellBody;
        private ItemData _pendingBattery;
        private Vector3 _snapStartLocalPosition;
        private Vector3 _snapTargetLocalPosition;
        private quaternion _snapStartLocalRotation;
        private quaternion _snapTargetLocalRotation;
        private float _pendingCharge01;
        private float _snapElapsedSeconds;
        private bool _snapBodyWasKinematic;
        private bool _snapBodyDetectedCollisions;
        private bool _snapInProgress;
        private bool _registeredTick;

        public float DoorOpen01 => _doorOpen01;
        public bool DoorOpenEnoughForSwap => _doorOpen01 >= doorOpenThreshold01;

        private void Awake()
        {
            if (batteryDoor != null)
                _doorClosedRotation = batteryDoor.localRotation;

            ApplyBatteryVisual();
        }

        public void SetBatteryDoorOpen01(float open01)
        {
            _doorOpen01 = math.saturate(open01);
            ApplyDoorVisual();
        }

        public bool TryPullInstalledCell(out ItemData removedBattery, out float removedCharge01)
        {
            removedBattery = null;
            removedCharge01 = 0f;

            if (_snapInProgress || !DoorOpenEnoughForSwap || !TryResolveTool(out IBatteryTool tool) || !tool.HasBattery)
                return false;

            removedCharge01 = tool.BatteryCharge;
            removedBattery = tool.RemoveBattery();
            ApplyBatteryVisual();
            return removedBattery != null;
        }

        public bool TryInsertCell(ItemData battery, float charge01)
        {
            return TryInsertCell(battery, charge01, batteryCellVisual);
        }

        public bool TryInsertCell(ItemData battery, float charge01, Transform insertedCellTransform)
        {
            if (_snapInProgress || !DoorOpenEnoughForSwap || battery == null || !TryResolveTool(out IBatteryTool tool) || tool.HasBattery)
                return false;

            if (acceptedBatteryItem != null && !ReferenceEquals(acceptedBatteryItem, battery))
                return false;

            Transform snapTarget = batterySocket != null ? batterySocket : batteryCellVisual;
            if (insertedCellTransform == null || snapTarget == null)
            {
                bool inserted = tool.InsertBattery(battery, math.saturate(charge01));
                ApplyBatteryVisual();
                return inserted;
            }

            BeginBatterySnap(insertedCellTransform, snapTarget, battery, math.saturate(charge01));
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!_snapInProgress)
            {
                TryUnregisterTick();
                return;
            }

            _snapElapsedSeconds = math.min(
                math.max(0.01f, batterySnapDurationSeconds),
                _snapElapsedSeconds + math.max(0f, deltaTime));
            float t = math.saturate(_snapElapsedSeconds / math.max(0.01f, batterySnapDurationSeconds));
            ApplyBatterySnapPose(t);

            if (t >= 1f)
                CompleteBatterySnap();
        }

        private bool TryResolveTool(out IBatteryTool tool)
        {
            tool = batteryToolOwner as IBatteryTool;
            return tool != null;
        }

        private void ApplyDoorVisual()
        {
            if (batteryDoor == null)
                return;

            Vector3 axis = ResolveDoorAxis();
            batteryDoor.localRotation = _doorClosedRotation * Quaternion.AngleAxis(doorTravelDegrees * _doorOpen01, axis);
        }

        private void ApplyBatteryVisual()
        {
            if (batteryCellVisual == null)
                return;

            bool hasBattery = _snapInProgress || TryResolveTool(out IBatteryTool tool) && tool.HasBattery;
            batteryCellVisual.gameObject.SetActive(hasBattery);
        }

        private void BeginBatterySnap(Transform cell, Transform snapTarget, ItemData battery, float charge01)
        {
            _snappingCell = cell;
            _pendingBattery = battery;
            _pendingCharge01 = charge01;
            _snapElapsedSeconds = 0f;
            _snapStartLocalPosition = cell.localPosition;
            _snapStartLocalRotation = new quaternion(cell.localRotation.x, cell.localRotation.y, cell.localRotation.z, cell.localRotation.w);
            ResolveSnapTargetLocalPose(cell, snapTarget, out _snapTargetLocalPosition, out _snapTargetLocalRotation);
            if (cell.TryGetComponent(out _snappingCellBody))
            {
                _snapBodyWasKinematic = _snappingCellBody.isKinematic;
                _snapBodyDetectedCollisions = _snappingCellBody.detectCollisions;
                _snappingCellBody.linearVelocity = Vector3.zero;
                _snappingCellBody.angularVelocity = Vector3.zero;
                _snappingCellBody.isKinematic = true;
                _snappingCellBody.detectCollisions = false;
            }

            _snapInProgress = true;
            ApplyBatteryVisual();
            TryRegisterTick();
        }

        private void ApplyBatterySnapPose(float t)
        {
            if (_snappingCell == null)
                return;

            float3 fromPosition = new float3(_snapStartLocalPosition.x, _snapStartLocalPosition.y, _snapStartLocalPosition.z);
            float3 toPosition = new float3(_snapTargetLocalPosition.x, _snapTargetLocalPosition.y, _snapTargetLocalPosition.z);
            float3 position = math.lerp(fromPosition, toPosition, t);
            _snappingCell.localPosition = new Vector3(position.x, position.y, position.z);
            float4 from = _snapStartLocalRotation.value;
            float4 to = _snapTargetLocalRotation.value;
            if (math.dot(from, to) < 0f)
                to = -to;

            quaternion rotation = new quaternion(math.normalize(math.lerp(from, to, t)));
            _snappingCell.localRotation = new Quaternion(rotation.value.x, rotation.value.y, rotation.value.z, rotation.value.w);
        }

        private void CompleteBatterySnap()
        {
            ApplyBatterySnapPose(1f);
            bool inserted = TryResolveTool(out IBatteryTool tool) && !tool.HasBattery &&
                            tool.InsertBattery(_pendingBattery, _pendingCharge01);
            if (!inserted && _snappingCellBody != null)
            {
                _snappingCellBody.isKinematic = _snapBodyWasKinematic;
                _snappingCellBody.detectCollisions = _snapBodyDetectedCollisions;
            }

            _pendingBattery = null;
            _pendingCharge01 = 0f;
            _snappingCell = null;
            _snappingCellBody = null;
            _snapBodyWasKinematic = false;
            _snapBodyDetectedCollisions = false;
            _snapInProgress = false;
            ApplyBatteryVisual();
            TryUnregisterTick();
        }

        private void AbortBatterySnap()
        {
            if (!_snapInProgress)
                return;

            if (_snappingCellBody != null)
            {
                _snappingCellBody.isKinematic = _snapBodyWasKinematic;
                _snappingCellBody.detectCollisions = _snapBodyDetectedCollisions;
            }

            _snappingCell = null;
            _snappingCellBody = null;
            _pendingBattery = null;
            _pendingCharge01 = 0f;
            _snapElapsedSeconds = 0f;
            _snapBodyWasKinematic = false;
            _snapBodyDetectedCollisions = false;
            _snapInProgress = false;
            ApplyBatteryVisual();
        }

        private static void ResolveSnapTargetLocalPose(
            Transform cell,
            Transform snapTarget,
            out Vector3 localPosition,
            out quaternion localRotation)
        {
            Transform parent = cell.parent;
            if (parent != null)
            {
                localPosition = parent.InverseTransformPoint(snapTarget.position);
                Quaternion parentInverse = Quaternion.Inverse(parent.rotation);
                Quaternion resolvedRotation = parentInverse * snapTarget.rotation;
                localRotation = new quaternion(resolvedRotation.x, resolvedRotation.y, resolvedRotation.z, resolvedRotation.w);
                return;
            }

            localPosition = snapTarget.position;
            Quaternion worldRotation = snapTarget.rotation;
            localRotation = new quaternion(worldRotation.x, worldRotation.y, worldRotation.z, worldRotation.w);
        }

        private void TryRegisterTick()
        {
            if (_registeredTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registeredTick = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregisterTick()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registeredTick = false;
        }

        private Vector3 ResolveDoorAxis()
        {
            float lengthSq = doorLocalAxis.sqrMagnitude;
            if (lengthSq <= 0.000001f)
                return Vector3.right;

            return doorLocalAxis * math.rsqrt(lengthSq);
        }

        private void OnDisable()
        {
            AbortBatterySnap();
            TryUnregisterTick();
        }
    }
}
