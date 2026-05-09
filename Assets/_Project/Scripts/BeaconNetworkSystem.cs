using System;
using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.SaveSystem;
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
            public readonly Color Color;
            public readonly float LightRange;

            public BeaconSnapshot(string id, string label, Vector3 position, Color color, float lightRange)
            {
                Id = id;
                Label = label;
                Position = position;
                Color = color;
                LightRange = lightRange;
            }
        }

        [SerializeField] private int maxTrackedBeacons = 24;
        [SerializeField] private string defaultLabelPrefix = "BEACON";
        [SerializeField] private bool verboseLogging;

        [Header("── Prefabs ───────────────────────────────────")]
        [Tooltip("Prefab for becons spawned from save data or as fallback. Should have BeaconRuntime component.")]
        [SerializeField] private GameObject beaconPrefab;

        private readonly List<BeaconRuntime> _activeBeacons = new List<BeaconRuntime>(32); // COLD ALLOC: List<BeaconRuntime>[32] — active beacon runtime registry — owner: BeaconNetworkSystem
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

            return runtime.TryRetractNearestInternal(origin, out beacon, out distance);
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

            return runtime.TryGetNearestInternal(origin, out snapshot, out distance);
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
                buffer[i] = beacon != null
                    ? new BeaconSnapshot(beacon.BeaconId, beacon.Label, beacon.transform.position, beacon.BeaconColor, beacon.LightRange)
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
                    entry.SetPosition(beacon.transform.position);
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

                // ── Zero-GC Spawn from Pool ──
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

            if (verboseLogging)
                LogBeaconDeployed(label, position);

            NetworkChanged?.Invoke();
            return true;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogBeaconDeployed(string label, Vector3 position)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[BeaconNetwork] Deployed {label} at {position}");
#endif
        }

        private static bool TryResolvePlayerOwnedInstance(out BeaconNetworkSystem beaconNetworkSystem)
        {
            beaconNetworkSystem = null;

            GameObject playerObject = SceneBootstrap.CurrentPlayerObject;
            if (playerObject == null &&
                SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                playerObject = playerTransform.gameObject;
            }

            if (playerObject == null)
                return false;

            playerObject.TryGetComponent(out beaconNetworkSystem);
            return beaconNetworkSystem != null;
        }

        private bool TryRetractNearestInternal(Vector3 origin, out BeaconRuntime beacon, out float distance)
        {
            CleanupNullEntries();
            beacon = null;
            distance = 0f;

            float bestSqr = float.MaxValue;
            for (int i = _activeBeacons.Count - 1; i >= 0; i--)
            {
                BeaconRuntime candidate = _activeBeacons[i];
                if (candidate == null)
                    continue;

                float sqr = (candidate.transform.position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    beacon = candidate;
                }
            }

            if (beacon == null)
                return false;

            distance = ApproximateDistance(bestSqr);
            _activeBeacons.Remove(beacon);
            beacon.DespawnSelf();
            NetworkChanged?.Invoke();
            return true;
        }

        private bool TryGetNearestInternal(Vector3 origin, out BeaconSnapshot snapshot, out float distance)
        {
            snapshot = default;
            distance = 0f;
            if (_activeBeacons.Count == 0)
                return false;

            BeaconRuntime best = null;
            Vector3 bestPosition = default;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _activeBeacons.Count; i++)
            {
                BeaconRuntime beacon = _activeBeacons[i];
                if (beacon == null)
                    continue;

                Vector3 beaconPosition = beacon.transform.position;
                float sqr = (beaconPosition - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = beacon;
                    bestPosition = beaconPosition;
                }
            }

            if (best == null)
                return false;

            distance = ApproximateDistance(bestSqr);
            snapshot = new BeaconSnapshot(best.BeaconId, best.Label, bestPosition, best.BeaconColor, best.LightRange);
            return true;
        }

        private static float ApproximateDistance(float distanceSq)
        {
            return distanceSq > 0f && float.IsFinite(distanceSq)
                ? distanceSq * math.rsqrt(distanceSq)
                : 0f;
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
                        pooled = instance.AddComponent<BeaconRuntime>();
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
            GameObject beaconRoot = new GameObject("Beacon_Runtime");
            beaconRoot.transform.SetPositionAndRotation(position, rotation);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
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

            Light lightComp = beaconRoot.AddComponent<Light>();
            lightComp.type = LightType.Point;
            lightComp.range = lightRange;
            lightComp.intensity = 1.6f;
            lightComp.color = color;

            BeaconRuntime runtime = beaconRoot.AddComponent<BeaconRuntime>();
            runtime.SetOwnedFallbackMaterial(fallbackMaterial);
            return runtime;
        }

        private void UnregisterRuntime(BeaconRuntime beacon)
        {
            if (beacon == null)
                return;

            if (_activeBeacons.Remove(beacon))
                NetworkChanged?.Invoke();
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
            string label = $"{prefix}-{_nextSequence:00}";
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

        // ══════════════════════════════════════════════════════════
        //  ZERO-GC STRING CACHING
        // ══════════════════════════════════════════════════════════

        private static readonly string[] _cachedUpperStrings = new string[16]; // COLD ALLOC: string[16] — upper-case label cache slots — owner: BeaconNetworkSystem

        /// <summary>
        /// Кэшированный ToUpperInvariant для избежания повторных аллокаций строк.
        /// Хранит до 16 последних преобразований для повторного использования.
        /// </summary>
        private static string CachedToUpperInvariant(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Простой hash для кэширования (не криптографический)
            int hash = input.GetHashCode() & 0xF; // Маска для индекса 0-15

            string cached = _cachedUpperStrings[hash];
            if (cached != null && string.Equals(cached, input, System.StringComparison.OrdinalIgnoreCase))
                return cached;

            // Создаем новую строку и кэшируем
            string upper = input.ToUpperInvariant();
            _cachedUpperStrings[hash] = upper;
            return upper;
        }
    }
}
