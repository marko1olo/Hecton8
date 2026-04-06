// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  HADES HECTON-8 | FloorBiolumZone                                           ║
// ║  Sea floor bioluminescence (clustered, dense, ecosystem-like)               ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using UnityEngine;

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

        private Vector3[] _clusterCenters;   // COLD ALLOC: cluster positions
        private int[] _lightsPerCluster;     // COLD ALLOC: how many lights per cluster
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
            _clusterCenters = new Vector3[_maxLights]; // COLD ALLOC
            _lightsPerCluster = new int[_maxLights];   // COLD ALLOC
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
            _pulsePhase = Time.time * _pulseFrequency;

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
            return _clusterType switch
            {
                FloorClusterType.Coral => Color.Lerp(_coralRed, _coralOrange, Mathf.Sin(_pulsePhase) * 0.5f + 0.5f),
                FloorClusterType.Fungi => _fungiGreen,
                FloorClusterType.Vent => Color.Lerp(_ventRed, _ventOrange, Mathf.Sin(_pulsePhase) * 0.5f + 0.5f),
                FloorClusterType.Garden => Color.Lerp(_gardenCyan, _fungiGreen, Mathf.Sin(_pulsePhase * 0.5f) * 0.5f + 0.5f),
                _ => Color.white
            };
        }

        /// <summary>
        /// Floor zone intensity (dense, concentrated).
        /// Pulses based on phase.
        /// </summary>
        protected override float GetBiolumIntensity()
        {
            float baseIntensity = _intensityMultiplier * 1.2f; // Floor clusters are bright
            float pulse = Mathf.Lerp(1f - _pulseIntensity, 1f + _pulseIntensity, Mathf.Sin(_pulsePhase) * 0.5f + 0.5f);
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
            for (int i = 0; i < _clusterCount && i < _maxLights; i++)
            {
                float angle = (i / (float)_clusterCount) * 360f;
                float distance = _clusterSize * 1.5f;
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * distance,
                    -1f, // On floor
                    Mathf.Sin(angle * Mathf.Deg2Rad) * distance
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

            for (int cluster = 0; cluster < _clusterCount && _activeLightCount < _maxLights; cluster++)
            {
                Vector3 clusterCenter = transform.position + _clusterCenters[cluster];
                int lightsInCluster = _lightsPerCluster[cluster];

                for (int light = 0; light < lightsInCluster && _activeLightCount < _maxLights; light++)
                {
                    // Scatter lights within cluster
                    Vector3 scatter = Random.insideUnitSphere * (_clusterSize * 0.3f);
                    Vector3 lightPos = clusterCenter + scatter;

                    // Slight color variation within cluster
                    Color variedColor = Color.Lerp(baseColor, GetClusterVariantColor(), Random.value * 0.2f);

                    GetOrCreateLight(
                        lightPos,
                        variedColor,
                        baseRange,
                        baseIntensity * (0.8f + Random.value * 0.4f)
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

            for (int i = 0; i < _activeLightCount; i++)
            {
                Light light = _activeLights[i];
                if (light == null) continue;

                // Slight position drift (organic movement)
                float drift = Mathf.PerlinNoise(Time.time * 0.2f + i, i) - 0.5f;
                int clusterIdx = i / 3; // Rough cluster assignment
                if (clusterIdx < _clusterCount)
                {
                    Vector3 newPos = transform.position + _clusterCenters[clusterIdx];
                    newPos += new Vector3(drift, drift * 0.5f, drift) * 0.3f;
                    light.transform.position = newPos;
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

        #if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            // Draw cluster centers
            Gizmos.color = GetBiolumColor();
            for (int i = 0; i < _clusterCount; i++)
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
