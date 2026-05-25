using Hecton8.Core;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.World
{
    /// <summary>
    /// Builds first-party damping facade textures for future ocean-donor inputs without touching ocean shaders or materials.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-102)]
    public sealed class SargassumCrestDampingController : MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private struct LegacyInputState
        {
            public Renderer Renderer;
            public Transform Transform;
            public Vector3 OriginalLocalScale;
            public bool OriginalRendererEnabled;
            public bool IsCaptured;
        }

        private const string WavesInputName = "SargassumWaveDampingInput";
        private const string FoamInputName = "SargassumFoamDampingInput";
        private const string OilFilmInputName = "SargassumOilFilmInput";
        private const int FacadeThreadGroupSize = 8;
#if UNITY_EDITOR
        private const string FacadeComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_SargassumDampingFacade.compute";
#endif

        private static readonly int _DensityTexId = Shader.PropertyToID("_DensityTex");
        private static readonly int _DensityWorldRectId = Shader.PropertyToID("_DensityWorldRect");
        private static readonly int _CutMaskTexId = Shader.PropertyToID("_CutMaskTex");
        private static readonly int _CutMaskWorldRectId = Shader.PropertyToID("_CutMaskWorldRect");
        private static readonly int _CutMaskActiveId = Shader.PropertyToID("_CutMaskActive");
        private static readonly int _GlobalDriftOffsetId = Shader.PropertyToID("_GlobalDriftOffset");
        private static readonly int _WaveFacadeParamsId = Shader.PropertyToID("_WaveFacadeParams");
        private static readonly int _OilFacadeParamsId = Shader.PropertyToID("_OilFacadeParams");
        private static readonly int _WaveDampingMaskResultId = Shader.PropertyToID("_WaveDampingMaskResult");
        private static readonly int _OilFilmMaskResultId = Shader.PropertyToID("_OilFilmMaskResult");
        private static readonly int _WaveDampingMaskTextureId = Shader.PropertyToID("_SargassumWaveDampingMaskRT");
        private static readonly int _WaveDampingMaskWorldRectId = Shader.PropertyToID("_SargassumWaveDampingMaskWorldRect");
        private static readonly int _WaveDampingMaskActiveId = Shader.PropertyToID("_SargassumWaveDampingMaskActive");
        private static readonly int _OilFilmMaskTextureId = Shader.PropertyToID("_SargassumOilFilmMaskRT");
        private static readonly int _OilFilmMaskWorldRectId = Shader.PropertyToID("_SargassumOilFilmMaskWorldRect");
        private static readonly int _OilFilmMaskActiveId = Shader.PropertyToID("_SargassumOilFilmMaskActive");

        [Header("── Runtime Wiring ──────────────────")]
        [SerializeField]
        [Tooltip("Primary density owner. If null the controller resolves the active runtime singleton.")]
        private SargassumGlobalDragManager dragManager;

        [SerializeField]
        [Tooltip("Primary cut-mask owner. If null the controller resolves the active runtime singleton.")]
        private SargassumCutManager cutManager;

        [SerializeField]
        [Tooltip("Optional explicit compute shader override used to bake both public damping facade textures in one dispatch.")]
        private ComputeShader facadeBakeComputeOverride;

        [Header("── Wave Damping Facade ─────────────")]
        [SerializeField, Range(0.5f, 4f)]
        [Tooltip("Power applied to canopy density before writing the public wave-damping facade texture.")]
        private float waveDampingDensityPower = 1.35f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How strongly the active cut mask punches holes through the public wave-damping facade texture.")]
        private float waveDampingCutRelief = 1f;

        [Header("── Oil Film Facade ─────────────────")]
        [SerializeField, Range(0.5f, 4f)]
        [Tooltip("Power applied to canopy density before writing the public oil-film facade texture.")]
        private float oilFilmDensityPower = 1.45f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How strongly the active cut mask punches holes through the public oil-film facade texture.")]
        private float oilFilmCutRelief = 1f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Alpha scale baked into the public oil-film facade texture.")]
        private float oilFilmAlphaScale = 0.92f;

        [Header("── Diagnostics ─────────────────────")]
        [SerializeField]
        [Tooltip("Current world rect encoded as minX, minZ, invSizeX, invSizeZ for both public facade textures.")]
        private Vector4 _debugFacadeWorldRect;

        [SerializeField]
        [Tooltip("Current drift offset consumed while baking the facade textures.")]
        private Vector3 _debugAppliedDriftOffset;

        [SerializeField]
        [Tooltip("Resolution of the live public wave-damping facade texture.")]
        private int _debugWaveFacadeResolution;

        [SerializeField]
        [Tooltip("Resolution of the live public oil-film facade texture.")]
        private int _debugOilFacadeResolution;

        private ComputeShader _facadeBakeCompute;
        private int _facadeBakeKernel = -1;
        private RenderTexture _waveDampingMask;
        private RenderTexture _oilFilmMask;
        private Renderer _wavesInputRenderer;
        private Renderer _foamInputRenderer;
        private Renderer _oilFilmInputRenderer;
        private Texture2D _activeDensityTexture;
        private Vector4 _activeDensityWorldRect;
        private Vector4 _activeCutMaskWorldRect;
        private Vector3 _activeDriftOffset;
        private int _activeFieldRevision = -1;
        private bool _legacyInputsResolved;
        private bool _usesCrest4LegacyInputs;
        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrameTick;
        private bool _registeredHotSwap;
        private bool _hasPublishedFacadeData;
        private bool _facadeGlobalsDirty;
        private bool _pendingFacadeGlobalsActive;
        private bool _pendingFacadeGlobalsForceClear;
        private bool _legacyInputDisableDirty;
        private bool _facadeRefreshRequested;
        private bool _facadeRefreshForce;
        private LegacyInputState _wavesInputState;
        private LegacyInputState _foamInputState;
        private LegacyInputState _oilFilmInputState;

        /// <summary>
        /// Public damping facade texture intended for future ocean wave or water-depth inputs.
        /// </summary>
        public RenderTexture WaveDampingMaskTexture => _waveDampingMask;

        /// <summary>
        /// Public oil-film facade texture intended for future ocean albedo inputs.
        /// </summary>
        public RenderTexture OilFilmMaskTexture => _oilFilmMask;

        /// <summary>
        /// Shared density-space world rect used by both public facade textures.
        /// </summary>
        public Vector4 FacadeWorldRect => _activeDensityWorldRect;

        private void Awake()
        {
            SanitizeSettings();
            CacheRegistryServicesCold();
            DisableLegacyInputs();
            EnsureFacadeResources();
            RefreshFacadeTextures(force: true);
        }

        private void OnEnable()
        {
            SanitizeSettings();
            TryRegisterHotSwapListener();
            CacheRegistryServicesCold();
            DisableLegacyInputs();
            EnsureFacadeResources();
            RefreshFacadeTextures(force: true);
            HectonFloatingOrigin.RegisterListener(this);
            TryRegister();
        }

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregister();
            TryUnregisterHotSwapListener();
            RestoreLegacyInputs();
            ReleaseFacadeResources();
            FlushFacadeGlobals(active: false, forceClear: true);
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregister();
            TryUnregisterHotSwapListener();
            RestoreLegacyInputs();
            ReleaseFacadeResources();
            FlushFacadeGlobals(active: false, forceClear: true);
        }

        /// <summary>
        /// Rebuilds public facade textures when drift, density, or cut-mask state changes.
        /// </summary>
        /// <param name="dt">Frame delta supplied by GameTickManager.</param>
        public void Tick(float dt)
        {
            ResolveDependencies();
            QueueLegacyInputDisable();

            if (dragManager == null)
            {
                QueueFacadeGlobals(active: false);
                return;
            }

            bool densityAvailable = dragManager.TryGetDensityFieldTexture(out Texture2D densityTexture, out Vector4 densityWorldRect);
            if (!densityAvailable || densityTexture == null)
            {
                _activeDensityTexture = null;
                _activeDensityWorldRect = Vector4.zero;
                _activeFieldRevision = dragManager.FieldRevision;
                QueueFacadeGlobals(active: false);
                return;
            }

            Vector3 driftOffset = dragManager.GlobalDriftOffset;
            Vector4 cutMaskWorldRect = Vector4.zero;
            bool cutMaskAvailable = cutManager != null && cutManager.TryGetCutMask(out _, out cutMaskWorldRect);
            bool fieldChanged =
                densityTexture != _activeDensityTexture ||
                densityWorldRect != _activeDensityWorldRect ||
                dragManager.FieldRevision != _activeFieldRevision;
            bool driftChanged = driftOffset != _activeDriftOffset;
            bool cutRectChanged = cutMaskAvailable != (_activeCutMaskWorldRect != Vector4.zero) || cutMaskWorldRect != _activeCutMaskWorldRect;

            if (!fieldChanged && !driftChanged && !cutRectChanged && cutManager == null)
                return;

            QueueFacadeRefresh(force: fieldChanged || driftChanged || cutRectChanged || cutManager != null);
        }

        /// <summary>
        /// Rebuilds the facade textures after slow sargassum field changes.
        /// </summary>
        public void SlowTick()
        {
            ResolveDependencies();
            QueueLegacyInputDisable();
            QueueFacadeRefresh(force: true);
        }

        /// <summary>
        /// Flushes facade shader globals after simulation and sargassum mask state are stable.
        /// </summary>
        public void LateFrameTick()
        {
            if (_legacyInputDisableDirty)
            {
                DisableLegacyInputs();
                _legacyInputDisableDirty = false;
            }

            if (_facadeRefreshRequested)
            {
                bool force = _facadeRefreshForce;
                _facadeRefreshRequested = false;
                _facadeRefreshForce = false;
                RefreshFacadeTextures(force);
            }

            if (!_facadeGlobalsDirty)
                return;

            FlushFacadeGlobals(_pendingFacadeGlobalsActive, _pendingFacadeGlobalsForceClear);
            _facadeGlobalsDirty = false;
            _pendingFacadeGlobalsForceClear = false;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled || shiftData.ShiftOffset.sqrMagnitude <= 0.0001f)
                return;

            ApplyRuntimeOffsetToCachedState(-shiftData.ShiftOffset);
        }

        private void ResolveDependencies()
        {
            ResolveLegacyInputs();
        }

        private void CacheRegistryServicesCold()
        {
            if (dragManager == null)
                dragManager = GlobalRegistry.SargassumDrag;

            if (cutManager == null)
                cutManager = GlobalRegistry.SargassumCut;

            ResolveLegacyInputs();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.SargassumDragRuntime)
            {
                if (dragManager == null || ReferenceEquals(dragManager, previousService))
                    dragManager = currentService as SargassumGlobalDragManager;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.SargassumCutRuntime &&
                (cutManager == null || ReferenceEquals(cutManager, previousService)))
            {
                cutManager = currentService as SargassumCutManager;
            }
        }

        private void RefreshFacadeTextures(bool force)
        {
            if (dragManager == null)
            {
                QueueFacadeGlobals(active: false);
                return;
            }

            if (!dragManager.TryGetDensityFieldTexture(out Texture2D densityTexture, out Vector4 densityWorldRect) || densityTexture == null)
            {
                _activeDensityTexture = null;
                _activeDensityWorldRect = Vector4.zero;
                _activeCutMaskWorldRect = Vector4.zero;
                _activeFieldRevision = dragManager.FieldRevision;
                QueueFacadeGlobals(active: false);
                return;
            }

            EnsureFacadeResources(densityTexture.width, densityTexture.height);

            RenderTexture cutMaskTexture = null;
            Vector4 cutMaskWorldRect = Vector4.zero;
            bool cutMaskAvailable = cutManager != null && cutManager.TryGetCutMask(out cutMaskTexture, out cutMaskWorldRect);
            _activeDensityTexture = densityTexture;
            _activeDensityWorldRect = densityWorldRect;
            _activeCutMaskWorldRect = cutMaskAvailable ? cutMaskWorldRect : Vector4.zero;
            _activeDriftOffset = dragManager.GlobalDriftOffset;
            _activeFieldRevision = dragManager.FieldRevision;

            DispatchFacadeBake(densityTexture, cutMaskTexture, densityWorldRect, cutMaskWorldRect, cutMaskAvailable);
            QueueFacadeGlobals(active: true);
        }

        private void DispatchFacadeBake(
            Texture densityTexture,
            Texture cutMaskTexture,
            Vector4 densityWorldRect,
            Vector4 cutMaskWorldRect,
            bool cutMaskActive)
        {
            if (_waveDampingMask == null || _oilFilmMask == null || densityTexture == null)
                return;

#if UNITY_EDITOR
            TryAutoAssignFacadeCompute();
#endif
            if (_facadeBakeCompute == null)
                _facadeBakeCompute = facadeBakeComputeOverride;

            if (_facadeBakeCompute == null)
                return;

            if (_facadeBakeKernel < 0)
                _facadeBakeKernel = _facadeBakeCompute.FindKernel("CSMain");

            _facadeBakeCompute.SetTexture(_facadeBakeKernel, _DensityTexId, densityTexture);
            _facadeBakeCompute.SetTexture(_facadeBakeKernel, _CutMaskTexId, cutMaskActive && cutMaskTexture != null ? cutMaskTexture : Texture2D.blackTexture);
            _facadeBakeCompute.SetTexture(_facadeBakeKernel, _WaveDampingMaskResultId, _waveDampingMask);
            _facadeBakeCompute.SetTexture(_facadeBakeKernel, _OilFilmMaskResultId, _oilFilmMask);
            _facadeBakeCompute.SetVector(_DensityWorldRectId, densityWorldRect);
            _facadeBakeCompute.SetVector(_CutMaskWorldRectId, cutMaskWorldRect);
            _facadeBakeCompute.SetInt(_CutMaskActiveId, cutMaskActive ? 1 : 0);
            _facadeBakeCompute.SetVector(_GlobalDriftOffsetId, _activeDriftOffset);
            _facadeBakeCompute.SetVector(_WaveFacadeParamsId, new Vector4(waveDampingDensityPower, waveDampingCutRelief, 1f, 0f));
            _facadeBakeCompute.SetVector(_OilFacadeParamsId, new Vector4(oilFilmDensityPower, oilFilmCutRelief, oilFilmAlphaScale, 0f));

            int groupCountX = Mathf.Max(1, Mathf.CeilToInt(_waveDampingMask.width / (float)FacadeThreadGroupSize));
            int groupCountY = Mathf.Max(1, Mathf.CeilToInt(_waveDampingMask.height / (float)FacadeThreadGroupSize));
            _facadeBakeCompute.Dispatch(_facadeBakeKernel, groupCountX, groupCountY, 1);
        }

        private void EnsureFacadeResources()
        {
            if (dragManager == null || !dragManager.TryGetDensityFieldTexture(out Texture2D densityTexture, out _ ) || densityTexture == null)
                return;

            EnsureFacadeResources(densityTexture.width, densityTexture.height);
        }

        private void EnsureFacadeResources(int width, int height)
        {
            _waveDampingMask = EnsureRenderTexture(ref _waveDampingMask, "__SargassumWaveDampingFacade", width, height);
            _oilFilmMask = EnsureRenderTexture(ref _oilFilmMask, "__SargassumOilFilmFacade", width, height);
            _debugWaveFacadeResolution = _waveDampingMask != null ? _waveDampingMask.width : 0;
            _debugOilFacadeResolution = _oilFilmMask != null ? _oilFilmMask.width : 0;
        }

        private static RenderTexture EnsureRenderTexture(ref RenderTexture texture, string name, int width, int height)
        {
            if (texture != null && texture.width == width && texture.height == height)
                return texture;

            if (texture != null)
            {
                texture.Release();
                Object.Destroy(texture);
                texture = null;
            }

            bool supportsR8RandomWrite = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8) &&
                                         SystemInfo.SupportsRandomWriteOnRenderTextureFormat(RenderTextureFormat.R8);
            RenderTextureFormat format = supportsR8RandomWrite
                ? RenderTextureFormat.R8
                : RenderTextureFormat.ARGB32;
            texture = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = true,
                hideFlags = HideFlags.HideAndDontSave
            }; // COLD ALLOC: RenderTexture[1] - public sargassum damping facade texture for ocean wave/albedo inputs - owner: SargassumCrestDampingController
            texture.Create();
            return texture;
        }

        private void ReleaseFacadeResources()
        {
            ReleaseRenderTexture(ref _waveDampingMask);
            ReleaseRenderTexture(ref _oilFilmMask);

            _debugWaveFacadeResolution = 0;
            _debugOilFacadeResolution = 0;
        }

        private static void ReleaseRenderTexture(ref RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();
            Object.Destroy(texture);
            texture = null;
        }

        private void QueueFacadeGlobals(bool active, bool forceClear = false)
        {
            _pendingFacadeGlobalsActive = active;
            _pendingFacadeGlobalsForceClear |= forceClear;
            _facadeGlobalsDirty = true;
        }

        private void FlushFacadeGlobals(bool active, bool forceClear = false)
        {
            if (!active || _waveDampingMask == null || _oilFilmMask == null)
            {
                if (!forceClear && _hasPublishedFacadeData && _waveDampingMask != null && _oilFilmMask != null)
                    return;

                Shader.SetGlobalTexture(_WaveDampingMaskTextureId, Texture2D.blackTexture);
                Shader.SetGlobalVector(_WaveDampingMaskWorldRectId, Vector4.zero);
                Shader.SetGlobalFloat(_WaveDampingMaskActiveId, 0f);
                Shader.SetGlobalTexture(_OilFilmMaskTextureId, Texture2D.blackTexture);
                Shader.SetGlobalVector(_OilFilmMaskWorldRectId, Vector4.zero);
                Shader.SetGlobalFloat(_OilFilmMaskActiveId, 0f);
                _debugFacadeWorldRect = Vector4.zero;
                _debugAppliedDriftOffset = Vector3.zero;
                _hasPublishedFacadeData = false;
                return;
            }

            Shader.SetGlobalTexture(_WaveDampingMaskTextureId, _waveDampingMask);
            Shader.SetGlobalVector(_WaveDampingMaskWorldRectId, _activeDensityWorldRect);
            Shader.SetGlobalFloat(_WaveDampingMaskActiveId, 1f);
            Shader.SetGlobalTexture(_OilFilmMaskTextureId, _oilFilmMask);
            Shader.SetGlobalVector(_OilFilmMaskWorldRectId, _activeDensityWorldRect);
            Shader.SetGlobalFloat(_OilFilmMaskActiveId, 1f);
            _debugFacadeWorldRect = _activeDensityWorldRect;
            _debugAppliedDriftOffset = _activeDriftOffset;
            _hasPublishedFacadeData = true;
        }

        private void ApplyRuntimeOffsetToCachedState(Vector3 runtimeOffset)
        {
            _activeDensityWorldRect = TranslateWorldRectXZ(_activeDensityWorldRect, runtimeOffset);
            _activeCutMaskWorldRect = TranslateWorldRectXZ(_activeCutMaskWorldRect, runtimeOffset);
            _activeDriftOffset += runtimeOffset;
            _debugFacadeWorldRect = TranslateWorldRectXZ(_debugFacadeWorldRect, runtimeOffset);
            _debugAppliedDriftOffset += runtimeOffset;

            if (_hasPublishedFacadeData)
                QueueFacadeGlobals(active: true);
        }

        private void QueueLegacyInputDisable()
        {
            _legacyInputDisableDirty = true;
        }

        private void QueueFacadeRefresh(bool force)
        {
            _facadeRefreshRequested = true;
            _facadeRefreshForce |= force;
        }

        private static Vector4 TranslateWorldRectXZ(Vector4 worldRect, Vector3 runtimeOffset)
        {
            if (worldRect == Vector4.zero)
                return worldRect;

            worldRect.x += runtimeOffset.x;
            worldRect.y += runtimeOffset.z;
            return worldRect;
        }

