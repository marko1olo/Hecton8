// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  HADES HECTON-8 | FloorBiolumZone                                           ║
// ║  Sea floor bioluminescence (clustered, dense, ecosystem-like)               ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using UnityEngine;
using Unity.Mathematics;
using Hecton8.Core;

namespace Hecton8.Biolum
{
    /// <summary>
    /// Bioluminescence for sea floor environments (corals, fungi, clusters).
    /// Dense, clustered lights with warm/exotic colors.
    /// Represents localized ecosystems (coral gardens, chemosynthetic vents, etc).
    /// </summary>
    [DisallowMultipleComponent]
    public class FloorBiolumZone : HectonBiolumZone
    {
        // ─────────────────────────────────────────────────────────────────────────────
        // INSPECTOR SETTINGS
        // ─────────────────────────────────────────────────────────────────────────────

        [Header("── Floor-Specific Settings ──────")]
        [SerializeField, Tooltip("Floor cluster type (Coral/Fungi/Vent/Garden)")]
        protected FloorClusterType _clusterType = FloorClusterType.Garden;

        [SerializeField, Range(1, 8), Tooltip("Number of light clusters")]
        protected int _clusterCount = 3;

        [SerializeField, Range(1f, 10f), Tooltip("Cluster size (radius)")]
        protected float _clusterSize = 3f;

        [SerializeField, Range(0f, 1f), Tooltip("Pulse intensity (0=steady, 1=breathing)")]
        protected float _pulseIntensity = 0.3f;

        [SerializeField, Range(0.1f, 2f), Tooltip("Pulse frequency (Hz)")]
        protected float _pulseFrequency = 0.5f;

        // ─────────────────────────────────────────────────────────────────────────────
        // PRIVATE STATE
        // ─────────────────────────────────────────────────────────────────────────────

        // COLD ALLOC: Vector3[_maxLights] - floor biolum cluster centers - owner: FloorBiolumZone
        private Vector3[] _clusterCenters;
        // COLD ALLOC: int[_maxLights] - floor biolum lights per cluster - owner: FloorBiolumZone
        private int[] _lightsPerCluster;
        private float _pulsePhase = 0f;

        // Floor color palettes
        private Color _coralRed = new Color(1f, 0.3f, 0.2f);        // Coral: warm red
        private Color _coralOrange = new Color(1f, 0.6f, 0.2f);     // Coral: orange
        private Color _fungiGreen = new Color(0.3f, 1f, 0.5f);      // Fungi: biolum green
        private Color _ventRed = new Color(1f, 0.2f, 0.1f);         // Vent: hot red
        private Color _ventOrange = new Color(1f, 0.4f, 0.1f);      // Vent: orange glow
        private Color _gardenCyan = new Color(0.2f, 1f, 0.8f);      // Garden: cyan

        // ─────────────────────────────────────────────────────────────────────────────
        // LIFECYCLE
        // ─────────────────────────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            _clusterCount = Mathf.Clamp(_clusterCount, 1, _maxLights);
            _clusterSize = SanitizeNonNegative(_clusterSize, 3f);
            _clusterCenters = new Vector3[_maxLights]; // COLD ALLOC: Vector3[_maxLights] - floor biolum cluster centers - owner: FloorBiolumZone
            _lightsPerCluster = new int[_maxLights]; // COLD ALLOC: int[_maxLights] - floor biolum lights per cluster - owner: FloorBiolumZone
            GenerateClusterCenters();
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // IMPLEMENTATION: Floor-Specific Logic
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluate floor zone lighting (clustered).
        /// </summary>
        protected override void EvaluateBiolumState()
        {
            float safePulseFrequency = math.min(SanitizeNonNegative(_pulseFrequency, 0.5f), 2f);
            _pulsePhase = BiolumTickTime * safePulseFrequency;

            if (_activeLightCount == 0)
            {
                CreateFloorLights();
            }
            else
            {
                UpdateFloorLights();
            }
        }

