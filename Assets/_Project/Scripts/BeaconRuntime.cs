using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Gameplay
{
    public sealed class BeaconRuntime : MonoBehaviour
    {
        private static Material s_fallbackBeaconMaterial;

        private GameObject _sourcePrefab;
        private Light _light;
        private float _baseIntensity;

        public string BeaconId { get; private set; }
        public string Label { get; private set; }
        public Color BeaconColor { get; private set; }
        public float LightRange { get; private set; }

        public void Configure(string beaconId, string label, GameObject sourcePrefab, Color color, float range)
        {
            BeaconId = string.IsNullOrWhiteSpace(beaconId) ? System.Guid.NewGuid().ToString("N") : beaconId;
            Label = string.IsNullOrWhiteSpace(label) ? "BEACON" : label.Trim().ToUpperInvariant();
            BeaconColor = color;
            LightRange = Mathf.Max(0.5f, range);
            _sourcePrefab = sourcePrefab;
            _light = GetComponent<Light>();
            if (_light != null)
            {
                _light.color = color;
                _light.range = LightRange;
                _baseIntensity = _light.intensity <= 0f ? 1.6f : _light.intensity;
            }
        }

        private void Update()
        {
            if (_light != null)
                _light.intensity = _baseIntensity * (0.8f + Mathf.Sin(Time.time * 3.5f) * 0.15f);
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
