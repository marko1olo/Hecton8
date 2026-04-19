using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Physics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// Publishes global flora interaction and environment shader inputs.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-105)]
    public sealed class FloraInteractionManager : MonoBehaviour, ITickable
    {
        private const int MaxPublishedInteractionPoints = 12;
        private const int MaxQueryColliders = 32;
        private const int InteractionPointStride = 16;

        private static readonly int _PropWashPosId = Shader.PropertyToID("_HectonPropWashPosition");
        private static readonly int _PropWashForceId = Shader.PropertyToID("_HectonPropWashForce");
        private static readonly int _InteractionBufferId = Shader.PropertyToID("_HectonFloraInteractionPoints");
        private static readonly int _InteractionCountId = Shader.PropertyToID("_HectonFloraInteractionCount");
        private static readonly int _VegetationFogColorId = Shader.PropertyToID("_HectonVegetationFogColor");
        private static readonly int _VegetationAmbientColorId = Shader.PropertyToID("_HectonVegetationAmbientColor");
        private static readonly int _VegetationDepthId = Shader.PropertyToID("_HectonVegetationDepth");
        private static readonly int _VegetationLightFactorId = Shader.PropertyToID("_HectonVegetationLightFactor");
        private static readonly int _VegetationTurbidityId = Shader.PropertyToID("_HectonVegetationTurbidity");
        private static readonly int _VegetationWaterLevelId = Shader.PropertyToID("_HectonVegetationWaterLevel");
        private static readonly int _VegetationCurrentVectorId = Shader.PropertyToID("_HectonVegetationCurrentVector");
        private static readonly int _VegetationCurrentStrengthId = Shader.PropertyToID("_HectonVegetationCurrentStrength");
        private static readonly int _VegetationCurrentNoiseScaleId = Shader.PropertyToID("_HectonVegetationCurrentNoiseScale");
        private static readonly int _VegetationCurrentTimeScaleId = Shader.PropertyToID("_HectonVegetationCurrentTimeScale");
        private static readonly int _VegetationCurrentVerticalFactorId = Shader.PropertyToID("_HectonVegetationCurrentVerticalFactor");

        [Header("Interaction")]
        [SerializeField, Range(1f, 10f)]
        [Tooltip("Base radius around the player influence point.")]
        private float _baseRadius = 3.5f;

        [SerializeField, Range(0f, 5f)]
        [Tooltip("How much player speed increases the published interaction radius.")]
        private float _velocityRadiusMultiplier = 0.45f;

        [SerializeField, Range(0.1f, 10f)]
        [Tooltip("Maximum player interaction force pushed into legacy flora shaders.")]
        private float _maxInteractionForce = 4.2f;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Position smoothing speed for the player interaction point.")]
        private float _positionSmoothSpeed = 12f;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Radius and force smoothing speed for the player interaction point.")]
        private float _intensitySmoothSpeed = 8f;

        [Header("Dynamic Interactors")]
        [SerializeField, Range(1, MaxPublishedInteractionPoints)]
        [Tooltip("Maximum number of interaction points published to the global flora buffer, including the player.")]
        private int _maxInteractionPoints = 12;

        [SerializeField, Range(4f, 20f)]
        [Tooltip("Search radius for dynamic rigidbodies that should bend flora away from themselves.")]
        private float _dynamicInteractionRadius = 15f;

        [SerializeField, Range(0.5f, 8f)]
        [Tooltip("Base radius used for non-player rigidbody interaction points.")]
        private float _dynamicObjectBaseRadius = 2.25f;

        [SerializeField, Range(0f, 1.5f)]
        [Tooltip("Extra dynamic-object radius per meter per second of linear speed.")]
        private float _dynamicVelocityRadiusMultiplier = 0.18f;

        [SerializeField]
        [Tooltip("Physics layers considered for dynamic flora interaction queries.")]
        private LayerMask _dynamicInteractionMask = ~0;

        private Vector3 _smoothPosition;
        private float _smoothRadius;
        private float _smoothForce;
        private Transform _playerTransform;
        private Rigidbody _playerRb;
        private Vector3 _lastPlayerPosition;
        private bool _hasLastPlayerPosition;
        private bool _isRegistered;

        private Vector4[] _interactionPoints;
        private Collider[] _interactionColliders;
        private Rigidbody[] _interactionBodies;
        private ComputeBuffer _interactionBuffer;

        private void Awake()
        {
            _maxInteractionPoints = Mathf.Clamp(_maxInteractionPoints, 1, MaxPublishedInteractionPoints);

            // COLD ALLOC: Vector4[_maxInteractionPoints] - global flora interaction payload - owner: FloraInteractionManager
            _interactionPoints = new Vector4[_maxInteractionPoints];
            // COLD ALLOC: Collider[32] - NonAlloc interaction query results - owner: FloraInteractionManager
            _interactionColliders = new Collider[MaxQueryColliders];
            // COLD ALLOC: Rigidbody[32] - duplicate suppression for interaction query results - owner: FloraInteractionManager
            _interactionBodies = new Rigidbody[MaxQueryColliders];
            // COLD ALLOC: ComputeBuffer[_maxInteractionPoints] - global flora interaction StructuredBuffer - owner: FloraInteractionManager
            _interactionBuffer = new ComputeBuffer(_maxInteractionPoints, InteractionPointStride, ComputeBufferType.Structured);

            Shader.SetGlobalBuffer(_InteractionBufferId, _interactionBuffer);
            ResetInteractionGlobals();
            PublishEnvironmentGlobals();
        }

        private void OnEnable()
        {
            if (_interactionBuffer != null)
                Shader.SetGlobalBuffer(_InteractionBufferId, _interactionBuffer);

            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            ResetInteractionGlobals();
        }

        private void OnDestroy()
        {
            TryUnregister();
            ResetInteractionGlobals();

            if (_interactionBuffer != null)
            {
                _interactionBuffer.Release();
                _interactionBuffer = null;
            }
        }

        /// <summary>
        /// Updates published flora interaction and environment globals.
        /// </summary>
        /// <param name="deltaTime">Current frame delta.</param>
        public void Tick(float deltaTime)
        {
            PublishEnvironmentGlobals();

            if (!BootstrapState.IsGameReady)
            {
                ResetInteractionGlobals();
                return;
            }

            Transform runtimePlayerTransform = BootstrapState.CurrentPlayerTransform;
            if (runtimePlayerTransform == null)
            {
                ResetInteractionGlobals();
                return;
            }

            ResolvePlayerState(runtimePlayerTransform);

            Vector3 targetPosition = runtimePlayerTransform.position;
            float velocityMagnitude = ResolvePlayerSpeed(targetPosition, deltaTime);

            float targetRadius = _baseRadius + velocityMagnitude * _velocityRadiusMultiplier;
            float targetForce = Mathf.Clamp(velocityMagnitude * 0.85f, 0f, _maxInteractionForce);

            _smoothPosition = Vector3.Lerp(_smoothPosition, targetPosition, deltaTime * _positionSmoothSpeed);
            _smoothRadius = Mathf.Lerp(_smoothRadius, targetRadius, deltaTime * _intensitySmoothSpeed);
            _smoothForce = Mathf.Lerp(_smoothForce, targetForce, deltaTime * _intensitySmoothSpeed);

            int interactionCount = 0;
            interactionCount = AppendInteractionPoint(_smoothPosition, _smoothRadius, interactionCount);
            interactionCount = CollectDynamicInteractionPoints(targetPosition, interactionCount);

            Shader.SetGlobalVector(
                _PropWashPosId,
                new Vector4(_smoothPosition.x, _smoothPosition.y, _smoothPosition.z, _smoothRadius));
            Shader.SetGlobalFloat(_PropWashForceId, _smoothForce);

            if (_interactionBuffer != null && interactionCount > 0)
            {
                _interactionBuffer.SetData(_interactionPoints, 0, 0, interactionCount);
                Shader.SetGlobalBuffer(_InteractionBufferId, _interactionBuffer);
                Shader.SetGlobalInt(_InteractionCountId, interactionCount);
                return;
            }

            Shader.SetGlobalInt(_InteractionCountId, 0);
        }

        private void ResolvePlayerState(Transform runtimePlayerTransform)
        {
            if (_playerTransform == runtimePlayerTransform)
                return;

            _playerTransform = runtimePlayerTransform;
            _playerRb = runtimePlayerTransform.GetComponent<Rigidbody>();
            _smoothPosition = runtimePlayerTransform.position;
            _lastPlayerPosition = runtimePlayerTransform.position;
            _hasLastPlayerPosition = true;
        }

        private float ResolvePlayerSpeed(Vector3 targetPosition, float deltaTime)
        {
            if (_playerRb != null)
            {
                _lastPlayerPosition = targetPosition;
                _hasLastPlayerPosition = true;
                return _playerRb.linearVelocity.magnitude;
            }

            if (!_hasLastPlayerPosition)
            {
                _lastPlayerPosition = targetPosition;
                _hasLastPlayerPosition = true;
                return 0f;
            }

            float speed = deltaTime > 0.0001f
                ? (targetPosition - _lastPlayerPosition).magnitude / deltaTime
                : 0f;
            _lastPlayerPosition = targetPosition;
            return speed;
        }

        private int CollectDynamicInteractionPoints(Vector3 targetPosition, int interactionCount)
        {
            int hitCount = global::UnityEngine.Physics.OverlapSphereNonAlloc(
                targetPosition,
                _dynamicInteractionRadius,
                _interactionColliders,
                _dynamicInteractionMask,
                QueryTriggerInteraction.Ignore);

            int uniqueBodyCount = 0;
            for (int i = 0; i < hitCount && interactionCount < _maxInteractionPoints; i++)
            {
                Collider hitCollider = _interactionColliders[i];
                if (hitCollider == null)
                    continue;

                Rigidbody hitBody = hitCollider.attachedRigidbody;
                if (hitBody == null || hitBody == _playerRb || hitBody.transform == _playerTransform)
                    continue;

                bool duplicate = false;
                for (int j = 0; j < uniqueBodyCount; j++)
                {
                    if (_interactionBodies[j] == hitBody)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (duplicate)
                    continue;

                if (uniqueBodyCount < _interactionBodies.Length)
                    _interactionBodies[uniqueBodyCount++] = hitBody;

                float radius = _dynamicObjectBaseRadius + hitBody.linearVelocity.magnitude * _dynamicVelocityRadiusMultiplier;
                interactionCount = AppendInteractionPoint(hitBody.worldCenterOfMass, radius, interactionCount);
            }

            return interactionCount;
        }

        private int AppendInteractionPoint(Vector3 position, float radius, int interactionCount)
        {
            if (interactionCount >= _maxInteractionPoints)
                return interactionCount;

            _interactionPoints[interactionCount] = new Vector4(
                position.x,
                position.y,
                position.z,
                Mathf.Max(0.05f, radius));
            return interactionCount + 1;
        }

        private void PublishEnvironmentGlobals()
        {
            HectonUnderwaterVisuals underwaterVisuals = HectonUnderwaterVisuals.ActiveRuntimeInstance;
            float depth = underwaterVisuals != null ? underwaterVisuals.CurrentDepth : 0f;
            float lightFactor = underwaterVisuals != null ? underwaterVisuals.CurrentLightFactor : 1f;
            float turbidity = underwaterVisuals != null ? underwaterVisuals.CurrentTurbidity : 0f;

            HectonFluidEngine fluidEngine = HectonFluidEngine.Instance;
            float waterLevel = fluidEngine != null ? fluidEngine.WaterLevel : 0f;
            Vector3 currentVector = fluidEngine != null ? fluidEngine.CurrentVector : Vector3.zero;
            float currentStrength = fluidEngine != null ? fluidEngine.CurrentStrength : 0f;
            float currentNoiseScale = fluidEngine != null && fluidEngine.EnablePhantomCurrent ? fluidEngine.CurrentNoiseScale : 0f;
            float currentTimeScale = fluidEngine != null && fluidEngine.EnablePhantomCurrent ? fluidEngine.CurrentTimeScale : 0f;
            float currentVerticalFactor = fluidEngine != null && fluidEngine.EnablePhantomCurrent ? fluidEngine.CurrentVerticalFactor : 0f;

            Shader.SetGlobalColor(_VegetationFogColorId, RenderSettings.fogColor);
            Shader.SetGlobalColor(_VegetationAmbientColorId, ResolveAmbientColor());
            Shader.SetGlobalFloat(_VegetationDepthId, depth);
            Shader.SetGlobalFloat(_VegetationLightFactorId, lightFactor);
            Shader.SetGlobalFloat(_VegetationTurbidityId, turbidity);
            Shader.SetGlobalFloat(_VegetationWaterLevelId, waterLevel);
            Shader.SetGlobalVector(
                _VegetationCurrentVectorId,
                new Vector4(currentVector.x, currentVector.y, currentVector.z, 0f));
            Shader.SetGlobalFloat(_VegetationCurrentStrengthId, currentStrength);
            Shader.SetGlobalFloat(_VegetationCurrentNoiseScaleId, currentNoiseScale);
            Shader.SetGlobalFloat(_VegetationCurrentTimeScaleId, currentTimeScale);
            Shader.SetGlobalFloat(_VegetationCurrentVerticalFactorId, currentVerticalFactor);
        }

        private static Color ResolveAmbientColor()
        {
            switch (RenderSettings.ambientMode)
            {
                case AmbientMode.Flat:
                    return RenderSettings.ambientLight;
                case AmbientMode.Trilight:
                    return RenderSettings.ambientEquatorColor;
                default:
                    return RenderSettings.ambientSkyColor;
            }
        }

        private void ResetInteractionGlobals()
        {
            Shader.SetGlobalVector(_PropWashPosId, Vector4.zero);
            Shader.SetGlobalFloat(_PropWashForceId, 0f);
            Shader.SetGlobalInt(_InteractionCountId, 0);

            if (_interactionBuffer != null)
                Shader.SetGlobalBuffer(_InteractionBufferId, _interactionBuffer);
        }

        private void TryRegister()
        {
            if (_isRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register(this);
            _isRegistered = true;
        }

        private void TryUnregister()
        {
            if (!_isRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister(this);

            _isRegistered = false;
        }
    }
}
