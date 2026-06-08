using Hecton8.Core;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.VFX
{
    /// <summary>
    /// AUP-stable trail renderer. Samples absolute-universe positions and rebuilds runtime vertices after origin shifts.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/VFX/Native Trail Renderer")]
    public sealed class NativeTrailRenderer : MonoBehaviour,
        ILateFrameTickable,
        IColdTickable,
        IRenderable,
        IOriginShiftListener,
        IGlobalRegistryHotSwapListener
    {
        private const int MinimumCapacity = 2;
        private const int MaximumCapacity = 256;
        private const float MinimumWidthMeters = 0.001f;
        private const float MinimumLifetimeSeconds = 0.02f;
        private const float MinimumSampleIntervalSeconds = 0.005f;

        private struct TrailSample
        {
            public AbsoluteUniversePosition Position;
            public float AgeSeconds;
            public float WidthMeters;
            public byte Active;
        }

        [Header("Trail")]
        [SerializeField] private Transform target;
        [SerializeField] private Material trailMaterial;
        [SerializeField, Min(MinimumCapacity)] private int capacity = 64;
        [SerializeField, Min(MinimumLifetimeSeconds)] private float lifetimeSeconds = 1.2f;
        [SerializeField, Min(MinimumWidthMeters)] private float widthMeters = 0.12f;
        [SerializeField, Min(0f)] private float minimumSampleDistanceMeters = 0.12f;
        [SerializeField, Min(MinimumSampleIntervalSeconds)] private float sampleIntervalSeconds = 0.016f;
        [SerializeField] private Color headColor = new Color(0.62f, 0.92f, 1f, 0.78f);
        [SerializeField] private Color tailColor = new Color(0.10f, 0.24f, 0.32f, 0f);
        [SerializeField] private ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off;
        [SerializeField] private bool receiveShadows;
        [SerializeField] private bool emit = true;

        private TrailSample[] _samples;
        private Vector3[] _vertices;
        private Vector2[] _uvs;
        private Color[] _colors;
        private int[] _triangles;

        private Mesh _mesh;
        private ITickDispatcher _tickDispatcher;
        private RenderDispatcher _renderDispatcher;
        private int _resolvedCapacity;
        private int _headIndex = -1;
        private int _sampleCount;
        private float _sampleTimer;
        private bool _meshDirty;
        private bool _registeredUpdate;
        private bool _registeredColdTick;
        private bool _registeredRender;
        private bool _registeredOriginShift;
        private bool _registeredHotSwap;
        private bool _dispatcherReady;
        private bool _renderDispatcherReady;
        private bool _hasLastSample;
        private bool _bufferRepairRequested;
        private AbsoluteUniversePosition _lastSampleAup;
        private Vector3 _lastSampleRuntimePosition;

        private void Awake()
        {
            EnsureBuffers();
        }

        private void OnEnable()
        {
            RefreshDispatcherReadyCold();
            TryRegisterHotSwap();
            TryRegister();
        }

        private void Start()
        {
            RefreshDispatcherReadyCold();
            TryRegisterHotSwap();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwap();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterHotSwap();
            if (_mesh != null)
            {
                Destroy(_mesh);
                _mesh = null;
            }
        }

        public void LateFrameTick()
        {
            float deltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            if (!HasBuffersReady())
            {
                QueueBufferRepair();
                return;
            }

            AdvanceAges(math.max(0f, deltaTime));

            if (!emit || target == null)
                return;

            _sampleTimer -= math.max(0f, deltaTime);
            Vector3 runtimePosition = ResolveTargetRuntimePosition(target);
            if (!IsFinite(runtimePosition))
                return;

            if (_sampleTimer > 0f && !ShouldForceDistanceSample(runtimePosition))
                return;

            AddSample(runtimePosition);
            _sampleTimer = math.max(MinimumSampleIntervalSeconds, sampleIntervalSeconds);
        }

        public void ColdTick()
        {
            if (!_bufferRepairRequested && HasBuffersReady())
                return;

            _bufferRepairRequested = false;
            EnsureBuffers();
        }

        private static Vector3 ResolveTargetRuntimePosition(Transform source)
        {
            return source != null ? source.position : Vector3.zero;
        }

        public void Render(float deltaTime)
        {
            if (_mesh == null || trailMaterial == null || _sampleCount < 2)
                return;

            if (_meshDirty)
                RebuildMesh(GlobalRenderContext.CurrentCamera);

            if (_sampleCount < 2)
                return;

            UnityEngine.Graphics.DrawMesh(
                _mesh,
                Matrix4x4.identity,
                trailMaterial,
                gameObject.layer,
                GlobalRenderContext.CurrentCamera,
                0,
                null,
                shadowCastingMode,
                receiveShadows,
                null,
                LightProbeUsage.Off,
                null);
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!IsFinite(shiftOffset) || !math.isfinite(shiftSqrMagnitude))
            {
                ClearTrail();
                return;
            }

            if (shiftSqrMagnitude <= 0.000001f)
                return;

            if (_hasLastSample && !TryRefreshLastSampleRuntimePosition())
            {
                ClearTrail();
                return;
            }

            _meshDirty = true;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                ITickDispatcher tickDispatcher = currentService as ITickDispatcher;
                if (!ReferenceEquals(_tickDispatcher, tickDispatcher))
                {
                    TryUnregisterDispatcherTicks();
                    _tickDispatcher = tickDispatcher;
                }

                _dispatcherReady = tickDispatcher != null;
                TryRegister();
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.RenderDispatcher)
            {
                RenderDispatcher renderDispatcher = currentService as RenderDispatcher;
                if (!ReferenceEquals(_renderDispatcher, renderDispatcher))
                {
                    if (_registeredRender)
                    {
                        GlobalRegistry.Renderables.TryUnregister(this);
                        _registeredRender = false;
                    }

                    _renderDispatcher = renderDispatcher;
                }

                _renderDispatcherReady = renderDispatcher != null;
                TryRegister();
            }
        }

        public void ClearTrail()
        {
            _headIndex = -1;
            _sampleCount = 0;
            _hasLastSample = false;
            _meshDirty = true;
            if (_mesh != null)
                _mesh.Clear(false);
        }

        private void EnsureBuffers()
        {
            int requestedCapacity = math.clamp(capacity, MinimumCapacity, MaximumCapacity);
            if (HasBuffersReady(requestedCapacity))
                return;

            _resolvedCapacity = requestedCapacity;
            _samples = new TrailSample[_resolvedCapacity]; // COLD ALLOC: TrailSample[capacity] - AUP ring buffer - owner: NativeTrailRenderer
            _vertices = new Vector3[_resolvedCapacity * 2]; // COLD ALLOC: Vector3[capacity*2] - generated trail mesh vertices - owner: NativeTrailRenderer
            _uvs = new Vector2[_resolvedCapacity * 2]; // COLD ALLOC: Vector2[capacity*2] - generated trail mesh UVs - owner: NativeTrailRenderer
            _colors = new Color[_resolvedCapacity * 2]; // COLD ALLOC: Color[capacity*2] - generated trail mesh colors - owner: NativeTrailRenderer
            _triangles = new int[(_resolvedCapacity - 1) * 6]; // COLD ALLOC: Int32[(capacity-1)*6] - generated trail mesh indices - owner: NativeTrailRenderer
            _headIndex = -1;
            _sampleCount = 0;
            _hasLastSample = false;
            _meshDirty = true;

            if (_mesh == null)
            {
                _mesh = new Mesh
                {
                    name = "Hecton Native AUP Trail"
                }; // COLD ALLOC: Mesh[1] - generated AUP trail mesh - owner: NativeTrailRenderer
                _mesh.MarkDynamic();
            }

            _bufferRepairRequested = false;
        }

        private bool HasBuffersReady()
        {
            int requestedCapacity = math.clamp(capacity, MinimumCapacity, MaximumCapacity);
            return HasBuffersReady(requestedCapacity);
        }

        private bool HasBuffersReady(int requestedCapacity)
        {
            return _resolvedCapacity == requestedCapacity &&
                   _samples != null &&
                   _samples.Length >= requestedCapacity &&
                   _vertices != null &&
                   _vertices.Length >= requestedCapacity * 2 &&
                   _uvs != null &&
                   _uvs.Length >= requestedCapacity * 2 &&
                   _colors != null &&
                   _colors.Length >= requestedCapacity * 2 &&
                   _triangles != null &&
                   _triangles.Length >= (requestedCapacity - 1) * 6 &&
                   _mesh != null;
        }

        private void QueueBufferRepair()
        {
            _bufferRepairRequested = true;
        }

        private void AdvanceAges(float deltaTime)
        {
            if (_sampleCount <= 0 || deltaTime <= 0f)
                return;

            for (int visualIndex = 0; visualIndex < _sampleCount; visualIndex++)
            {
                int sampleIndex = ResolveRingIndex(visualIndex);
                TrailSample sample = _samples[sampleIndex];
                if (sample.Active == 0)
                    continue;

                sample.AgeSeconds += deltaTime;
                _samples[sampleIndex] = sample;
            }

            bool removedExpired = false;
            float lifetime = math.max(MinimumLifetimeSeconds, lifetimeSeconds);
            while (_sampleCount > 0)
            {
                int oldestIndex = ResolveRingIndex(0);
                if (_samples[oldestIndex].AgeSeconds <= lifetime)
                    break;

                _samples[oldestIndex].Active = 0;
                _sampleCount--;
                removedExpired = true;
            }

            if (removedExpired || _sampleCount > 1)
                _meshDirty = true;
        }

        private bool ShouldForceDistanceSample(Vector3 runtimePosition)
        {
            if (!_hasLastSample)
                return true;

            float minDistance = math.max(0f, minimumSampleDistanceMeters);
            if (minDistance <= 0f)
                return true;

            Vector3 delta = runtimePosition - _lastSampleRuntimePosition;
            return IsFinite(delta) && delta.sqrMagnitude >= minDistance * minDistance;
        }

        private void AddSample(Vector3 runtimePosition)
        {
            if (_resolvedCapacity < MinimumCapacity)
                return;

            if (!TryResolveRuntimeAup(runtimePosition, out AbsoluteUniversePosition aup))
                return;

            _headIndex = (_headIndex + 1) % _resolvedCapacity;
            _samples[_headIndex] = new TrailSample
            {
                Position = aup,
                AgeSeconds = 0f,
                WidthMeters = math.max(MinimumWidthMeters, widthMeters),
                Active = 1
            };

            if (_sampleCount < _resolvedCapacity)
                _sampleCount++;

            _lastSampleAup = aup;
            _lastSampleRuntimePosition = runtimePosition;
            _hasLastSample = true;
            _meshDirty = true;
        }

        private void RebuildMesh(Camera renderCamera)
        {
            _meshDirty = false;
            if (_sampleCount < 2)
            {
                _mesh.Clear(false);
                return;
            }

            int vertexCount = _sampleCount * 2;
            int triangleCount = (_sampleCount - 1) * 6;
            Vector3 cameraPosition = renderCamera != null ? renderCamera.transform.position : Vector3.zero;
            Vector3 fallbackRight = renderCamera != null ? renderCamera.transform.right : transform.right;
            float lifetime = math.max(MinimumLifetimeSeconds, lifetimeSeconds);

            for (int visualIndex = 0; visualIndex < _sampleCount; visualIndex++)
            {
                Vector3 runtimePosition = ResolveSampleRuntimePosition(visualIndex);
                Vector3 previousPosition = ResolveSampleRuntimePosition(math.max(0, visualIndex - 1));
                Vector3 nextPosition = ResolveSampleRuntimePosition(math.min(_sampleCount - 1, visualIndex + 1));
                Vector3 tangent = NormalizeOrFallback(nextPosition - previousPosition, transform.forward);
                Vector3 toCamera = NormalizeOrFallback(cameraPosition - runtimePosition, fallbackRight);
                Vector3 side = NormalizeOrFallback(Vector3.Cross(tangent, toCamera), fallbackRight);

                TrailSample sample = _samples[ResolveRingIndex(visualIndex)];
                float fade01 = math.saturate(1f - (sample.AgeSeconds / lifetime));
                Color sampleColor = Color.Lerp(tailColor, headColor, fade01);
                float halfWidth = math.max(MinimumWidthMeters, sample.WidthMeters) * 0.5f * fade01;
                int vertexIndex = visualIndex * 2;
                _vertices[vertexIndex] = runtimePosition - side * halfWidth;
                _vertices[vertexIndex + 1] = runtimePosition + side * halfWidth;
                float uvX = _sampleCount > 1 ? visualIndex / (float)(_sampleCount - 1) : 0f;
                _uvs[vertexIndex] = new Vector2(uvX, 0f);
                _uvs[vertexIndex + 1] = new Vector2(uvX, 1f);
                _colors[vertexIndex] = sampleColor;
                _colors[vertexIndex + 1] = sampleColor;
            }

            for (int segmentIndex = 0; segmentIndex < _sampleCount - 1; segmentIndex++)
            {
                int vertexIndex = segmentIndex * 2;
                int triangleIndex = segmentIndex * 6;
                _triangles[triangleIndex] = vertexIndex;
                _triangles[triangleIndex + 1] = vertexIndex + 2;
                _triangles[triangleIndex + 2] = vertexIndex + 1;
                _triangles[triangleIndex + 3] = vertexIndex + 1;
                _triangles[triangleIndex + 4] = vertexIndex + 2;
                _triangles[triangleIndex + 5] = vertexIndex + 3;
            }

            _mesh.Clear(false);
            _mesh.SetVertices(_vertices, 0, vertexCount);
            _mesh.SetUVs(0, _uvs, 0, vertexCount);
            _mesh.SetColors(_colors, 0, vertexCount);
            _mesh.SetTriangles(_triangles, 0, triangleCount, 0, false);
            _mesh.RecalculateBounds();
        }

        private Vector3 ResolveSampleRuntimePosition(int visualIndex)
        {
            TrailSample sample = _samples[ResolveRingIndex(visualIndex)];
            float3 runtime = sample.Position.ToRuntimeFloat3();
            return new Vector3(runtime.x, runtime.y, runtime.z);
        }

        private int ResolveRingIndex(int visualIndex)
        {
            int startIndex = _headIndex - _sampleCount + 1;
            while (startIndex < 0)
                startIndex += _resolvedCapacity;

            return (startIndex + visualIndex) % _resolvedCapacity;
        }

        private void TryRegister()
        {
            if (!Application.isPlaying)
                return;

            if (_dispatcherReady && !_registeredUpdate)
                _registeredUpdate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            if (_dispatcherReady && !_registeredColdTick)
                _registeredColdTick = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment);
            if (_renderDispatcherReady && !_registeredRender)
                _registeredRender = GlobalRegistry.Renderables.TryRegister(this);
            if (_dispatcherReady && !_registeredOriginShift)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _registeredOriginShift = HectonFloatingOrigin.IsListenerRegistered(this);
            }
        }

        private void TryRegisterHotSwap()
        {
            if (!Application.isPlaying || _registeredHotSwap)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void RefreshDispatcherReadyCold()
        {
            _tickDispatcher = GlobalRegistry.TickDispatcher;
            _renderDispatcher = GlobalRegistry.RenderDispatcher;
            _dispatcherReady = _tickDispatcher != null;
            _renderDispatcherReady = _renderDispatcher != null;
        }

        private void TryUnregisterHotSwap()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
            _tickDispatcher = null;
            _renderDispatcher = null;
            _dispatcherReady = false;
            _renderDispatcherReady = false;
        }

        private void TryUnregister()
        {
            if (_registeredOriginShift)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShift = false;
            }

            if (_registeredRender)
            {
                GlobalRegistry.Renderables.TryUnregister(this);
                _registeredRender = false;
            }

            if (_registeredUpdate)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredUpdate = false;
            }

            if (_registeredColdTick)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
                _registeredColdTick = false;
            }
        }

        private void TryUnregisterDispatcherTicks()
        {
            if (_registeredUpdate)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredUpdate = false;
            }

            if (_registeredColdTick)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
                _registeredColdTick = false;
            }
        }

        private static Vector3 NormalizeOrFallback(Vector3 value, Vector3 fallback)
        {
            if (!IsFinite(value))
                return IsFinite(fallback) ? fallback : Vector3.forward;

            float lengthSq = value.sqrMagnitude;
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return IsFinite(fallback) ? fallback : Vector3.forward;

            float invLength = math.rsqrt(lengthSq);
            return new Vector3(value.x * invLength, value.y * invLength, value.z * invLength);
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFinite(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private bool TryRefreshLastSampleRuntimePosition()
        {
            if (!_lastSampleAup.TryToRuntimeFloat3(out float3 runtime) ||
                !math.all(math.isfinite(runtime)))
            {
                return false;
            }

            _lastSampleRuntimePosition = new Vector3(runtime.x, runtime.y, runtime.z);
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }
    }
}
