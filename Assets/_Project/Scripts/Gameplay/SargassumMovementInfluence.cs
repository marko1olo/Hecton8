// ============================================================================
// HECTON-8 - SargassumMovementInfluence.cs
// Player-owned sticky-drag and entanglement receiver for dense floating sargassum.
// ============================================================================

namespace Hecton8.Gameplay
{
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Stores the current sticky-drag and entanglement influence imposed by floating sargassum.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Sargassum Movement Influence")]
    public sealed class SargassumMovementInfluence : MonoBehaviour
    {
        [Header("â”€â”€ Influence Response â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Blend speed used while entering dense sargassum.")]
        [SerializeField, Range(1f, 24f)] private float enterBlendSpeed = 8f;

        [Tooltip("Blend speed used while recovering after leaving dense sargassum.")]
        [SerializeField, Range(1f, 24f)] private float exitBlendSpeed = 4f;

        [Tooltip("Short grace period that prevents trigger jitter from instantly clearing the sticky effect.")]
        [SerializeField, Range(0f, 0.5f)] private float exitGraceTime = 0.1f;

        [Header("â”€â”€ Entanglement â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Blend speed used when the global field transitions into or out of an entangling snare state.")]
        [SerializeField, Range(1f, 24f)] private float entanglementBlendSpeed = 7f;

        [Tooltip("Local camera shake amplitude applied while the player is being tensioned by sargassum stems.")]
        [SerializeField, Range(0f, 0.2f)] private float cameraShakeAmplitude = 0.045f;

        [Tooltip("Pitch shake amplitude in degrees while entangled.")]
        [SerializeField, Range(0f, 3f)] private float cameraPitchAmplitude = 0.65f;

        [Tooltip("Roll shake amplitude in degrees while entangled.")]
        [SerializeField, Range(0f, 4f)] private float cameraRollAmplitude = 1.15f;

        [Tooltip("Oscillation frequency used by the tension shake.")]
        [SerializeField, Range(0.5f, 18f)] private float cameraShakeFrequency = 7.5f;

