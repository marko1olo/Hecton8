using UnityEngine;
using Hecton8.Core;
using Hecton8.Bootstrap;
using Hecton8.Gameplay;

namespace Hecton8.World
{
    /// <summary>
    /// Master Grade Flora Interaction Manager.
    /// Drives global shader variables for high-fidelity vegetation interaction (kelp, sea grass, corals).
    /// Uses Zero-GC architecture and central Tick system.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-105)] // After Player, before rendering
    public sealed class FloraInteractionManager : MonoBehaviour, ITickable
    {
        // ─────────────────────────────────────────────────────────────────────────────
        // INSPECTOR SETTINGS
        // ─────────────────────────────────────────────────────────────────────────────

        [Header("── Prop Wash Settings ────────────────────────")]
        [SerializeField, Range(1f, 10f)]
        [Tooltip("Base radius of interaction around the player/vehicle.")]
        private float _baseRadius = 3.5f;

        [SerializeField, Range(0f, 5f)]
        [Tooltip("How much velocity increases the interaction radius.")]
        private float _velocityRadiusMultiplier = 0.45f;

        [SerializeField, Range(0.1f, 10f)]
        [Tooltip("Max force applied to flora vertices.")]
        private float _maxInteractionForce = 4.2f;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Smoothing speed for position updates.")]
        private float _positionSmoothSpeed = 12f;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Smoothing speed for force/radius updates.")]
        private float _intensitySmoothSpeed = 8f;

        // ─────────────────────────────────────────────────────────────────────────────
        // PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────────────

        private static readonly int _PropWashPosId = Shader.PropertyToID("_HectonPropWashPosition");
        private static readonly int _PropWashForceId = Shader.PropertyToID("_HectonPropWashForce");

        private Vector3 _smoothPosition;
        private float _smoothRadius;
        private float _smoothForce;
        private Rigidbody _playerRb;
        private bool _isRegistered;

        // ─────────────────────────────────────────────────────────────────────────────
        // LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (GameTickManager.Instance != null && !_isRegistered)
            {
                GameTickManager.Instance.Register(this);
                _isRegistered = true;
            }
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance != null && _isRegistered)
            {
                GameTickManager.Instance.Unregister(this);
                _isRegistered = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // ITICKABLE
        // ─────────────────────────────────────────────────────────────────────────────

        public void Tick(float deltaTime)
        {
            if (!SceneBootstrap.IsGameReady) return;

            Transform playerT = SceneBootstrap.CurrentPlayerTransform;
            if (playerT == null) return;

            // Lazy cache player Rigidbody
            if (_playerRb == null)
            {
                _playerRb = playerT.GetComponent<Rigidbody>();
                if (_playerRb == null) return;
                
                // Initialize smooth state to player pos to avoid startup pops
                _smoothPosition = _playerRb.position;
            }

            // 1. Calculate target values
            Vector3 targetPos = _playerRb.position;
            float velocityMagnitude = _playerRb.linearVelocity.magnitude;
            
            float targetRadius = _baseRadius + (velocityMagnitude * _velocityRadiusMultiplier);
            float targetForce = Mathf.Clamp(velocityMagnitude * 0.85f, 0f, _maxInteractionForce);

            // 2. Smooth updates
            _smoothPosition = Vector3.Lerp(_smoothPosition, targetPos, deltaTime * _positionSmoothSpeed);
            _smoothRadius = Mathf.Lerp(_smoothRadius, targetRadius, deltaTime * _intensitySmoothSpeed);
            _smoothForce = Mathf.Lerp(_smoothForce, targetForce, deltaTime * _intensitySmoothSpeed);

            // 3. Push to Global Shader variables
            // xyz = position, w = radius
            Vector4 posRadius = new Vector4(_smoothPosition.x, _smoothPosition.y, _smoothPosition.z, _smoothRadius);
            Shader.SetGlobalVector(_PropWashPosId, posRadius);
            Shader.SetGlobalFloat(_PropWashForceId, _smoothForce);
        }
    }
}
