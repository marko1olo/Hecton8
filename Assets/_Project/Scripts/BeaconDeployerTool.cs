using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class BeaconDeployerTool : PlayerTool
    {
        [Header("Deployment")]
        [SerializeField] private float deployRange = 12f;
        [SerializeField] private float deployCooldown = 0.25f;
        [SerializeField] private int maxActiveBeacons = 24;
        [SerializeField] private LayerMask deploymentMask = ~0;
        [SerializeField] private GameObject worldBeaconPrefab;

        [Header("Fallback Beacon")]
        [SerializeField] private Color beaconColor = new Color(0.25f, 1f, 0.95f, 1f);
        [SerializeField] private Vector3 beaconScale = new Vector3(0.22f, 0.45f, 0.22f);
        [SerializeField] private float fallbackLightRange = 4f;

        private static readonly List<BeaconRuntime> ActiveBeacons = new List<BeaconRuntime>(32);

        private Transform _cachedTransform;
        private float _cooldown;

        private void Awake()
        {
            _cachedTransform = transform;
        }

        public override void UsePrimary(float deltaTime)
        {
            base.UsePrimary(deltaTime);

            if (!IsEquipped || _cooldown > 0f)
                return;

            Vector3 spawnPosition;
            Quaternion spawnRotation;

            if (UnityEngine.Physics.Raycast(
                _cachedTransform.position,
                _cachedTransform.forward,
                out RaycastHit hit,
                deployRange,
                deploymentMask,
                QueryTriggerInteraction.Ignore))
            {
                spawnPosition = hit.point + hit.normal * 0.08f;
                spawnRotation = Quaternion.LookRotation(hit.normal);
            }
            else
            {
                spawnPosition = _cachedTransform.position + _cachedTransform.forward * 4f;
                spawnRotation = Quaternion.identity;
            }

            BeaconRuntime beacon = SpawnBeacon(spawnPosition, spawnRotation);
            if (beacon != null)
            {
                RegisterBeacon(beacon);
                _cooldown = deployCooldown;
            }
        }

        public override void UseSecondary(float deltaTime)
        {
            base.UseSecondary(deltaTime);

            if (!IsEquipped || _cooldown > 0f || ActiveBeacons.Count == 0)
                return;

            BeaconRuntime nearest = null;
            float bestSqr = float.MaxValue;
            Vector3 origin = _cachedTransform.position;

            for (int i = ActiveBeacons.Count - 1; i >= 0; i--)
            {
                BeaconRuntime beacon = ActiveBeacons[i];
                if (beacon == null)
                {
                    ActiveBeacons.RemoveAt(i);
                    continue;
                }

                float sqr = (beacon.transform.position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = beacon;
                }
            }

            if (nearest != null)
            {
                ActiveBeacons.Remove(nearest);
                nearest.DespawnSelf();
                _cooldown = deployCooldown;
            }
        }

        public override void ToolTick(float deltaTime)
        {
            if (_cooldown > 0f)
                _cooldown = Mathf.Max(0f, _cooldown - deltaTime);
        }

        private BeaconRuntime SpawnBeacon(Vector3 position, Quaternion rotation)
        {
            if (worldBeaconPrefab != null && ObjectPoolManager.Instance != null)
            {
                GameObject instance = ObjectPoolManager.Instance.Spawn(worldBeaconPrefab, position, rotation);
                if (instance != null)
                {
                    BeaconRuntime pooledBeacon = instance.GetComponent<BeaconRuntime>();
                    if (pooledBeacon == null)
                        pooledBeacon = instance.AddComponent<BeaconRuntime>();

                    pooledBeacon.Configure(worldBeaconPrefab, beaconColor, fallbackLightRange);
                    return pooledBeacon;
                }
            }

            GameObject beaconRoot = new GameObject("Beacon_Runtime");
            beaconRoot.transform.SetPositionAndRotation(position, rotation);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "BeaconBody";
            body.transform.SetParent(beaconRoot.transform, false);
            body.transform.localScale = beaconScale;
            body.transform.localPosition = new Vector3(0f, beaconScale.y * 0.5f, 0f);

            Collider bodyCollider = body.GetComponent<Collider>();
            if (bodyCollider != null)
                Object.Destroy(bodyCollider);

            Renderer renderer = body.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = beaconColor;
                renderer.sharedMaterial = material;
            }

            Light lightComp = beaconRoot.AddComponent<Light>();
            lightComp.type = LightType.Point;
            lightComp.range = fallbackLightRange;
            lightComp.intensity = 1.6f;
            lightComp.color = beaconColor;

            BeaconRuntime beacon = beaconRoot.AddComponent<BeaconRuntime>();
            beacon.Configure(null, beaconColor, fallbackLightRange);
            return beacon;
        }

        private void RegisterBeacon(BeaconRuntime beacon)
        {
            ActiveBeacons.Add(beacon);

            while (ActiveBeacons.Count > maxActiveBeacons)
            {
                BeaconRuntime oldest = ActiveBeacons[0];
                ActiveBeacons.RemoveAt(0);
                if (oldest != null)
                    oldest.DespawnSelf();
            }
        }
    }

    public sealed class BeaconRuntime : MonoBehaviour
    {
        private GameObject _sourcePrefab;
        private Light _light;
        private float _baseIntensity;

        public void Configure(GameObject sourcePrefab, Color color, float range)
        {
            _sourcePrefab = sourcePrefab;
            _light = GetComponent<Light>();
            if (_light != null)
            {
                _light.color = color;
                _light.range = range;
                _baseIntensity = _light.intensity;
            }
        }

        private void Update()
        {
            if (_light != null)
                _light.intensity = _baseIntensity * (0.8f + Mathf.Sin(Time.time * 3.5f) * 0.15f);
        }

        public void DespawnSelf()
        {
            if (_sourcePrefab != null && ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Despawn(gameObject);
                return;
            }

            Destroy(gameObject);
        }
    }
}