        [Header("â”€â”€ Diagnostics â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private int _debugActiveContacts;
        [SerializeField] private float _debugTargetSpeedMultiplier = 1f;
        [SerializeField] private float _debugTargetDragMultiplier = 1f;
        [SerializeField] private float _debugCurrentSpeedMultiplier = 1f;
        [SerializeField] private float _debugCurrentDragMultiplier = 1f;
        [SerializeField] private bool _debugFieldActive;
        [SerializeField] private float _debugFieldDensity01;
        [SerializeField] private bool _debugEntangled;
        [SerializeField] private float _debugEntanglement01;
        [SerializeField] private Vector3 _debugEntanglementAnchorWS;

        private int _activeContacts;
        private float _targetSpeedMultiplier = 1f;
        private float _targetDragMultiplier = 1f;
        private float _currentSpeedMultiplier = 1f;
        private float _currentDragMultiplier = 1f;
        private float _exitGraceTimer;
        private bool _fieldActive;
        private float _fieldSpeedMultiplier = 1f;
        private float _fieldDragMultiplier = 1f;
        private float _fieldDensity01;
        private Vector3 _fieldEntanglementAnchorWS;
        private float _fieldEntanglement01;
        private float _currentEntanglement01;
        private Vector3 _currentEntanglementAnchorWS;
        private float _entanglementShakeTime;
        private Vector3 _cameraLocalOffset;
        private float _cameraPitchOffset;
        private float _cameraRollOffset;

        private const float BlendSpeedFloor = 0.01f;
        private const float BlendDenominatorFloor = 0.0001f;
        private const float PadeOneTwelfth = 0.0833333333f;
        private const float InvTwoPi = 0.1591549431f;
        private const float HalfPi = 1.5707963268f;

        /// <summary>
        /// Gets the current sticky max-speed multiplier.
        /// </summary>
        public float SpeedMultiplier => ResolveSpeedMultiplier(_currentSpeedMultiplier);

        /// <summary>
        /// Gets the current sticky drag multiplier.
        /// </summary>
        public float DragMultiplier => ResolveDragMultiplier(_currentDragMultiplier);

        /// <summary>
        /// Gets the current normalized entanglement tension.
        /// </summary>
        internal float Entanglement01 => Resolve01(_currentEntanglement01);

        /// <summary>
        /// Gets the current world-space entanglement anchor.
        /// </summary>
        internal Vector3 EntanglementAnchorWS => IsFiniteVector3(_currentEntanglementAnchorWS) ? _currentEntanglementAnchorWS : Vector3.zero;

        /// <summary>
        /// Gets the current local camera shake offset.
        /// </summary>
        internal Vector3 CameraLocalOffset => IsFiniteVector3(_cameraLocalOffset) ? _cameraLocalOffset : Vector3.zero;

        /// <summary>
        /// Gets the current additive camera pitch offset in degrees.
        /// </summary>
        internal float CameraPitchOffset => math.isfinite(_cameraPitchOffset) ? _cameraPitchOffset : 0f;

        /// <summary>
        /// Gets the current additive camera roll offset in degrees.
        /// </summary>
        internal float CameraRollOffset => math.isfinite(_cameraRollOffset) ? _cameraRollOffset : 0f;

        /// <summary>
        /// Increments the active sticky-contact count when entering a sargassum zone.
        /// </summary>
        /// <param name="speedMultiplier">Target max-speed multiplier inside the zone.</param>
        /// <param name="dragMultiplier">Target drag multiplier inside the zone.</param>
        public void EnterZone(float speedMultiplier, float dragMultiplier)
        {
            if (_activeContacts < int.MaxValue)
                _activeContacts++;

            RegisterInfluence(speedMultiplier, dragMultiplier);
            SyncDebugState();
        }

        /// <summary>
        /// Refreshes the sticky influence while a collider stays inside a sargassum zone.
        /// </summary>
        /// <param name="speedMultiplier">Target max-speed multiplier inside the zone.</param>
        /// <param name="dragMultiplier">Target drag multiplier inside the zone.</param>
        public void StayZone(float speedMultiplier, float dragMultiplier)
        {
            RegisterInfluence(speedMultiplier, dragMultiplier);
            SyncDebugState();
        }

        /// <summary>
        /// Decrements the active sticky-contact count when leaving a sargassum zone.
        /// </summary>
        public void ExitZone()
        {
            if (_activeContacts > 0)
                _activeContacts--;

            if (_activeContacts <= 0)
            {
                _activeContacts = 0;
                _exitGraceTimer = ResolveNonNegative(exitGraceTime, 0.1f);
            }

            SyncDebugState();
        }

        /// <summary>
        /// Publishes the current world-space sargassum field sample for this frame.
        /// </summary>
        /// <param name="active">True while the player intersects meaningful global sargassum density.</param>
        /// <param name="speedMultiplier">Resolved speed multiplier produced by the global density field.</param>
        /// <param name="dragMultiplier">Resolved drag multiplier produced by the global density field.</param>
        /// <param name="density01">Normalized 0..1 density value used for diagnostics.</param>
        public void ApplyFieldInfluence(bool active, float speedMultiplier, float dragMultiplier, float density01)
        {
            ApplyDetailedFieldInfluence(active, speedMultiplier, dragMultiplier, density01, _currentEntanglementAnchorWS, 0f);
        }

        /// <summary>
        /// Publishes the current world-space sargassum field sample together with entanglement data.
        /// </summary>
        /// <param name="active">True while the player intersects meaningful global sargassum density.</param>
        /// <param name="speedMultiplier">Resolved speed multiplier produced by the global density field.</param>
        /// <param name="dragMultiplier">Resolved drag multiplier produced by the global density field.</param>
        /// <param name="density01">Normalized 0..1 density value used for diagnostics.</param>
        /// <param name="entanglementAnchorWS">World-space snare anchor used by locomotion to pull the body back into the mass.</param>
        /// <param name="entanglement01">Normalized snare tension resolved by the global field sample.</param>
        internal void ApplyDetailedFieldInfluence(
            bool active,
            float speedMultiplier,
            float dragMultiplier,
            float density01,
            Vector3 entanglementAnchorWS,
            float entanglement01)
        {
            bool hasFiniteAnchor = IsFiniteVector3(entanglementAnchorWS);
            _fieldActive = active;
            _fieldSpeedMultiplier = active ? ResolveSpeedMultiplier(speedMultiplier) : 1f;
            _fieldDragMultiplier = active ? ResolveDragMultiplier(dragMultiplier) : 1f;
            _fieldDensity01 = active ? Resolve01(density01) : 0f;
            _fieldEntanglementAnchorWS = active && hasFiniteAnchor ? entanglementAnchorWS : EntanglementAnchorWS;
            _fieldEntanglement01 = active && hasFiniteAnchor ? Resolve01(entanglement01) : 0f;
            SyncDebugState();
        }

        /// <summary>
        /// Advances the sticky-drag and entanglement blend for the current fixed-step.
        /// </summary>
        /// <param name="deltaTime">Fixed-step delta time supplied by locomotion.</param>
        public void Advance(float deltaTime)
        {
            NormalizeRuntimeState();
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            bool shouldRecover = _activeContacts <= 0 && !_fieldActive;
            if (shouldRecover)
            {
                if (_exitGraceTimer > 0f)
                {
                    _exitGraceTimer -= safeDeltaTime;
                    if (_exitGraceTimer < 0f)
                        _exitGraceTimer = 0f;
                }

                if (_exitGraceTimer <= 0f)
                {
                    _targetSpeedMultiplier = 1f;
                    _targetDragMultiplier = 1f;
                }
            }
            else
            {
                _exitGraceTimer = ResolveNonNegative(exitGraceTime, 0.1f);
            }

            float resolvedTargetSpeedMultiplier = _targetSpeedMultiplier;
            float resolvedTargetDragMultiplier = _targetDragMultiplier;
            if (_fieldActive)
            {
                resolvedTargetSpeedMultiplier = math.min(resolvedTargetSpeedMultiplier, _fieldSpeedMultiplier);
                resolvedTargetDragMultiplier = math.max(resolvedTargetDragMultiplier, _fieldDragMultiplier);
            }

            float blendSpeed = shouldRecover ? exitBlendSpeed : enterBlendSpeed;
            float blendT = FastExpDecayBlend01(blendSpeed, safeDeltaTime);
            _currentSpeedMultiplier = math.lerp(_currentSpeedMultiplier, resolvedTargetSpeedMultiplier, blendT);
            _currentDragMultiplier = math.lerp(_currentDragMultiplier, resolvedTargetDragMultiplier, blendT);

            float entanglementBlendT = FastExpDecayBlend01(entanglementBlendSpeed, safeDeltaTime);
            _currentEntanglement01 = math.lerp(_currentEntanglement01, _fieldEntanglement01, entanglementBlendT);
            _currentEntanglementAnchorWS = LerpVector3(_currentEntanglementAnchorWS, _fieldEntanglementAnchorWS, entanglementBlendT);
            AdvanceCameraTension(safeDeltaTime);
            SyncDebugState();
        }

        private void OnDisable()
        {
            _activeContacts = 0;
            _exitGraceTimer = 0f;
            _targetSpeedMultiplier = 1f;
            _targetDragMultiplier = 1f;
            _currentSpeedMultiplier = 1f;
            _currentDragMultiplier = 1f;
            _fieldActive = false;
            _fieldSpeedMultiplier = 1f;
            _fieldDragMultiplier = 1f;
            _fieldDensity01 = 0f;
            _fieldEntanglementAnchorWS = Vector3.zero;
            _fieldEntanglement01 = 0f;
            _currentEntanglement01 = 0f;
            _currentEntanglementAnchorWS = Vector3.zero;
            _entanglementShakeTime = 0f;
            _cameraLocalOffset = Vector3.zero;
            _cameraPitchOffset = 0f;
            _cameraRollOffset = 0f;
            SyncDebugState();
        }

        private void RegisterInfluence(float speedMultiplier, float dragMultiplier)
        {
            NormalizeRuntimeState();
            _targetSpeedMultiplier = math.min(_targetSpeedMultiplier, ResolveSpeedMultiplier(speedMultiplier));
            _targetDragMultiplier = math.max(_targetDragMultiplier, ResolveDragMultiplier(dragMultiplier));
            _exitGraceTimer = ResolveNonNegative(exitGraceTime, 0.1f);
        }

        internal void ApplyOriginShiftOffset(Vector3 shiftOffset)
        {
            if (!IsFiniteVector3(shiftOffset) || shiftOffset.sqrMagnitude <= 0.000001f)
                return;

            NormalizeRuntimeState();
            _fieldEntanglementAnchorWS -= shiftOffset;
            _currentEntanglementAnchorWS -= shiftOffset;
            _debugEntanglementAnchorWS = _currentEntanglementAnchorWS;
        }

        private void SyncDebugState()
        {
            _debugActiveContacts = _activeContacts;
            _debugTargetSpeedMultiplier = ResolveSpeedMultiplier(_targetSpeedMultiplier);
            _debugTargetDragMultiplier = ResolveDragMultiplier(_targetDragMultiplier);
            _debugCurrentSpeedMultiplier = SpeedMultiplier;
            _debugCurrentDragMultiplier = DragMultiplier;
            _debugFieldActive = _fieldActive;
            _debugFieldDensity01 = Resolve01(_fieldDensity01);
            _debugEntangled = Entanglement01 > 0.01f;
            _debugEntanglement01 = Entanglement01;
            _debugEntanglementAnchorWS = EntanglementAnchorWS;
        }

        private void AdvanceCameraTension(float deltaTime)
        {
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            float tension = Resolve01(_currentEntanglement01);
            if (tension <= 0.0001f)
            {
                float recoverBlendT = FastExpDecayBlend01(entanglementBlendSpeed, safeDeltaTime);
                _cameraLocalOffset = LerpVector3(_cameraLocalOffset, Vector3.zero, recoverBlendT);
                _cameraPitchOffset = math.lerp(_cameraPitchOffset, 0f, recoverBlendT);
                _cameraRollOffset = math.lerp(_cameraRollOffset, 0f, recoverBlendT);
                return;
            }

            float safeFrequency = ResolveNonNegative(cameraShakeFrequency, 7.5f);
            _entanglementShakeTime += safeDeltaTime * math.lerp(safeFrequency * 0.65f, safeFrequency * 1.4f, tension);
            if (!math.isfinite(_entanglementShakeTime))
                _entanglementShakeTime = 0f;

            float sinA = TriangleWaveSigned(_entanglementShakeTime * InvTwoPi);
            float sinB = TriangleWaveSigned((_entanglementShakeTime * 1.73f + 0.67f) * InvTwoPi);
            float cosA = TriangleWaveSigned((_entanglementShakeTime * 1.21f + 1.14f + HalfPi) * InvTwoPi);
            float amplitude = ResolveNonNegative(cameraShakeAmplitude, 0f) * tension;
            _cameraLocalOffset.x = sinA * amplitude;
            _cameraLocalOffset.y = cosA * (amplitude * 0.42f);
            _cameraLocalOffset.z = -math.abs(sinB) * (amplitude * 0.75f);
            _cameraPitchOffset = sinB * ResolveNonNegative(cameraPitchAmplitude, 0f) * tension;
            _cameraRollOffset = cosA * ResolveNonNegative(cameraRollAmplitude, 0f) * tension;
        }

        private static float FastExpDecayBlend01(float blendSpeed, float deltaTime)
        {
            float safeBlendSpeed = math.isfinite(blendSpeed) ? math.max(BlendSpeedFloor, blendSpeed) : BlendSpeedFloor;
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            float rawX = safeBlendSpeed * safeDeltaTime;
            float x = math.isfinite(rawX) ? math.min(rawX, 64f) : 64f;
            float x2 = x * x;
            float numerator = 1f - 0.5f * x + x2 * PadeOneTwelfth;
            float denominator = 1f + 0.5f * x + x2 * PadeOneTwelfth;
            return math.saturate(1f - numerator / math.max(BlendDenominatorFloor, denominator));
        }

        private static Vector3 LerpVector3(Vector3 current, Vector3 target, float t)
        {
            Vector3 safeCurrent = IsFiniteVector3(current) ? current : Vector3.zero;
            Vector3 safeTarget = IsFiniteVector3(target) ? target : safeCurrent;
            float safeT = Resolve01(t);
            return new Vector3(
                math.lerp(safeCurrent.x, safeTarget.x, safeT),
                math.lerp(safeCurrent.y, safeTarget.y, safeT),
                math.lerp(safeCurrent.z, safeTarget.z, safeT));
        }

        private static float TriangleWaveSigned(float phase)
        {
            if (!math.isfinite(phase))
                return 0f;

            float cycle = phase - math.floor(phase);
            return 1f - math.abs((cycle * 4f) - 2f);
        }

        private void NormalizeRuntimeState()
        {
            _targetSpeedMultiplier = ResolveSpeedMultiplier(_targetSpeedMultiplier);
            _targetDragMultiplier = ResolveDragMultiplier(_targetDragMultiplier);
            _currentSpeedMultiplier = ResolveSpeedMultiplier(_currentSpeedMultiplier);
            _currentDragMultiplier = ResolveDragMultiplier(_currentDragMultiplier);
            _fieldSpeedMultiplier = ResolveSpeedMultiplier(_fieldSpeedMultiplier);
            _fieldDragMultiplier = ResolveDragMultiplier(_fieldDragMultiplier);
            _fieldDensity01 = Resolve01(_fieldDensity01);
            _fieldEntanglementAnchorWS = IsFiniteVector3(_fieldEntanglementAnchorWS) ? _fieldEntanglementAnchorWS : Vector3.zero;
            _fieldEntanglement01 = Resolve01(_fieldEntanglement01);
            _currentEntanglement01 = Resolve01(_currentEntanglement01);
            _currentEntanglementAnchorWS = IsFiniteVector3(_currentEntanglementAnchorWS) ? _currentEntanglementAnchorWS : Vector3.zero;
            _cameraLocalOffset = IsFiniteVector3(_cameraLocalOffset) ? _cameraLocalOffset : Vector3.zero;
            _cameraPitchOffset = math.isfinite(_cameraPitchOffset) ? _cameraPitchOffset : 0f;
            _cameraRollOffset = math.isfinite(_cameraRollOffset) ? _cameraRollOffset : 0f;
            _exitGraceTimer = ResolveNonNegative(_exitGraceTimer, 0f);
        }

        private static float ResolveSpeedMultiplier(float value)
        {
            return math.isfinite(value) ? math.clamp(value, 0.1f, 1f) : 1f;
        }

        private static float ResolveDragMultiplier(float value)
        {
            return math.isfinite(value) ? math.max(1f, value) : 1f;
        }

        private static float Resolve01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float ResolveNonNegative(float value, float fallback)
        {
            float safeFallback = math.isfinite(fallback) ? math.max(0f, fallback) : 0f;
            return math.isfinite(value) ? math.max(0f, value) : safeFallback;
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }
    }
}
