using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

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
        private const string DefaultBeaconLabel = "BEACON";

        private GameObject _sourcePrefab;
        private Transform _cachedTransform;
        private Light _light;
        private AbsoluteUniversePosition _cachedAup;
        private float _baseIntensity;
        private float _flickerTime;
        private bool _registeredLateFrame;
        private bool _hotSwapListenerRegistered;
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
                ? DefaultBeaconLabel
                : label;
            BeaconColor = color;
            LightRange = Mathf.Max(0.5f, range);
            _sourcePrefab = sourcePrefab;
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
            BeaconNetworkSystem.NotifyRuntimeDestroyed(this);
        }

        public void DespawnSelf()
        {
            if (_sourcePrefab != null && _pooledOwner != null)
            {
                _pooledOwner.Despawn(gameObject);
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

        internal void SetPooledOwner(IObjectPoolService pool)
        {
            _pooledOwner = pool;
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

        private void CacheRegistryServicesCold()
        {
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
    }
}
