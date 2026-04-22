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
    /// Draws distant landmark silhouettes through one indirect call using an externally owned matrix buffer.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
    public sealed class HectonDistantLandmarkRenderer : MonoBehaviour, ITickable
    {
        private const int IndirectArgsCount = 5;
#if UNITY_EDITOR
        private const string SilhouetteShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_DistantLandmarkSilhouette.shader";
#endif

        private static readonly int LandmarkMatricesId = Shader.PropertyToID("_HectonLandmarkMatrices");
        private static readonly int LandmarkFadeId = Shader.PropertyToID("_HectonHLODInstanceFade");

        [Header("-- Rendering ----------------")]
        [SerializeField]
        [Tooltip("Shared mesh drawn for each distant landmark instance.")]
        private Mesh _mesh;

        [SerializeField]
        [Tooltip("Material used for the silhouette-only indirect draw. If empty, the hidden shader fallback is used.")]
        private Material _material;

        [SerializeField]
        [Tooltip("Optional shader fallback used to build the hidden landmark material when no material is assigned.")]
        private Shader _silhouetteShader;

        [SerializeField]
        [Tooltip("Submesh index rendered through DrawMeshInstancedIndirect.")]
        private int _subMeshIndex;

        [SerializeField]
        [Tooltip("Optional camera override. Leave null to let Unity draw for all cameras.")]
        private Camera _cameraOverride;

        [Header("-- Bounds -------------------")]
        [SerializeField]
        [Tooltip("Fallback local center offset used when no explicit bounds are published with the landmark buffer.")]
        private Vector3 _boundsCenterOffset = Vector3.zero;

        [SerializeField]
        [Tooltip("Fallback local bounds size used when no explicit world bounds are published with the landmark buffer.")]
        private Vector3 _boundsSize = new Vector3(1200f, 600f, 1200f);

        private ComputeBuffer _matrixBuffer;
        private ComputeBuffer _argsBuffer;
        private MaterialPropertyBlock _propertyBlock;
        private Material _runtimeMaterial;
        private uint[] _indirectArgs;
        private NativeArray<Matrix4x4> _uploadedLandmarkMatrices;
        private NativeArray<Vector4> _uploadedLandmarkFade;
        private ComputeBuffer _uploadedMatrixBuffer;
        private ComputeBuffer _uploadedFadeBuffer;
        private Bounds _drawBounds;
        private int _instanceCount;
        private bool _hasBoundsOverride;
        private bool _isRegistered;
        private bool _ownsRuntimeMaterial;
        private bool _argsDirty = true;

        /// <summary>
        /// Gets whether an external landmark matrix buffer is currently bound.
        /// </summary>
        public bool HasMatrixBuffer => _matrixBuffer != null;

        /// <summary>
        /// Gets the currently bound landmark instance count.
        /// </summary>
        public int BoundInstanceCount => _instanceCount;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - distant landmark indirect properties - owner: HectonDistantLandmarkRenderer
            _indirectArgs = new uint[IndirectArgsCount]; // COLD ALLOC: uint[5] - indirect draw args payload - owner: HectonDistantLandmarkRenderer
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
            if (_instanceCount <= 0 || _matrixBuffer == null)
                return;

            EnsureResources();
            if (_argsBuffer == null)
                return;

            UpdateArgsBuffer();
            Material activeMaterial = ResolveMaterial();
            if (_mesh == null || activeMaterial == null)
                return;

            _propertyBlock.SetBuffer(LandmarkMatricesId, _matrixBuffer);
            if (_uploadedFadeBuffer != null && _uploadedFadeBuffer.count >= _instanceCount)
                _propertyBlock.SetBuffer(LandmarkFadeId, _uploadedFadeBuffer);
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
        /// Binds the externally owned matrix buffer and world bounds used by the distant landmark draw.
        /// </summary>
        /// <param name="matrixBuffer">World matrix buffer with one <see cref="Matrix4x4"/> per landmark instance.</param>
        /// <param name="instanceCount">Visible landmark count stored in <paramref name="matrixBuffer"/>.</param>
        /// <param name="drawBounds">World-space bounds that conservatively cover the published landmarks.</param>
        public void BindInstanceBuffer(ComputeBuffer matrixBuffer, int instanceCount, Bounds drawBounds)
        {
            _matrixBuffer = matrixBuffer;
            _instanceCount = Mathf.Max(0, instanceCount);
            _drawBounds = drawBounds;
            _hasBoundsOverride = true;
            _argsDirty = true;
        }

        /// <summary>
        /// Uploads cartographer-owned landmark bounds into renderer-owned indirect matrix storage.
        /// Accepts <see cref="NativeList{T}.AsArray"/> without managed allocations.
        /// </summary>
        /// <param name="landmarkBounds">Native bounds list published by the cartographer.</param>
        /// <param name="landmarkCount">Valid landmark count.</param>
        public void BindNativeBounds(NativeArray<Bounds> landmarkBounds, int landmarkCount)
        {
            if (!landmarkBounds.IsCreated || landmarkCount <= 0 || landmarkBounds.Length < landmarkCount)
            {
                ClearBinding();
                return;
            }

            EnsureOwnedMatrixUploadCapacity(landmarkCount);
            if (!_uploadedLandmarkMatrices.IsCreated || _uploadedMatrixBuffer == null || !_uploadedLandmarkFade.IsCreated || _uploadedFadeBuffer == null)
            {
                ClearBinding();
                return;
            }

            Bounds combinedBounds = default;
            bool hasCombinedBounds = false;
            for (int i = 0; i < landmarkCount; i++)
            {
                Bounds landmark = landmarkBounds[i];
                Vector3 clampedSize = new Vector3(
                    Mathf.Max(0.5f, landmark.size.x),
                    Mathf.Max(0.5f, landmark.size.y),
                    Mathf.Max(0.5f, landmark.size.z));

                _uploadedLandmarkMatrices[i] = Matrix4x4.TRS(landmark.center, Quaternion.identity, clampedSize);
                _uploadedLandmarkFade[i] = new Vector4(1f, 0f, 0f, 0f);
                if (hasCombinedBounds)
                    combinedBounds.Encapsulate(landmark);
                else
                {
                    combinedBounds = landmark;
                    hasCombinedBounds = true;
                }
            }

            _uploadedMatrixBuffer.SetData(_uploadedLandmarkMatrices, 0, 0, landmarkCount);
            _uploadedFadeBuffer.SetData(_uploadedLandmarkFade, 0, 0, landmarkCount);
            _matrixBuffer = _uploadedMatrixBuffer;
            _instanceCount = landmarkCount;
            _drawBounds = hasCombinedBounds
                ? combinedBounds
                : new Bounds(transform.position + _boundsCenterOffset, _boundsSize);
            _hasBoundsOverride = hasCombinedBounds;
            _argsDirty = true;
        }

        /// <summary>
        /// Uploads bridge-owned HLOD payload into renderer-owned indirect matrix storage without managed allocations.
        /// </summary>
        /// <param name="hlodEntries">Native HLOD registry payload published by the world bridge.</param>
        /// <param name="hlodCount">Valid HLOD entry count.</param>
        public void BindNativeHLOD(NativeArray<HLODData> hlodEntries, int hlodCount)
        {
            if (!hlodEntries.IsCreated || hlodCount <= 0 || hlodEntries.Length < hlodCount)
            {
                ClearBinding();
                return;
            }

            EnsureOwnedMatrixUploadCapacity(hlodCount);
            if (!_uploadedLandmarkMatrices.IsCreated || _uploadedMatrixBuffer == null || !_uploadedLandmarkFade.IsCreated || _uploadedFadeBuffer == null)
            {
                ClearBinding();
                return;
            }

            Bounds combinedBounds = default;
            bool hasCombinedBounds = false;
            for (int i = 0; i < hlodCount; i++)
            {
                HLODData entry = hlodEntries[i];
                Vector3 clampedSize = new Vector3(
                    Mathf.Max(0.5f, entry.Size.x),
                    Mathf.Max(0.5f, entry.Size.y),
                    Mathf.Max(0.5f, entry.Size.z));
                Bounds bounds = new Bounds(entry.Center, clampedSize);
                _uploadedLandmarkMatrices[i] = Matrix4x4.TRS(entry.Center, Quaternion.identity, clampedSize);
                _uploadedLandmarkFade[i] = new Vector4(Mathf.Clamp01(entry.Fade01), 0f, 0f, 0f);
                if (hasCombinedBounds)
                    combinedBounds.Encapsulate(bounds);
                else
                {
                    combinedBounds = bounds;
                    hasCombinedBounds = true;
                }
            }

            _uploadedMatrixBuffer.SetData(_uploadedLandmarkMatrices, 0, 0, hlodCount);
            _uploadedFadeBuffer.SetData(_uploadedLandmarkFade, 0, 0, hlodCount);
            _matrixBuffer = _uploadedMatrixBuffer;
            _instanceCount = hlodCount;
            _drawBounds = hasCombinedBounds
                ? combinedBounds
                : new Bounds(transform.position + _boundsCenterOffset, _boundsSize);
            _hasBoundsOverride = hasCombinedBounds;
            _argsDirty = true;
        }

        /// <summary>
        /// Clears the current distant landmark binding and suppresses rendering until a new buffer is published.
        /// </summary>
        public void ClearBinding()
        {
            _matrixBuffer = null;
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
                _argsBuffer = new ComputeBuffer(1, sizeof(uint) * IndirectArgsCount, ComputeBufferType.IndirectArguments); // COLD ALLOC: ComputeBuffer[1] - indirect landmark args buffer - owner: HectonDistantLandmarkRenderer
                _argsDirty = true;
            }

#if UNITY_EDITOR
            if (_silhouetteShader == null)
                _silhouetteShader = AssetDatabase.LoadAssetAtPath<Shader>(SilhouetteShaderAssetPath);
#endif

            if (_material == null && _runtimeMaterial == null && _silhouetteShader != null)
            {
                _runtimeMaterial = new Material(_silhouetteShader)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    name = "__HectonDistantLandmarkRuntimeMaterial",
                    enableInstancing = true
                }; // COLD ALLOC: Material[1] - first-party hidden silhouette material - owner: HectonDistantLandmarkRenderer
                _ownsRuntimeMaterial = true;
            }
        }

        private void EnsureOwnedMatrixUploadCapacity(int instanceCount)
        {
            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, instanceCount));
            if (_uploadedLandmarkMatrices.IsCreated &&
                _uploadedLandmarkMatrices.Length >= nextCapacity &&
                _uploadedMatrixBuffer != null &&
                _uploadedMatrixBuffer.count >= nextCapacity &&
                _uploadedLandmarkFade.IsCreated &&
                _uploadedLandmarkFade.Length >= nextCapacity &&
                _uploadedFadeBuffer != null &&
                _uploadedFadeBuffer.count >= nextCapacity)
                return;

            if (_uploadedLandmarkMatrices.IsCreated)
                _uploadedLandmarkMatrices.Dispose();
            if (_uploadedLandmarkFade.IsCreated)
                _uploadedLandmarkFade.Dispose();

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

            _uploadedLandmarkMatrices = new NativeArray<Matrix4x4>(nextCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Matrix4x4>[NextPowerOfTwo(requiredCount)] - distant landmark native upload cache - owner: HectonDistantLandmarkRenderer
            _uploadedLandmarkFade = new NativeArray<Vector4>(nextCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<Vector4>[NextPowerOfTwo(requiredCount)] - distant landmark fade upload cache - owner: HectonDistantLandmarkRenderer
            _uploadedMatrixBuffer = new ComputeBuffer(nextCapacity, sizeof(float) * 16, ComputeBufferType.Structured); // COLD ALLOC: ComputeBuffer[NextPowerOfTwo(requiredCount)] - distant landmark indirect matrix upload buffer - owner: HectonDistantLandmarkRenderer
            _uploadedFadeBuffer = new ComputeBuffer(nextCapacity, sizeof(float) * 4, ComputeBufferType.Structured); // COLD ALLOC: ComputeBuffer[NextPowerOfTwo(requiredCount)] - distant landmark indirect fade upload buffer - owner: HectonDistantLandmarkRenderer
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

            if (_uploadedLandmarkMatrices.IsCreated)
                _uploadedLandmarkMatrices.Dispose();
            if (_uploadedLandmarkFade.IsCreated)
                _uploadedLandmarkFade.Dispose();

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
    }
}