#if UNITY_EDITOR
        private void TryAutoAssignFacadeCompute()
        {
            if (facadeBakeComputeOverride == null)
                facadeBakeComputeOverride = AssetDatabase.LoadAssetAtPath<ComputeShader>(FacadeComputeAssetPath);
        }
#endif

        private void DisableLegacyInputs()
        {
            ResolveLegacyInputs();
            bool suppressLegacyInputs = !_usesCrest4LegacyInputs;
            ApplyLegacyInputState(ref _wavesInputState, suppressLegacyInputs);
            ApplyLegacyInputState(ref _foamInputState, suppressLegacyInputs);
            ApplyLegacyInputState(ref _oilFilmInputState, suppressLegacyInputs);
        }

        private void ResolveLegacyInputs()
        {
            if (_legacyInputsResolved)
                return;

            _legacyInputsResolved = true;
            _usesCrest4LegacyInputs = false;
            _wavesInputRenderer = ResolveLegacyInputRenderer(WavesInputName, ref _wavesInputState);
            _foamInputRenderer = ResolveLegacyInputRenderer(FoamInputName, ref _foamInputState);
            _oilFilmInputRenderer = ResolveLegacyInputRenderer(OilFilmInputName, ref _oilFilmInputState);
        }

        private Renderer ResolveLegacyInputRenderer(string childName, ref LegacyInputState state)
        {
            Transform child = transform.Find(childName);
            if (child == null || !child.TryGetComponent(out Renderer renderer))
                return null;

            state.Renderer = renderer;
            state.Transform = child;
            state.OriginalLocalScale = child.localScale;
            state.OriginalRendererEnabled = renderer.enabled;
            state.IsCaptured = true;
            return renderer;
        }

        private static void ApplyLegacyInputState(ref LegacyInputState state, bool suppress)
        {
            if (!state.IsCaptured || state.Renderer == null || state.Transform == null)
                return;

            if (suppress)
            {
                state.Transform.localScale = Vector3.zero;
                state.Renderer.enabled = false;
                return;
            }

            state.Transform.localScale = state.OriginalLocalScale;
            state.Renderer.enabled = state.OriginalRendererEnabled;
        }

        private void RestoreLegacyInputs()
        {
            ApplyLegacyInputState(ref _wavesInputState, suppress: false);
            ApplyLegacyInputState(ref _foamInputState, suppress: false);
            ApplyLegacyInputState(ref _oilFilmInputState, suppress: false);
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            }

            if (!_registeredSlowTick)
            {
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            }

            if (!_registeredLateFrameTick)
            {
                _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            }
        }

        private void TryUnregister()
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

            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTick = false;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (!Application.isPlaying || _registeredHotSwap)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void SanitizeSettings()
        {
            waveDampingDensityPower = Mathf.Clamp(waveDampingDensityPower, 0.5f, 4f);
            waveDampingCutRelief = Mathf.Clamp01(waveDampingCutRelief);
            oilFilmDensityPower = Mathf.Clamp(oilFilmDensityPower, 0.5f, 4f);
            oilFilmCutRelief = Mathf.Clamp01(oilFilmCutRelief);
            oilFilmAlphaScale = Mathf.Clamp01(oilFilmAlphaScale);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            SanitizeSettings();
            TryAutoAssignFacadeCompute();
        }
#endif
    }
}
