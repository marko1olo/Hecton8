// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  HADES HECTON-8 | OceanBiolumZone                                           ║
// ║  Open water bioluminescence (scattered, cold, depth-dependent)              ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using UnityEngine;

namespace Hecton8.Biolum
{
    /// <summary>
    /// Bioluminescence for open ocean environments.
    /// Scattered lights at variable heights (mid-water biota).
    /// Predominantly cold colors (blues, cyans, greens).
    /// Intensity scales with depth and water clarity.
    /// </summary>
    [DisallowMultipleComponent]
    public class OceanBiolumZone : HectonBiolumZone
    {
        // ─────────────────────────────────────────────────────────────────────────────
        // INSPECTOR SETTINGS
        // ─────────────────────────────────────────────────────────────────────────────

        [Header("── Ocean-Specific Settings ──────")]
        [SerializeField, Range(0f, 1f), Tooltip("Depth ratio (0=surface, 1=abyss)")]
        protected float _depthRatio = 0.5f;

        [SerializeField, Range(2, 10), Tooltip("Number of scattered lights")]
        protected int _lightCount = 4;

        [SerializeField, Range(0f, 20f), Tooltip("Radius of light scatter")]
        protected float _scatterRadius = 10f;

        [SerializeField, Tooltip("Use Perlin noise for light position variation")]
        protected bool _useNoiseVariation = true;

        // ─────────────────────────────────────────────────────────────────────────────
        // PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────────────

        private Vector3[] _lightPositions; // COLD ALLOC: positions for scattered lights
        private Color[] _lightColors;      // COLD ALLOC: colors for each light

        // Ocean color palette
        private Color _surfaceBlue = new Color(0.3f, 0.7f, 1f);     // Surface: bright blue
        private Color _twilightBlue = new Color(0.2f, 0.4f, 0.8f);  // Twilight: darker blue
        private Color _abyssBlue = new Color(0.1f, 0.2f, 0.5f);     // Abyss: deep blue
        private Color _biolumGreen = new Color(0.2f, 1f, 0.6f);     // Biolum: green-cyan
        private Color _biolumPurple = new Color(0.8f, 0.3f, 1f);    // Exotic: purple glow

        // ─────────────────────────────────────────────────────────────────────────────
        // LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            _lightPositions = new Vector3[_maxLights]; // COLD ALLOC
            _lightColors = new Color[_maxLights];      // COLD ALLOC
            GenerateLightPositions();
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // IMPLEMENTATION: Ocean-Specific Logic
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluate ocean lighting state.
        /// Creates scattered lights at variable heights (mid-water creatures).
        /// </summary>
        protected override void EvaluateBiolumState()
        {
            if (_activeLightCount == 0)
            {
                CreateOceanLights();
            }
            else
            {
                UpdateOceanLights();
            }
        }

        /// <summary>
        /// Get cold-spectrum color for ocean bioluminescence.
        /// Depth determines hue:
        /// - Surface: bright blue
        /// - Twilight: darker blue
        /// - Abyss: exotic green/purple
        /// </summary>
        protected override Color GetBiolumColor()
        {
            if (_depthRatio < 0.33f)
            {
                // Surface transition
                return Color.Lerp(_surfaceBlue, _twilightBlue, _depthRatio * 3f);
            }
            else if (_depthRatio < 0.66f)
            {
                // Twilight zone
                return Color.Lerp(_twilightBlue, _biolumGreen, (_depthRatio - 0.33f) * 3f);
            }
            else
            {
                // Abyss (exotic colors)
                return Color.Lerp(_biolumGreen, _biolumPurple, (_depthRatio - 0.66f) * 3f);
            }
        }

        /// <summary>
        /// Ocean light intensity (scattered, weaker than caves).
        /// Scaled by mood and depth (deeper = dimmer normally, but mood can brighten).
        /// </summary>
        protected override float GetBiolumIntensity()
        {
            float baseIntensity = _intensityMultiplier * 0.7f; // Ocean lights are dimmer
            float depthScale = Mathf.Lerp(1.2f, 0.6f, _depthRatio); // Deeper = dimmer
            return ScaleIntensityByMood(baseIntensity) * depthScale;
        }

