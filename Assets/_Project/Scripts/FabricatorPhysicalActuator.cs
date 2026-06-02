using Hecton8.Core;
using Hecton8.UI;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Crafting
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Crafting/Fabricator Physical Actuator")]
    public sealed class FabricatorPhysicalActuator : MonoBehaviour, IPanelInteractable, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const float MechanicalStartThreshold01 = 0.85f;
        private const float Pi = 3.14159265359f;
        private const float TwoPi = 6.28318530718f;
        private const float HalfPi = 1.57079632679f;
        private const float DegreesToRadians = 0.01745329252f;
        private const float MaxLeverTravelDegrees = 90f;
        private const float MaxEmergencyStopOffsetMeters = 0.15f;
        private const float EmergencyStopReturnDurationSeconds = 0.12f;
        private const float MaxActuatorDeltaSeconds = 0.05f;
        private const int MaxCraftMultiplier = 99;

        [Header("Fabricator")]
        [SerializeField] private Fabricator fabricator;
        [SerializeField] private RecipeData armedRecipe;
        [SerializeField, Min(1)] private int craftMultiplier = 1;

        [Header("Physical Inputs")]
        [SerializeField, Range(0.85f, 1f)] private float leverStartThreshold01 = MechanicalStartThreshold01;
        [SerializeField] private int leverPanelId = 41;
        [SerializeField] private int emergencyStopPanelId = 42;

        [Header("Visuals")]
        [SerializeField] private Transform leverVisual;
        [SerializeField] private Vector3 leverLocalAxis = Vector3.right;
        [SerializeField] private float leverTravelDegrees = -42f;
        [SerializeField] private Transform emergencyStopVisual;
        [SerializeField] private Vector3 emergencyStopPressedOffset = new Vector3(0f, 0f, -0.025f);

        private Quaternion _leverRestRotation;
        private Vector3 _emergencyStopRestPosition;
        private Vector3 _emergencyStopPressedPosition;
        private Vector3 _resolvedEmergencyStopPressedOffset;
        private float _lever01;
        private float _resolvedLeverStartThreshold01 = MechanicalStartThreshold01;
        private float _resolvedLeverTravelDegrees = -42f;
        private float _emergencyStopReturnSeconds;
        private bool _leverStartLatched;
        private bool _leverLockedDown;
        private bool _emergencyStopPressed;
        private bool _registeredTick;
        private bool _registeredLateFrame;
        private bool _hotSwapRegistered;
        private bool _leverVisualDirty;
        private bool _emergencyStopVisualDirty;
        private Vector3 _pendingEmergencyStopVisualPosition;

        public float Lever01 => _lever01;
        public bool LeverLockedDown => _leverLockedDown;

        private void Awake()
        {
            if (fabricator == null)
                TryGetComponent(out fabricator);

            CacheScalarConfig();
            if (leverVisual != null)
                _leverRestRotation = IsFiniteQuaternion(leverVisual.localRotation)
                    ? leverVisual.localRotation
                    : Quaternion.identity;
            if (emergencyStopVisual != null)
                _emergencyStopRestPosition = IsFiniteVector(emergencyStopVisual.localPosition)
                    ? emergencyStopVisual.localPosition
                    : Vector3.zero;
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            if (_leverLockedDown && (fabricator == null || !fabricator.IsCrafting))
                ReleaseLeverLock();

            CacheScalarConfig();
            TryRegisterLateFrame();
            ApplyLeverVisual();
            ApplyEmergencyStopVisual();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher || currentService == null || !isActiveAndEnabled)
                return;

            TryUnregisterLateFrame();
            TryRegisterLateFrame();

            if (!_leverLockedDown && !_emergencyStopPressed)
                return;

            TryUnregisterTick();
            TryRegisterTick();
        }

        public void ArmRecipe(RecipeData recipe, int multiplier)
        {
            armedRecipe = recipe;
            craftMultiplier = math.clamp(multiplier, 1, MaxCraftMultiplier);
            if (!_leverLockedDown)
                _leverStartLatched = false;
        }

        public bool SetLever01(float lever01)
        {
            if (_leverLockedDown)
            {
                _lever01 = 1f;
                ApplyLeverVisual();
                return false;
            }

            _lever01 = math.isfinite(lever01) ? math.saturate(lever01) : 0f;
            ApplyLeverVisual();

            if (_lever01 < _resolvedLeverStartThreshold01)
            {
                _leverStartLatched = false;
                return false;
            }

            if (_leverStartLatched)
                return false;

            if (!TryStartArmedCraft())
                return false;

            _leverStartLatched = true;
            _leverLockedDown = true;
            _lever01 = 1f;
            ApplyLeverVisual();
            TryRegisterTick();
            return true;
        }

        public void ResetLever()
        {
            if (_leverLockedDown && fabricator != null && fabricator.IsCrafting)
            {
                _lever01 = 1f;
                ApplyLeverVisual();
                return;
            }

            ReleaseLeverLock();
            TryUnregisterTickIfIdle();
        }

        public void Tick(float deltaTime)
        {
            float safeDeltaSeconds = SanitizeDeltaSeconds(deltaTime);

            if (_leverLockedDown && (fabricator == null || !fabricator.IsCrafting))
                ReleaseLeverLock();

            if (_emergencyStopPressed)
                UpdateEmergencyStopReturn(safeDeltaSeconds);
        }

        private void ReleaseLeverLock()
        {
            _lever01 = 0f;
            _leverStartLatched = false;
            _leverLockedDown = false;
            ApplyLeverVisual();
        }

        public void HitEmergencyStop()
        {
            if (fabricator != null)
                fabricator.CancelCraft();

            _emergencyStopPressed = true;
            _emergencyStopReturnSeconds = EmergencyStopReturnDurationSeconds;
            _emergencyStopPressedPosition = _emergencyStopRestPosition + _resolvedEmergencyStopPressedOffset;
            ApplyEmergencyStopVisual();

            ReleaseLeverLock();
            TryRegisterTick();
        }

        public void ReceiveCanvasInput(in DiegeticPanelInputEvent inputEvent)
        {
            if (DiegeticPanelInputEvent.ResolvePrimaryPointerAction(inputEvent.EventType) != DiegeticPanelInputEventType.Down)
                return;

            if (inputEvent.PanelId == leverPanelId)
            {
                SetLever01(1f);
                return;
            }

            if (inputEvent.PanelId == emergencyStopPanelId)
                HitEmergencyStop();
        }

        private bool TryStartArmedCraft()
        {
            if (fabricator == null || armedRecipe == null)
                return false;

            return fabricator.StartCraft(armedRecipe, ResolveSafeCraftMultiplier());
        }

        private void TryRegisterTick()
        {
            if (_registeredTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterTick()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registeredTick = false;
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrame = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void TryUnregisterTickIfIdle()
        {
            if (!_leverLockedDown && !_emergencyStopPressed)
                TryUnregisterTick();
        }

        private void ApplyLeverVisual()
        {
            _leverVisualDirty = true;
        }

        private void ApplyEmergencyStopVisual()
        {
            QueueEmergencyStopVisual(_emergencyStopPressed ? _emergencyStopPressedPosition : _emergencyStopRestPosition);
        }

        public void LateFrameTick()
        {
            if (_leverVisualDirty)
            {
                _leverVisualDirty = false;
                if (leverVisual != null)
                {
                    Vector3 axis = ResolveLeverAxis();
                    leverVisual.localRotation = _leverRestRotation * ApproximateAngleAxisDegreesNoTrig(_resolvedLeverTravelDegrees * _lever01, axis);
                }
            }

            if (_emergencyStopVisualDirty)
            {
                _emergencyStopVisualDirty = false;
                if (emergencyStopVisual != null)
                    emergencyStopVisual.localPosition = _pendingEmergencyStopVisualPosition;
            }
        }

        private void QueueEmergencyStopVisual(Vector3 position)
        {
            _pendingEmergencyStopVisualPosition = position;
            _emergencyStopVisualDirty = true;
        }

        private void UpdateEmergencyStopReturn(float deltaSeconds)
        {
            if (!_emergencyStopPressed)
                return;

            _emergencyStopReturnSeconds = math.max(0f, _emergencyStopReturnSeconds - deltaSeconds);
            if (_emergencyStopReturnSeconds > 0f)
            {
                if (emergencyStopVisual == null)
                    return;

                float t = 1f - math.saturate(_emergencyStopReturnSeconds / EmergencyStopReturnDurationSeconds);
                float3 from = new float3(_emergencyStopPressedPosition.x, _emergencyStopPressedPosition.y, _emergencyStopPressedPosition.z);
                float3 to = new float3(_emergencyStopRestPosition.x, _emergencyStopRestPosition.y, _emergencyStopRestPosition.z);
                float3 position = math.lerp(from, to, t);
                QueueEmergencyStopVisual(new Vector3(position.x, position.y, position.z));
                return;
            }

            _emergencyStopPressed = false;
            _emergencyStopReturnSeconds = 0f;
            ApplyEmergencyStopVisual();
        }

        private Vector3 ResolveLeverAxis()
        {
            float lengthSq = leverLocalAxis.sqrMagnitude;
            if (lengthSq <= 0.000001f || !IsFiniteVector(leverLocalAxis))
                return Vector3.right;

            return leverLocalAxis * math.rsqrt(lengthSq);
        }

        private void CacheScalarConfig()
        {
            _resolvedLeverStartThreshold01 = math.isfinite(leverStartThreshold01)
                ? math.clamp(leverStartThreshold01, MechanicalStartThreshold01, 1f)
                : MechanicalStartThreshold01;
            _resolvedLeverTravelDegrees = math.isfinite(leverTravelDegrees)
                ? math.clamp(leverTravelDegrees, -MaxLeverTravelDegrees, MaxLeverTravelDegrees)
                : -42f;
            _resolvedEmergencyStopPressedOffset = SanitizeOffset(emergencyStopPressedOffset, MaxEmergencyStopOffsetMeters);
            craftMultiplier = ResolveSafeCraftMultiplier();
            _lever01 = math.isfinite(_lever01) ? math.saturate(_lever01) : 0f;
        }

        private int ResolveSafeCraftMultiplier()
        {
            return math.clamp(craftMultiplier, 1, MaxCraftMultiplier);
        }

        private static Vector3 SanitizeOffset(Vector3 value, float maxMagnitude)
        {
            if (!IsFiniteVector(value))
                return Vector3.zero;

            float maxSq = maxMagnitude * maxMagnitude;
            float lengthSq = value.sqrMagnitude;
            if (lengthSq <= maxSq || lengthSq <= 0.000001f)
                return value;

            return value * math.rsqrt(lengthSq) * maxMagnitude;
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
            float lenSq = math.max(math.dot(v, v), 0.000001f);
            v *= math.rsqrt(lenSq);
            return new Quaternion(v.x, v.y, v.z, v.w);
        }

        private static float SanitizeDeltaSeconds(float value)
        {
            return math.isfinite(value) ? math.clamp(value, 0f, MaxActuatorDeltaSeconds) : 0f;
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

        private void OnDisable()
        {
            _emergencyStopPressed = false;
            _emergencyStopReturnSeconds = 0f;
            if (!_leverLockedDown || fabricator == null || !fabricator.IsCrafting)
                ReleaseLeverLock();
            ApplyEmergencyStopVisual();
            TryUnregisterTick();
            TryUnregisterLateFrame();
            TryUnregisterHotSwapListener();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (leverStartThreshold01 < MechanicalStartThreshold01)
                leverStartThreshold01 = MechanicalStartThreshold01;
            if (!math.isfinite(leverStartThreshold01))
                leverStartThreshold01 = MechanicalStartThreshold01;
            leverStartThreshold01 = math.clamp(leverStartThreshold01, MechanicalStartThreshold01, 1f);
            if (!math.isfinite(leverTravelDegrees))
                leverTravelDegrees = -42f;
            leverTravelDegrees = math.clamp(leverTravelDegrees, -MaxLeverTravelDegrees, MaxLeverTravelDegrees);
            emergencyStopPressedOffset = SanitizeOffset(emergencyStopPressedOffset, MaxEmergencyStopOffsetMeters);
            craftMultiplier = math.clamp(craftMultiplier, 1, MaxCraftMultiplier);
            CacheScalarConfig();
        }
#endif
    }
}
