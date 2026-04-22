using Hecton8.Core;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.World
{
    /// <summary>
    /// Draws far-field cartographer HLODs through one indirect call with per-instance fade.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-89)]
    public sealed class HectonHLODRenderer : MonoBehaviour, ITickable
    {
        private const int IndirectArgsCount = 5;
#if UNITY_EDITOR
        private const string ShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_HLODUnlitFog.shader";
#endif

        private static readonly int InstanceMatricesId = Shader.PropertyToID("_HectonHLODInstanceMatrices");
        private static readonly int InstanceFadeId = Shader.PropertyToID("_HectonHLODInstanceFade");
        private static readonly int GlobalFloatingOffsetId = Shader.PropertyToID("_GlobalFloatingOffset");

        [Header("-- Rendering ----------------")]
        [SerializeField]
        [Tooltip("Shared HLOD mesh drawn for every published far-field instance.")]
        private Mesh _mesh;

        [SerializeField]
        [Tooltip("Optional explicit HLOD material. Hidden fallback is used when empty.")]
        private Material _material;

        [SerializeField]
        [Tooltip("Optional hidden shader fallback used for the runtime HLOD material.")]
        private Shader _shader;

        [SerializeField]
        [Tooltip("Submesh index rendered through the indirect call.")]
        private int _subMeshIndex;

        [SerializeField]
        [Tooltip("Optional camera override. Leave null for all cameras.")]
        private Camera _cameraOverride;

        [Header("-- Bounds -------------------")]
        [SerializeField]
        [Tooltip("Fallback center offset used when no explicit HLOD bounds were published.")]
        private Vector3 _boundsCenterOffset = Vector3.zero;

        [SerializeField]
        [Tooltip("Fallback conservative HLOD bounds.")]
        private Vector3 _boundsSize = new Vector3(3000f, 1600f, 3000f);

        private ComputeBuffer _matrixBuffer;
        private ComputeBuffer _fadeBuffer;
        private ComputeBuffer _argsBuffer;
        private MaterialPropertyBlock _propertyBlock;
        private Material _runtimeMaterial;
        private uint[] _indirectArgs;
        private NativeArray<Matrix4x4> _uploadedMatrices;
        private NativeArray<Vector4> _uploadedFade;
        private ComputeBuffer _uploadedMatrixBuffer;
        private ComputeBuffer _uploadedFadeBuffer;
        private Bounds _drawBounds;
        private int _instanceCount;
        private bool _hasBoundsOverride;
        private bool _isRegistered;
        private bool _ownsRuntimeMaterial;
        private bool _argsDirty = true;
        private Vector4 _lastGlobalFloatingOffset = new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - HLOD indirect properties - owner: HectonHLODRenderer
            _indirectArgs = new uint[IndirectArgsCount]; // COLD ALLOC: uint[5] - HLOD indirect args payload - owner: HectonHLODRenderer
            _drawBounds = new Bounds(transform.position + _boundsCenterOffset, _boundsSize);
            EnsureResources();
        }

        private void OnEnable()
        {
            RegisterTick();
        }

        private void OnDisable()
        {
            UnregisterTick();
        }

        private void OnDestroy()
        {
            ReleaseResources();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (_instanceCount <= 0 || _matrixBuffer == null || _fadeBuffer == null)
                return;

            EnsureResources();
            if (_argsBuffer == null)
                return;

            UpdateArgsBuffer();
            Material activeMaterial = ResolveMaterial();
            if (_mesh == null || activeMaterial == null)
                return;

            _propertyBlock.SetBuffer(InstanceMatricesId, _matrixBuffer);
            _propertyBlock.SetBuffer(InstanceFadeId, _fadeBuffer);

            Vector4 globalFloatingOffset = ResolveGlobalFloatingOffset();
            if (_lastGlobalFloatingOffset != globalFloatingOffset)
            {
                _propertyBlock.SetVector(GlobalFloatingOffsetId, globalFloatingOffset);
                _lastGlobalFloatingOffset = globalFloatingOffset;
            }

            Graphics.DrawMeshInstancedIndirect(
                _mesh,
                Mathf.Max(0, _subMeshIndex),
                activeMaterial,
                ResolveDrawBounds(),
                _argsBuffer,
                0,
                _propertyBlock,
                ShadowCastingMode.Off,
                false,
                gameObject.layer,
                _cameraOverride,
                LightProbeUsage.Off,
                null);
        }

        /// <summary>
        /// Uploads cartographer-owned HLOD instances into renderer-owned indirect buffers without managed allocations.
        /// </summary>
        public void BindNativeInstances(NativeArray<HLODInstance> instances, int instanceCount)
        {
            if (!instances.IsCreated || instanceCount <= 0 || instances.Length < instanceCount)
            {
                ClearBinding();
                return;
            }

            EnsureOwnedUploadCapacity(instanceCount);
            if (!_uploadedMatrices.IsCreated || !_uploadedFade.IsCreated || _uploadedMatrixBuffer == null || _uploadedFadeBuffer == null)
            {
                ClearBinding();
                return;
            }

            Vector4 globalFloatingOffset = ResolveGlobalFloatingOffset();
            Vector3 floatingOffset = new Vector3(globalFloatingOffset.x, globalFloatingOffset.y, globalFloatingOffset.z);
            Bounds combinedBounds = default;
            bool hasCombinedBounds = false;
            for (int i = 0; i < instanceCount; i++)
            {
                HLODInstance instance = instances[i];
                _uploadedMatrices[i] = instance.LocalToWorld;
                _uploadedFade[i] = new Vector4(Mathf.Clamp01(instance.Fade01), 0f, 0f, 0f);

                Bounds worldBounds = instance.LocalBounds;
                worldBounds.center += floatingOffset;
                if (hasCombinedBounds)
                    combinedBounds.Encapsulate(worldBounds);
                else
                {
                    combinedBounds = worldBounds;
                    hasCombinedBounds = true;
                }
            }

            _uploadedMatrixBuffer.SetData(_uploadedMatrices, 0, 0, instanceCount);
            _uploadedFadeBuffer.SetData(_uploadedFade, 0, 0, instanceCount);
            _matrixBuffer = _uploadedMatrixBuffer;
            _fadeBuffer = _uploadedFadeBuffer;
            _instanceCount = instanceCount;
            _drawBounds = hasCombinedBounds ? combinedBounds : new Bounds(transform.position + _boundsCenterOffset, _boundsSize);
            _hasBoundsOverride = hasCombinedBounds;
            _argsDirty = true;
        }

        /// <summary>
        /// Clears the current HLOD binding and suppresses rendering until a new instance list arrives.
        /// </summary>
        public void ClearBinding()
        {
            _matrixBuffer = null;
            _fadeBuffer = null;
            _instanceCount = 0;
            _hasBoundsOverride = false;
            _argsDirty = true;
        }

        private void RegisterTick()
        {
            if (_isRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register(this);
            _isRegistered = true;
        }

        private void UnregisterTick()
        {
            if (!_isRegistered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister(this);

            _isRegistered = false;
        }

        private void EnsureResources()
        {
            if (_argsBuffer == null)
            {
                _argsBuffer = new ComputeBuffer(1, sizeof(uint) * IndirectArgsCount, ComputeBufferType.IndirectArguments); // COLD ALLOC: ComputeBuffer[1] - HLOD indirect args buffer - owner: HectonHLODRenderer
                _argsDirty = true;
            }

#if UNITY_EDITOR
            if (_shader == null)
                _shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
#endif

            if (_material == null && _runtimeMaterial == null && _shader != null)
            {
                _runtimeMaterial = new Material(_shader)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    name = "__HectonHLODRuntimeMaterial",
                    enableInstancing = true
                }; // COLD ALLOC: Material[1] - hidden first-party HLOD material - owner: HectonHLODRenderer
                _ownsRuntimeMaterial = true;
            }
        }

        private void EnsureOwnedUploadCapacity(int instanceCount)
        {
            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, instanceCount));
            if (_uploadedMatrices.IsCreated &&
                _uploadedMatrices.Length >= nextCapacity &&
                _uploadedFade.IsCreated &&
                _uploadedFade.Length >= nextCapacity &&
                _uploadedMatrixBuffer != null &&
                _uploadedMatrixBuffer.count >= nextCapacity &&
                _uploadedFadeBuffer != null &&
                _uploadedFadeBuffer.count >= nextCapacity)
            {
                return;
            }

            if (_uploadedMatrices.IsCreated)
                _uploadedMatrices.Dispose();
            if (_uploadedFade.IsCreated)
                _uploadedFade.Dispose();

            if (_uploadedMatrixBuffer != null)
            {
                _uploadedMatrixBuffer.Release();
                _uploadedMatrixBuffer = null;
            }

            if (_uploadedFadeBuffer != null)
            {
                _uploadedFadeBuffer.Release();
                _uploadedFadeBuffer = null;
            }

            _uploadedMatrices = new NativeArray<Matrix4x4>(nextCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Matrix4x4>[NextPowerOfTwo(requiredCount)] - HLOD matrix upload cache - owner: HectonHLODRenderer
            _uploadedFade = new NativeArray<Vector4>(nextCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Vector4>[NextPowerOfTwo(requiredCount)] - HLOD fade upload cache - owner: HectonHLODRenderer
            _uploadedMatrixBuffer = new ComputeBuffer(nextCapacity, sizeof(float) * 16, ComputeBufferType.Structured); // COLD ALLOC: ComputeBuffer[NextPowerOfTwo(requiredCount)] - HLOD matrix buffer - owner: HectonHLODRenderer
            _uploadedFadeBuffer = new ComputeBuffer(nextCapacity, sizeof(float) * 4, ComputeBufferType.Structured); // COLD ALLOC: ComputeBuffer[NextPowerOfTwo(requiredCount)] - HLOD fade buffer - owner: HectonHLODRenderer
        }

        private void UpdateArgsBuffer()
        {
            if (!_argsDirty || _argsBuffer == null)
                return;

            bool validSubMesh = _mesh != null && _subMeshIndex >= 0 && _subMeshIndex < _mesh.subMeshCount;
            uint indexCount = validSubMesh ? _mesh.GetIndexCount(_subMeshIndex) : 0u;
            uint indexStart = validSubMesh ? _mesh.GetIndexStart(_subMeshIndex) : 0u;
            uint baseVertex = validSubMesh ? _mesh.GetBaseVertex(_subMeshIndex) : 0u;

            _indirectArgs[0] = indexCount;
            _indirectArgs[1] = (uint)Mathf.Max(0, _instanceCount);
            _indirectArgs[2] = indexStart;
            _indirectArgs[3] = baseVertex;
            _indirectArgs[4] = 0u;
            _argsBuffer.SetData(_indirectArgs);
            _argsDirty = false;
        }

        private Material ResolveMaterial()
        {
            return _material != null ? _material : _runtimeMaterial;
        }

        private Bounds ResolveDrawBounds()
        {
            if (_hasBoundsOverride)
                return _drawBounds;

            return new Bounds(transform.position + _boundsCenterOffset, _boundsSize);
        }

        private void ReleaseResources()
        {
            if (_argsBuffer != null)
            {
                _argsBuffer.Release();
                _argsBuffer = null;
            }

            if (_uploadedMatrixBuffer != null)
            {
                _uploadedMatrixBuffer.Release();
                _uploadedMatrixBuffer = null;
            }

            if (_uploadedFadeBuffer != null)
            {
                _uploadedFadeBuffer.Release();
                _uploadedFadeBuffer = null;
            }

            if (_uploadedMatrices.IsCreated)
                _uploadedMatrices.Dispose();
            if (_uploadedFade.IsCreated)
                _uploadedFade.Dispose();

            if (_ownsRuntimeMaterial && _runtimeMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(_runtimeMaterial);
                else
                    DestroyImmediate(_runtimeMaterial);
            }

            _runtimeMaterial = null;
            _ownsRuntimeMaterial = false;
        }

        private static Vector4 ResolveGlobalFloatingOffset()
        {
            Vector3 totalOffset = HectonMapMagicVegetationBridge.GlobalTotalUniverseOffset;
            return new Vector4(totalOffset.x, totalOffset.y, totalOffset.z, 0f);
        }
    }
}