        /// <summary>
        /// Get color based on cluster type.
        /// Coral: warm red/orange
        /// Fungi: biolum green
        /// Vent: hot red/orange
        /// Garden: cyan/green mix
        /// </summary>
        protected override Color GetBiolumColor()
        {
            Color color = _clusterType switch
            {
                FloorClusterType.Coral => Color.Lerp(_coralRed, _coralOrange, Cheap01Wave(_pulsePhase)),
                FloorClusterType.Fungi => _fungiGreen,
                FloorClusterType.Vent => Color.Lerp(_ventRed, _ventOrange, Cheap01Wave(_pulsePhase)),
                FloorClusterType.Garden => Color.Lerp(_gardenCyan, _fungiGreen, Cheap01Wave(_pulsePhase * 0.5f)),
                _ => Color.white
            };
            return SanitizeBiolumColor(color);
        }

        /// <summary>
        /// Floor zone intensity (dense, concentrated).
        /// Pulses based on phase.
        /// </summary>
        protected override float GetBiolumIntensity()
        {
            float baseIntensity = _intensityMultiplier * 1.2f; // Floor clusters are bright
            float safePulseIntensity = Sanitize01(_pulseIntensity, 0.3f);
            float pulse = math.lerp(1f - safePulseIntensity, 1f + safePulseIntensity, Cheap01Wave(_pulsePhase));
            return ScaleIntensityByMood(baseIntensity) * pulse;
        }

