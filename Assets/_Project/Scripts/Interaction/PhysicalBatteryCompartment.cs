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
        private const float Pi = 3.14159265359f;
        private const float TwoPi = 6.28318530718f;
        private const float HalfPi = 1.57079632679f;
        private const float DegreesToRadians = 0.01745329252f;
        private const float MaxSnapDeltaSeconds = 0.05f;
        private const float MaxDoorTravelDegrees = 180f;
        private const float MaxBatterySnapDurationSeconds = 1.0f;
        private const float MaxBatterySnapOffsetMeters = 2f;

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
        private Vector3 _resolvedDoorLocalAxis = Vector3.right;
        private float _doorOpen01;
        private float _resolvedDoorTravelDegrees = 72f;
        private float _resolvedDoorOpenThreshold01 = 0.85f;
        private float _resolvedBatterySnapDurationSeconds = 0.2f;
        private Transform _snappingCell;
        private Rigidbody _snappingCellBody;
        private ItemData _pendingBattery;
        private IBatteryTool _cachedBatteryTool;
        private Vector3 _snapStartLocalPosition;
        private Vector3 _snapTargetLocalPosition;
        private Vector3 _snapBodyLinearVelocity;
        private Vector3 _snapBodyAngularVelocity;
        private quaternion _snapStartLocalRotation;
        private quaternion _snapTargetLocalRotation;
        private float _pendingCharge01;
        private float _snapElapsedSeconds;
        private bool _snapBodyWasKinematic;
        private bool _snapBodyDetectedCollisions;
        private bool _snapInProgress;
        private bool _registeredTick;
        private bool _batteryVisualStateCached;
        private bool _batteryVisualActive;

        public float DoorOpen01 => _doorOpen01;
        public bool DoorOpenEnoughForSwap => _doorOpen01 >= _resolvedDoorOpenThreshold01;

        /// <summary>
        /// True while an inserted cell is being kinematically snapped into the socket.
        /// </summary>
        public bool IsSnapInProgress => _snapInProgress;

        /// <summary>
        /// True when the bound battery-capable tool currently owns an installed cell.
        /// </summary>
        public bool HasInstalledCell => TryResolveTool(out IBatteryTool tool) && tool.HasBattery;

        /// <summary>
        /// Current installed cell charge in normalized 0..1 space.
        /// </summary>
        public float InstalledCharge01 => TryResolveTool(out IBatteryTool tool) ? SanitizeCharge01(tool.BatteryCharge) : 0f;

        private void Awake()
        {
            RefreshBatteryToolCache();
            CacheScalarConfig();
            CacheDoorAxis();

            if (batteryDoor != null)
            {
                _doorClosedRotation = IsFiniteQuaternion(batteryDoor.localRotation)
                    ? batteryDoor.localRotation
                    : Quaternion.identity;
            }

            ApplyBatteryVisual();
        }

        private void OnEnable()
        {
            RefreshBatteryToolCache();
            CacheScalarConfig();
            CacheDoorAxis();
            _batteryVisualStateCached = false;
            ApplyDoorVisual();
            ApplyBatteryVisual();
        }

        private void OnDisable()
        {
            AbortBatterySnap();
            _batteryVisualStateCached = false;
            TryUnregisterTick();
        }

        public void SetBatteryDoorOpen01(float open01)
        {
            _doorOpen01 = math.isfinite(open01) ? math.saturate(open01) : 0f;
            ApplyDoorVisual();
        }

        public bool TryPullInstalledCell(out ItemData removedBattery, out float removedCharge01)
        {
            removedBattery = null;
            removedCharge01 = 0f;

            if (_snapInProgress || !DoorOpenEnoughForSwap || !TryResolveTool(out IBatteryTool tool) || !tool.HasBattery)
                return false;

            removedCharge01 = SanitizeCharge01(tool.BatteryCharge);
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
                bool inserted = tool.InsertBattery(battery, SanitizeCharge01(charge01));
                ApplyBatteryVisual();
                return inserted;
            }

            BeginBatterySnap(insertedCellTransform, snapTarget, battery, SanitizeCharge01(charge01));
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!_snapInProgress)
            {
                TryUnregisterTick();
                return;
            }

            if (_snappingCell == null || _pendingBattery == null)
            {
                AbortBatterySnap();
                TryUnregisterTick();
                return;
            }

            float safeDeltaTime = SanitizeDeltaSeconds(deltaTime);
            _snapElapsedSeconds = math.min(_resolvedBatterySnapDurationSeconds, _snapElapsedSeconds + safeDeltaTime);
            float t = math.saturate(_snapElapsedSeconds / _resolvedBatterySnapDurationSeconds);
            ApplyBatterySnapPose(t);

            if (t >= 1f)
                CompleteBatterySnap();
        }

        private bool TryResolveTool(out IBatteryTool tool)
        {
            tool = _cachedBatteryTool;
            if (tool != null)
                return true;

            RefreshBatteryToolCache();
            tool = _cachedBatteryTool;
            return tool != null;
        }

        private void RefreshBatteryToolCache()
        {
            _cachedBatteryTool = batteryToolOwner as IBatteryTool;
        }

        private void ApplyDoorVisual()
        {
            if (batteryDoor == null)
                return;

            batteryDoor.localRotation = _doorClosedRotation * ApproximateAngleAxisDegreesNoTrig(_resolvedDoorTravelDegrees * _doorOpen01, _resolvedDoorLocalAxis);
        }

        private void ApplyBatteryVisual()
        {
            if (batteryCellVisual == null)
                return;

            bool hasBattery = _snapInProgress || TryResolveTool(out IBatteryTool tool) && tool.HasBattery;
            if (_batteryVisualStateCached && _batteryVisualActive == hasBattery)
                return;

            batteryCellVisual.gameObject.SetActive(hasBattery);
            _batteryVisualActive = hasBattery;
            _batteryVisualStateCached = true;
        }

        private void BeginBatterySnap(Transform cell, Transform snapTarget, ItemData battery, float charge01)
        {
            _snappingCell = cell;
            _pendingBattery = battery;
            _pendingCharge01 = charge01;
            _snapElapsedSeconds = 0f;
            _snapStartLocalPosition = IsFiniteVector(cell.localPosition) ? cell.localPosition : Vector3.zero;
            Quaternion cellLocalRotation = IsFiniteQuaternion(cell.localRotation) ? cell.localRotation : Quaternion.identity;
            _snapStartLocalRotation = new quaternion(cellLocalRotation.x, cellLocalRotation.y, cellLocalRotation.z, cellLocalRotation.w);
            ResolveSnapTargetLocalPose(cell, snapTarget, out _snapTargetLocalPosition, out _snapTargetLocalRotation);
            _snapTargetLocalPosition = SanitizeSnapTargetLocalPosition(_snapStartLocalPosition, _snapTargetLocalPosition);
            if (cell.TryGetComponent(out _snappingCellBody))
            {
                _snapBodyWasKinematic = _snappingCellBody.isKinematic;
                _snapBodyDetectedCollisions = _snappingCellBody.detectCollisions;
                _snapBodyLinearVelocity = SanitizeVector(_snappingCellBody.linearVelocity, Vector3.zero);
                _snapBodyAngularVelocity = SanitizeVector(_snappingCellBody.angularVelocity, Vector3.zero);
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

            quaternion rotation = NormalizeQuaternionLerp(from, to, t, _snapTargetLocalRotation);
            _snappingCell.localRotation = new Quaternion(rotation.value.x, rotation.value.y, rotation.value.z, rotation.value.w);
        }

        private static quaternion NormalizeQuaternionLerp(float4 from, float4 to, float t, quaternion fallback)
        {
            float4 blended = math.lerp(from, to, t);
            float lengthSq = math.lengthsq(blended);
            if (lengthSq <= 0.000001f || !math.all(math.isfinite(blended)))
                return fallback;

            return new quaternion(blended * ApproximateInverseMagnitudeNoSqrt(blended));
        }

        private void CompleteBatterySnap()
        {
            ApplyBatterySnapPose(1f);
            bool inserted = TryResolveTool(out IBatteryTool tool) && !tool.HasBattery &&
                            tool.InsertBattery(_pendingBattery, _pendingCharge01);
            if (!inserted)
            {
                RestoreSnappingCellPose();
                RestoreSnappingCellBodyState();
            }
            else
            {
                LockInsertedCellBodyState();
            }

            _pendingBattery = null;
            _pendingCharge01 = 0f;
            _snappingCell = null;
            _snappingCellBody = null;
            _snapStartLocalPosition = Vector3.zero;
            _snapTargetLocalPosition = Vector3.zero;
            _snapStartLocalRotation = quaternion.identity;
            _snapTargetLocalRotation = quaternion.identity;
            _snapBodyWasKinematic = false;
            _snapBodyDetectedCollisions = false;
            _snapBodyLinearVelocity = Vector3.zero;
            _snapBodyAngularVelocity = Vector3.zero;
            _snapInProgress = false;
            ApplyBatteryVisual();
            TryUnregisterTick();
        }

        private void AbortBatterySnap()
        {
            if (!_snapInProgress)
                return;

            RestoreSnappingCellPose();
            RestoreSnappingCellBodyState();

            _snappingCell = null;
            _snappingCellBody = null;
            _pendingBattery = null;
            _pendingCharge01 = 0f;
            _snapElapsedSeconds = 0f;
            _snapStartLocalPosition = Vector3.zero;
            _snapTargetLocalPosition = Vector3.zero;
            _snapStartLocalRotation = quaternion.identity;
            _snapTargetLocalRotation = quaternion.identity;
            _snapBodyWasKinematic = false;
            _snapBodyDetectedCollisions = false;
            _snapBodyLinearVelocity = Vector3.zero;
            _snapBodyAngularVelocity = Vector3.zero;
            _snapInProgress = false;
            ApplyBatteryVisual();
            TryUnregisterTick();
        }

        private void RestoreSnappingCellPose()
        {
            if (_snappingCell == null)
                return;

            if (IsFiniteVector(_snapStartLocalPosition))
                _snappingCell.localPosition = _snapStartLocalPosition;

            Quaternion startRotation = new Quaternion(
                _snapStartLocalRotation.value.x,
                _snapStartLocalRotation.value.y,
                _snapStartLocalRotation.value.z,
                _snapStartLocalRotation.value.w);
            if (IsFiniteQuaternion(startRotation))
                _snappingCell.localRotation = startRotation;
        }

        private void RestoreSnappingCellBodyState()
        {
            if (_snappingCellBody == null)
                return;

            _snappingCellBody.isKinematic = _snapBodyWasKinematic;
            _snappingCellBody.detectCollisions = _snapBodyDetectedCollisions;
            _snappingCellBody.linearVelocity = _snapBodyLinearVelocity;
            _snappingCellBody.angularVelocity = _snapBodyAngularVelocity;
        }

        private void LockInsertedCellBodyState()
        {
            if (_snappingCellBody == null)
                return;

            _snappingCellBody.linearVelocity = Vector3.zero;
            _snappingCellBody.angularVelocity = Vector3.zero;
            _snappingCellBody.isKinematic = true;
            _snappingCellBody.detectCollisions = false;
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
                localPosition = IsFiniteVector(snapTarget.position)
                    ? parent.InverseTransformPoint(snapTarget.position)
                    : cell.localPosition;
                if (!IsFiniteVector(localPosition))
                    localPosition = cell.localPosition;

                Quaternion parentRotation = IsFiniteQuaternion(parent.rotation) ? parent.rotation : Quaternion.identity;
                Quaternion targetRotation = IsFiniteQuaternion(snapTarget.rotation) ? snapTarget.rotation : cell.rotation;
                Quaternion parentInverse = Quaternion.Inverse(parentRotation);
                Quaternion resolvedRotation = parentInverse * targetRotation;
                if (!IsFiniteQuaternion(resolvedRotation))
                    resolvedRotation = IsFiniteQuaternion(cell.localRotation) ? cell.localRotation : Quaternion.identity;

                localRotation = new quaternion(resolvedRotation.x, resolvedRotation.y, resolvedRotation.z, resolvedRotation.w);
                return;
            }

            localPosition = IsFiniteVector(snapTarget.position) ? snapTarget.position : cell.localPosition;
            if (!IsFiniteVector(localPosition))
                localPosition = cell.localPosition;

            Quaternion worldRotation = snapTarget.rotation;
            if (!IsFiniteQuaternion(worldRotation))
                worldRotation = cell.rotation;
            if (!IsFiniteQuaternion(worldRotation))
                worldRotation = Quaternion.identity;

            localRotation = new quaternion(worldRotation.x, worldRotation.y, worldRotation.z, worldRotation.w);
        }

        private void TryRegisterTick()
        {
            if (_registeredTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
        }

        private void TryUnregisterTick()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registeredTick = false;
        }

        private void CacheDoorAxis()
        {
            float lengthSq = doorLocalAxis.sqrMagnitude;
            if (lengthSq <= 0.000001f || !math.all(math.isfinite(new float3(doorLocalAxis.x, doorLocalAxis.y, doorLocalAxis.z))))
            {
                _resolvedDoorLocalAxis = Vector3.right;
                return;
            }

            _resolvedDoorLocalAxis = doorLocalAxis * ApproximateInverseMagnitudeNoSqrt(doorLocalAxis);
        }

        private static Quaternion ApproximateAngleAxisDegreesNoTrig(float angleDegrees, Vector3 normalizedAxis)
        {
            ApproximateSinCosFullNoTrig(angleDegrees * DegreesToRadians * 0.5f, out float sinHalf, out float cosHalf);
            Quaternion rotation = new Quaternion(
                normalizedAxis.x * sinHalf,
                normalizedAxis.y * sinHalf,
                normalizedAxis.z * sinHalf,
                cosHalf);
            return NormalizeQuaternionNoSqrt(rotation);
        }

        private static void ApproximateSinCosFullNoTrig(float radians, out float sin, out float cos)
        {
            float x = radians - (TwoPi * math.round(radians / TwoPi));
            float cosSign = 1f;
            if (x > HalfPi)
            {
                x = Pi - x;
                cosSign = -1f;
            }
            else if (x < -HalfPi)
            {
                x = -Pi - x;
                cosSign = -1f;
            }

            float x2 = x * x;
            sin = x * (1f - (x2 * (0.16666667f - (x2 * 0.008333333f))));
            cos = cosSign * (1f - (x2 * (0.5f - (x2 * 0.041666667f))));
        }

        private static Quaternion NormalizeQuaternionNoSqrt(Quaternion value)
        {
            float4 v = new float4(value.x, value.y, value.z, value.w);
            v *= ApproximateInverseMagnitudeNoSqrt(v);
            return new Quaternion(v.x, v.y, v.z, v.w);
        }

        private static float SanitizeCharge01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float SanitizeDeltaSeconds(float value)
        {
            return math.isfinite(value) ? math.clamp(value, 0f, MaxSnapDeltaSeconds) : 0f;
        }

        private static float SanitizePositiveSeconds(float value, float fallback)
        {
            float resolved = math.isfinite(value) ? value : fallback;
            return math.clamp(resolved, 0.01f, MaxBatterySnapDurationSeconds);
        }

        private static Vector3 SanitizeVector(Vector3 value, Vector3 fallback)
        {
            return IsFiniteVector(value) ? value : fallback;
        }

        private static Vector3 SanitizeSnapTargetLocalPosition(Vector3 start, Vector3 target)
        {
            if (!IsFiniteVector(start))
                start = Vector3.zero;
            if (!IsFiniteVector(target))
                return start;

            Vector3 delta = target - start;
            if (!IsFiniteVector(delta))
                return start;

            float maxOffsetSq = MaxBatterySnapOffsetMeters * MaxBatterySnapOffsetMeters;
            float deltaSq = delta.sqrMagnitude;
            if (deltaSq <= maxOffsetSq || deltaSq <= 0.000001f)
                return target;

            Vector3 clampedDelta = delta * ApproximateInverseMagnitudeNoSqrt(delta) * MaxBatterySnapOffsetMeters;
            return start + clampedDelta;
        }

        private static float ApproximateInverseMagnitudeNoSqrt(Vector3 value)
        {
            float3 absValue = math.abs(new float3(value.x, value.y, value.z));
            float largest = math.cmax(absValue);
            float smallest = math.cmin(absValue);
            float middle = absValue.x + absValue.y + absValue.z - largest - smallest;
            float magnitude = largest + (middle * 0.375f) + (smallest * 0.125f);
            return math.rcp(math.max(magnitude, 0.000001f));
        }

        private static float ApproximateInverseMagnitudeNoSqrt(float4 value)
        {
            float4 absValue = math.abs(value);
            float largest = math.max(math.max(absValue.x, absValue.y), math.max(absValue.z, absValue.w));
            float smallest = math.min(math.min(absValue.x, absValue.y), math.min(absValue.z, absValue.w));
            float middleSum = absValue.x + absValue.y + absValue.z + absValue.w - largest - smallest;
            float magnitude = largest + (middleSum * 0.25f) + (smallest * 0.125f);
            return math.rcp(math.max(magnitude, 0.000001f));
        }

        private void CacheScalarConfig()
        {
            _resolvedDoorOpenThreshold01 = ResolveSafeDoorOpenThreshold01();
            _resolvedDoorTravelDegrees = ResolveSafeDoorTravelDegrees();
            _resolvedBatterySnapDurationSeconds = SanitizePositiveSeconds(batterySnapDurationSeconds, 0.2f);
        }

        private float ResolveSafeDoorOpenThreshold01()
        {
            return math.isfinite(doorOpenThreshold01) ? math.saturate(doorOpenThreshold01) : 0.85f;
        }

        private float ResolveSafeDoorTravelDegrees()
        {
            return math.isfinite(doorTravelDegrees)
                ? math.clamp(doorTravelDegrees, -MaxDoorTravelDegrees, MaxDoorTravelDegrees)
                : 72f;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            float4 q = new float4(value.x, value.y, value.z, value.w);
            return math.all(math.isfinite(q)) && math.lengthsq(q) > 0.000001f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!math.isfinite(doorTravelDegrees))
                doorTravelDegrees = 72f;
            doorTravelDegrees = math.clamp(doorTravelDegrees, -MaxDoorTravelDegrees, MaxDoorTravelDegrees);
            if (!math.isfinite(doorOpenThreshold01))
                doorOpenThreshold01 = 0.85f;
            doorOpenThreshold01 = math.saturate(doorOpenThreshold01);
            if (!math.isfinite(batterySnapDurationSeconds) || batterySnapDurationSeconds < 0.01f)
                batterySnapDurationSeconds = 0.2f;
            batterySnapDurationSeconds = math.min(batterySnapDurationSeconds, MaxBatterySnapDurationSeconds);
            _cachedBatteryTool = null;
            _batteryVisualStateCached = false;
            CacheScalarConfig();
            CacheDoorAxis();
        }
#endif
    }
}
