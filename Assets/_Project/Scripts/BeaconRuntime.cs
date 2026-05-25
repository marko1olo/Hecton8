using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Gameplay
{
    public sealed class BeaconRuntime : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const float FlickerBase = 0.8f;
        private const float FlickerAmplitude = 0.15f;
        private const float FlickerCyclesPerSecond = 0.5570423f;
        private const ulong BeaconIdFnvOffset = 1469598103934665603UL;
        private const ulong BeaconIdFnvPrime = 1099511628211UL;
        private const int BeaconIdPrefixLength = 7;
        private const int BeaconIdHexLength = 16;
        private const int BeaconIdLength = BeaconIdPrefixLength + BeaconIdHexLength;

        private static Shader s_fallbackBeaconShader;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_fallbackBeaconShader = null;
        }

        private GameObject _sourcePrefab;
        private Transform _cachedTransform;
        private Light _light;
        private Material _ownedFallbackMaterial;
        private AbsoluteUniversePosition _cachedAup;
        private float _baseIntensity;
        private float _flickerTime;
        private bool _registeredLateFrame;
        private bool _isFallbackRuntime;
        private bool _hotSwapListenerRegistered;
        private IObjectPoolService _cachedObjectPool;
        private IObjectPoolService _pooledOwner;
        private ILocalizationTextReadModel _cachedLocalization;

        public string BeaconId { get; private set; }
        public string Label { get; private set; }
        public Color BeaconColor { get; private set; }
        public float LightRange { get; private set; }
        public AbsoluteUniversePosition PositionAup => _cachedAup;
        public Vector3 RuntimePosition => ResolveRuntimePosition();

        private void Awake()
        {
            CacheTransform();
            TryGetComponent(out _light);
            if (_light != null)
                _baseIntensity = _light.intensity <= 0f ? 1.6f : _light.intensity;
            RefreshCachedAup();
        }

        private void OnEnable()
        {
            CacheTransform();
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            RegisterToLateFrame();
        }

        private void OnDisable()
        {
            if (_light != null)
                _light.intensity = _baseIntensity;

            UnregisterFromLateFrame();
            TryUnregisterHotSwapListener();
        }

        public void Configure(string beaconId, string label, GameObject sourcePrefab, Color color, float range)
        {
            RefreshCachedAup();
            BeaconId = string.IsNullOrWhiteSpace(beaconId)
                ? CreateDeterministicBeaconId(in _cachedAup, unchecked((int)EntityId.ToULong(GetEntityId())))
                : beaconId;
            Label = string.IsNullOrWhiteSpace(label)
                ? ResolveLocalized(LocalizationKeys.BEACON_PREFIX, "BEACON")
                : CachedToUpperInvariant(label.Trim());
            BeaconColor = color;
            LightRange = Mathf.Max(0.5f, range);
            _sourcePrefab = _isFallbackRuntime ? null : sourcePrefab;
            _flickerTime = 0f;
            if (_light == null)
                TryGetComponent(out _light);
            if (_light != null)
            {
                _light.color = color;
                _light.range = LightRange;
                _baseIntensity = _light.intensity <= 0f ? 1.6f : _light.intensity;
            }
        }

        private void CacheTransform()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;
        }

        private Vector3 ResolveRuntimePosition()
        {
            CacheTransform();
            return _cachedTransform.position;
        }

        private void RefreshCachedAup()
        {
            _cachedAup = ResolveAupFromRuntimeOrigin(ResolveRuntimePosition());
        }

        private static AbsoluteUniversePosition ResolveAupFromRuntimeOrigin(Vector3 runtimePosition)
        {
            if (!float.IsFinite(runtimePosition.x) ||
                !float.IsFinite(runtimePosition.y) ||
                !float.IsFinite(runtimePosition.z))
            {
                return RuntimeOriginRoute.CurrentRuntimeOriginAup();
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            return AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
        }

        public void LateFrameTick()
        {
            if (_light == null)
                return;

            float deltaTime = math.max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            _flickerTime = math.frac(_flickerTime + (math.max(0f, deltaTime) * FlickerCyclesPerSecond));
            float triangle = 1f - math.abs((_flickerTime * 2f) - 1f);
            float signedTriangle = (triangle * 2f) - 1f;
            _light.intensity = _baseIntensity * (FlickerBase + (signedTriangle * FlickerAmplitude));
        }

        private void OnDestroy()
        {
            UnregisterFromLateFrame();
            TryUnregisterHotSwapListener();
            ReleaseOwnedFallbackMaterial();
            BeaconNetworkSystem.NotifyRuntimeDestroyed(this);
        }

        public void DespawnSelf()
        {
            IObjectPoolService pool = _pooledOwner != null ? _pooledOwner : _cachedObjectPool;
            if (_sourcePrefab != null && pool != null)
            {
                pool.Despawn(gameObject);
                return;
            }

            Destroy(gameObject);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.ObjectPool:
                    _cachedObjectPool = currentService as IObjectPoolService;
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _cachedLocalization = currentService as ILocalizationTextReadModel;
                    break;
            }
        }

        private void RegisterToLateFrame()
        {
            if (_registeredLateFrame || _light == null || !Application.isPlaying)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void UnregisterFromLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
        }

        /// <summary>
        /// Creates an owned fallback beacon material for one spawned fallback beacon.
        /// </summary>
        /// <param name="color">Beacon color baked into the material instance.</param>
        /// <returns>New material instance, or null when no compatible fallback shader is available.</returns>
        /// <remarks>The returned material must be passed to <see cref="SetOwnedFallbackMaterial"/> so it is destroyed with the beacon runtime.</remarks>
        public static Material GetFallbackBeaconMaterial(Color color)
        {
            Shader shader = ResolveFallbackBeaconShader();
            if (shader == null)
                return null;

            // COLD ALLOC: Material[1] - per fallback beacon color instance with BeaconRuntime ownership - owner: BeaconRuntime
            Material material = new Material(shader)
            {
                name = "MAT_Runtime_BeaconFallback",
                hideFlags = HideFlags.DontSave
            };
            ApplyFallbackBeaconColor(material, color);
            return material;
        }

        internal void SetOwnedFallbackMaterial(Material material)
        {
            _ownedFallbackMaterial = material;
            _isFallbackRuntime = true;
        }

        internal void SetPooledOwner(IObjectPoolService pool)
        {
            _pooledOwner = pool;
        }

        private static void ApplyFallbackBeaconColor(Material material, Color color)
        {
            if (material == null)
                return;

            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }

        private void ReleaseOwnedFallbackMaterial()
        {
            if (_ownedFallbackMaterial == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(_ownedFallbackMaterial);
            else
#endif
                Destroy(_ownedFallbackMaterial);

            _ownedFallbackMaterial = null;
        }

        private static Shader ResolveFallbackBeaconShader()
        {
            if (s_fallbackBeaconShader != null)
                return s_fallbackBeaconShader;

            RenderPipelineAsset renderPipeline = GraphicsSettings.currentRenderPipeline ?? GraphicsSettings.defaultRenderPipeline;
            Material defaultMaterial = renderPipeline != null ? renderPipeline.defaultMaterial : null;
            s_fallbackBeaconShader = defaultMaterial != null ? defaultMaterial.shader : null;
            return s_fallbackBeaconShader;
        }

        private static string CreateDeterministicBeaconId(in AbsoluteUniversePosition aup, int instanceId)
        {
            ulong hash = HashBeaconIdentity(in aup, instanceId);
            return string.Create(BeaconIdLength, hash, (buffer, value) =>
            {
                buffer[0] = 'B';
                buffer[1] = 'E';
                buffer[2] = 'A';
                buffer[3] = 'C';
                buffer[4] = 'O';
                buffer[5] = 'N';
                buffer[6] = '-';
                for (int i = 0; i < BeaconIdHexLength; i++)
                {
                    int nibble = (int)((value >> ((BeaconIdHexLength - 1 - i) * 4)) & 0xFUL);
                    buffer[BeaconIdPrefixLength + i] = (char)(nibble < 10 ? '0' + nibble : 'A' + (nibble - 10));
                }
            });
        }

        private static ulong HashBeaconIdentity(in AbsoluteUniversePosition aup, int instanceId)
        {
            ulong hash = BeaconIdFnvOffset;
            hash = MixBeaconIdHash(hash, (ulong)aup.GridX);
            hash = MixBeaconIdHash(hash, (ulong)aup.GridY);
            hash = MixBeaconIdHash(hash, (ulong)aup.GridZ);
            hash = MixBeaconIdHash(hash, (uint)(int)((aup.LocalX * 10f) + 0.5f));
            hash = MixBeaconIdHash(hash, (uint)(int)((aup.LocalY * 10f) + 0.5f));
            hash = MixBeaconIdHash(hash, (uint)(int)((aup.LocalZ * 10f) + 0.5f));
            hash = MixBeaconIdHash(hash, (uint)instanceId);
            return hash;
        }

        private static ulong MixBeaconIdHash(ulong hash, ulong value)
        {
            hash ^= value;
            return hash * BeaconIdFnvPrime;
        }



        // ----------------------------------------------------------
        //  ZERO-GC STRING CACHING
        // ----------------------------------------------------------

        private string ResolveLocalized(string key, string fallback)
        {
            ILocalizationTextReadModel manager = _cachedLocalization;
            return manager != null
                ? manager.GetOrFallback(key, fallback)
                : fallback;
        }

        private void CacheRegistryServicesCold()
        {
            _cachedObjectPool = GlobalRegistry.ObjectPoolService;
            _cachedLocalization = Hecton8.Core.GlobalRegistry.LocalizationText;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private static readonly string[] _cachedUpperStrings = new string[16]; // COLD ALLOC: string[16] - upper-case label cache slots - owner: BeaconRuntime

        /// <summary>
        /// Cached ToUpperInvariant path to avoid repeated string allocations.
        /// Stores up to 16 recent conversions for reuse.
        /// </summary>
        private static string CachedToUpperInvariant(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Simple cache hash; not cryptographic.
            int hash = input.GetHashCode() & 0xF;

            string cached = _cachedUpperStrings[hash];
            if (cached != null && string.Equals(cached, input, System.StringComparison.OrdinalIgnoreCase))
                return cached;

            string upper = input.ToUpperInvariant();
            _cachedUpperStrings[hash] = upper;
            return upper;
        }
    }
}