        /// <summary>
        /// Floor zone range (localized, short range).
        /// </summary>
        protected override float GetBiolumRange()
        {
            float baseRange = _rangeMultiplier * 0.6f; // Tighter range for floor clusters
            return ScaleRangeByHazard(baseRange);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // PRIVATE HELPERS: Floor-Specific
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Generate cluster positions on/near floor.
        /// </summary>
        private void GenerateClusterCenters()
        {
            int safeClusterCount = Mathf.Clamp(_clusterCount, 1, _maxLights);
            float safeClusterSize = SanitizeNonNegative(_clusterSize, 3f);
            for (int i = 0; i < safeClusterCount; i++)
            {
                float angle = (i / (float)safeClusterCount) * 360f;
                float distance = safeClusterSize * 1.5f;
                Vector3 offset = new Vector3(
                    CinematicMath.FastCos(angle * Mathf.Deg2Rad) * distance,
                    -1f, // On floor
                    CinematicMath.FastSin(angle * Mathf.Deg2Rad) * distance
                );
                _clusterCenters[i] = offset;
                _lightsPerCluster[i] = 2 + i % 2; // 2-3 lights per cluster
            }
        }

        /// <summary>
        /// Create floor cluster lights (2-3 per cluster, tight grouping).
        /// </summary>
        private void CreateFloorLights()
        {
            Color baseColor = GetBiolumColor();
            float baseIntensity = GetBiolumIntensity();
            float baseRange = GetBiolumRange();

            int safeClusterCount = Mathf.Clamp(_clusterCount, 1, _maxLights);
            float safeClusterSize = SanitizeNonNegative(_clusterSize, 3f);
            for (int cluster = 0; cluster < safeClusterCount && _activeLightCount < _maxLights; cluster++)
            {
                Vector3 clusterCenter = transform.position + _clusterCenters[cluster];
                int lightsInCluster = _lightsPerCluster[cluster];

                for (int light = 0; light < lightsInCluster && _activeLightCount < _maxLights; light++)
                {
                    // Scatter lights within cluster
                    Vector3 scatter = DeterministicScatter(cluster, light, safeClusterSize * 0.3f);
                    Vector3 lightPos = clusterCenter + scatter;

                    // Slight color variation within cluster
                    Color variedColor = Color.Lerp(baseColor, GetClusterVariantColor(), Hash01(cluster * 37 + light * 13 + 5) * 0.2f);

                    GetOrCreateLight(
                        lightPos,
                        variedColor,
                        baseRange,
                        baseIntensity * (0.8f + Hash01(cluster * 43 + light * 17 + 11) * 0.4f)
                    );
                }
            }
        }

        /// <summary>
        /// Update floor lights (pulse effect, slight movement).
        /// </summary>
        private void UpdateFloorLights()
        {
            Color baseColor = GetBiolumColor();
            float baseIntensity = GetBiolumIntensity();
            float baseRange = GetBiolumRange();
            float time = BiolumTickTime;
            int safeClusterCount = Mathf.Clamp(_clusterCount, 1, _maxLights);

            for (int i = 0; i < _activeLightCount; i++)
            {
                Light light = _activeLights[i];
                if (light == null) continue;

                // Slight position drift (organic movement)
                float drift = CheapSignedWave((time * 0.2f) + Hash01((i * 23) + 7));
                int clusterIdx = i / 3; // Rough cluster assignment
                if (clusterIdx < safeClusterCount)
                {
                    Vector3 newPos = transform.position + _clusterCenters[clusterIdx];
                    newPos += new Vector3(drift, drift * 0.5f, drift) * 0.3f;
                    UpdateLightPosition(light, newPos);
                }

                UpdateLight(
                    light,
                    baseColor,
                    baseRange,
                    baseIntensity
                );
            }
        }

        /// <summary>
        /// Get a color variant for cluster type.
        /// </summary>
        private Color GetClusterVariantColor()
        {
            return _clusterType switch
            {
                FloorClusterType.Coral => _coralOrange,
                FloorClusterType.Fungi => _fungiGreen,
                FloorClusterType.Vent => _ventOrange,
                FloorClusterType.Garden => _gardenCyan,
                _ => Color.white
            };
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // EDITOR
        // ─────────────────────────────────────────────────────────────────────────────

        private static Vector3 DeterministicScatter(int clusterIndex, int lightIndex, float radius)
        {
            int seed = clusterIndex * 131 + lightIndex * 47 + 17;
            return new Vector3(
                ((Hash01(seed) * 2f) - 1f) * radius,
                ((Hash01(seed + 19) * 2f) - 1f) * radius * 0.35f,
                ((Hash01(seed + 41) * 2f) - 1f) * radius);
        }

        private static float Hash01(int seed)
        {
            uint state = unchecked((uint)seed * 747796405u);
            state = unchecked((state ^ (state >> 16)) * 2246822519u);
            state = unchecked((state ^ (state >> 13)) * 3266489917u);
            state ^= state >> 16;
            return (state & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private static float CheapSignedWave(float phase)
        {
            float wrapped = math.frac(phase);
            float triangle = wrapped < 0.5f ? wrapped * 2f : (1f - wrapped) * 2f;
            return triangle - 0.5f;
        }

        private static float Cheap01Wave(float phase)
        {
            return CheapSignedWave(phase) + 0.5f;
        }

        #if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            if (_clusterCenters == null || _clusterCenters.Length == 0 || _clusterCount <= 0)
                return;

            int clusterCount = Mathf.Min(_clusterCount, _clusterCenters.Length);

            // Draw cluster centers
            Gizmos.color = GetBiolumColor();
            for (int i = 0; i < clusterCount; i++)
            {
                Vector3 center = transform.position + _clusterCenters[i];
                Gizmos.DrawWireSphere(center, _clusterSize);
            }

            // Draw floor reference
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position + Vector3.left * 5f, transform.position + Vector3.right * 5f);
        }
        #endif
    }

    /// <summary>
    /// Floor cluster types (for visual variety).
    /// </summary>
    public enum FloorClusterType
    {
        Coral,   // Warm red/orange (reef-like)
        Fungi,   // Green-cyan (alien life)
        Vent,    // Hot red/orange (chemosynthetic)
        Garden   // Mixed cyan/green (ecosystem)
    }
}
