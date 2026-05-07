using Hecton8.Core;
using Hecton8.UI;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Crafting
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Crafting/Fabricator Physical Actuator")]
    public sealed class FabricatorPhysicalActuator : MonoBehaviour, IPanelInteractable, IUpdatable
    {
        private const float MechanicalStartThreshold01 = 0.85f;

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
        private float _lever01;
        private bool _leverStartLatched;
        private bool _leverLockedDown;
        private bool _registeredTick;

        public float Lever01 => _lever01;
        public bool LeverLockedDown => _leverLockedDown;

        private void Awake()
        {
            if (fabricator == null)
                TryGetComponent(out fabricator);

            if (leverVisual != null)
                _leverRestRotation = leverVisual.localRotation;
            if (emergencyStopVisual != null)
                _emergencyStopRestPosition = emergencyStopVisual.localPosition;
        }

        public void ArmRecipe(RecipeData recipe, int multiplier)
        {
            armedRecipe = recipe;
            craftMultiplier = math.max(1, multiplier);
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

            _lever01 = math.saturate(lever01);
            ApplyLeverVisual();

            if (_lever01 < math.max(MechanicalStartThreshold01, leverStartThreshold01))
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
        }

        public void Tick(float deltaTime)
        {
            if (!_leverLockedDown)
            {
                TryUnregisterTick();
                return;
            }

            if (fabricator != null && fabricator.IsCrafting)
                return;

            ReleaseLeverLock();
        }

        private void ReleaseLeverLock()
        {
            _lever01 = 0f;
            _leverStartLatched = false;
            _leverLockedDown = false;
            ApplyLeverVisual();
            TryUnregisterTick();
        }

        public void HitEmergencyStop()
        {
            if (fabricator != null)
                fabricator.CancelCraft();

            if (emergencyStopVisual != null)
                emergencyStopVisual.localPosition = _emergencyStopRestPosition + emergencyStopPressedOffset;

            ReleaseLeverLock();
        }

        public void ReceiveCanvasInput(in DiegeticPanelInputEvent inputEvent)
        {
            if ((inputEvent.EventType & DiegeticPanelInputEventType.Down) == 0)
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

            return fabricator.StartCraft(armedRecipe, math.max(1, craftMultiplier));
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

        private void ApplyLeverVisual()
        {
            if (leverVisual == null)
                return;

            Vector3 axis = ResolveLeverAxis();
            leverVisual.localRotation = _leverRestRotation * Quaternion.AngleAxis(leverTravelDegrees * _lever01, axis);
        }

        private Vector3 ResolveLeverAxis()
        {
            float lengthSq = leverLocalAxis.sqrMagnitude;
            if (lengthSq <= 0.000001f)
                return Vector3.right;

            return leverLocalAxis * math.rsqrt(lengthSq);
        }

        private void OnDisable()
        {
            TryUnregisterTick();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (leverStartThreshold01 < MechanicalStartThreshold01)
                leverStartThreshold01 = MechanicalStartThreshold01;
        }
#endif
    }
}
