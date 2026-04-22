using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Builds first-party damping facade textures for future Crest 5 inputs without touching Crest ocean shaders or materials.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-102)]
    public sealed class SargassumCrestDampingController : MonoBehaviour, ITickable, ISlowTickable
    {
        private const string FacadeCopyShaderName = "Hidden/Hecton8/SargassumDampingFacadeCopy";
        private const string WavesInputName = "SargassumWaveDampingInput";
        private const string FoamInputName = "SargassumFoamDampingInput";
        private const string OilFilmInputName = "SargassumOilFilmInput";

        private static readonly int _DensityTexId = Shader.PropertyToID("_DensityTex");
        private static readonly int _DensityWorldRectId = Shader.PropertyToID("_DensityWorldRect");
        private static readonly int _CutMaskTexId = Shader.PropertyToID("_CutMaskTex");
        private static readonly int _CutMaskWorldRectId = Shader.PropertyToID("_CutMaskWorldRect");
        private static readonly int _CutMaskActiveId = Shader.PropertyToID("_CutMaskActive");
        private static readonly int _GlobalDriftOffsetId = Shader.PropertyToID("_GlobalDriftOffset");
        private static readonly int _DensityPowerId = Shader.PropertyToID("_DensityPower");
        private static readonly int _CutReliefId = Shader.PropertyToID("_CutRelief");
        private static readonly int _AlphaScaleId = Shader.PropertyToID("_AlphaScale");
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
        [Tooltip("Optional explicit facade copy shader. Falls back to Hidden/Hecton8/SargassumDampingFacadeCopy when left empty.")]
        private Shader facadeCopyShader;

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

        private Material _facadeCopyMaterial;
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
        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _hasPublishedFacadeData;

        /// <summary>
        /// Public damping facade texture intended for future Crest 5 wave or water-depth inputs.
        /// </summary>
        public RenderTexture WaveDampingMaskTexture => _waveDampingMask;

        /// <summary>
        /// Public oil-film facade texture intended for future Crest 5 albedo inputs.
        /// </summary>
        public RenderTexture OilFilmMaskTexture => _oilFilmMask;

        /// <summary>
        /// Shared density-space world rect used by both public facade textures.
        /// </summary>
        public Vector4 FacadeWorldRect => _activeDensityWorldRect;

        private void Awake()
        {
            SanitizeSettings();
            ResolveDependencies();
            DisableLegacyInputs();
            EnsureFacadeResources();
            RefreshFacadeTextures(force: true);
        }

        private void OnEnable()
        {
            SanitizeSettings();
            ResolveDependencies();
            DisableLegacyInputs();
            EnsureFacadeResources();
            RefreshFacadeTextures(force: true);
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            ReleaseFacadeResources();
            PublishGlobals(active: false, forceClear: true);
        }

        private void OnDestroy()
        {
            TryUnregister();
            ReleaseFacadeResources();
            PublishGlobals(active: false, forceClear: true);
        }

        /// <summary>
        /// Rebuilds public facade textures when drift, density, or cut-mask state changes.
        /// </summary>
        /// <param name="dt">Frame delta supplied by GameTickManager.</param>
        public void Tick(float dt)
        {
            ResolveDependencies();
            DisableLegacyInputs();

            if (dragManager == null)
            {
                PublishGlobals(active: false);
                return;
            }

            bool densityAvailable = dragManager.TryGetDensityFieldTexture(out Texture2D densityTexture, out Vector4 densityWorldRect);
            if (!densityAvailable || densityTexture == null)
            {
                _activeDensityTexture = null;
                _activeDensityWorldRect = Vector4.zero;
                _activeFieldRevision = dragManager.FieldRevision;
                PublishGlobals(active: false);
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

            RefreshFacadeTextures(force: fieldChanged || driftChanged || cutRectChanged || cutManager != null);
        }

        /// <summary>
        /// Rebuilds the facade textures after slow sargassum field changes.
        /// </summary>
        public void SlowTick()
        {
            ResolveDependencies();
            DisableLegacyInputs();
            RefreshFacadeTextures(force: true);
        }

        private void ResolveDependencies()
        {
            if (dragManager == null)
                dragManager = SargassumGlobalDragManager.Instance;

            if (cutManager == null)
                cutManager = SargassumCutManager.Instance;

            ResolveLegacyInputs();

            if (_facadeCopyMaterial == null)
            {
                Shader shader = facadeCopyShader != null ? facadeCopyShader : Shader.Find(FacadeCopyShaderName);
                if (shader != null)
                {
                    // COLD ALLOC: Material[1] - first-party damping facade blit material, independent from Crest runtime assets - owner: SargassumCrestDampingController
                    _facadeCopyMaterial = new Material(shader)
                    {
                        name = "MAT_Runtime_SargassumDampingFacadeCopy"
                    };
                    _facadeCopyMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
            }
        }

        private void RefreshFacadeTextures(bool force)
        {
            if (dragManager == null || _facadeCopyMaterial == null)
            {
                PublishGlobals(active: false);
                return;
            }

            if (!dragManager.TryGetDensityFieldTexture(out Texture2D densityTexture, out Vector4 densityWorldRect) || densityTexture == null)
            {
                _activeDensityTexture = null;
                _activeDensityWorldRect = Vector4.zero;
                _activeCutMaskWorldRect = Vector4.zero;
                _activeFieldRevision = dragManager.FieldRevision;
                PublishGlobals(active: false);
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

            BakeFacadeTexture(_waveDampingMask, densityTexture, cutMaskTexture, densityWorldRect, cutMaskWorldRect, cutMaskAvailable, waveDampingDensityPower, waveDampingCutRelief, 1f, 0);
            BakeFacadeTexture(_oilFilmMask, densityTexture, cutMaskTexture, densityWorldRect, cutMaskWorldRect, cutMaskAvailable, oilFilmDensityPower, oilFilmCutRelief, oilFilmAlphaScale, 1);
            PublishGlobals(active: true);
        }

        private void BakeFacadeTexture(
            RenderTexture target,
            Texture densityTexture,
            Texture cutMaskTexture,
            Vector4 densityWorldRect,
            Vector4 cutMaskWorldRect,
            bool cutMaskActive,
            float densityPower,
            float cutRelief,
            float alphaScale,
            int passIndex)
        {
            if (target == null || _facadeCopyMaterial == null)
                return;

            _facadeCopyMaterial.SetTexture(_DensityTexId, densityTexture);
            _facadeCopyMaterial.SetVector(_DensityWorldRectId, densityWorldRect);
            _facadeCopyMaterial.SetTexture(_CutMaskTexId, cutMaskActive && cutMaskTexture != null ? cutMaskTexture : Texture2D.blackTexture);
            _facadeCopyMaterial.SetVector(_CutMaskWorldRectId, cutMaskWorldRect);
            _facadeCopyMaterial.SetFloat(_CutMaskActiveId, cutMaskActive ? 1f : 0f);
            _facadeCopyMaterial.SetVector(_GlobalDriftOffsetId, _activeDriftOffset);
            _facadeCopyMaterial.SetFloat(_DensityPowerId, densityPower);
            _facadeCopyMaterial.SetFloat(_CutReliefId, cutRelief);
            _facadeCopyMaterial.SetFloat(_AlphaScaleId, alphaScale);
            Graphics.Blit(null, target, _facadeCopyMaterial, passIndex);
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

            texture = new RenderTexture(width, height, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = false,
                hideFlags = HideFlags.HideAndDontSave
            }; // COLD ALLOC: RenderTexture[1] - public sargassum damping facade texture for Crest 5 wave/albedo inputs - owner: SargassumCrestDampingController
            texture.Create();
            return texture;
        }

        private void ReleaseFacadeResources()
        {
            ReleaseRenderTexture(ref _waveDampingMask);
            ReleaseRenderTexture(ref _oilFilmMask);
            if (_facadeCopyMaterial != null)
            {
                Destroy(_facadeCopyMaterial);
                _facadeCopyMaterial = null;
            }

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

        private void PublishGlobals(bool active, bool forceClear = false)
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

        private void DisableLegacyInputs()
        {
            ResolveLegacyInputs();
            DisableLegacyInputRenderer(_wavesInputRenderer);
            DisableLegacyInputRenderer(_foamInputRenderer);
            DisableLegacyInputRenderer(_oilFilmInputRenderer);
        }

        private void ResolveLegacyInputs()
        {
            if (_legacyInputsResolved)
                return;

            _legacyInputsResolved = true;
            _wavesInputRenderer = ResolveLegacyInputRenderer(WavesInputName);
            _foamInputRenderer = ResolveLegacyInputRenderer(FoamInputName);
            _oilFilmInputRenderer = ResolveLegacyInputRenderer(OilFilmInputName);
        }

        private Renderer ResolveLegacyInputRenderer(string childName)
        {
            Transform child = transform.Find(childName);
            if (child == null || !child.TryGetComponent(out Renderer renderer))
                return null;

            child.localScale = Vector3.zero;
            return renderer;
        }

        private static void DisableLegacyInputRenderer(Renderer renderer)
        {
            if (renderer == null)
                return;

            renderer.transform.localScale = Vector3.zero;
            renderer.enabled = false;
        }

        private void TryRegister()
        {
            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            if (!_registeredTick)
            {
                tickManager.Register((ITickable)this);
                _registeredTick = true;
            }

            if (!_registeredSlowTick)
            {
                tickManager.Register((ISlowTickable)this);
                _registeredSlowTick = true;
            }
        }

        private void TryUnregister()
        {
            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            if (_registeredTick)
            {
                tickManager.Unregister((ITickable)this);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                tickManager.Unregister((ISlowTickable)this);
                _registeredSlowTick = false;
            }
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
        }
#endif
    }
}
