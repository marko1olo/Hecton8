using Hecton8.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    /// <summary>
    /// Indirect renderer for dense procedural vegetation driven by an external matrix buffer.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
    public sealed class HectonIndirectVegetationRenderer : MonoBehaviour, ITickable
    {
        /// <summary>Stride of one Matrix4x4 entry expected in the external instance buffer.</summary>
        public const int InstanceMatrixStride = 64;

        private const int IndirectArgsCount = 5;
        private static readonly int _InstanceMatricesId = Shader.PropertyToID("_HectonInstanceMatrices");

        [Header("Rendering")]
        [SerializeField]
        [Tooltip("Material that consumes _HectonInstanceMatrices in the vertex stage.")]
        private Material _material;

        [SerializeField]
        [Tooltip("Optional authored mesh. If empty, a strip mesh is generated once at runtime.")]
        private Mesh _mesh;

        [SerializeField]
        [Tooltip("Submesh index rendered through the indirect draw call.")]
        private int _subMeshIndex;

        [SerializeField]
        [Tooltip("Optional camera override. Leave null to render in all cameras.")]
        private Camera _cameraOverride;

        [SerializeField]
        [Tooltip("Shadow mode for the indirect vegetation draw call.")]
        private ShadowCastingMode _shadowCastingMode = ShadowCastingMode.Off;

        [SerializeField]
        [Tooltip("Whether the indirect vegetation draw call should receive shadows.")]
        private bool _receiveShadows = false;

        [Header("Runtime Mesh")]
        [SerializeField]
        [Tooltip("Generates a single strip mesh once at runtime when no authored mesh is assigned.")]
        private bool _generateMeshAtRuntime = true;

        [SerializeField, Range(4, 6)]
        [Tooltip("Strip segment count. User task requires 4-6 segments.")]
        private int _segmentCount = 5;

        [SerializeField, Min(0.05f)]
        [Tooltip("Generated strip height.")]
        private float _stripHeight = 1.8f;

        [SerializeField, Min(0.005f)]
        [Tooltip("Generated strip width at the base.")]
        private float _stripBaseWidth = 0.12f;

        [SerializeField, Min(0.001f)]
        [Tooltip("Generated strip width at the tip.")]
        private float _stripTipWidth = 0.015f;

        [Header("Draw Bounds")]
        [SerializeField]
        [Tooltip("Local center offset used when no explicit bounds override is supplied.")]
        private Vector3 _boundsCenterOffset = Vector3.zero;

        [SerializeField]
        [Tooltip("Fallback draw bounds size used when no explicit bounds override is supplied.")]
        private Vector3 _boundsSize = new Vector3(128f, 32f, 128f);

        private Mesh _generatedMesh;
        private ComputeBuffer _instanceMatrixBuffer;
        private ComputeBuffer _indirectArgsBuffer;
        private MaterialPropertyBlock _materialPropertyBlock;
        private Bounds _explicitBounds;
        private bool _hasBoundsOverride;
        private bool _isRegistered;
        private bool _argsDirty = true;
        private bool _bufferBindingDirty;
        private int _instanceCount;

        // COLD ALLOC: uint[5] - indirect draw arguments payload - owner: HectonIndirectVegetationRenderer
        private readonly uint[] _indirectArgs = new uint[IndirectArgsCount];

        private void Awake()
        {
            if (_material == null)
            {
                Debug.LogError("[HectonIndirectVegetationRenderer] Material is required.", this);
                enabled = false;
                return;
            }

            if (_generateMeshAtRuntime || _mesh == null)
            {
                _generatedMesh = HectonProceduralVegetationStripBuilder.Build(
                    $"{nameof(HectonIndirectVegetationRenderer)}_Strip",
                    _segmentCount,
                    _stripHeight,
                    _stripBaseWidth,
                    _stripTipWidth);
            }

            Mesh renderMesh = ResolveRenderMesh();
            if (renderMesh == null)
            {
                Debug.LogError("[HectonIndirectVegetationRenderer] No render mesh resolved.", this);
                enabled = false;
                return;
            }

            _subMeshIndex = Mathf.Clamp(_subMeshIndex, 0, renderMesh.subMeshCount - 1);

            // COLD ALLOC: MaterialPropertyBlock[1] - indirect vegetation draw property block - owner: HectonIndirectVegetationRenderer
            _materialPropertyBlock = new MaterialPropertyBlock();
            // COLD ALLOC: ComputeBuffer[1] - indirect arguments buffer - owner: HectonIndirectVegetationRenderer
            _indirectArgsBuffer = new ComputeBuffer(1, sizeof(uint) * IndirectArgsCount, ComputeBufferType.IndirectArguments);
            _bufferBindingDirty = true;
            RefreshArgsBuffer();
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();

            if (_indirectArgsBuffer != null)
            {
                _indirectArgsBuffer.Release();
                _indirectArgsBuffer = null;
            }

            if (_generatedMesh != null)
            {
                Destroy(_generatedMesh);
                _generatedMesh = null;
            }
        }

        /// <summary>
        /// Binds the external per-instance matrix buffer populated by another system.
        /// </summary>
        /// <param name="instanceMatrixBuffer">Structured buffer of Matrix4x4 transforms.</param>
        /// <param name="instanceCount">Active instance count contained in the buffer.</param>
        public void BindInstanceBuffer(ComputeBuffer instanceMatrixBuffer, int instanceCount)
        {
            _instanceMatrixBuffer = instanceMatrixBuffer;
            _bufferBindingDirty = true;
            SetInstanceCount(instanceCount);
        }

        /// <summary>
        /// Clears the current external instance buffer binding.
        /// </summary>
        public void ClearInstanceBuffer()
        {
            _instanceMatrixBuffer = null;
            _instanceCount = 0;
            _argsDirty = true;
            _bufferBindingDirty = true;
        }

        /// <summary>
        /// Updates the active instance count used by the indirect args buffer.
        /// </summary>
        /// <param name="instanceCount">Number of instances to draw.</param>
        public void SetInstanceCount(int instanceCount)
        {
            int clampedCount = Mathf.Max(0, instanceCount);
            if (_instanceCount == clampedCount)
                return;

            _instanceCount = clampedCount;
            _argsDirty = true;
        }

        /// <summary>
        /// Overrides the world-space draw bounds used by the indirect draw call.
        /// </summary>
        /// <param name="drawBounds">Explicit world-space bounds.</param>
        public void SetDrawBounds(Bounds drawBounds)
        {
            _explicitBounds = drawBounds;
            _hasBoundsOverride = true;
        }

        /// <summary>
        /// Returns to transform-relative fallback draw bounds.
        /// </summary>
        public void ClearDrawBoundsOverride()
        {
            _hasBoundsOverride = false;
        }

        /// <summary>
        /// Executes the indirect vegetation draw call.
        /// </summary>
        /// <param name="deltaTime">Unused current frame delta required by ITickable.</param>
        public void Tick(float deltaTime)
        {
            if (_instanceMatrixBuffer == null || _instanceCount <= 0 || _material == null)
                return;

            Mesh renderMesh = ResolveRenderMesh();
            if (renderMesh == null || _indirectArgsBuffer == null || _materialPropertyBlock == null)
                return;

            if (_bufferBindingDirty)
            {
                _materialPropertyBlock.SetBuffer(_InstanceMatricesId, _instanceMatrixBuffer);
                _bufferBindingDirty = false;
            }

            if (_argsDirty)
                RefreshArgsBuffer();

            Bounds drawBounds = _hasBoundsOverride
                ? _explicitBounds
                : new Bounds(transform.position + _boundsCenterOffset, _boundsSize);

#pragma warning disable CS0618
            Graphics.DrawMeshInstancedIndirect(
                renderMesh,
                _subMeshIndex,
                _material,
                drawBounds,
                _indirectArgsBuffer,
                0,
                _materialPropertyBlock,
                _shadowCastingMode,
                _receiveShadows,
                gameObject.layer,
                _cameraOverride,
                LightProbeUsage.Off);
#pragma warning restore CS0618
        }

        private Mesh ResolveRenderMesh()
        {
            return _generatedMesh != null ? _generatedMesh : _mesh;
        }

        private void RefreshArgsBuffer()
        {
            Mesh renderMesh = ResolveRenderMesh();
            if (renderMesh == null || _indirectArgsBuffer == null)
                return;

            _subMeshIndex = Mathf.Clamp(_subMeshIndex, 0, renderMesh.subMeshCount - 1);
            _indirectArgs[0] = (uint)renderMesh.GetIndexCount(_subMeshIndex);
            _indirectArgs[1] = (uint)_instanceCount;
            _indirectArgs[2] = (uint)renderMesh.GetIndexStart(_subMeshIndex);
            _indirectArgs[3] = (uint)renderMesh.GetBaseVertex(_subMeshIndex);
            _indirectArgs[4] = 0u;
            _indirectArgsBuffer.SetData(_indirectArgs);
            _argsDirty = false;
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
