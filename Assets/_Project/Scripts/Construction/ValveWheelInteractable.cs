using System;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Hecton8/Construction/Valve Wheel Interactable")]
    public sealed class ValveWheelInteractable : MonoBehaviour, IInteractable, IInteractableTextProvider, IPhysicalPanelButtonReceiver, IUpdatable, IGlobalRegistryHotSwapListener
    {
        private const int MaxParentResolveDepth = 32;
        private const float MinimumPcStepOpen01 = 0.05f;
        private const float MaximumPcStepOpen01 = 1f;
        private const float MinimumHandContactTimeoutSeconds = 0.02f;
        private const float MaximumHandContactTimeoutSeconds = 0.5f;
        private const float MaximumValveDeltaSeconds = 0.05f;

        [Header("Valve")]
        [SerializeField] private VRValveWheelHandle valveWheel;
        [SerializeField] private FluidValveRuntime valveRuntime;
        [SerializeField] private ValveMetadata valveMetadata;
        [SerializeField] private Collider activationCollider;
        [SerializeField] private Transform ikAnchor;

        [Header("Fallback Interaction")]
        [SerializeField, Range(MinimumPcStepOpen01, MaximumPcStepOpen01)] private float pcStepOpen01 = 0.25f;
        [SerializeField, Range(MinimumHandContactTimeoutSeconds, MaximumHandContactTimeoutSeconds)] private float handContactTimeoutSeconds = 0.12f;
        [SerializeField] private string turnPrompt = "Turn Valve";
        [SerializeField] private string closePrompt = "Close Valve";

        private InteractionHighlighter _highlighter;
        private Transform _cachedTransform;
        private string _cachedTurnPrompt = "Turn Valve";
        private string _cachedClosePrompt = "Close Valve";
        private bool _registeredReceiver;
        private bool _registeredTick;
        private bool _registeredHotSwap;
        private bool _dispatcherAvailable;
        private bool _physicalContactThisTick;
        private bool _physicalGrabActive;
        private float _secondsSinceLastPhysicalSample;
        private float _resolvedPcStepOpen01 = 0.25f;
        private float _resolvedHandContactTimeoutSeconds = 0.12f;
        private int _lastSampleFrame = -1;
        private Collider _registeredCollider;

        public Collider ActivationCollider => activationCollider;
        public Transform IkAnchor => ikAnchor;

        private void Awake()
        {
            CacheScalarConfig();
            CacheColdReferences();
            RebuildPromptCache();
        }

        private void OnEnable()
        {
            _dispatcherAvailable = GlobalRegistry.Dispatcher != null;
            CacheScalarConfig();
            CacheColdReferences();
            RebuildPromptCache();
            TryRegisterHotSwapListener();
            InteractableRegistry.RegisterTree(this);
            RegisterReceiver();
            SyncRuntimeVisualLoad();
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            UnregisterReceiver();
            StopPhysicalGrab();
            TryUnregisterTick();
            TryUnregisterHotSwapListener();
            if (_highlighter != null)
                _highlighter.SetHighlight(false);
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
            UnregisterReceiver();
            StopPhysicalGrab();
            TryUnregisterTick();
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            bool shouldRestoreTick = _physicalGrabActive;
            TryUnregisterTick();
            _dispatcherAvailable = currentService != null;
            if (shouldRestoreTick && currentService != null && isActiveAndEnabled)
                TryRegisterTick();
        }

        public void OnHoverStart()
        {
            if (_highlighter != null)
                _highlighter.SetHighlight(true);
        }

        public void OnHoverEnd()
        {
            if (_highlighter != null)
                _highlighter.SetHighlight(false);
        }

        public void Interact(Transform interactor)
        {
            VRValveWheelHandle wheel = valveWheel;
            if (wheel == null)
                return;

            StopPhysicalGrab();
            float currentOpen = Sanitize01(wheel.IsOpen01);
            float nextOpen = currentOpen >= 1f - (_resolvedPcStepOpen01 * 0.5f)
                ? 0f
                : math.saturate(currentOpen + _resolvedPcStepOpen01);
            wheel.SetOpen01Direct(nextOpen);
            SyncRuntimeVisualLoad();
        }

        public string GetInteractText()
        {
            return IsValveFullyOpen() ? _cachedClosePrompt : _cachedTurnPrompt;
        }

        public bool TryCopyInteractText(Span<char> destination, out int length)
        {
            return InteractableTextCopy.TryCopy(IsValveFullyOpen() ? _cachedClosePrompt : _cachedTurnPrompt, destination, out length);
        }

        public bool TryQueueHandPress(
            Vector3 handPosition,
            Vector3 handForward,
            IInteractionSignalService interactionSignals,
            Collider handSourceCollider,
            PhysicalHandSide fallbackHandSide,
            int sampleFrame = -1)
        {
            VRValveWheelHandle wheel = valveWheel;
            if (wheel == null || !IsFiniteVector(handPosition))
                return false;

            int currentFrame = SystemDispatcher.CurrentFrameIndex;
            int resolvedSampleFrame = sampleFrame >= 0 ? sampleFrame : currentFrame;
            if (resolvedSampleFrame > currentFrame || resolvedSampleFrame < _lastSampleFrame)
                return false;

            _lastSampleFrame = resolvedSampleFrame;
            if (!_physicalGrabActive)
            {
                wheel.BeginGrab(handPosition);
                _physicalGrabActive = true;
            }
            else
            {
                wheel.SampleControllerPose(handPosition);
            }

            _physicalContactThisTick = true;
            _secondsSinceLastPhysicalSample = 0f;
            SyncRuntimeVisualLoad();
            TryRegisterTick();
            return true;
        }

        public void Tick(float dt)
        {
            if (!_physicalGrabActive)
            {
                TryUnregisterTick();
                return;
            }

            float safeDeltaTime = SanitizeDeltaSeconds(dt);
            if (_physicalContactThisTick)
            {
                _physicalContactThisTick = false;
                _secondsSinceLastPhysicalSample = 0f;
                SyncRuntimeVisualLoad();
                return;
            }

            _secondsSinceLastPhysicalSample += safeDeltaTime;
            if (_secondsSinceLastPhysicalSample < _resolvedHandContactTimeoutSeconds)
                return;

            StopPhysicalGrab();
            SyncRuntimeVisualLoad();
            TryUnregisterTick();
        }

        public bool ValidateEditorBindingForBake()
        {
            CacheScalarConfig();
            CacheColdReferences();
            return valveWheel != null &&
                   valveRuntime != null &&
                   valveMetadata != null &&
                   ikAnchor != null &&
                   activationCollider != null &&
                   !activationCollider.isTrigger &&
                   activationCollider.gameObject.layer == HectonLayerMasks.Interactable;
        }

        private void StopPhysicalGrab()
        {
            if (!_physicalGrabActive)
                return;

            if (valveWheel != null)
                valveWheel.EndGrab();

            _physicalGrabActive = false;
            _physicalContactThisTick = false;
            _secondsSinceLastPhysicalSample = 0f;
            _lastSampleFrame = -1;
        }

        private void SyncRuntimeVisualLoad()
        {
            if (valveRuntime == null || valveWheel == null)
                return;

            valveRuntime.SetVisualLoad01(valveWheel.IsOpen01);
        }

        private bool IsValveFullyOpen()
        {
            return valveWheel != null && Sanitize01(valveWheel.IsOpen01) >= 0.999f;
        }

        private void CacheColdReferences()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;
            if (activationCollider == null)
                TryGetComponent(out activationCollider);
            if (ikAnchor == null)
                ikAnchor = _cachedTransform;
            if (valveWheel == null)
                TryResolveParentComponent(_cachedTransform, out valveWheel);
            if (valveRuntime == null)
                TryResolveParentComponent(_cachedTransform, out valveRuntime);
            if (valveMetadata == null)
                TryResolveParentComponent(_cachedTransform, out valveMetadata);
            if (_highlighter == null)
                TryGetComponent(out _highlighter);
        }

        private void RegisterReceiver()
        {
            Collider targetCollider = activationCollider;
            if (targetCollider == null)
                return;

            if (_registeredReceiver)
            {
                if (ReferenceEquals(_registeredCollider, targetCollider))
                    return;

                UnregisterReceiver();
            }

            if (!Application.isPlaying || !PhysicalHandReceiverRegistry.TryRegister(targetCollider, this))
                return;

            _registeredCollider = targetCollider;
            _registeredReceiver = true;
        }

        private void UnregisterReceiver()
        {
            if (!_registeredReceiver)
                return;

            PhysicalHandReceiverRegistry.Unregister(_registeredCollider, this);
            _registeredCollider = null;
            _registeredReceiver = false;
        }

        private bool TryRegisterTick()
        {
            if (_registeredTick)
                return true;
            if (!Application.isPlaying || !_dispatcherAvailable)
                return false;

            _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
            return _registeredTick;
        }

        private void TryUnregisterTick()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registeredTick = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void CacheScalarConfig()
        {
            _resolvedPcStepOpen01 = math.isfinite(pcStepOpen01)
                ? math.clamp(pcStepOpen01, MinimumPcStepOpen01, MaximumPcStepOpen01)
                : 0.25f;
            _resolvedHandContactTimeoutSeconds = math.isfinite(handContactTimeoutSeconds)
                ? math.clamp(handContactTimeoutSeconds, MinimumHandContactTimeoutSeconds, MaximumHandContactTimeoutSeconds)
                : 0.12f;
        }

        private void RebuildPromptCache()
        {
            _cachedTurnPrompt = string.IsNullOrWhiteSpace(turnPrompt) ? "Turn Valve" : turnPrompt;
            _cachedClosePrompt = string.IsNullOrWhiteSpace(closePrompt) ? "Close Valve" : closePrompt;
        }

        private static bool TryResolveParentComponent<T>(Transform start, out T component) where T : Component
        {
            component = null;
            Transform current = start;
            int depth = 0;
            while (current != null && depth < MaxParentResolveDepth)
            {
                if (current.TryGetComponent(out component))
                    return true;

                current = current.parent;
                depth++;
            }

            return false;
        }

        private static float Sanitize01(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 0f);
        }

        private static float SanitizeDeltaSeconds(float value)
        {
            return math.isfinite(value) ? math.clamp(value, 0f, MaximumValveDeltaSeconds) : 0f;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

#if UNITY_EDITOR
        public void ConfigureEditorBake(
            VRValveWheelHandle bakedValveWheel,
            FluidValveRuntime bakedValveRuntime,
            ValveMetadata bakedValveMetadata,
            Collider bakedActivationCollider,
            Transform bakedIkAnchor)
        {
            valveWheel = bakedValveWheel;
            valveRuntime = bakedValveRuntime;
            valveMetadata = bakedValveMetadata;
            activationCollider = bakedActivationCollider;
            ikAnchor = bakedIkAnchor;
            CacheScalarConfig();
            RebuildPromptCache();
        }

        private void OnValidate()
        {
            if (!math.isfinite(pcStepOpen01))
                pcStepOpen01 = 0.25f;
            if (!math.isfinite(handContactTimeoutSeconds))
                handContactTimeoutSeconds = 0.12f;
            pcStepOpen01 = math.clamp(pcStepOpen01, MinimumPcStepOpen01, MaximumPcStepOpen01);
            handContactTimeoutSeconds = math.clamp(handContactTimeoutSeconds, MinimumHandContactTimeoutSeconds, MaximumHandContactTimeoutSeconds);
            CacheScalarConfig();
            CacheColdReferences();
            RebuildPromptCache();
        }
#endif
    }
}
