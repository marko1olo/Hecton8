using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Two-quad meteor splash fake. No particle systems, no per-impact mesh mutation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MeteorSplashQuadVfx : MonoBehaviour, IPoolable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int SingleQuad = 1;
        private static readonly Matrix4x4[] s_splashMatrix = new Matrix4x4[SingleQuad];
        private static readonly Matrix4x4[] s_rippleMatrix = new Matrix4x4[SingleQuad];
        private static readonly Quaternion s_flatRippleRotation = new Quaternion(0.70710678f, 0f, 0f, 0.70710678f);

        [SerializeField] private Mesh splashQuadMesh;
        [SerializeField] private Material splashMaterial;
        [SerializeField] private Material distortionRippleMaterial;
        [SerializeField, Min(0.1f)] private float lifetimeSeconds = 1.4f;
        [SerializeField, Min(0.01f)] private float splashWidthMeters = 5.5f;
        [SerializeField, Min(0.01f)] private float splashHeightMeters = 2.4f;
        [SerializeField, Min(0.01f)] private float rippleDiameterMeters = 11f;

        private float _ageSeconds;
        private bool _active;
        private bool _registeredToDispatcher;
        private bool _hotSwapRegistered;
        private Transform _cachedTransform;
        private Vector3 _cachedOrigin;
        private Quaternion _cachedRotation;
        private Mesh _cachedQuadMesh;
        private int _cachedLayer;

        public void OnSpawn()
        {
            CacheRuntimeHandles();
            _ageSeconds = 0f;
            _active = true;
            TryRegisterHotSwapListener();
            TryRegisterDispatcher();
        }

        public void OnDespawn()
        {
            _ageSeconds = 0f;
            _active = false;
            TryUnregisterDispatcher();
            TryUnregisterHotSwapListener();
        }

        private void Awake()
        {
            CacheRuntimeHandles();
        }

        private void OnDisable()
        {
            _active = false;
            TryUnregisterDispatcher();
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher || currentService == null || !_active || !isActiveAndEnabled)
                return;

            TryUnregisterDispatcher();
            TryRegisterDispatcher();
        }

        public void LateFrameTick()
        {
            RenderSplashVisualSync(SystemDispatcher.CurrentFrameDeltaTime);
        }

        private void RenderSplashVisualSync(float deltaTime)
        {
            if (!_active || _cachedQuadMesh == null)
                return;

            if (_cachedTransform == null)
                return;

            _ageSeconds += deltaTime;
            float lifetime = math.max(0.1f, lifetimeSeconds);
            float age01 = math.saturate(_ageSeconds / lifetime);
            Vector3 origin = _cachedOrigin;
            Quaternion rotation = _cachedRotation;

            if (splashMaterial != null)
            {
                float height = math.max(0.01f, splashHeightMeters) * (0.45f + age01 * 0.55f);
                Vector3 center = origin + Vector3.up * (height * 0.5f);
                Vector3 scale = new Vector3(
                    math.max(0.01f, splashWidthMeters) * (0.75f + age01 * 0.25f),
                    height,
                    1f);
                s_splashMatrix[0] = Matrix4x4.TRS(center, rotation, scale);
                UnityEngine.Graphics.DrawMeshInstanced(
                    _cachedQuadMesh,
                    0,
                    splashMaterial,
                    s_splashMatrix,
                    SingleQuad,
                    null,
                    ShadowCastingMode.Off,
                    false,
                    _cachedLayer);
            }

            if (distortionRippleMaterial != null)
            {
                float diameter = math.max(0.01f, rippleDiameterMeters) * (0.35f + age01 * 0.65f);
                Vector3 scale = new Vector3(diameter, diameter, 1f);
                s_rippleMatrix[0] = Matrix4x4.TRS(origin, rotation * s_flatRippleRotation, scale);
                UnityEngine.Graphics.DrawMeshInstanced(
                    _cachedQuadMesh,
                    0,
                    distortionRippleMaterial,
                    s_rippleMatrix,
                    SingleQuad,
                    null,
                    ShadowCastingMode.Off,
                    false,
                    _cachedLayer);
            }
        }

        private void CacheRuntimeHandles()
        {
            Transform cachedTransform = transform;
            _cachedTransform = cachedTransform;
            _cachedOrigin = cachedTransform.position;
            _cachedRotation = cachedTransform.rotation;
            _cachedQuadMesh = splashQuadMesh;
            _cachedLayer = gameObject.layer;
        }

        private void TryRegisterDispatcher()
        {
            if (_registeredToDispatcher)
                return;

            _registeredToDispatcher = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterDispatcher()
        {
            if (!_registeredToDispatcher)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredToDispatcher = false;
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

    }
}
