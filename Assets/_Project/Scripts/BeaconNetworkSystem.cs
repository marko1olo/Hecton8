using System;
using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Beacon Network System")]
    public sealed class BeaconNetworkSystem : MonoBehaviour, ISaveable, IBeaconNetworkService, IGlobalRegistryHotSwapListener
    {
        public readonly struct BeaconSnapshot
        {
            public readonly string Id;
            public readonly string Label;
            public readonly Vector3 Position;
            public readonly AbsoluteUniversePosition PositionAup;
            public readonly Color Color;
            public readonly float LightRange;

            public BeaconSnapshot(string id, string label, Vector3 position, Color color, float lightRange)
                : this(id, label, position, ResolveAupFromRuntimeOrigin(position), color, lightRange)
            {
            }

            public BeaconSnapshot(
                string id,
                string label,
                Vector3 position,
                AbsoluteUniversePosition positionAup,
                Color color,
                float lightRange)
            {
                Id = id;
                Label = label;
                Position = position;
                PositionAup = positionAup;
                Color = color;
                LightRange = lightRange;
            }
        }

        [SerializeField] private int maxTrackedBeacons = 24;
        [SerializeField] private string defaultLabelPrefix = "BEACON";
        [SerializeField] private bool verboseLogging;
        private const string DefaultBeaconPrefix = "BEACON";

        [Header("Prefabs")]
        [Tooltip("Authored beacon prefab spawned from save data and deployment. Must include BeaconRuntime, Renderer, Light, and authored static materials.")]
        [SerializeField] private GameObject beaconPrefab;

        private readonly List<BeaconRuntime> _activeBeacons = new List<BeaconRuntime>(32); // COLD ALLOC: List<BeaconRuntime>[32] - active beacon runtime registry - owner: BeaconNetworkSystem
        private int _nextSequence = 1;
        private bool _serviceRegistered;
        private bool _hotSwapListenerRegistered;
        private IObjectPoolService _cachedObjectPool;
        private ILocalizationTextReadModel _cachedLocalization;
        private ISaveService _cachedSaveService;
        private readonly char[] _labelPrefixBuffer = new char[32];
        private static BeaconNetworkSystem s_activeRuntime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_activeRuntime = null;
        }

        private static AbsoluteUniversePosition ResolveAupFromRuntimeOrigin(Vector3 runtimePosition)
        {
            return TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition aup)
                ? aup
                : RuntimeOriginRoute.CurrentRuntimeOriginAup();
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!float.IsFinite(runtimePosition.x) ||
                !float.IsFinite(runtimePosition.y) ||
                !float.IsFinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return math.all(math.isfinite(aup.ToAbsoluteDouble3()));
        }

        public int SavePriority => 37;
        public int LoadPriority => 37;
        public int ActiveCount => _activeBeacons.Count;

        public event Action NetworkChanged;

        private void Awake()
        {
            BeaconNetworkSystem registered = s_activeRuntime;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

            s_activeRuntime = this;
            CacheRegistryServicesCold();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegisterService();
            _cachedSaveService?.Register(this);
        }

        private void OnDisable()
        {
            _cachedSaveService?.Unregister(this);
            TryUnregisterService();
            TryUnregisterHotSwapListener();
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;
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
                case GlobalRegistryServiceSlot.Save:
                    if (Application.isPlaying && previousService is ISaveService previousSave)
                        previousSave.Unregister(this);

                    _cachedSaveService = currentService as ISaveService;

                    if (Application.isPlaying && _cachedSaveService != null && isActiveAndEnabled)
                        _cachedSaveService.Register(this);
                    break;
                case GlobalRegistryServiceSlot.BeaconNetworkRuntime:
                    if (currentService is BeaconNetworkSystem currentBeaconNetwork)
                        s_activeRuntime = currentBeaconNetwork;
                    else if (ReferenceEquals(previousService, this) && ReferenceEquals(s_activeRuntime, this))
                        s_activeRuntime = null;
                    break;
            }
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterBeaconNetworkRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.BeaconNetwork, this);
            if (_serviceRegistered)
                s_activeRuntime = this;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.BeaconNetwork, this))
                GlobalRegistry.UnregisterBeaconNetworkRuntime(this);

            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;
            _serviceRegistered = false;
        }

        public static BeaconNetworkSystem GetOrCreate()
        {
            BeaconNetworkSystem registered = s_activeRuntime;
            if (registered != null)
                return registered;

            if (TryResolvePlayerOwnedInstance(out BeaconNetworkSystem existing))
            {
                if (Application.isPlaying)
                    existing.TryRegisterService();
                s_activeRuntime = existing;
                return existing;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError("[BeaconNetwork] Missing bootstrap-owned BeaconNetworkSystem service.");
#endif
            return null;
        }

        public static bool TryDeployBeacon(
            GameObject worldBeaconPrefab,
            Vector3 position,
            Quaternion rotation,
            Color color,
            float lightRange,
            Vector3 fallbackScale,
            int maxActive,
            out BeaconRuntime beacon,
            out string label)
        {
            BeaconNetworkSystem runtime = GetOrCreate();
            if (runtime == null)
            {
                beacon = null;
                label = string.Empty;
                return false;
            }

            return runtime.TryDeployInternal(
                worldBeaconPrefab,
                position,
                rotation,
                color,
                lightRange,
                fallbackScale,
                maxActive,
                out beacon,
                out label);
        }

        public static bool TryRetractNearest(Vector3 origin, out BeaconRuntime beacon, out float distance)
        {
            BeaconNetworkSystem runtime = s_activeRuntime;
            if (runtime == null)
            {
                beacon = null;
                distance = 0f;
                return false;
            }

            if (!TryResolveAupFromRuntimeOrigin(origin, out AbsoluteUniversePosition originAup))
            {
                beacon = null;
                distance = 0f;
                return false;
            }

            return runtime.TryRetractNearestInternal(in originAup, out beacon, out distance);
        }

        public static bool TryRetractNearest(in AbsoluteUniversePosition originAup, out BeaconRuntime beacon, out float distance)
        {
            BeaconNetworkSystem runtime = s_activeRuntime;
            if (runtime == null)
            {
                beacon = null;
                distance = 0f;
                return false;
            }

            return runtime.TryRetractNearestInternal(in originAup, out beacon, out distance);
        }

        public static bool TryGetNearest(Vector3 origin, out BeaconSnapshot snapshot, out float distance)
        {
            BeaconNetworkSystem runtime = s_activeRuntime;
            if (runtime == null)
            {
                snapshot = default;
                distance = 0f;
                return false;
            }

            if (!TryResolveAupFromRuntimeOrigin(origin, out AbsoluteUniversePosition originAup))
            {
                snapshot = default;
                distance = 0f;
                return false;
            }

            return runtime.TryGetNearestInternal(in originAup, out snapshot, out distance);
        }

        public static bool TryGetNearest(in AbsoluteUniversePosition originAup, out BeaconSnapshot snapshot, out float distance)
        {
            BeaconNetworkSystem runtime = s_activeRuntime;
            if (runtime == null)
            {
                snapshot = default;
                distance = 0f;
                return false;
            }

            return runtime.TryGetNearestInternal(in originAup, out snapshot, out distance);
        }

        public bool TryDeployBeaconFromTool(
            GameObject worldBeaconPrefab,
            Vector3 position,
            Quaternion rotation,
            Color color,
            float lightRange,
            Vector3 fallbackScale,
            int maxActive,
            out BeaconRuntime beacon,
            out string label)
        {
            return TryDeployInternal(
                worldBeaconPrefab,
                position,
                rotation,
                color,
                lightRange,
                fallbackScale,
                maxActive,
                out beacon,
                out label);
        }

        public bool TryDeployBeaconFromTool(
            GameObject worldBeaconPrefab,
            Vector3 position,
            Quaternion rotation,
            Color color,
            float lightRange,
            Vector3 fallbackScale,
            int maxActive,
            out string label)
        {
            return TryDeployInternal(
                worldBeaconPrefab,
                position,
                rotation,
                color,
                lightRange,
                fallbackScale,
                maxActive,
                out _,
                out label);
        }

        public bool TryRetractNearestFromTool(in AbsoluteUniversePosition originAup, out BeaconRuntime beacon, out float distance)
        {
            return TryRetractNearestInternal(in originAup, out beacon, out distance);
        }

        public bool TryRetractNearestFromTool(in AbsoluteUniversePosition originAup, out float distance)
        {
            return TryRetractNearestInternal(in originAup, out _, out distance);
        }

        public bool TryGetNearestFromTool(in AbsoluteUniversePosition originAup, out BeaconSnapshot snapshot, out float distance)
        {
            return TryGetNearestInternal(in originAup, out snapshot, out distance);
        }

        public bool TryGetNearestFromTool(in AbsoluteUniversePosition originAup, out BeaconNetworkSnapshot snapshot, out float distance)
        {
            if (TryGetNearestInternal(in originAup, out BeaconSnapshot ownerSnapshot, out distance))
            {
                snapshot = ToContractSnapshot(ownerSnapshot);
                return true;
            }

            snapshot = default;
            return false;
        }

        public int CopySnapshots(BeaconSnapshot[] buffer)
        {
            CleanupNullEntries();
            if (buffer == null || buffer.Length == 0 || _activeBeacons.Count == 0)
                return 0;

            int count = Mathf.Min(buffer.Length, _activeBeacons.Count);
            for (int i = 0; i < count; i++)
            {
                BeaconRuntime beacon = _activeBeacons[i];
                if (beacon != null)
                {
                    Vector3 runtimePosition = beacon.RuntimePosition;
                    buffer[i] = new BeaconSnapshot(
                        beacon.BeaconId,
                        beacon.Label,
                        runtimePosition,
                        beacon.PositionAup,
                        beacon.BeaconColor,
                        beacon.LightRange);
                }
                else
                {
                    buffer[i] = default;
                }
            }

            return count;
        }

        public int CopySnapshots(BeaconNetworkSnapshot[] buffer)
        {
            CleanupNullEntries();
            if (buffer == null || buffer.Length == 0 || _activeBeacons.Count == 0)
                return 0;

            int count = Mathf.Min(buffer.Length, _activeBeacons.Count);
            for (int i = 0; i < count; i++)
            {
                BeaconRuntime beacon = _activeBeacons[i];
                buffer[i] = beacon != null
                    ? new BeaconNetworkSnapshot(
                        beacon.BeaconId,
                        beacon.Label,
                        beacon.RuntimePosition,
                        beacon.PositionAup,
                        beacon.BeaconColor,
                        beacon.LightRange)
                    : default;
            }

            return count;
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            CleanupNullEntries();
            data.beaconNetwork.EnsureCapacity();
            data.beaconNetwork.activeCount = Mathf.Min(_activeBeacons.Count, BeaconNetworkDTO.MaxEntries);
            data.beaconNetwork.nextSequence = Mathf.Max(1, _nextSequence);

            for (int i = 0; i < data.beaconNetwork.activeCount; i++)
            {
                BeaconRuntime beacon = _activeBeacons[i];
                BeaconEntryDTO entry = new BeaconEntryDTO
                {
                    id = beacon != null ? beacon.BeaconId : string.Empty,
                    label = beacon != null ? beacon.Label : string.Empty,
                    colorR = beacon != null ? beacon.BeaconColor.r : 0f,
                    colorG = beacon != null ? beacon.BeaconColor.g : 0f,
                    colorB = beacon != null ? beacon.BeaconColor.b : 0f,
                    colorA = beacon != null ? beacon.BeaconColor.a : 1f,
                    lightRange = beacon != null ? beacon.LightRange : 4f
                };

                if (beacon != null)
                {
                    entry.SetPosition(beacon.RuntimePosition);
                    entry.SetRotation(beacon.transform.rotation);
                }

                data.beaconNetwork.entries[i] = entry;
            }

            for (int i = data.beaconNetwork.activeCount; i < BeaconNetworkDTO.MaxEntries; i++)
                data.beaconNetwork.entries[i] = default;
        }

        public void LoadFromSaveData(SaveData data)
        {
            ClearAllRuntimeBeacons();

            if (data == null)
                return;

            BeaconNetworkDTO dto = data.beaconNetwork;
            _nextSequence = Mathf.Max(1, dto.nextSequence);

            int count = Mathf.Clamp(dto.activeCount, 0, dto.entries != null ? dto.entries.Length : 0);
            for (int i = 0; i < count; i++)
            {
                BeaconEntryDTO entry = dto.entries[i];
                if (string.IsNullOrWhiteSpace(entry.label))
                    continue;

                // Zero-GC spawn from pool.
                BeaconRuntime runtime = SpawnRuntimeBeacon(
                    beaconPrefab, 
                    entry.GetPosition(),
                    entry.GetRotation(),
                    entry.GetColor(),
                    Mathf.Max(0.5f, entry.lightRange),
                    new Vector3(0.22f, 0.45f, 0.22f));

                if (runtime == null)
                    continue;

                runtime.Configure(entry.id, entry.label, beaconPrefab, entry.GetColor(), entry.lightRange);
                _activeBeacons.Add(runtime);
            }

            CleanupNullEntries();
            NetworkChanged?.Invoke();
        }

        internal static void NotifyRuntimeDestroyed(BeaconRuntime beacon)
        {
            BeaconNetworkSystem runtime = s_activeRuntime;
            if (beacon == null || runtime == null)
                return;

            runtime.UnregisterRuntime(beacon);
        }

        private bool TryDeployInternal(
            GameObject worldBeaconPrefab,
            Vector3 position,
            Quaternion rotation,
            Color color,
            float lightRange,
            Vector3 fallbackScale,
            int maxActive,
            out BeaconRuntime beacon,
            out string label)
        {
            CleanupNullEntries();

            beacon = SpawnRuntimeBeacon(worldBeaconPrefab, position, rotation, color, lightRange, fallbackScale);
            if (beacon == null)
            {
                label = string.Empty;
                return false;
            }

            label = BuildNextLabel();
            beacon.Configure(label, label, worldBeaconPrefab, color, lightRange);
            _activeBeacons.Add(beacon);

            int cap = Mathf.Clamp(maxActive > 0 ? maxActive : maxTrackedBeacons, 1, BeaconNetworkDTO.MaxEntries);
            TrimOldestBeaconsToCap(cap);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogging)
                LogBeaconDeployed();
#endif

            NetworkChanged?.Invoke();
            return true;
        }

        private void TrimOldestBeaconsToCap(int cap)
        {
            int excessCount = _activeBeacons.Count - cap;
            if (excessCount <= 0)
                return;

            for (int i = 0; i < excessCount; i++)
            {
                BeaconRuntime oldest = _activeBeacons[i];
                if (oldest == null)
                    continue;

                oldest.DespawnSelf();
                FieldOperationLogSystem.RecordOperation(
                    DefaultBeaconPrefix,
                    "BEACON GRID TRIMMED",
                    "Oldest beacon anchor was retired to preserve the active field-marker cap.",
                    "WARN");
            }

            _activeBeacons.RemoveRange(0, excessCount);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogBeaconDeployed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log("[BeaconNetwork] Beacon deployed.");
#endif
        }

        private static bool TryResolvePlayerOwnedInstance(out BeaconNetworkSystem beaconNetworkSystem)
        {
            beaconNetworkSystem = null;

            GameObject playerObject = GameBootstrapper.CurrentPlayerObject;
            if (playerObject == null &&
                GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                playerObject = playerTransform.gameObject;
            }

            if (playerObject == null)
                return false;

            playerObject.TryGetComponent(out beaconNetworkSystem);
            return beaconNetworkSystem != null;
        }

        private bool TryRetractNearestInternal(in AbsoluteUniversePosition originAup, out BeaconRuntime beacon, out float distance)
        {
            beacon = null;
            distance = 0f;

            double bestDistanceSq = double.MaxValue;
            int bestIndex = -1;
            for (int i = _activeBeacons.Count - 1; i >= 0; i--)
            {
                BeaconRuntime candidate = _activeBeacons[i];
                if (candidate == null)
                    continue;

                AbsoluteUniversePosition candidateAup = candidate.PositionAup;
                double distanceSq = AbsoluteUniversePosition.DistanceSq(in candidateAup, in originAup);
                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    beacon = candidate;
                    bestIndex = i;
                }
            }

            if (beacon == null || bestIndex < 0)
                return false;

            distance = ApproximateDistance(bestDistanceSq);
            _activeBeacons.RemoveAt(bestIndex);
            beacon.DespawnSelf();
            NetworkChanged?.Invoke();
            return true;
        }

        private bool TryGetNearestInternal(in AbsoluteUniversePosition originAup, out BeaconSnapshot snapshot, out float distance)
        {
            snapshot = default;
            distance = 0f;
            if (_activeBeacons.Count == 0)
                return false;

            BeaconRuntime best = null;
            Vector3 bestPosition = default;
            AbsoluteUniversePosition bestAup = default;
            double bestDistanceSq = double.MaxValue;
            for (int i = 0; i < _activeBeacons.Count; i++)
            {
                BeaconRuntime beacon = _activeBeacons[i];
                if (beacon == null)
                    continue;

                AbsoluteUniversePosition beaconAup = beacon.PositionAup;
                double distanceSq = AbsoluteUniversePosition.DistanceSq(in beaconAup, in originAup);
                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    best = beacon;
                    bestPosition = beacon.RuntimePosition;
                    bestAup = beaconAup;
                }
            }

            if (best == null)
                return false;

            distance = ApproximateDistance(bestDistanceSq);
            snapshot = new BeaconSnapshot(best.BeaconId, best.Label, bestPosition, bestAup, best.BeaconColor, best.LightRange);
            return true;
        }

        private static BeaconNetworkSnapshot ToContractSnapshot(BeaconSnapshot snapshot)
        {
            return new BeaconNetworkSnapshot(
                snapshot.Id,
                snapshot.Label,
                snapshot.Position,
                snapshot.PositionAup,
                snapshot.Color,
                snapshot.LightRange);
        }

        private static float ApproximateDistance(float distanceSq)
        {
            return ApproximateDistance((double)distanceSq);
        }

        private static float ApproximateDistance(double distanceSq)
        {
            if (distanceSq <= 0d || double.IsNaN(distanceSq) || double.IsInfinity(distanceSq))
                return 0f;

            if (distanceSq >= float.MaxValue)
                return float.MaxValue;

            float distanceSqFloat = (float)distanceSq;
            return distanceSqFloat * math.rsqrt(distanceSqFloat);
        }

        private BeaconRuntime SpawnRuntimeBeacon(
            GameObject worldBeaconPrefab,
            Vector3 position,
            Quaternion rotation,
            Color color,
            float lightRange,
            Vector3 fallbackScale)
        {
            _ = color;
            _ = lightRange;
            _ = fallbackScale;

            IObjectPoolService pool = _cachedObjectPool;
            if (worldBeaconPrefab != null && pool != null)
            {
                GameObject instance = pool.Spawn(worldBeaconPrefab, position, rotation);
                if (instance != null)
                {
                    instance.TryGetComponent(out BeaconRuntime pooled);
                    if (pooled == null)
                    {
                        pool.Despawn(instance);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        if (verboseLogging)
                            Hecton8.Core.H8Debug.LogWarning("[BeaconNetwork] Beacon prefab is missing BeaconRuntime; spawn rejected.");
#endif
                        return null;
                    }

                    pooled.SetPooledOwner(pool);
                    return pooled;
                }
            }

            return null;
        }

        private void UnregisterRuntime(BeaconRuntime beacon)
        {
            if (beacon == null)
                return;

            if (TryRemoveActiveBeacon(beacon))
                NetworkChanged?.Invoke();
        }

        private bool TryRemoveActiveBeacon(BeaconRuntime beacon)
        {
            int index = IndexOfActiveBeacon(beacon);
            if (index < 0)
                return false;

            _activeBeacons.RemoveAt(index);
            return true;
        }

        private int IndexOfActiveBeacon(BeaconRuntime beacon)
        {
            for (int i = 0; i < _activeBeacons.Count; i++)
            {
                if (ReferenceEquals(_activeBeacons[i], beacon))
                    return i;
            }

            return -1;
        }

        private void ClearAllRuntimeBeacons()
        {
            CleanupNullEntries();
            for (int i = _activeBeacons.Count - 1; i >= 0; i--)
            {
                BeaconRuntime beacon = _activeBeacons[i];
                if (beacon != null)
                    beacon.DespawnSelf();
            }

            _activeBeacons.Clear();
        }

        private void CleanupNullEntries()
        {
            int count = _activeBeacons.Count;
            int writeIndex = 0;
            for (int readIndex = 0; readIndex < count; readIndex++)
            {
                BeaconRuntime beacon = _activeBeacons[readIndex];
                if (beacon == null)
                    continue;

                if (writeIndex != readIndex)
                    _activeBeacons[writeIndex] = beacon;

                writeIndex++;
            }

            for (int i = count - 1; i >= writeIndex; i--)
                _activeBeacons.RemoveAt(i);
        }

        private string BuildNextLabel()
        {
            ReadOnlySpan<char> prefix = ResolveLabelPrefixSpan();
            string label = CreateBeaconLabel(prefix, _nextSequence);
            _nextSequence++;
            return label;
        }

        private ReadOnlySpan<char> ResolveLabelPrefixSpan()
        {
            ReadOnlySpan<char> authoredPrefix = string.IsNullOrWhiteSpace(defaultLabelPrefix)
                ? ReadOnlySpan<char>.Empty
                : defaultLabelPrefix.AsSpan();
            if (!authoredPrefix.IsEmpty)
                return CopyLabelPrefix(authoredPrefix);

            ILocalizationTextReadModel manager = _cachedLocalization;
            ReadOnlySpan<char> localized = manager != null
                ? manager.GetRawSpanOrFallback(LocHash.Compute(LocalizationKeys.BEACON_PREFIX), DefaultBeaconPrefix.AsSpan())
                : DefaultBeaconPrefix.AsSpan();
            return CopyLabelPrefix(localized);
        }

        private ReadOnlySpan<char> CopyLabelPrefix(ReadOnlySpan<char> source)
        {
            int length = math.min(source.Length, _labelPrefixBuffer.Length);
            if (length <= 0)
            {
                source = DefaultBeaconPrefix.AsSpan();
                length = math.min(source.Length, _labelPrefixBuffer.Length);
            }

            source.Slice(0, length).CopyTo(_labelPrefixBuffer);
            return _labelPrefixBuffer.AsSpan(0, length);
        }

        private void CacheRegistryServicesCold()
        {
            _cachedObjectPool = GlobalRegistry.ObjectPoolService;
            _cachedLocalization = Hecton8.Core.GlobalRegistry.LocalizationText;
            _cachedSaveService = GlobalRegistry.Save;
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

        private readonly struct BeaconLabelState
        {
            public readonly char[] PrefixBuffer;
            public readonly int PrefixLength;
            public readonly int Sequence;

            public BeaconLabelState(char[] prefixBuffer, int prefixLength, int sequence)
            {
                PrefixBuffer = prefixBuffer;
                PrefixLength = prefixLength;
                Sequence = sequence;
            }
        }

        private string CreateBeaconLabel(ReadOnlySpan<char> prefix, int sequence)
        {
            int safeSequence = math.max(0, sequence);
            int digitCount = safeSequence < 100 ? 2 : CountDecimalDigits(safeSequence);
            int prefixLength = math.min(prefix.Length, _labelPrefixBuffer.Length);
            return string.Create(prefixLength + 1 + digitCount, new BeaconLabelState(_labelPrefixBuffer, prefixLength, safeSequence), (buffer, state) =>
            {
                char[] statePrefix = state.PrefixBuffer;
                int statePrefixLength = state.PrefixLength;
                for (int i = 0; i < statePrefixLength; i++)
                    buffer[i] = statePrefix[i];

                buffer[statePrefixLength] = '-';
                int write = buffer.Length - 1;
                int remaining = state.Sequence;
                do
                {
                    buffer[write--] = (char)('0' + remaining % 10);
                    remaining /= 10;
                }
                while (write > statePrefixLength);
            });
        }

        private static int CountDecimalDigits(int value)
        {
            int digits = 1;
            int remaining = math.max(0, value);
            while (remaining >= 10)
            {
                remaining /= 10;
                digits++;
            }

            return digits;
        }
    }
}
