using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Core.Signals;
using Hecton8.Gameplay;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Physical visor HUD projection mesh. No screen-space canvas, no physics raycasts.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class DiegeticVisorHudMesh : MonoBehaviour, IUpdatable, IPlayerSignalEventListener, IDamageReceiver
    {
        private const int BlackBoxCapacity = 300;
        private const float DefaultDistanceMeters = 0.48f;
        private const float DefaultHorizontalDegrees = 78f;
        private const float DefaultVerticalDegrees = 48f;
        private const float DegreesToHalfRadians = 0.008726646f;
        private const float Epsilon = 0.0001f;
        private const string DefaultShaderName = "Hecton8/UI/DiegeticVisorCurvedHUD";

        private static readonly int PanelPowerLevelId = Shader.PropertyToID("_PanelPowerLevel");
        private static readonly int DamageGlitchId = Shader.PropertyToID("_DamageGlitch");
        private static readonly int Humidity01Id = Shader.PropertyToID("_Humidity01");
        private static readonly int StencilRefId = Shader.PropertyToID("_StencilRef");

        [Header("Projection")]
        [SerializeField] private Camera visorCamera;
        [SerializeField] private bool parentToCamera = true;
        [SerializeField, Min(0.05f)] private float distanceMeters = DefaultDistanceMeters;
        [SerializeField, Range(16f, 130f)] private float horizontalDegrees = DefaultHorizontalDegrees;
        [SerializeField, Range(12f, 90f)] private float verticalDegrees = DefaultVerticalDegrees;
        [SerializeField, Range(4, 64)] private int horizontalSegments = 24;
        [SerializeField, Range(2, 32)] private int verticalSegments = 10;
        [SerializeField, Range(0f, 0.18f)] private float curvatureMeters = 0.045f;

        [Header("Render State")]
        [SerializeField] private Material sourceMaterial;
        [SerializeField] private bool releaseRuntimeObjectsOnDisable;
        [SerializeField] private bool releaseBlackBoxOnDisable;
        [SerializeField] private int stencilReference = 17;
        [SerializeField, Range(0f, 1f)] private float panelPower01 = 1f;
        [SerializeField, Range(0.05f, 4f)] private float glitchRecoveryPerSecond = 1.8f;

        [Header("Signals")]
        [SerializeField] private BaseAtmosphereEngine atmosphereEngine;
        [SerializeField, Min(0.05f)] private float humiditySampleIntervalSeconds = 0.5f;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _runtimeMesh;
        private Material _runtimeMaterial;
        private Transform _cameraTransform;
        private NativeArray<DiegeticHudTelemetryEntry> _blackBox;
        private int _blackBoxCursor;
        private bool _registered;
        private bool _playerSignalRegistered;
        private bool _nativeRegistered;
        private bool _blackBoxDumped;
        private float _brownout01;
        private float _damageGlitch01;
        private float _humidity01;
        private float _humiditySampleTimer;
        private float _lastPanelPower = -1f;
        private float _lastDamageGlitch = -1f;
        private float _lastHumidity = -1f;
        private int _lastStencilReference = int.MinValue;
        private int _meshHorizontalSegments = -1;
        private int _meshVerticalSegments = -1;
        private HectonQualityTier _meshTier;
        private float _meshDistanceMeters = -1f;
        private float _meshHorizontalDegrees = -1f;
        private float _meshVerticalDegrees = -1f;
        private float _meshCurvatureMeters = -1f;
        private Vector3[] _vertices;
        private Vector3[] _normals;
        private Vector2[] _uv;
        private int[] _indices;

        public float PanelPower01 => panelPower01;
        public float Brownout01 => _brownout01;
        public float DamageGlitch01 => _damageGlitch01;
        public float Humidity01 => _humidity01;

        private void OnEnable()
        {
            ResolveComponents();
            ResolveCamera();
            RebuildMesh();
            EnsureRuntimeMaterial();
            EnsureBlackBox();
            TryRegisterTick();
            PlayerSignalEvents.Register(this);
            _playerSignalRegistered = true;
        }

        private void OnDisable()
        {
            TryUnregisterTick();
            if (_playerSignalRegistered)
            {
                PlayerSignalEvents.Unregister(this);
                _playerSignalRegistered = false;
            }

            if (releaseBlackBoxOnDisable)
                DisposeBlackBox();
            if (releaseRuntimeObjectsOnDisable)
                ReleaseRuntimeObjects();
        }

        private void OnDestroy()
        {
            TryUnregisterTick();
            DisposeBlackBox();
            ReleaseRuntimeObjects();
        }

        public void Tick(float deltaTime)
        {
            float dt = math.max(0f, deltaTime);
            if (_damageGlitch01 > 0f)
                _damageGlitch01 = math.max(0f, _damageGlitch01 - dt * glitchRecoveryPerSecond);
            if (_brownout01 > 0f)
                _brownout01 = math.max(0f, _brownout01 - dt);

            SampleHumidity(dt);
            ApplyMaterialState();
            RecordTelemetry();
        }

        public void OnTraumaHudSignal(in TraumaHudSignal signal)
        {
            panelPower01 = math.saturate(signal.TransportPower01);
            float hullDamage = signal.HullIntegrity01 < 0.3f ? 1f - math.saturate(signal.HullIntegrity01 * 3.3333333f) : 0f;
            _damageGlitch01 = math.saturate(math.max(_damageGlitch01, math.max(signal.GlitchIntensity, hullDamage)));
        }

        public void OnInteractionSignal(in PlayerInteractionStressSignal signal)
        {
        }

        public void OnToolDepletedSignal(in ToolDepletedSignal signal)
        {
        }

        public void ReceiveDamage(in DamagePacket packet)
        {
            if (packet.Channel != DamageChannel.Integrity &&
                packet.Channel != DamageChannel.Clarity &&
                packet.Channel != DamageChannel.Trauma)
            {
                return;
            }

            float health01 = packet.NextValue > 0f ? math.saturate(packet.NextValue) : 1f - math.saturate(packet.Magnitude);
            if (health01 >= 0.3f)
                return;

            float missingLowHealth = 1f - math.saturate(health01 * 3.3333333f);
            float impact = math.saturate(packet.Magnitude);
            _damageGlitch01 = math.saturate(math.max(_damageGlitch01, math.max(missingLowHealth, impact)));
        }

        public void ApplyBrownoutSignal(in BrownoutSignal signal)
        {
            panelPower01 = math.saturate(signal.SupplyRatio);
            _brownout01 = math.saturate(math.max(_brownout01, signal.Severity01));
        }

        public void ApplyDamageSignal(in Hecton8.Core.Signals.DamageSignal signal, float health01)
        {
            if (health01 >= 0.3f)
                return;

            float missingLowHealth = 1f - math.saturate(health01 * 3.3333333f);
            float impact = math.saturate(signal.Magnitude * 0.05f);
            _damageGlitch01 = math.saturate(math.max(_damageGlitch01, math.max(missingLowHealth, impact)));
        }

        public void ApplyAtmosphereHumidity(byte humidityPercent)
        {
            _humidity01 = math.saturate(humidityPercent * 0.01f);
        }

        public bool TryProjectViewportPoint(Vector2 viewportPoint, out Vector2 visorUv, out Vector3 localHit)
        {
            visorUv = default;
            localHit = default;
            if (visorCamera == null)
                return false;

            Ray ray = visorCamera.ViewportPointToRay(new Vector3(viewportPoint.x, viewportPoint.y, 0f));
            return TryProjectRayToVisor(ray, out visorUv, out localHit);
        }

        public bool TryProjectRayToVisor(Ray worldRay, out Vector2 visorUv, out Vector3 localHit)
        {
            visorUv = default;
            localHit = default;

            Transform self = transform;
            Vector3 localOrigin = self.InverseTransformPoint(worldRay.origin);
            Vector3 localDirection = self.InverseTransformDirection(worldRay.direction);
            if (math.abs(localDirection.z) < Epsilon)
                return false;

            float t = (distanceMeters - localOrigin.z) * math.rcp(localDirection.z);
            if (t < 0f)
                return false;

            localHit = localOrigin + localDirection * t;
            float halfWidth = ResolveHalfWidth();
            float halfHeight = ResolveHalfHeight();
            if (halfWidth <= Epsilon || halfHeight <= Epsilon)
                return false;

            float u = (localHit.x * math.rcp(halfWidth * 2f)) + 0.5f;
            float v = (localHit.y * math.rcp(halfHeight * 2f)) + 0.5f;
            if (u < 0f || u > 1f || v < 0f || v > 1f)
                return false;

            visorUv = new Vector2(u, v);
            return true;
        }

        public static float RationalTan(float radians)
        {
            float x = math.clamp(radians, -1.2f, 1.2f);
            float x2 = x * x;
            float denominator = 27f - (9f * x2);
            if (math.abs(denominator) < Epsilon)
                denominator = denominator < 0f ? -Epsilon : Epsilon;

            return x * (27f - x2) * math.rcp(denominator);
        }

        private static int ResolveSegmentCount(int authoringCount, HectonQualityTier tier, int min, int max)
        {
            int safeCount = math.clamp(authoringCount, min, max);
            switch (tier)
            {
                case HectonQualityTier.Low:
                    return math.max(min, safeCount >> 1);
                case HectonQualityTier.Mx350:
                    return math.max(min, (safeCount * 3) >> 2);
                case HectonQualityTier.High:
                    return math.min(max, safeCount + (safeCount >> 1));
                case HectonQualityTier.Ultra:
                    return max;
                default:
                    return safeCount;
            }
        }

        private void ResolveComponents()
        {
            if (_meshFilter == null)
                _meshFilter = GetComponent<MeshFilter>();
            if (_meshRenderer == null)
                _meshRenderer = GetComponent<MeshRenderer>();
        }

        private void ResolveCamera()
        {
            if (visorCamera == null && GlobalRegistry.Player != null)
                visorCamera = GlobalRegistry.Player.PlayerCamera;
            if (visorCamera == null)
                visorCamera = GetComponentInParent<Camera>();
            if (visorCamera == null)
                return;

            _cameraTransform = visorCamera.transform;
            if (!parentToCamera)
                return;

            transform.SetParent(_cameraTransform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private void RebuildMesh()
        {
            ResolveComponents();
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            int hSegments = ResolveSegmentCount(horizontalSegments, tier, 4, 64);
            int vSegments = ResolveSegmentCount(verticalSegments, tier, 2, 32);
            if (IsMeshCurrent(tier, hSegments, vSegments))
            {
                if (_meshFilter != null && _meshFilter.sharedMesh != _runtimeMesh)
                    _meshFilter.sharedMesh = _runtimeMesh;

                return;
            }

            int vertexCount = (hSegments + 1) * (vSegments + 1);
            int indexCount = hSegments * vSegments * 6;
            EnsureMeshArrays(vertexCount, indexCount);

            if (_runtimeMesh == null)
            {
                _runtimeMesh = new Mesh(); // COLD ALLOC: Mesh[1] - visor physical projection surface - owner: DiegeticVisorHudMesh
                _runtimeMesh.name = nameof(DiegeticVisorHudMesh);
            }
            else
            {
                _runtimeMesh.Clear();
            }

            float halfHorizontal = horizontalDegrees * DegreesToHalfRadians;
            float halfVertical = verticalDegrees * DegreesToHalfRadians;
            float invHSegments = math.rcp((float)math.max(1, hSegments));
            float invVSegments = math.rcp((float)math.max(1, vSegments));
            int vertexIndex = 0;
            for (int y = 0; y <= vSegments; y++)
            {
                float y01 = y * invVSegments;
                float ySigned = (y01 * 2f) - 1f;
                for (int x = 0; x <= hSegments; x++)
                {
                    float x01 = x * invHSegments;
                    float xSigned = (x01 * 2f) - 1f;
                    float localX = RationalTan(xSigned * halfHorizontal) * distanceMeters;
                    float localY = RationalTan(ySigned * halfVertical) * distanceMeters;
                    float curveDepth = curvatureMeters * ((xSigned * xSigned) + (0.35f * ySigned * ySigned));
                    _vertices[vertexIndex] = new Vector3(localX, localY, distanceMeters - curveDepth);
                    _normals[vertexIndex] = Vector3.back;
                    _uv[vertexIndex] = new Vector2(x01, y01);
                    vertexIndex++;
                }
            }

            int index = 0;
            int stride = hSegments + 1;
            for (int y = 0; y < vSegments; y++)
            {
                for (int x = 0; x < hSegments; x++)
                {
                    int a = y * stride + x;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    _indices[index++] = a;
                    _indices[index++] = c;
                    _indices[index++] = b;
                    _indices[index++] = b;
                    _indices[index++] = c;
                    _indices[index++] = d;
                }
            }

            _runtimeMesh.vertices = _vertices;
            _runtimeMesh.normals = _normals;
            _runtimeMesh.uv = _uv;
            _runtimeMesh.triangles = _indices;
            _runtimeMesh.RecalculateBounds();
            _meshFilter.sharedMesh = _runtimeMesh;
            _meshTier = tier;
            _meshHorizontalSegments = hSegments;
            _meshVerticalSegments = vSegments;
            _meshDistanceMeters = distanceMeters;
            _meshHorizontalDegrees = horizontalDegrees;
            _meshVerticalDegrees = verticalDegrees;
            _meshCurvatureMeters = curvatureMeters;
        }

        private void EnsureRuntimeMaterial()
        {
            ResolveComponents();
            if (_runtimeMaterial != null)
            {
                _meshRenderer.sharedMaterial = _runtimeMaterial;
                return;
            }

            if (sourceMaterial == null)
            {
                Shader shader = Shader.Find(DefaultShaderName);
                if (shader != null)
                    _runtimeMaterial = new Material(shader); // COLD ALLOC: Material[1] - fallback visor shader instance - owner: DiegeticVisorHudMesh
            }
            else
            {
                _runtimeMaterial = new Material(sourceMaterial); // COLD ALLOC: Material[1] - per-visor shader state - owner: DiegeticVisorHudMesh
            }

            if (_runtimeMaterial == null)
                return;

            _meshRenderer.sharedMaterial = _runtimeMaterial;
            _lastPanelPower = -1f;
            _lastDamageGlitch = -1f;
            _lastHumidity = -1f;
            _lastStencilReference = int.MinValue;
            ApplyMaterialState();
        }

        private void ApplyMaterialState()
        {
            if (_runtimeMaterial == null)
                return;

            float resolvedPanelPower = math.saturate(panelPower01) * (1f - (_brownout01 * 0.65f));
            if (!math.isfinite(resolvedPanelPower) ||
                !math.isfinite(_damageGlitch01) ||
                !math.isfinite(_humidity01))
            {
                DumpBlackBox();
                return;
            }

            if (math.abs(resolvedPanelPower - _lastPanelPower) > 0.001f)
            {
                _runtimeMaterial.SetFloat(PanelPowerLevelId, resolvedPanelPower);
                _lastPanelPower = resolvedPanelPower;
            }

            if (math.abs(_damageGlitch01 - _lastDamageGlitch) > 0.001f)
            {
                _runtimeMaterial.SetFloat(DamageGlitchId, _damageGlitch01);
                _lastDamageGlitch = _damageGlitch01;
            }

            if (math.abs(_humidity01 - _lastHumidity) > 0.001f)
            {
                _runtimeMaterial.SetFloat(Humidity01Id, _humidity01);
                _lastHumidity = _humidity01;
            }

            if (stencilReference != _lastStencilReference)
            {
                _runtimeMaterial.SetInt(StencilRefId, stencilReference);
                _lastStencilReference = stencilReference;
            }
        }

        private void SampleHumidity(float deltaTime)
        {
            if (atmosphereEngine == null)
                return;

            _humiditySampleTimer += deltaTime;
            if (_humiditySampleTimer < humiditySampleIntervalSeconds)
                return;

            _humiditySampleTimer = 0f;
            if (atmosphereEngine.TryGetCompartmentState(atmosphereEngine.ActiveCompartmentIndex, out CompartmentState state))
                _humidity01 = math.saturate(state.HumidityPercent * 0.01f);
        }

        private void TryRegisterTick()
        {
            if (_registered)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
        }

        private void TryUnregisterTick()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }

        private void EnsureBlackBox()
        {
            if (_blackBox.IsCreated)
                return;

            _blackBox = new NativeArray<DiegeticHudTelemetryEntry>(
                BlackBoxCapacity,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<DiegeticHudTelemetryEntry>[300] - visor HUD crash black box - owner: DiegeticVisorHudMesh
            NativeMemorySentinel.RegisterNativeArray(_blackBox, nameof(DiegeticVisorHudMesh), nameof(_blackBox), NativeAllocationLifetime.Scene);
            _nativeRegistered = true;
            _blackBoxCursor = 0;
            _blackBoxDumped = false;
        }

        private void DisposeBlackBox()
        {
            if (!_blackBox.IsCreated)
                return;

            if (_nativeRegistered)
            {
                NativeMemorySentinel.UnregisterNativeArray(_blackBox);
                _nativeRegistered = false;
            }

            _blackBox.Dispose();
            _blackBox = default;
            _blackBoxCursor = 0;
            _blackBoxDumped = false;
        }

        private void RecordTelemetry()
        {
            if (!_blackBox.IsCreated)
                return;

            Vector3 localPosition = transform.localPosition;
            if (!math.isfinite(localPosition.x) || !math.isfinite(localPosition.y) || !math.isfinite(localPosition.z))
            {
                DumpBlackBox();
                return;
            }

            _blackBox[_blackBoxCursor] = new DiegeticHudTelemetryEntry
            {
                Frame = Time.frameCount,
                Power01 = math.saturate(panelPower01),
                Brownout01 = math.saturate(_brownout01),
                DamageGlitch01 = math.saturate(_damageGlitch01),
                Humidity01 = math.saturate(_humidity01),
                LocalX = localPosition.x,
                LocalY = localPosition.y,
                LocalZ = localPosition.z,
                Flags = (uint)((_registered ? 1 : 0) | (_playerSignalRegistered ? 2 : 0) | (_runtimeMaterial != null ? 4 : 0))
            };
            _blackBoxCursor++;
            if (_blackBoxCursor >= BlackBoxCapacity)
                _blackBoxCursor = 0;
        }

        private void DumpBlackBox()
        {
            if (_blackBoxDumped || !_blackBox.IsCreated)
                return;

            _blackBoxDumped = true;
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string directory = Path.Combine(root, "Docs", "AgentLogs");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "Dump_UI_DIEGETIC_HUD.bin");
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(BlackBoxCapacity);
                writer.Write(_blackBoxCursor);
                for (int i = 0; i < _blackBox.Length; i++)
                {
                    DiegeticHudTelemetryEntry entry = _blackBox[i];
                    writer.Write(entry.Frame);
                    writer.Write(entry.Power01);
                    writer.Write(entry.Brownout01);
                    writer.Write(entry.DamageGlitch01);
                    writer.Write(entry.Humidity01);
                    writer.Write(entry.LocalX);
                    writer.Write(entry.LocalY);
                    writer.Write(entry.LocalZ);
                    writer.Write(entry.Flags);
                }
            }
        }

        private void ReleaseRuntimeObjects()
        {
            if (_meshFilter != null)
                _meshFilter.sharedMesh = null;
            if (_meshRenderer != null && _meshRenderer.sharedMaterial == _runtimeMaterial)
                _meshRenderer.sharedMaterial = null;

            if (_runtimeMesh != null)
            {
                Destroy(_runtimeMesh);
                _runtimeMesh = null;
            }

            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }

            _meshHorizontalSegments = -1;
            _meshVerticalSegments = -1;
            _meshDistanceMeters = -1f;
            _meshHorizontalDegrees = -1f;
            _meshVerticalDegrees = -1f;
            _meshCurvatureMeters = -1f;
            _vertices = null;
            _normals = null;
            _uv = null;
            _indices = null;
        }

        private bool IsMeshCurrent(HectonQualityTier tier, int hSegments, int vSegments)
        {
            return _runtimeMesh != null &&
                   _meshHorizontalSegments == hSegments &&
                   _meshVerticalSegments == vSegments &&
                   _meshTier == tier &&
                   math.abs(_meshDistanceMeters - distanceMeters) <= Epsilon &&
                   math.abs(_meshHorizontalDegrees - horizontalDegrees) <= Epsilon &&
                   math.abs(_meshVerticalDegrees - verticalDegrees) <= Epsilon &&
                   math.abs(_meshCurvatureMeters - curvatureMeters) <= Epsilon;
        }

        private void EnsureMeshArrays(int vertexCount, int indexCount)
        {
            if (_vertices == null || _vertices.Length != vertexCount)
            {
                _vertices = new Vector3[vertexCount]; // COLD ALLOC: Vector3[vertexCount] - retained visor mesh vertices - owner: DiegeticVisorHudMesh
                _normals = new Vector3[vertexCount]; // COLD ALLOC: Vector3[vertexCount] - retained visor mesh normals - owner: DiegeticVisorHudMesh
                _uv = new Vector2[vertexCount]; // COLD ALLOC: Vector2[vertexCount] - retained visor mesh uv - owner: DiegeticVisorHudMesh
            }

            if (_indices == null || _indices.Length != indexCount)
                _indices = new int[indexCount]; // COLD ALLOC: int[indexCount] - retained visor mesh triangles - owner: DiegeticVisorHudMesh
        }

        private float ResolveHalfWidth()
        {
            return RationalTan(horizontalDegrees * DegreesToHalfRadians) * distanceMeters;
        }

        private float ResolveHalfHeight()
        {
            return RationalTan(verticalDegrees * DegreesToHalfRadians) * distanceMeters;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct DiegeticHudTelemetryEntry
    {
        public int Frame;
        public float Power01;
        public float Brownout01;
        public float DamageGlitch01;
        public float Humidity01;
        public float LocalX;
        public float LocalY;
        public float LocalZ;
        public uint Flags;
    }
}
