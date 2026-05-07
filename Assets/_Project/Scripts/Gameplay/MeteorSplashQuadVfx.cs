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
    public sealed class MeteorSplashQuadVfx : MonoBehaviour, IPoolable, IUpdatable
    {
        private const int SingleQuad = 1;
        private static readonly Matrix4x4[] s_splashMatrix = new Matrix4x4[SingleQuad];
        private static readonly Matrix4x4[] s_rippleMatrix = new Matrix4x4[SingleQuad];
        private static readonly Quaternion s_flatRippleRotation = new Quaternion(0.70710678f, 0f, 0f, 0.70710678f);
        private static readonly Vector3[] s_quadVertices =
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        }; // COLD ALLOC: Vector3[4] - shared meteor splash quad vertices - owner: MeteorSplashQuadVfx
        private static readonly Vector2[] s_quadUvs =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        }; // COLD ALLOC: Vector2[4] - shared meteor splash quad UVs - owner: MeteorSplashQuadVfx
        private static readonly int[] s_quadTriangles = { 0, 1, 2, 0, 2, 3 }; // COLD ALLOC: int[6] - shared meteor splash quad indices - owner: MeteorSplashQuadVfx
        private static Mesh s_quadMesh;

        [SerializeField] private Material splashMaterial;
        [SerializeField] private Material distortionRippleMaterial;
        [SerializeField, Min(0.1f)] private float lifetimeSeconds = 1.4f;
        [SerializeField, Min(0.01f)] private float splashWidthMeters = 5.5f;
        [SerializeField, Min(0.01f)] private float splashHeightMeters = 2.4f;
        [SerializeField, Min(0.01f)] private float rippleDiameterMeters = 11f;

        private float _ageSeconds;
        private bool _active;
        private bool _registeredToDispatcher;
        private Transform _cachedTransform;
        private int _cachedLayer;

        public void OnSpawn()
        {
            EnsureQuadMesh();
            CacheRuntimeHandles();
            _ageSeconds = 0f;
            _active = true;
            TryRegisterDispatcher();
        }

        public void OnDespawn()
        {
            _ageSeconds = 0f;
            _active = false;
            TryUnregisterDispatcher();
        }

        private void Awake()
        {
            EnsureQuadMesh();
            CacheRuntimeHandles();
        }

        private void OnDisable()
        {
            _active = false;
            TryUnregisterDispatcher();
        }

        public void Tick(float deltaTime)
        {
            if (!_active || s_quadMesh == null)
                return;

            Transform cachedTransform = _cachedTransform;
            if (cachedTransform == null)
                return;

            _ageSeconds += deltaTime;
            float lifetime = math.max(0.1f, lifetimeSeconds);
            float age01 = math.saturate(_ageSeconds / lifetime);
            Vector3 origin = cachedTransform.position;
            Quaternion rotation = cachedTransform.rotation;

            if (splashMaterial != null)
            {
                float height = math.max(0.01f, splashHeightMeters) * (0.45f + age01 * 0.55f);
                Vector3 center = origin + Vector3.up * (height * 0.5f);
                Vector3 scale = new Vector3(
                    math.max(0.01f, splashWidthMeters) * (0.75f + age01 * 0.25f),
                    height,
                    1f);
                s_splashMatrix[0] = Matrix4x4.TRS(center, rotation, scale);
                Graphics.DrawMeshInstanced(
                    s_quadMesh,
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
                Graphics.DrawMeshInstanced(
                    s_quadMesh,
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
            _cachedTransform = transform;
            _cachedLayer = gameObject.layer;
        }

        private void TryRegisterDispatcher()
        {
            if (_registeredToDispatcher)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registeredToDispatcher = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregisterDispatcher()
        {
            if (!_registeredToDispatcher)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registeredToDispatcher = false;
        }

        private static void EnsureQuadMesh()
        {
            if (s_quadMesh != null)
                return;

            s_quadMesh = new Mesh
            {
                name = "__HectonMeteorSplashQuad"
            }; // COLD ALLOC: Mesh[1] - shared two-quad meteor splash draw source - owner: MeteorSplashQuadVfx
            s_quadMesh.SetVertices(s_quadVertices);
            s_quadMesh.SetUVs(0, s_quadUvs);
            s_quadMesh.SetTriangles(s_quadTriangles, 0);
            s_quadMesh.UploadMeshData(true);
        }
    }
}
