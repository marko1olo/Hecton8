// ============================================================================
// HECTON-8 - SargassumMovementInfluence.cs
// Player-owned sticky-drag and entanglement receiver for dense floating sargassum.
// ============================================================================

namespace Hecton8.Gameplay
{
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

        /// <summary>
        /// Gets the current sticky max-speed multiplier.
        /// </summary>
        public float SpeedMultiplier => _currentSpeedMultiplier;

        /// <summary>
        /// Gets the current sticky drag multiplier.
        /// </summary>
        public float DragMultiplier => _currentDragMultiplier;

        /// <summary>
        /// Gets the current normalized entanglement tension.
        /// </summary>
        internal float Entanglement01 => _currentEntanglement01;

        /// <summary>
        /// Gets the current world-space entanglement anchor.
        /// </summary>
        internal Vector3 EntanglementAnchorWS => _currentEntanglementAnchorWS;

        /// <summary>
        /// Gets the current local camera shake offset.
        /// </summary>
        internal Vector3 CameraLocalOffset => _cameraLocalOffset;

        /// <summary>
        /// Gets the current additive camera pitch offset in degrees.
        /// </summary>
        internal float CameraPitchOffset => _cameraPitchOffset;

        /// <summary>
        /// Gets the current additive camera roll offset in degrees.
        /// </summary>
        internal float CameraRollOffset => _cameraRollOffset;

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
                _exitGraceTimer = exitGraceTime;
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
            _fieldActive = active;
            _fieldSpeedMultiplier = active ? Mathf.Clamp(speedMultiplier, 0.1f, 1f) : 1f;
            _fieldDragMultiplier = active ? Mathf.Max(1f, dragMultiplier) : 1f;
            _fieldDensity01 = active ? Mathf.Clamp01(density01) : 0f;
            _fieldEntanglementAnchorWS = active ? entanglementAnchorWS : _currentEntanglementAnchorWS;
            _fieldEntanglement01 = active ? Mathf.Clamp01(entanglement01) : 0f;
            SyncDebugState();
        }

        /// <summary>
        /// Advances the sticky-drag and entanglement blend for the current fixed-step.
        /// </summary>
        /// <param name="deltaTime">Fixed-step delta time supplied by locomotion.</param>
        public void Advance(float deltaTime)
        {
            bool shouldRecover = _activeContacts <= 0 && !_fieldActive;
            if (shouldRecover)
            {
                if (_exitGraceTimer > 0f)
                {
                    _exitGraceTimer -= deltaTime;
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
                _exitGraceTimer = exitGraceTime;
            }

            float resolvedTargetSpeedMultiplier = _targetSpeedMultiplier;
            float resolvedTargetDragMultiplier = _targetDragMultiplier;
            if (_fieldActive)
            {
                resolvedTargetSpeedMultiplier = Mathf.Min(resolvedTargetSpeedMultiplier, _fieldSpeedMultiplier);
                resolvedTargetDragMultiplier = Mathf.Max(resolvedTargetDragMultiplier, _fieldDragMultiplier);
            }

            float blendSpeed = shouldRecover ? exitBlendSpeed : enterBlendSpeed;
            float blendT = 1f - Mathf.Exp(-Mathf.Max(0.01f, blendSpeed) * deltaTime);
            _currentSpeedMultiplier = Mathf.Lerp(_currentSpeedMultiplier, resolvedTargetSpeedMultiplier, blendT);
            _currentDragMultiplier = Mathf.Lerp(_currentDragMultiplier, resolvedTargetDragMultiplier, blendT);

            float entanglementBlendT = 1f - Mathf.Exp(-Mathf.Max(0.01f, entanglementBlendSpeed) * deltaTime);
            _currentEntanglement01 = Mathf.Lerp(_currentEntanglement01, _fieldEntanglement01, entanglementBlendT);
            _currentEntanglementAnchorWS = Vector3.Lerp(_currentEntanglementAnchorWS, _fieldEntanglementAnchorWS, entanglementBlendT);
            AdvanceCameraTension(deltaTime);
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
            _targetSpeedMultiplier = Mathf.Min(_targetSpeedMultiplier, Mathf.Clamp(speedMultiplier, 0.1f, 1f));
            _targetDragMultiplier = Mathf.Max(_targetDragMultiplier, Mathf.Max(1f, dragMultiplier));
            _exitGraceTimer = exitGraceTime;
        }

        private void SyncDebugState()
        {
            _debugActiveContacts = _activeContacts;
            _debugTargetSpeedMultiplier = _targetSpeedMultiplier;
            _debugTargetDragMultiplier = _targetDragMultiplier;
            _debugCurrentSpeedMultiplier = _currentSpeedMultiplier;
            _debugCurrentDragMultiplier = _currentDragMultiplier;
            _debugFieldActive = _fieldActive;
            _debugFieldDensity01 = _fieldDensity01;
            _debugEntangled = _currentEntanglement01 > 0.01f;
            _debugEntanglement01 = _currentEntanglement01;
            _debugEntanglementAnchorWS = _currentEntanglementAnchorWS;
        }

        private void AdvanceCameraTension(float deltaTime)
        {
            float tension = Mathf.Clamp01(_currentEntanglement01);
            if (tension <= 0.0001f)
            {
                float recoverBlendT = 1f - Mathf.Exp(-Mathf.Max(0.01f, entanglementBlendSpeed) * deltaTime);
                _cameraLocalOffset = Vector3.Lerp(_cameraLocalOffset, Vector3.zero, recoverBlendT);
                _cameraPitchOffset = Mathf.Lerp(_cameraPitchOffset, 0f, recoverBlendT);
                _cameraRollOffset = Mathf.Lerp(_cameraRollOffset, 0f, recoverBlendT);
                return;
            }

            _entanglementShakeTime += deltaTime * Mathf.Lerp(cameraShakeFrequency * 0.65f, cameraShakeFrequency * 1.4f, tension);
            float sinA = Mathf.Sin(_entanglementShakeTime);
            float sinB = Mathf.Sin(_entanglementShakeTime * 1.73f + 0.67f);
            float cosA = Mathf.Cos(_entanglementShakeTime * 1.21f + 1.14f);
            float amplitude = cameraShakeAmplitude * tension;
            _cameraLocalOffset.x = sinA * amplitude;
            _cameraLocalOffset.y = cosA * (amplitude * 0.42f);
            _cameraLocalOffset.z = -Mathf.Abs(sinB) * (amplitude * 0.75f);
            _cameraPitchOffset = sinB * cameraPitchAmplitude * tension;
            _cameraRollOffset = cosA * cameraRollAmplitude * tension;
        }
    }
}
