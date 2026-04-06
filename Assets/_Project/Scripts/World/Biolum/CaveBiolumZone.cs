// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  HADES HECTON-8 | CaveBiolumZone (OPTIMIZED)                               ║
// ║  Cave-specific: pre-computed spectral colors, light pooling, LOD            ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using UnityEngine;
using Hecton8.Caves;

namespace Hecton8.Biolum
{
    /// <summary>
    /// Cave-specific zone: spectral colors (warm→white→cold), light pooling.
    /// Uses pre-computed BiolumSpectrums for O(1) color lookup.
    /// </summary>
    [DisallowMultipleComponent]
    public class CaveBiolumZone : HectonBiolumZone
    {
        [Header("── Cave-Specific ──────────────────")]
        [SerializeField] protected SpawnContext _spawnContext = SpawnContext.CaveShallow;
        [SerializeField, Range(0f, 1f)] protected float _spectralPosition = 0.5f;
        [SerializeField] protected HectonVoxelVolume _caveVolume;

        protected override void Awake()
        {
            base.Awake();
            InitializeSpectralPosition();
        }

        protected override void EvaluateBiolumState()
        {
            if (_activeLightCount == 0)
                CreateCaveLights();
            else
                UpdateCaveLights();
        }

        protected override Color GetBiolumColor()
        {
            return BiolumSpectrums.Sample(BiolumSpectrums.CaveSpectrum, _spectralPosition);
        }

        protected override float GetBiolumIntensity()
        {
            return ScaleIntensityByMood(_intensityMultiplier);
        }

        protected override float GetBiolumRange()
        {
            return ScaleRangeByHazard(_rangeMultiplier);
        }

        private void InitializeSpectralPosition()
        {
            _spectralPosition = _spawnContext switch
            {
                SpawnContext.CaveShallow => 0.0f,
                SpawnContext.CaveMid => 0.5f,
                SpawnContext.CaveDeep => 1.0f,
                _ => 0.5f
            };
        }

        private void CreateCaveLights()
        {
            Color color = GetBiolumColor();
            float intensity = GetBiolumIntensity();
            float range = GetBiolumRange();

            GetOrCreateLight(
                _cachedTransform.position + Vector3.up * 2f,
                color,
                range * 0.8f,
                intensity * 1.2f
            );

            if (_caveVolume != null || _maxLights >= 3)
            {
                GetOrCreateLight(
                    _cachedTransform.position + Vector3.forward * 5f + Vector3.up * 1f,
                    color,
                    range * 0.6f,
                    intensity * 0.8f
                );
            }

            if (_hazardLevel > 0.5f)
            {
                Color hazardColor = Color.Lerp(color, Color.red, 0.3f);
                GetOrCreateLight(
                    _cachedTransform.position + Vector3.down * 2f,
                    hazardColor,
                    range * 0.5f,
                    intensity * 0.6f
                );
            }
        }

        private void UpdateCaveLights()
        {
            Color color = GetBiolumColor();
            float intensity = GetBiolumIntensity();
            float range = GetBiolumRange();

            for (int i = 0; i < _activeLightCount; i++)
            {
                UpdateLight(
                    _activeLights[i],
                    color,
                    range * (1f - i * 0.2f),
                    intensity * (1f - i * 0.2f)
                );
            }
        }

        #if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Color spectralColor = BiolumSpectrums.Sample(BiolumSpectrums.CaveSpectrum, _spectralPosition);
            Gizmos.color = spectralColor;
            Gizmos.DrawWireSphere(transform.position, 3f);
            Gizmos.color = new Color(spectralColor.r, spectralColor.g, spectralColor.b, 0.2f);
            Gizmos.DrawWireSphere(transform.position, GetBiolumRange());
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
                InitializeSpectralPosition();
        }
        #endif
    }
}
