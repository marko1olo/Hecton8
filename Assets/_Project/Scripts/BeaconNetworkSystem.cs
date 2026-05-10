using System;
using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Beacon Network System")]
    public sealed class BeaconNetworkSystem : MonoBehaviour, ISaveable
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
                : this(id, label, position, AbsoluteUniversePosition.FromRuntimePosition(position), color, lightRange)
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

        [Header("Prefabs")]
        [Tooltip("Prefab for becons spawned from save data or as fallback. Should have BeaconRuntime component.")]
        [SerializeField] private GameObject beaconPrefab;

        private readonly List<BeaconRuntime> _activeBeacons = new List<BeaconRuntime>(32); // COLD ALLOC: List<BeaconRuntime>[32] - active beacon runtime registry - owner: BeaconNetworkSystem
        private int _nextSequence = 1;
        private bool _serviceRegistered;

        public static BeaconNetworkSystem Instance => GlobalRegistry.BeaconNetwork;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
        }

        public int SavePriority => 37;
        public int LoadPriority => 37;
        public int ActiveCount => _activeBeacons.Count;

        public event Action NetworkChanged;

        private void Awake()
        {
            BeaconNetworkSystem registered = GlobalRegistry.BeaconNetwork;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void OnEnable()
        {
            TryRegisterService();
            Hecton8.Core.GlobalRegistry.SaveRuntime?.Register(this);
        }

        private void OnDisable()
        {
            TryUnregisterService();
            Hecton8.Core.GlobalRegistry.SaveRuntime?.Unregister(this);
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterBeaconNetworkRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.BeaconNetwork, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.BeaconNetwork, this))
                GlobalRegistry.UnregisterBeaconNetworkRuntime(this);

            _serviceRegistered = false;
        }

        public static BeaconNetworkSystem GetOrCreate()
        {
            BeaconNetworkSystem registered = GlobalRegistry.BeaconNetwork;
            if (registered != null)
                return registered;

            if (TryResolvePlayerOwnedInstance(out BeaconNetworkSystem existing))
            {
                if (Application.isPlaying && !ReferenceEquals(GlobalRegistry.BeaconNetwork, existing))
                    GlobalRegistry.RegisterBeaconNetworkRuntime(existing);

                return existing;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[BeaconNetwork] Missing bootstrap-owned BeaconNetworkSystem service.");
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
            BeaconNetworkSystem runtime = GlobalRegistry.BeaconNetwork;
            if (runtime == null)
            {
                beacon = null;
                distance = 0f;
                return false;
            }

            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(origin);
            return runtime.TryRetractNearestInternal(in originAup, out beacon, out distance);
        }

        public static bool TryRetractNearest(in AbsoluteUniversePosition originAup, out BeaconRuntime beacon, out float distance)
        {
            BeaconNetworkSystem runtime = GlobalRegistry.BeaconNetwork;
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
            BeaconNetworkSystem runtime = GlobalRegistry.BeaconNetwork;
            if (runtime == null)
            {
                snapshot = default;
                distance = 0f;
                return false;
            }

            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(origin);
            return runtime.TryGetNearestInternal(in originAup, out snapshot, out distance);
        }

        public static bool TryGetNearest(in AbsoluteUniversePosition originAup, out BeaconSnapshot snapshot, out float distance)
        {
            BeaconNetworkSystem runtime = GlobalRegistry.BeaconNetwork;
            if (runtime == null)
            {
                snapshot = default;
                distance = 0f;
                return false;
            }

            return runtime.TryGetNearestInternal(in originAup, out snapshot, out distance);
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
            BeaconNetworkSystem runtime = GlobalRegistry.BeaconNetwork;
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
            while (_activeBeacons.Count > cap)
            {
                BeaconRuntime oldest = _activeBeacons[0];
                _activeBeacons.RemoveAt(0);
                if (oldest != null)
                {
                    oldest.DespawnSelf();
                    FieldOperationLogSystem.RecordOperation(
                        ResolveLocalized(LocalizationKeys.BEACON_PREFIX, "BEACON"),
                        ResolveLocalized(LocalizationKeys.BEACON_LOG_TRIMMED_TITLE, "BEACON GRID TRIMMED"),
                        ResolveLocalized(
                            LocalizationKeys.BEACON_LOG_TRIMMED_MESSAGE,
                            "Oldest beacon anchor was retired to preserve the active field-marker cap."),
                        "WARN");
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogging)
                LogBeaconDeployed();
#endif

            NetworkChanged?.Invoke();
            return true;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogBeaconDeployed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[BeaconNetwork] Beacon deployed.");
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
            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (worldBeaconPrefab != null && pool != null)
            {
                GameObject instance = pool.Spawn(worldBeaconPrefab, position, rotation);
                if (instance != null)
                {
                    instance.TryGetComponent(out BeaconRuntime pooled);
                    if (pooled == null)
                        pooled = instance.AddComponent<BeaconRuntime>(); // COLD ALLOC: BeaconRuntime[1] - prefab missing runtime component fallback - owner: BeaconNetworkSystem
                    return pooled;
                }
            }

            return SpawnFallbackBeacon(position, rotation, color, lightRange, fallbackScale);
        }

        private BeaconRuntime SpawnFallbackBeacon(
            Vector3 position,
            Quaternion rotation,
            Color color,
            float lightRange,
            Vector3 fallbackScale)
        {
            GameObject beaconRoot = new GameObject("Beacon_Runtime"); // COLD ALLOC: GameObject[1] - fallback beacon root when prefab/pool is unavailable - owner: BeaconNetworkSystem
            beaconRoot.transform.SetPositionAndRotation(position, rotation);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube); // COLD ALLOC: GameObject[1] - fallback beacon body primitive when prefab/pool is unavailable - owner: BeaconNetworkSystem
            body.name = "BeaconBody";
            body.transform.SetParent(beaconRoot.transform, false);
            body.transform.localScale = fallbackScale;
            body.transform.localPosition = new Vector3(0f, fallbackScale.y * 0.5f, 0f);

            body.TryGetComponent(out Collider bodyCollider);
            if (bodyCollider != null)
                Destroy(bodyCollider);

            body.TryGetComponent(out Renderer renderer);
            Material fallbackMaterial = null;
            if (renderer != null)
            {
                fallbackMaterial = BeaconRuntime.GetFallbackBeaconMaterial(color);
                renderer.sharedMaterial = fallbackMaterial;
            }

            Light lightComp = beaconRoot.AddComponent<Light>(); // COLD ALLOC: Light[1] - fallback beacon point light when prefab/pool is unavailable - owner: BeaconNetworkSystem
            lightComp.type = LightType.Point;
            lightComp.range = lightRange;
            lightComp.intensity = 1.6f;
            lightComp.color = color;

            BeaconRuntime runtime = beaconRoot.AddComponent<BeaconRuntime>(); // COLD ALLOC: BeaconRuntime[1] - fallback beacon runtime when prefab/pool is unavailable - owner: BeaconNetworkSystem
            runtime.SetOwnedFallbackMaterial(fallbackMaterial);
            return runtime;
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
            for (int i = _activeBeacons.Count - 1; i >= 0; i--)
            {
                if (_activeBeacons[i] == null)
                    _activeBeacons.RemoveAt(i);
            }
        }

        private string BuildNextLabel()
        {
            string prefix = string.IsNullOrWhiteSpace(defaultLabelPrefix)
                ? ResolveLocalized(LocalizationKeys.BEACON_PREFIX, "BEACON")
                : CachedToUpperInvariant(defaultLabelPrefix.Trim());
            string label = CreateBeaconLabel(prefix, _nextSequence);
            _nextSequence++;
            return label;
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }

        // ZERO-GC STRING CACHING

        private static readonly string[] _cachedUpperStrings = new string[16]; // COLD ALLOC: string[16] - upper-case label cache slots - owner: BeaconNetworkSystem

        /// <summary>
        /// Caches uppercase label variants to avoid repeated string allocations.
        /// Keeps the last 16 transformed labels for reuse.
        /// </summary>
        private static string CachedToUpperInvariant(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            int hash = input.GetHashCode() & 0xF;

            string cached = _cachedUpperStrings[hash];
            if (cached != null && string.Equals(cached, input, System.StringComparison.OrdinalIgnoreCase))
                return cached;

            string upper = input.ToUpperInvariant();
            _cachedUpperStrings[hash] = upper;
            return upper;
        }

        private readonly struct BeaconLabelState
        {
            public readonly string Prefix;
            public readonly int Sequence;

            public BeaconLabelState(string prefix, int sequence)
            {
                Prefix = prefix;
                Sequence = sequence;
            }
        }

        private static string CreateBeaconLabel(string prefix, int sequence)
        {
            int safeSequence = math.max(0, sequence);
            int digitCount = safeSequence < 100 ? 2 : CountDecimalDigits(safeSequence);
            int prefixLength = prefix != null ? prefix.Length : 0;
            return string.Create(prefixLength + 1 + digitCount, new BeaconLabelState(prefix, safeSequence), static (buffer, state) =>
            {
                string statePrefix = state.Prefix;
                int statePrefixLength = statePrefix != null ? statePrefix.Length : 0;
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
