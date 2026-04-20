using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Projects the baked sargassum density field into Crest animated waves and foam inputs.
    /// Uses asset materials only and drives them through MaterialPropertyBlock.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-102)]
    public sealed class SargassumCrestDampingController : MonoBehaviour, ITickable, ISlowTickable
    {
        private static readonly int _MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int _DensityWorldRectId = Shader.PropertyToID("_DensityWorldRect");

        private const string WavesInputName = "SargassumWaveDampingInput";
        private const string FoamInputName = "SargassumFoamDampingInput";
        private const string OilFilmInputName = "SargassumOilFilmInput";

        [Header("── Runtime Wiring ──────────────────")]
        [SerializeField]
        [Tooltip("Primary density owner. If null the controller resolves the active runtime singleton.")]
        private SargassumGlobalDragManager dragManager;

        [SerializeField]
        [Tooltip("Optional direct transform for the Crest animated-waves input quad.")]
        private Transform wavesInputTransform;

        [SerializeField]
        [Tooltip("Optional direct renderer for the Crest animated-waves input quad.")]
        private Renderer wavesInputRenderer;

        [SerializeField]
        [Tooltip("Optional direct transform for the Crest foam input quad.")]
        private Transform foamInputTransform;

        [SerializeField]
        [Tooltip("Optional direct renderer for the Crest foam input quad.")]
        private Renderer foamInputRenderer;

        [SerializeField]
        [Tooltip("Optional direct transform for the Crest albedo input quad used to tint and smooth oily sargassum slicks.")]
        private Transform oilFilmInputTransform;

        [SerializeField]
        [Tooltip("Optional direct renderer for the Crest albedo input quad used to tint and smooth oily sargassum slicks.")]
        private Renderer oilFilmInputRenderer;

        [Header("── Ocean Placement ──────────────────")]
        [SerializeField, Min(0f)]
        [Tooltip("World-space water level used when positioning the Crest damping quads.")]
        private float waterLevel = 4900f;

        [SerializeField]
        [Tooltip("Small offset applied above the ocean surface to avoid coplanar precision issues.")]
        private float surfaceOffset = 0.05f;

        [Header("── Diagnostics ──────────────────")]
        [SerializeField]
        [Tooltip("Current density rect encoded as minX, minZ, invSizeX, invSizeZ.")]
        private Vector4 _debugDensityWorldRect;

        [SerializeField]
        [Tooltip("Current drift offset used to place the Crest damping quads in visual space.")]
        private Vector3 _debugAppliedDriftOffset;

        private MaterialPropertyBlock _wavesPropertyBlock;
        private MaterialPropertyBlock _foamPropertyBlock;
        private MaterialPropertyBlock _oilFilmPropertyBlock;

        private bool _registeredTick;
        private bool _registeredSlowTick;
        private Texture2D _activeDensityTexture;
        private Vector4 _activeDensityWorldRect;
        private Vector3 _activeDriftOffset;
        private int _activeFieldRevision = -1;

        private void Awake()
        {
            _wavesPropertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - Crest animated-waves damping properties - owner: SargassumCrestDampingController
            _foamPropertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - Crest foam damping properties - owner: SargassumCrestDampingController
            _oilFilmPropertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - Crest albedo slick properties - owner: SargassumCrestDampingController
            ResolveDependencies();
            RefreshInputs(force: true);
        }

        private void OnEnable()
        {
            _wavesPropertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - Crest animated-waves damping properties - owner: SargassumCrestDampingController
            _foamPropertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - Crest foam damping properties - owner: SargassumCrestDampingController
            _oilFilmPropertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - Crest albedo slick properties - owner: SargassumCrestDampingController
            ResolveDependencies();
            RefreshInputs(force: true);
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        /// <summary>
        /// Keeps the Crest damping quads aligned with the latest visual drift offset.
        /// </summary>
        /// <param name="dt">Frame delta supplied by GameTickManager.</param>
        public void Tick(float dt)
        {
            if (dragManager == null)
            {
                ResolveDependencies();
                return;
            }

            if (dragManager.FieldRevision != _activeFieldRevision)
            {
                RefreshInputs(force: false);
                if (_activeDensityTexture == null)
                    return;
            }

            Vector3 currentDrift = dragManager.GlobalDriftOffset;
            if (currentDrift == _activeDriftOffset && _activeDensityTexture != null)
                return;

            ApplyInputPlacement(_activeDensityWorldRect, currentDrift);
        }

        /// <summary>
        /// Refreshes the baked density texture and Crest material bindings after the sargassum field rebuilds.
        /// </summary>
        public void SlowTick()
        {
            ResolveDependencies();
            RefreshInputs(force: false);
        }

        private void ResolveDependencies()
        {
            if (dragManager == null)
                dragManager = SargassumGlobalDragManager.Instance;

            if (wavesInputTransform == null)
            {
                Transform child = transform.Find(WavesInputName);
                if (child != null)
                    wavesInputTransform = child;
            }

            if (wavesInputRenderer == null && wavesInputTransform != null)
                wavesInputRenderer = wavesInputTransform.GetComponent<Renderer>();

            if (foamInputTransform == null)
            {
                Transform child = transform.Find(FoamInputName);
                if (child != null)
                    foamInputTransform = child;
            }

            if (foamInputRenderer == null && foamInputTransform != null)
                foamInputRenderer = foamInputTransform.GetComponent<Renderer>();

            if (oilFilmInputTransform == null)
            {
                Transform child = transform.Find(OilFilmInputName);
                if (child != null)
                    oilFilmInputTransform = child;
            }

            if (oilFilmInputRenderer == null && oilFilmInputTransform != null)
                oilFilmInputRenderer = oilFilmInputTransform.GetComponent<Renderer>();
        }

        private void RefreshInputs(bool force)
        {
            if (dragManager == null ||
                wavesInputTransform == null ||
                wavesInputRenderer == null ||
                foamInputTransform == null ||
                foamInputRenderer == null ||
                oilFilmInputTransform == null ||
                oilFilmInputRenderer == null)
            {
                return;
            }

            if (!dragManager.TryGetDensityFieldTexture(out Texture2D densityTexture, out Vector4 densityWorldRect))
            {
                _activeDensityTexture = null;
                _activeDensityWorldRect = Vector4.zero;
                _activeDriftOffset = dragManager.GlobalDriftOffset;
                _activeFieldRevision = dragManager.FieldRevision;
                SetInputScaleZero();
                return;
            }

            if (!force &&
                densityTexture == _activeDensityTexture &&
                densityWorldRect == _activeDensityWorldRect &&
                dragManager.GlobalDriftOffset == _activeDriftOffset)
            {
                return;
            }

            _activeDensityTexture = densityTexture;
            _activeDensityWorldRect = densityWorldRect;
            _activeFieldRevision = dragManager.FieldRevision;
            ApplyMaterialProperties(wavesInputRenderer, _wavesPropertyBlock, densityTexture, densityWorldRect);
            ApplyMaterialProperties(foamInputRenderer, _foamPropertyBlock, densityTexture, densityWorldRect);
            ApplyMaterialProperties(oilFilmInputRenderer, _oilFilmPropertyBlock, densityTexture, densityWorldRect);
            ApplyInputPlacement(densityWorldRect, dragManager.GlobalDriftOffset);
        }

        private void ApplyMaterialProperties(Renderer renderer, MaterialPropertyBlock block, Texture densityTexture, Vector4 densityWorldRect)
        {
            renderer.GetPropertyBlock(block);
            block.SetTexture(_MainTexId, densityTexture);
            block.SetVector(_DensityWorldRectId, densityWorldRect);
            renderer.SetPropertyBlock(block);
        }

        private void ApplyInputPlacement(Vector4 densityWorldRect, Vector3 driftOffset)
        {
            float worldSizeX = densityWorldRect.z > 0f ? 1f / densityWorldRect.z : 0f;
            float worldSizeZ = densityWorldRect.w > 0f ? 1f / densityWorldRect.w : 0f;
            float centerX = densityWorldRect.x + worldSizeX * 0.5f + driftOffset.x;
            float centerZ = densityWorldRect.y + worldSizeZ * 0.5f + driftOffset.z;
            Vector3 position = new Vector3(centerX, waterLevel + surfaceOffset, centerZ);
            Vector3 scale = new Vector3(worldSizeX, worldSizeZ, 1f);
            Quaternion rotation = Quaternion.Euler(90f, 0f, 0f);

            wavesInputTransform.SetPositionAndRotation(position, rotation);
            wavesInputTransform.localScale = scale;
            foamInputTransform.SetPositionAndRotation(position, rotation);
            foamInputTransform.localScale = scale;
            oilFilmInputTransform.SetPositionAndRotation(position, rotation);
            oilFilmInputTransform.localScale = scale;

            _activeDriftOffset = driftOffset;
            _debugDensityWorldRect = densityWorldRect;
            _debugAppliedDriftOffset = driftOffset;
        }

        private void SetInputScaleZero()
        {
            if (wavesInputTransform != null)
                wavesInputTransform.localScale = Vector3.zero;

            if (foamInputTransform != null)
                foamInputTransform.localScale = Vector3.zero;

            if (oilFilmInputTransform != null)
                oilFilmInputTransform.localScale = Vector3.zero;
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
    }
}
