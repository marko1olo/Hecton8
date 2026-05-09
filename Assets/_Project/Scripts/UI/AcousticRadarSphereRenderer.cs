using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Gameplay;
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

        // COLD ALLOC: ActiveImpactEmitterSample[64] -- fixed acoustic impact copy buffer with cached AUP -- owner: AcousticRadarSphereRenderer
        private readonly SpatialAudioManager.ActiveImpactEmitterSample[] _samples =
            new SpatialAudioManager.ActiveImpactEmitterSample[MaxBlips];
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
                DestroyUnityObject(_runtimeMaterial);
                _runtimeMaterial = null;
            }

            if (_ownsRuntimeVoxelMesh && _runtimeVoxelMesh != null)
            {
                DestroyUnityObject(_runtimeVoxelMesh);
                _runtimeVoxelMesh = null;
                _ownsRuntimeVoxelMesh = false;
            }
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            _matrixCount = 0;
            if (_runtimeMaterial == null || _runtimeVoxelMesh == null)
            {
                RefreshLateFrameRegistration();
                return;
            }

            if (!(GlobalRegistry.Audio is SpatialAudioManager audioManager))
            {
                RefreshLateFrameRegistration();
                return;
            }

            Transform anchor = radarAnchor != null ? radarAnchor : transform;
            Transform listener = ResolveListenerTransform();
            if (anchor == null || listener == null)
            {
                RefreshLateFrameRegistration();
                return;
            }

            int sampleCount = audioManager.CopyActiveImpactEmitterSamples(_samples);
            if (sampleCount <= 0)
            {
                RefreshLateFrameRegistration();
                return;
            }

            Vector3 listenerPosition = listener.position;
            Quaternion listenerRotation = listener.rotation;
            float3 listenerRight = (float3)(listenerRotation * Vector3.right);
            float3 listenerUp = (float3)(listenerRotation * Vector3.up);
            float3 listenerForward = (float3)(listenerRotation * Vector3.forward);
            Quaternion anchorRotation = anchor.rotation;
            Vector3 anchorPosition = anchor.position;
            if (!TryResolveListenerAup(listenerPosition, out AbsoluteUniversePosition listenerAup))
            {
                RefreshLateFrameRegistration();
                return;
            }

            Transform forwardReference = submarineForwardReference != null ? submarineForwardReference : listener;
            float3 submarineForward = object.ReferenceEquals(forwardReference, listener)
                ? ResolveForwardUnitVector(listenerForward)
                : ResolveForwardUnitVector(forwardReference);
            float safeMaxDistance = math.max(1f, maxContactDistanceMeters);
            float safeMaxDistanceSq = safeMaxDistance * safeMaxDistance;
            float inverseMaxDistanceSq = math.rcp(safeMaxDistanceSq);
            float radius = math.max(0.01f, sphereRadius);
            float baseVoxelSize = math.max(0.001f, voxelSizeMeters);
            float minimumRadius = math.saturate(minimumRadiusFraction);

            for (int i = 0; i < sampleCount && _matrixCount < MaxBlips; i++)
            {
                SpatialAudioManager.ActiveImpactEmitterSample sample = _samples[i];
                float amplitude = math.saturate(sample.Amplitude);
                if (amplitude <= minimumAmplitude)
                    continue;

                AbsoluteUniversePosition sampleAup = sample.PositionAup;
                float3 deltaAup = AbsoluteUniversePosition.ToCameraRelativeFloat3(in sampleAup, in listenerAup);
                if (!math.all(math.isfinite(deltaAup)) || math.dot(submarineForward, deltaAup) <= 0f)
                    continue;

                float distanceSq = math.lengthsq(deltaAup);
                if (distanceSq <= 0.0001f || distanceSq > safeMaxDistanceSq)
                    continue;

                float approximateDistance = ApproximateMagnitude(deltaAup);
                if (approximateDistance <= 0.0001f)
                    continue;

                float inverseApproximateDistance = math.rcp(math.max(approximateDistance, 0.0001f));
                float distance01 = math.saturate(distanceSq * inverseMaxDistanceSq);
                float localX = math.dot(deltaAup, listenerRight) * inverseApproximateDistance;
                float localY = math.dot(deltaAup, listenerUp) * inverseApproximateDistance;
                float localZ = math.dot(deltaAup, listenerForward) * inverseApproximateDistance;
                Vector3 directionOnRadar = anchorRotation * new Vector3(localX, localY, localZ);
                float radial01 = math.lerp(minimumRadius, 1f, distance01);
                Vector3 position = anchorPosition + directionOnRadar * (radius * radial01);
                float scaleMeters = baseVoxelSize * math.lerp(0.7f, 2.4f, amplitude);
                Vector3 scale = new Vector3(scaleMeters, scaleMeters, scaleMeters);
                _matrices[_matrixCount] = Matrix4x4.TRS(position, anchorRotation, scale);
                _matrixCount++;
            }

            RefreshLateFrameRegistration();
        }

        private static float3 ResolveForwardUnitVector(Transform reference)
        {
            if (reference == null)
                return new float3(0f, 0f, 1f);

            Quaternion rotation = reference.rotation;
            float3 forward = (float3)(rotation * Vector3.forward);
            return ResolveForwardUnitVector(forward);
        }

        private static float3 ResolveForwardUnitVector(float3 forward)
        {
            return math.all(math.isfinite(forward)) && math.lengthsq(forward) > 0.25f
                ? forward
                : new float3(0f, 0f, 1f);
        }

        private static bool TryResolveListenerAup(Vector3 listenerPosition, out AbsoluteUniversePosition listenerAup)
        {
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext))
            {
                PlayerMovementRuntimeState movementState = runtimeContext.MovementState;
                if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                {
                    listenerAup = OffsetAupLocal(
                        in movementState.PredictedAup,
                        (Vector3)((float3)listenerPosition - movementState.PredictedWorldPosition));
                    return true;
                }
            }

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            HectonPlayerMovement movement = playerContext != null ? playerContext.PlayerMovement : null;
            if (movement != null)
            {
                listenerAup = movement.CurrentAup;
                return true;
            }

            listenerAup = default;
            return false;
        }

        private static AbsoluteUniversePosition OffsetAupLocal(in AbsoluteUniversePosition anchorAup, Vector3 runtimeOffset)
        {
            AbsoluteUniversePosition result = anchorAup;
            result.LocalX += runtimeOffset.x;
            result.LocalY += runtimeOffset.y;
            result.LocalZ += runtimeOffset.z;
            NormalizeAupLocalAxis(ref result.GridX, ref result.LocalX);
            NormalizeAupLocalAxis(ref result.GridY, ref result.LocalY);
            NormalizeAupLocalAxis(ref result.GridZ, ref result.LocalZ);
            return result;
        }

        private static void NormalizeAupLocalAxis(ref long grid, ref float local)
        {
            const float cellSize = AbsoluteUniversePosition.CellSizeMeters;
            if (local >= 0f && local < cellSize)
                return;

            long gridDelta = (long)math.floor(local / cellSize);
            grid += gridDelta;
            local -= gridDelta * cellSize;
            if (local < 0f)
            {
                local += cellSize;
                grid--;
                return;
            }

            if (local >= cellSize)
            {
                local -= cellSize;
                grid++;
            }
        }

        private static float ApproximateMagnitude(float3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float maxAxis = math.max(ax, math.max(ay, az));
            float minAxis = math.min(ax, math.min(ay, az));
            float midAxis = ax + ay + az - maxAxis - minAxis;
            return maxAxis + midAxis * 0.375f + minAxis * 0.125f;
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (_matrixCount <= 0 || _runtimeMaterial == null || _runtimeVoxelMesh == null)
                return;

            Camera renderCamera = ResolveRenderCamera();
            if (renderCamera == null)
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
                renderCamera,
                LightProbeUsage.Off,
                null);
        }

        private Camera ResolveRenderCamera()
        {
            Camera renderCamera = GlobalRenderContext.CurrentCamera;
            if (IsGameplayRenderCamera(renderCamera))
                return renderCamera;

            if (_viewCamera == null)
            {
                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                _viewCamera = playerContext != null ? playerContext.PlayerCamera : null;
            }

            return IsGameplayRenderCamera(_viewCamera) ? _viewCamera : null;
        }

        private static bool IsGameplayRenderCamera(Camera camera)
        {
            return camera != null &&
                   camera.isActiveAndEnabled &&
                   camera.cameraType != CameraType.Preview &&
                   camera.cameraType != CameraType.Reflection;
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

            _registeredToTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
            RefreshLateFrameRegistration();
        }

        private void RefreshLateFrameRegistration()
        {
            bool shouldRegisterLateFrame =
                isActiveAndEnabled &&
                Application.isPlaying &&
                GlobalRegistry.Dispatcher != null &&
                _matrixCount > 0 &&
                _runtimeMaterial != null &&
                _runtimeVoxelMesh != null;

            if (shouldRegisterLateFrame)
            {
                if (_registeredLateFrame)
                    return;

                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
                return;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }
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

            // COLD ALLOC: Vector3[8] -- one-time fallback voxel cube vertices -- owner: AcousticRadarSphereRenderer
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

            // COLD ALLOC: int[36] -- one-time fallback voxel cube indices -- owner: AcousticRadarSphereRenderer
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

        private static void DestroyUnityObject(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
            {
                Destroy(target);
                return;
            }

            DestroyImmediate(target);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            sphereRadius = math.clamp(sphereRadius, 0.05f, 1.5f);
            voxelSizeMeters = math.clamp(voxelSizeMeters, 0.002f, 0.08f);
            pulseIntensity = math.clamp(pulseIntensity, 0f, 4f);
            maxContactDistanceMeters = math.max(1f, maxContactDistanceMeters);
            minimumAmplitude = math.clamp(minimumAmplitude, 0f, 0.1f);
            minimumRadiusFraction = math.saturate(minimumRadiusFraction);
        }
#endif
    }
}
