using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Gameplay
{
    public sealed class BeaconRuntime : MonoBehaviour, ITickable
    {
        private static Material s_fallbackBeaconMaterial;

        private GameObject _sourcePrefab;
        private Light _light;
        private float _baseIntensity;
        private float _flickerTime;
        private bool _registeredToTickManager;

        public string BeaconId { get; private set; }
        public string Label { get; private set; }
        public Color BeaconColor { get; private set; }
        public float LightRange { get; private set; }

        private void Awake()
        {
            _light = GetComponent<Light>();
            if (_light != null)
                _baseIntensity = _light.intensity <= 0f ? 1.6f : _light.intensity;
        }

        private void OnEnable()
        {
            RegisterToTickManager();
        }

        private void OnDisable()
        {
            if (_light != null)
                _light.intensity = _baseIntensity;

            UnregisterFromTickManager();
        }

        public void Configure(string beaconId, string label, GameObject sourcePrefab, Color color, float range)
        {
            BeaconId = string.IsNullOrWhiteSpace(beaconId) ? System.Guid.NewGuid().ToString("N") : beaconId;
            Label = string.IsNullOrWhiteSpace(label) ? "BEACON" : label.Trim().ToUpperInvariant();
            BeaconColor = color;
            LightRange = Mathf.Max(0.5f, range);
            _sourcePrefab = sourcePrefab;
            _flickerTime = 0f;
            if (_light == null)
                _light = GetComponent<Light>();
            if (_light != null)
            {
                _light.color = color;
                _light.range = LightRange;
                _baseIntensity = _light.intensity <= 0f ? 1.6f : _light.intensity;
            }
        }

        public void Tick(float deltaTime)
        {
            if (_light == null)
                return;

            _flickerTime += deltaTime;
            _light.intensity = _baseIntensity * (0.8f + Mathf.Sin(_flickerTime * 3.5f) * 0.15f);
        }

        private void OnDestroy()
        {
            BeaconNetworkSystem.NotifyRuntimeDestroyed(this);
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

        private void RegisterToTickManager()
        {
            if (_registeredToTickManager || GameTickManager.Instance == null)
                return;

            GameTickManager.Instance.Register((ITickable)this);
            _registeredToTickManager = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTickManager || GameTickManager.Instance == null)
                return;

            GameTickManager.Instance.Unregister((ITickable)this);
            _registeredToTickManager = false;
        }

        public static Material GetFallbackBeaconMaterial(Color color)
        {
            if (s_fallbackBeaconMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");

                s_fallbackBeaconMaterial = new Material(shader);
            }

            s_fallbackBeaconMaterial.color = color;
            return s_fallbackBeaconMaterial;
        }
    }
}
