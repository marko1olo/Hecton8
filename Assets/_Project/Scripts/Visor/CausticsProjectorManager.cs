using Hecton8.Atmosphere;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Hecton8.Visor
{
    /// <summary>
    /// Camera-local URP decal owner for animated shallow-water caustics around the active player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CausticsProjectorManager : MonoBehaviour, ITickable, ISlowTickable
    {
        [Header("── References ──────────────────")]
        [Tooltip("Authored URP decal material used for shallow caustic projection. Runtime material copies are forbidden.")]
        [SerializeField] private Material causticsDecalMaterial;

        [Tooltip("Optional explicit gameplay camera reference. Falls back to the player's child camera.")]
        [SerializeField] private Camera gameplayCamera;

        [Header("── Projection ──────────────────")]
        [Tooltip("Projected footprint size around the player camera.")]
        [SerializeField] private Vector3 projectorSize = new Vector3(36f, 18f, 36f);

        [Tooltip("Vertical offset above the camera from which the caustics projector casts downward.")]
        [SerializeField, Min(0f)] private float projectorHeightOffset = 10f;

        [Tooltip("Maximum draw distance for the caustics decal.")]
        [SerializeField, Min(1f)] private float projectorDrawDistance = 70f;

        [Tooltip("UV panning speed along X for the caustic animation.")]
        [SerializeField] private float uvPanSpeedX = 0.038f;

        [Tooltip("UV panning speed along Y for the caustic animation.")]
        [SerializeField] private float uvPanSpeedY = 0.024f;

        [Tooltip("Base UV scale applied to the caustics decal.")]
        [SerializeField] private Vector2 uvScale = new Vector2(1.15f, 1.15f);

        [Header("── Depth Gating ──────────────────")]
        [Tooltip("Depth where shallow caustics are fully visible.")]
        [SerializeField, Min(0f)] private float causticsFadeInDepth = 1.5f;

        [Tooltip("Depth where caustics are fully faded out.")]
        [SerializeField, Min(0f)] private float causticsFadeOutDepth = 110f;

        [Tooltip("Additional fade applied while storm electrical activity is high near the surface.")]
        [SerializeField, Range(0f, 1f)] private float stormFadePenalty = 0.28f;

        [Header("── Diagnostics ──────────────────")]
        [SerializeField] private float _debugFade01;
        [SerializeField] private float _debugDepthMeters;

        private bool _registeredTick;
        private bool _registeredSlowTick;
        private HectonSurvivalSystem _survivalSystem;
        private Transform _playerTransform;
        private Transform _projectorTransform;
        private DecalProjector _projector;
        private Vector2 _uvBias;
        private float _fade01;

        private void Awake()
        {
            _playerTransform = transform;
            TryResolveDependencies();
            EnsureProjector();
        }

        private void OnEnable()
        {
            TryRegisterTickHandlers();
        }

        private void OnDisable()
        {
            TryUnregisterTickHandlers();

            if (_projector != null)
            {
                _projector.fadeFactor = 0f;
                _projector.enabled = false;
            }
        }

        private void OnDestroy()
        {
            TryUnregisterTickHandlers();
        }

        /// <summary>
        /// Advances UV panning and positions the shallow caustics projector around the camera.
        /// </summary>
        /// <param name="deltaTime">Tick delta supplied by <see cref="GameTickManager"/>.</param>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || _projector == null || gameplayCamera == null)
                return;

            Vector3 cameraPosition = gameplayCamera.transform.position;
            Vector3 projectorPosition = new Vector3(cameraPosition.x, cameraPosition.y + projectorHeightOffset, cameraPosition.z);
            Quaternion projectorRotation = Quaternion.Euler(90f, gameplayCamera.transform.eulerAngles.y, 0f);
            _projectorTransform.SetPositionAndRotation(projectorPosition, projectorRotation);

            _uvBias.x += uvPanSpeedX * deltaTime;
            _uvBias.y += uvPanSpeedY * deltaTime;
            if (_uvBias.x > 1f || _uvBias.x < -1f)
                _uvBias.x -= Mathf.Floor(_uvBias.x);
            if (_uvBias.y > 1f || _uvBias.y < -1f)
                _uvBias.y -= Mathf.Floor(_uvBias.y);

            _projector.uvBias = _uvBias;
            _projector.fadeFactor = _fade01;
            _projector.enabled = _fade01 > 0.001f && causticsDecalMaterial != null;
        }

        /// <summary>
        /// Refreshes camera references and depth-driven fade without touching save state or authored profiles.
        /// </summary>
        public void SlowTick()
        {
            TryResolveDependencies();

            float depthMeters = _survivalSystem != null ? Mathf.Max(0f, _survivalSystem.Depth) : 0f;
            float fadeIn = Mathf.Clamp01(depthMeters / Mathf.Max(0.01f, causticsFadeInDepth));
            float fadeOut = 1f - Mathf.Clamp01((depthMeters - causticsFadeInDepth) / Mathf.Max(0.01f, causticsFadeOutDepth - causticsFadeInDepth));
            float fade = fadeIn * fadeOut;

            DepthZoneProfile depthZone = DepthZoneDirector.Instance != null ? DepthZoneDirector.Instance.CurrentZone : null;
            if (depthZone != null && depthZone.dangerLevel >= 0.75f)
                fade *= 0.7f;

            HectonSurfaceWeatherDirector weatherDirector = HectonSurfaceWeatherDirector.Instance;
            if (weatherDirector != null && depthMeters <= 80f)
                fade *= 1f - (weatherDirector.CurrentElectricalActivity * stormFadePenalty);

            _fade01 = Mathf.Clamp01(fade);
            _debugDepthMeters = depthMeters;
            _debugFade01 = _fade01;
        }

        private void TryResolveDependencies()
        {
            if (_playerTransform == null && SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
                _playerTransform = playerTransform;

            if (_survivalSystem == null)
            {
                if (_playerTransform != null)
                    _playerTransform.TryGetComponent(out _survivalSystem);

                if (_survivalSystem == null)
                    TryGetComponent(out _survivalSystem);
            }

            if (gameplayCamera == null && _playerTransform != null)
                gameplayCamera = _playerTransform.GetComponentInChildren<Camera>(true);
        }

        private void EnsureProjector()
        {
            if (_projector != null)
                return;

            GameObject projectorObject = new GameObject("__CausticsProjector"); // COLD ALLOC: one player-local decal projector for shallow caustics - owner: CausticsProjectorManager
            projectorObject.transform.SetParent(transform, false);
            _projectorTransform = projectorObject.transform;
            _projector = projectorObject.AddComponent<DecalProjector>();
            _projector.material = causticsDecalMaterial;
            _projector.size = projectorSize;
            _projector.drawDistance = projectorDrawDistance;
            _projector.fadeScale = 1f;
            _projector.startAngleFade = 180f;
            _projector.endAngleFade = 180f;
            _projector.uvScale = uvScale;
            _projector.uvBias = Vector2.zero;
            _projector.fadeFactor = 0f;
            _projector.enabled = false;
        }

        private void TryRegisterTickHandlers()
        {
            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            if (!_registeredTick)
            {
                tickManager.Register((ITickable)this);
                _registeredTick = true;
            }

            if (!_registeredSlowTick)
            {
                tickManager.Register((ISlowTickable)this);
                _registeredSlowTick = true;
            }
        }

        private void TryUnregisterTickHandlers()
        {
            GameTickManager tickManager = GameTickManager.Instance;

            if (_registeredTick)
            {
                if (tickManager != null)
                    tickManager.Unregister((ITickable)this);

                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                if (tickManager != null)
                    tickManager.Unregister((ISlowTickable)this);

                _registeredSlowTick = false;
            }
        }
    }
}
