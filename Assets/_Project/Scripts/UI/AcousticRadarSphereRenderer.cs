using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.UI
{
    /// <summary>
    /// Draws physical acoustic contacts as a fixed-size instanced voxel sphere.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Acoustic Radar Sphere Renderer")]
    public sealed class AcousticRadarSphereRenderer : MonoBehaviour, ITickable, ILateFrameTickable
    {
        private const int MaxBlips = 64;
#if UNITY_EDITOR
        private const string VoxelShaderPath = "Assets/_Project/Art/Shaders/Hecton_AcousticRadarVoxel.shader";
#endif

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int PulseIntensityId = Shader.PropertyToID("_PulseIntensity");

        [Header("Anchors")]
        [SerializeField] private Transform radarAnchor = null;
        [SerializeField] private Transform listenerOrigin = null;
        [SerializeField, Tooltip("Forward reference for rear-hemisphere culling. Defaults to the listener transform.")]
        private Transform submarineForwardReference = null;

        [Header("Rendering")]
        [SerializeField] private Mesh voxelMesh = null;
        [SerializeField] private Shader voxelShader = null;
        [SerializeField] private Color voxelColor = new Color(0.38f, 0.98f, 0.88f, 0.72f);
        [SerializeField, Range(0.05f, 1.5f)] private float sphereRadius = 0.32f;
        [SerializeField, Range(0.002f, 0.08f)] private float voxelSizeMeters = 0.014f;
        [SerializeField, Range(0f, 4f)] private float pulseIntensity = 1.15f;
        [SerializeField] private int renderLayer = 0;

        [Header("Acoustics")]
        [SerializeField, Min(1f)] private float maxContactDistanceMeters = 80f;
        [SerializeField, Range(0f, 0.1f)] private float minimumAmplitude = 0.001f;
        [SerializeField, Range(0f, 1f)] private float minimumRadiusFraction = 0.08f;

        // COLD ALLOC: ActiveEmitterSample[64] -- fixed acoustic copy buffer -- owner: AcousticRadarSphereRenderer
        private readonly SpatialAudioManager.ActiveEmitterSample[] _samples =
            new SpatialAudioManager.ActiveEmitterSample[MaxBlips];
        // COLD ALLOC: Matrix4x4[64] -- DrawMeshInstanced payload -- owner: AcousticRadarSphereRenderer
        private readonly Matrix4x4[] _matrices = new Matrix4x4[MaxBlips];

        private bool _registeredToTick;
        private bool _registeredLateFrame;
        private bool _ownsRuntimeVoxelMesh;
        private int _matrixCount;
        private Material _runtimeMaterial;
        private Mesh _runtimeVoxelMesh;
        private Camera _viewCamera;

        private void OnEnable()
        {
            EnsureResources();
            TryRegisterTickManager();
        }

        private void Start()
        {
            EnsureResources();
            TryRegisterTickManager();
        }

        private void OnDisable()
        {
            _matrixCount = 0;
            TryUnregisterTickManager();
        }

        private void OnDestroy()
        {
            TryUnregisterTickManager();
            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }

            if (_ownsRuntimeVoxelMesh && _runtimeVoxelMesh != null)
            {
                Destroy(_runtimeVoxelMesh);
                _runtimeVoxelMesh = null;
                _ownsRuntimeVoxelMesh = false;
            }
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            _matrixCount = 0;
            EnsureResources();
            if (_runtimeMaterial == null || _runtimeVoxelMesh == null)
                return;

            if (!(GlobalRegistry.Audio is SpatialAudioManager audioManager))
                return;

            Transform anchor = radarAnchor != null ? radarAnchor : transform;
            Transform listener = ResolveListenerTransform();
            if (anchor == null || listener == null)
                return;

            int sampleCount = audioManager.CopyActiveImpactEmitterSamples(_samples);
            if (sampleCount <= 0)
                return;

            Vector3 floatingOriginOffset = HectonFloatingOrigin.Instance != null
                ? HectonFloatingOrigin.Instance.TotalOffset
                : Vector3.zero;
            Vector3 listenerAbsolutePosition = listener.position + floatingOriginOffset;
            Quaternion inverseListenerRotation = Quaternion.Inverse(listener.rotation);
            Quaternion anchorRotation = anchor.rotation;
            Vector3 anchorPosition = anchor.position;
            Transform forwardReference = submarineForwardReference != null ? submarineForwardReference : listener;
            float3 submarineForward = math.normalizesafe((float3)forwardReference.forward, new float3(0f, 0f, 1f));
            float safeMaxDistance = Mathf.Max(1f, maxContactDistanceMeters);
            float safeMaxDistanceSq = safeMaxDistance * safeMaxDistance;
            float inverseMaxDistance = 1f / safeMaxDistance;
            float radius = Mathf.Max(0.01f, sphereRadius);
            float baseVoxelSize = Mathf.Max(0.001f, voxelSizeMeters);
            float minimumRadius = math.saturate(minimumRadiusFraction);

            for (int i = 0; i < sampleCount && _matrixCount < MaxBlips; i++)
            {
                SpatialAudioManager.ActiveEmitterSample sample = _samples[i];
                float amplitude = Mathf.Clamp01(sample.Amplitude);
                if (amplitude <= minimumAmplitude)
                    continue;

                Vector3 delta = sample.Position - listenerAbsolutePosition;
                if (math.dot(submarineForward, (float3)delta) <= 0f)
                    continue;

                float distanceSq = delta.sqrMagnitude;
                if (distanceSq <= 0.0001f || distanceSq > safeMaxDistanceSq)
                    continue;

                float inverseDistance = math.rsqrt(distanceSq);
                float distance01 = math.saturate((distanceSq * inverseDistance) * inverseMaxDistance);
                Vector3 directionWs = delta * inverseDistance;
                Vector3 directionLocalToListener = inverseListenerRotation * directionWs;
                Vector3 directionOnRadar = anchorRotation * directionLocalToListener;
                float radial01 = math.lerp(minimumRadius, 1f, distance01);
                Vector3 position = anchorPosition + directionOnRadar * (radius * radial01);
                float scaleMeters = baseVoxelSize * math.lerp(0.7f, 2.4f, amplitude);
                Vector3 scale = new Vector3(scaleMeters, scaleMeters, scaleMeters);
                _matrices[_matrixCount] = Matrix4x4.TRS(position, anchorRotation, scale);
                _matrixCount++;
            }
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (_matrixCount <= 0 || _runtimeMaterial == null || _runtimeVoxelMesh == null)
                return;

            Graphics.DrawMeshInstanced(
                _runtimeVoxelMesh,
                0,
                _runtimeMaterial,
                _matrices,
                _matrixCount,
                null,
                ShadowCastingMode.Off,
                false,
                renderLayer,
                null,
                LightProbeUsage.Off,
                null);
        }

        private Transform ResolveListenerTransform()
        {
            if (listenerOrigin != null)
                return listenerOrigin;

            if (_viewCamera == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                _viewCamera = playerContext != null ? playerContext.PlayerCamera : null;
            }

            return _viewCamera != null ? _viewCamera.transform : transform;
        }

        private void EnsureResources()
        {
            if (_runtimeVoxelMesh == null)
            {
                if (voxelMesh != null)
                {
                    _runtimeVoxelMesh = voxelMesh;
                    _ownsRuntimeVoxelMesh = false;
                }
                else
                {
                    _runtimeVoxelMesh = CreateVoxelMesh();
                    _ownsRuntimeVoxelMesh = true;
                }
            }

#if UNITY_EDITOR
            if (voxelShader == null)
                voxelShader = AssetDatabase.LoadAssetAtPath<Shader>(VoxelShaderPath);
#endif
            if (_runtimeMaterial == null && voxelShader != null)
            {
                _runtimeMaterial = new Material(voxelShader)
                {
                    enableInstancing = true,
                    hideFlags = HideFlags.DontSave
                };
            }

            if (_runtimeMaterial == null)
                return;

            if (_runtimeMaterial.HasProperty(BaseColorId))
                _runtimeMaterial.SetColor(BaseColorId, voxelColor);
            if (_runtimeMaterial.HasProperty(PulseIntensityId))
                _runtimeMaterial.SetFloat(PulseIntensityId, pulseIntensity);
        }

        private void TryRegisterTickManager()
        {
            if (_registeredToTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registeredToTick = GlobalRegistry.Updatables.Contains(this);
            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.UI);
            _registeredLateFrame = SystemDispatcher.GetLateFrameLane(PriorityLayer.UI).Contains(this);
        }

        private void TryUnregisterTickManager()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }

            if (_registeredToTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registeredToTick = false;
            }
        }

        private static Mesh CreateVoxelMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "AcousticRadarVoxel",
                hideFlags = HideFlags.DontSave
            };

            Vector3[] vertices =
            {
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3( 0.5f, -0.5f, -0.5f),
                new Vector3( 0.5f,  0.5f, -0.5f),
                new Vector3(-0.5f,  0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f,  0.5f),
                new Vector3( 0.5f, -0.5f,  0.5f),
                new Vector3( 0.5f,  0.5f,  0.5f),
                new Vector3(-0.5f,  0.5f,  0.5f)
            };

            int[] triangles =
            {
                0, 2, 1, 0, 3, 2,
                1, 2, 6, 1, 6, 5,
                5, 6, 7, 5, 7, 4,
                4, 7, 3, 4, 3, 0,
                3, 7, 6, 3, 6, 2,
                4, 0, 1, 4, 1, 5
            };

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            sphereRadius = Mathf.Clamp(sphereRadius, 0.05f, 1.5f);
            voxelSizeMeters = Mathf.Clamp(voxelSizeMeters, 0.002f, 0.08f);
            pulseIntensity = Mathf.Clamp(pulseIntensity, 0f, 4f);
            maxContactDistanceMeters = Mathf.Max(1f, maxContactDistanceMeters);
            minimumAmplitude = Mathf.Clamp(minimumAmplitude, 0f, 0.1f);
            minimumRadiusFraction = Mathf.Clamp01(minimumRadiusFraction);
        }
#endif
    }
}
