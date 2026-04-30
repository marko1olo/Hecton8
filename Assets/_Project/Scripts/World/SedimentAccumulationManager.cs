using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.World
{
    /// <summary>
    /// Captures exposed upward-facing surfaces from a top-down orthographic view and accumulates a global sediment mask.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-120)]
    public sealed class SedimentAccumulationManager : MonoBehaviour, IUpdatable, ISlowTickable
    {
        private const string SedimentComputeAssetPath = "Assets/_Project/Art/Shaders/SedimentAccumulation.compute";
        private const string CaptureShaderAssetPath = "Assets/_Project/Art/Shaders/Hecton_SedimentCapture.shader";
        private const string CaptureCameraName = "__SedimentCaptureCamera";

        private static readonly int _SedimentMaskTextureId = Shader.PropertyToID("_HectonSedimentMaskTex");
        private static readonly int _SedimentWorldRectId = Shader.PropertyToID("_HectonSedimentWorldRect");
        private static readonly int _SedimentOverlayParamsAId = Shader.PropertyToID("_HectonSedimentOverlayParamsA");
        private static readonly int _SedimentOverlayParamsBId = Shader.PropertyToID("_HectonSedimentOverlayParamsB");
        private static readonly int _SedimentTintAId = Shader.PropertyToID("_HectonSedimentTintA");
        private static readonly int _SedimentTintBId = Shader.PropertyToID("_HectonSedimentTintB");
        private static readonly int _SedimentCaptureParamsId = Shader.PropertyToID("_HectonSedimentCaptureParams");
        private static readonly int _SedimentCaptureTexId = Shader.PropertyToID("_HectonSedimentCaptureTex");
        private static readonly int _SedimentMaskReadId = Shader.PropertyToID("_HectonSedimentMaskRead");
        private static readonly int _SedimentMaskWriteId = Shader.PropertyToID("_HectonSedimentMaskWrite");
        private static readonly int _SedimentComputeParamsAId = Shader.PropertyToID("_HectonSedimentComputeParamsA");
        private static readonly int _SedimentComputeParamsBId = Shader.PropertyToID("_HectonSedimentComputeParamsB");

        [Header("── Runtime References ──────────────────")]
        [SerializeField, Tooltip("Optional explicit player transform. Runtime falls back to GlobalRegistry.Player.")]
        private Transform playerTransform;
        [SerializeField, Tooltip("Optional explicit player camera. Runtime falls back to GlobalRegistry.Player.PlayerCamera.")]
        private Camera playerCamera;
        [SerializeField, Tooltip("Compute kernel that accumulates exposed sediment over the capture mask.")]
        private ComputeShader sedimentCompute;
        [SerializeField, Tooltip("Replacement shader used by the hidden top-down capture camera.")]
        private Shader sedimentCaptureShader;

        [Header("── Capture Volume ───────────────────────")]
        [SerializeField, Min(32f), Tooltip("World-space width of the sediment capture square around the player.")]
        private float captureWorldSize = 160f;
        [SerializeField, Min(16f), Tooltip("Meters above the player used as the top of the orthographic sediment capture slab.")]
        private float captureHeightAbovePlayer = 96f;
        [SerializeField, Min(16f), Tooltip("Meters below the player retained in the orthographic sediment capture slab.")]
        private float captureDepthBelowPlayer = 180f;
        [SerializeField, Range(128, 1024), Tooltip("Capture texture resolution. Keep conservative for MX350.")]
        private int captureResolution = 512;
        [SerializeField, Min(0.05f), Tooltip("Interval in seconds between capture/compute updates.")]
        private float captureIntervalSeconds = 0.25f;

        [Header("── Sediment Accumulation ───────────────")]
        [SerializeField, Min(0.01f), Tooltip("Sediment gain per second on exposed upward-facing surfaces.")]
        private float depositionRate = 0.085f;
        [SerializeField, Min(0.01f), Tooltip("Sediment loss per second when exposure disappears.")]
        private float erosionRate = 0.04f;
        [SerializeField, Min(0.01f), Tooltip("Additional sediment loss when geometry height shifts between captures.")]
        private float geometryShiftLossRate = 0.12f;
        [SerializeField, Min(0.001f), Tooltip("Normalized height tolerance used to keep accumulation stable between trench/collapse edits.")]
        private float heightMatchTolerance = 0.018f;
        [SerializeField, Range(0f, 1f), Tooltip("Minimum up-facing normal Y required to start sediment deposition.")]
        private float upFacingThreshold = 0.7f;
        [SerializeField, Range(0.01f, 1f), Tooltip("Controls how aggressively sediment darkens and de-metallizes the underlying shader.")]
        private float overlayIntensity = 0.9f;

        [Header("── Sediment Surface Response ───────────")]
        [SerializeField, Tooltip("Primary silt color blended into exposed surfaces.")]
        private Color sedimentTintA = new Color(0.71f, 0.67f, 0.58f, 1f);
        [SerializeField, Tooltip("Secondary dune tint used for low-frequency ripple variation.")]
        private Color sedimentTintB = new Color(0.57f, 0.54f, 0.47f, 1f);
        [SerializeField, Range(0.02f, 0.35f), Tooltip("World-space ripple frequency used by the procedural sand normal.")]
        private float rippleScale = 0.11f;
        [SerializeField, Range(0.05f, 1f), Tooltip("Strength of the procedural dune normal blended over lit surfaces.")]
        private float rippleNormalStrength = 0.32f;
        [SerializeField, Range(0f, 0.2f), Tooltip("Metallic target when fully covered by sediment.")]
        private float sedimentMetallic = 0.03f;
        [SerializeField, Range(0f, 1f), Tooltip("Smoothness target when fully covered by sediment.")]
        private float sedimentSmoothness = 0.28f;

        [Header("── Debug ────────────────────────────────")]
        [SerializeField, Tooltip("Current capture anchor in runtime space.")]
        private Vector3 _debugAnchorWS;
        [SerializeField, Tooltip("Current sediment world rect (x, z, invWidth, invHeight).")]
        private Vector4 _debugWorldRect;
        [SerializeField, Tooltip("Current normalized coverage strength used by shader overlays.")]
        private float _debugOverlayIntensity;

        private Camera _captureCamera;
        private RenderTexture _captureTexture;
        private RenderTexture _sedimentRead;
        private RenderTexture _sedimentWrite;
        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _computeReady;
        private int _kernelIndex = -1;
        private float _captureTimer;
        private Vector4 _worldRect;

        private void Awake()
        {
            EnsureCaptureCamera();
            PublishFallbackGlobals();
        }

        private void OnEnable()
        {
            EnsureCaptureCamera();
            EnsureResources();
            TryRegisterTickHandlers();
        }

        private void OnDisable()
        {
            TryUnregisterTickHandlers();
            ReleaseResources();
            PublishFallbackGlobals();
        }

        private void OnDestroy()
        {
            TryUnregisterTickHandlers();
            ReleaseResources();
            ReleaseCaptureCamera();
            PublishFallbackGlobals();
        }

        /// <summary>
        /// Executes the periodic sediment capture and compute accumulation pass.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_computeReady)
            {
                return;
            }

            if (!TryResolveRuntimeReferences())
            {
                PublishFallbackGlobals();
                return;
            }

            _captureTimer += deltaTime;
            if (_captureTimer < captureIntervalSeconds)
                return;

            float captureDeltaTime = _captureTimer;
            _captureTimer = 0f;
            UpdateWorldRect();
            UpdateCaptureCamera();
            RenderCapture();
            DispatchAccumulation(captureDeltaTime);
            PublishGlobals();
        }

        /// <summary>
        /// Refreshes lazy runtime references and shader assets outside the hot path.
        /// </summary>
        public void SlowTick()
        {
            TryResolveRuntimeReferences();
            EnsureResources();
            if (_computeReady)
                PublishGlobals();
        }

        private bool TryResolveRuntimeReferences()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerTransform == null && playerContext != null)
                playerTransform = playerContext.PlayerTransform;

            if (playerCamera == null && playerContext != null)
                playerCamera = playerContext.PlayerCamera;

            return playerTransform != null;
        }

        private void EnsureCaptureCamera()
        {
            if (_captureCamera != null)
                return;

            Transform existing = transform.Find(CaptureCameraName);
            if (existing != null)
            {
                existing.TryGetComponent(out _captureCamera);
                if (_captureCamera != null)
                    return;
            }

            GameObject cameraObject = new GameObject(CaptureCameraName, typeof(Camera))
            {
                hideFlags = HideFlags.HideAndDontSave
            }; // COLD ALLOC: GameObject[1] - persistent hidden orthographic sediment capture camera root - owner: SedimentAccumulationManager
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _captureCamera = cameraObject.GetComponent<Camera>();
            _captureCamera.enabled = false;
            _captureCamera.orthographic = true;
            _captureCamera.clearFlags = CameraClearFlags.SolidColor;
            _captureCamera.backgroundColor = Color.black;
            _captureCamera.allowHDR = false;
            _captureCamera.allowMSAA = false;
            _captureCamera.useOcclusionCulling = false;
            _captureCamera.forceIntoRenderTexture = true;
            _captureCamera.cullingMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;
        }

        private void ReleaseCaptureCamera()
        {
            if (_captureCamera == null)
                return;

            if (_captureCamera.gameObject != null)
                Destroy(_captureCamera.gameObject);

            _captureCamera = null;
        }

        private void EnsureResources()
        {
#if UNITY_EDITOR
            if (sedimentCompute == null)
                sedimentCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(SedimentComputeAssetPath);

            if (sedimentCaptureShader == null)
                sedimentCaptureShader = AssetDatabase.LoadAssetAtPath<Shader>(CaptureShaderAssetPath);
#endif

            if (sedimentCompute == null || sedimentCaptureShader == null)
            {
                _computeReady = false;
                return;
            }

            if (_kernelIndex < 0)
                _kernelIndex = sedimentCompute.FindKernel("AccumulateSediment");

            EnsureRenderTargets();
            _computeReady = _kernelIndex >= 0 && _captureTexture != null && _sedimentRead != null && _sedimentWrite != null;
        }

        private void EnsureRenderTargets()
        {
            int safeResolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(captureResolution, 128, 1024));
            _captureTexture = EnsureRenderTexture(
                _captureTexture,
                safeResolution,
                safeResolution,
                GraphicsFormat.R16G16B16A16_SFloat,
                false,
                "__HectonSedimentCapture");
            _sedimentRead = EnsureRenderTexture(
                _sedimentRead,
                safeResolution,
                safeResolution,
                GraphicsFormat.R16G16B16A16_SFloat,
                true,
                "__HectonSedimentAccumRead");
            _sedimentWrite = EnsureRenderTexture(
                _sedimentWrite,
                safeResolution,
                safeResolution,
                GraphicsFormat.R16G16B16A16_SFloat,
                true,
                "__HectonSedimentAccumWrite");

            if (_captureCamera != null)
                _captureCamera.targetTexture = _captureTexture;
        }

        private static RenderTexture EnsureRenderTexture(
            RenderTexture texture,
            int width,
            int height,
            GraphicsFormat format,
            bool randomWrite,
            string textureName)
        {
            if (texture != null &&
                texture.width == width &&
                texture.height == height &&
                texture.graphicsFormat == format &&
                texture.enableRandomWrite == randomWrite)
            {
                return texture;
            }

            ReleaseRenderTexture(ref texture);

            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(width, height)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
                graphicsFormat = format,
                depthBufferBits = 0,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = randomWrite,
                sRGB = false
            };
            texture = new RenderTexture(descriptor)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            }; // COLD ALLOC: RenderTexture[1] - persistent sediment capture or accumulation target - owner: SedimentAccumulationManager
            texture.Create();
            return texture;
        }

        private static void ReleaseRenderTexture(ref RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();
            Destroy(texture);
            texture = null;
        }

        private void UpdateWorldRect()
        {
            Vector3 anchor = playerCamera != null ? playerCamera.transform.position : playerTransform.position;
            float worldSize = math.max(32f, captureWorldSize);
            float halfSize = worldSize * 0.5f;
            _worldRect = new Vector4(
                anchor.x - halfSize,
                anchor.z - halfSize,
                1f / worldSize,
                1f / worldSize);
            _debugAnchorWS = anchor;
            _debugWorldRect = _worldRect;
        }

        private void UpdateCaptureCamera()
        {
            if (_captureCamera == null || playerTransform == null)
                return;

            Vector3 anchor = playerCamera != null ? playerCamera.transform.position : playerTransform.position;
            float captureMinY = anchor.y - math.max(16f, captureDepthBelowPlayer);
            float captureMaxY = anchor.y + math.max(16f, captureHeightAbovePlayer);
            float captureHeight = math.max(32f, captureMaxY - captureMinY);

            Transform cameraTransform = _captureCamera.transform;
            cameraTransform.position = new Vector3(anchor.x, captureMaxY + 1f, anchor.z);
            cameraTransform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _captureCamera.orthographicSize = math.max(16f, captureWorldSize * 0.5f);
            _captureCamera.nearClipPlane = 0.1f;
            _captureCamera.farClipPlane = captureHeight + 2f;

            Shader.SetGlobalVector(
                _SedimentCaptureParamsId,
                new Vector4(
                    captureMinY,
                    1f / math.max(1f, captureHeight),
                    upFacingThreshold,
                    1f / math.max(0.001f, 1f - upFacingThreshold)));
        }

        private void RenderCapture()
        {
            if (_captureCamera == null || _captureTexture == null || sedimentCaptureShader == null)
                return;

            _captureCamera.targetTexture = _captureTexture;
            _captureCamera.RenderWithShader(sedimentCaptureShader, string.Empty);
        }

        private void DispatchAccumulation(float deltaTime)
        {
            if (!_computeReady)
                return;

            float stabilityScale = 1f / math.max(0.0001f, heightMatchTolerance);
            sedimentCompute.SetTexture(_kernelIndex, _SedimentCaptureTexId, _captureTexture);
            sedimentCompute.SetTexture(_kernelIndex, _SedimentMaskReadId, _sedimentRead);
            sedimentCompute.SetTexture(_kernelIndex, _SedimentMaskWriteId, _sedimentWrite);
            sedimentCompute.SetVector(
                _SedimentComputeParamsAId,
                new Vector4(
                    deltaTime,
                    depositionRate,
                    erosionRate,
                    stabilityScale));
            sedimentCompute.SetVector(
                _SedimentComputeParamsBId,
                new Vector4(
                    geometryShiftLossRate,
                    upFacingThreshold,
                    overlayIntensity,
                    0f));

            int groupsX = (_captureTexture.width + 7) >> 3;
            int groupsY = (_captureTexture.height + 7) >> 3;
            sedimentCompute.Dispatch(_kernelIndex, groupsX, groupsY, 1);

            RenderTexture temp = _sedimentRead;
            _sedimentRead = _sedimentWrite;
            _sedimentWrite = temp;
        }

        private void PublishGlobals()
        {
            if (_sedimentRead == null)
            {
                PublishFallbackGlobals();
                return;
            }

            Shader.SetGlobalTexture(_SedimentMaskTextureId, _sedimentRead);
            Shader.SetGlobalVector(_SedimentWorldRectId, _worldRect);
            Shader.SetGlobalVector(
                _SedimentOverlayParamsAId,
                new Vector4(
                    1f,
                    upFacingThreshold,
                    1f / math.max(0.001f, 1f - upFacingThreshold),
                    rippleScale));
            Shader.SetGlobalVector(
                _SedimentOverlayParamsBId,
                new Vector4(
                    rippleNormalStrength,
                    sedimentMetallic,
                    sedimentSmoothness,
                    overlayIntensity));
            Shader.SetGlobalColor(_SedimentTintAId, sedimentTintA.linear);
            Shader.SetGlobalColor(_SedimentTintBId, sedimentTintB.linear);
            _debugOverlayIntensity = overlayIntensity;
        }

        private void PublishFallbackGlobals()
        {
            Shader.SetGlobalTexture(_SedimentMaskTextureId, Texture2D.blackTexture);
            Shader.SetGlobalVector(_SedimentWorldRectId, Vector4.zero);
            Shader.SetGlobalVector(_SedimentOverlayParamsAId, Vector4.zero);
            Shader.SetGlobalVector(_SedimentOverlayParamsBId, Vector4.zero);
            Shader.SetGlobalColor(_SedimentTintAId, Color.black);
            Shader.SetGlobalColor(_SedimentTintBId, Color.black);
            _debugOverlayIntensity = 0f;
        }

        private void ReleaseResources()
        {
            ReleaseRenderTexture(ref _captureTexture);
            ReleaseRenderTexture(ref _sedimentRead);
            ReleaseRenderTexture(ref _sedimentWrite);
            _computeReady = false;
            _kernelIndex = -1;
        }

        private void TryRegisterTickHandlers()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = true;
            }

            if (!_registeredSlowTick)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = true;
            }
        }

        private void TryUnregisterTickHandlers()
        {
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            captureResolution = Mathf.Clamp(captureResolution, 128, 1024);
            captureWorldSize = Mathf.Max(32f, captureWorldSize);
            captureHeightAbovePlayer = Mathf.Max(16f, captureHeightAbovePlayer);
            captureDepthBelowPlayer = Mathf.Max(16f, captureDepthBelowPlayer);
            captureIntervalSeconds = Mathf.Max(0.05f, captureIntervalSeconds);
            depositionRate = Mathf.Max(0.01f, depositionRate);
            erosionRate = Mathf.Max(0.01f, erosionRate);
            geometryShiftLossRate = Mathf.Max(0.01f, geometryShiftLossRate);
            heightMatchTolerance = Mathf.Max(0.001f, heightMatchTolerance);
            rippleScale = Mathf.Clamp(rippleScale, 0.02f, 0.35f);
            rippleNormalStrength = Mathf.Clamp(rippleNormalStrength, 0.05f, 1f);
            if (sedimentCompute == null)
                sedimentCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(SedimentComputeAssetPath);
            if (sedimentCaptureShader == null)
                sedimentCaptureShader = AssetDatabase.LoadAssetAtPath<Shader>(CaptureShaderAssetPath);
        }
#endif
    }
}