        /// <summary>
        /// Ocean light range (varies by depth).
        /// Hazard also scales range down.
        /// </summary>
        protected override float GetBiolumRange()
        {
            float baseRange = _rangeMultiplier * 0.8f;
            float depthScale = Mathf.Lerp(1f, 0.5f, _depthRatio); // Deeper = shorter range (water absorption)
            return ScaleRangeByHazard(baseRange) * depthScale;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // PRIVATE HELPERS: Ocean-Specific
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Generate scattered light positions (one-time setup).
        /// Uses Perlin noise for natural-looking variance.
        /// </summary>
        private void GenerateLightPositions()
        {
            for (int i = 0; i < _lightCount; i++)
            {
                float angle = (i / (float)_lightCount) * 360f;
                float distance = _scatterRadius * (0.5f + 0.5f * Random.value);
                float height = Random.Range(-_scatterRadius * 0.5f, _scatterRadius * 0.5f);

                if (_useNoiseVariation)
                {
                    float noise = Mathf.PerlinNoise(transform.position.x * 0.1f + i, Time.time * 0.01f);
                    distance *= (0.8f + noise * 0.4f);
                    height *= (0.8f + noise * 0.4f);
                }

                Vector3 offset = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * distance,
                    height,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * distance
                );

                _lightPositions[i] = offset;
            }
        }

        /// <summary>
        /// Create initial scattered ocean lights.
        /// </summary>
        private void CreateOceanLights()
        {
            Color baseColor = GetBiolumColor();
            float baseIntensity = GetBiolumIntensity();
            float baseRange = GetBiolumRange();

            for (int i = 0; i < _lightCount && i < _maxLights; i++)
            {
                Vector3 position = transform.position + _lightPositions[i];

                // Vary color slightly (natural variance)
                Color variedColor = Color.Lerp(baseColor, _biolumGreen, Random.value * 0.2f);
                if (_depthRatio > 0.66f) // Abyss can have purple tints
                    variedColor = Color.Lerp(variedColor, _biolumPurple, Random.value * 0.3f);

                GetOrCreateLight(
                    position,
                    variedColor,
                    baseRange * (0.7f + Random.value * 0.5f), // Vary range
                    baseIntensity * (0.6f + Random.value * 0.7f)  // Vary intensity
                );
            }
        }

        /// <summary>
        /// Update existing ocean lights (maintain scattered appearance).
        /// </summary>
        private void UpdateOceanLights()
        {
            Color baseColor = GetBiolumColor();
            float baseIntensity = GetBiolumIntensity();
            float baseRange = GetBiolumRange();

            for (int i = 0; i < _activeLightCount; i++)
            {
                Light light = _activeLights[i];
                if (light == null) continue;

                // Update position with slight drift (organic motion)
                if (_useNoiseVariation)
                {
                    float noiseX = Mathf.PerlinNoise(Time.time * 0.2f + i, 0f) - 0.5f;
                    float noiseY = Mathf.PerlinNoise(Time.time * 0.15f, i) - 0.5f;
                    float noiseZ = Mathf.PerlinNoise(Time.time * 0.1f + i * 2, Time.time * 0.1f) - 0.5f;
                    Vector3 drift = new Vector3(noiseX, noiseY, noiseZ) * 0.5f;
                    light.transform.position = transform.position + _lightPositions[i] + drift;
                }

                // Update properties
                Color variedColor = Color.Lerp(baseColor, _biolumGreen, Mathf.Sin(Time.time * 0.5f + i) * 0.15f);
                UpdateLight(
                    light,
                    variedColor,
                    baseRange * (0.8f + Mathf.Sin(Time.time * 0.3f + i) * 0.2f),
                    baseIntensity * (0.8f + Mathf.Cos(Time.time * 0.4f + i) * 0.2f)
                );
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // EDITOR
        // ─────────────────────────────────────────────────────────────────────────────

        #if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            // Draw ocean zone boundary
            Color oceanColor = GetBiolumColor();
            Gizmos.color = new Color(oceanColor.r, oceanColor.g, oceanColor.b, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _scatterRadius);

            // Draw light positions
            if (_lightPositions != null)
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < _lightPositions.Length && i < _lightCount; i++)
                {
                    Vector3 lightPos = transform.position + _lightPositions[i];
                    Gizmos.DrawWireSphere(lightPos, 0.5f);
                }
            }
        }

        private void OnValidate()
        {
            _maxLights = Mathf.Max(_maxLights, _lightCount);
        }
        #endif
    }
}
