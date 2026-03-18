// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  ScannerTool.cs — Project HECTON-8 Hydroacoustic Scanner                   ║
// ║  Unity 6 (URP) | Shapes 4.x | Zero GC                                    ║
// ║  v1.1 — Fixed: composition instead of multiple inheritance                 ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using Hecton8.Audio;
using Hecton8.Scavenging;
using Shapes;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ScannerTool : PlayerTool
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("═══ SCAN PARAMETERS ═══")]

        [Tooltip("Scan radius in meters. Upgradeable in future.")]
        [SerializeField] private float scanRadius = 50f;

        [Tooltip("Cooldown between scans in seconds.")]
        [SerializeField] private float scanCooldown = 3f;

        [Tooltip("Physics layer mask for scannable objects.\n" +
                 "Should include 'Mineable' layer at minimum.")]
        [SerializeField] private LayerMask scanLayerMask = ~0;

        [Header("═══ PULSE VISUAL ═══")]

        [Tooltip("Duration of the expanding ring animation (seconds).")]
        [SerializeField] private float pulseDuration = 1.5f;

        [Tooltip("Ring color at start of pulse.")]
        [SerializeField] private Color pulseColor = new Color(0f, 0.9f, 1f, 0.8f);

        [Tooltip("Ring thickness in world units.")]
        [SerializeField] private float pulseThickness = 0.15f;

        [Header("═══ AUDIO ═══")]

        [Tooltip("Sonar ping sound. Low-frequency, submarine-style.")]
        [SerializeField] private AudioClip pingClip;

        [Tooltip("Volume of the sonar ping (0-1).")]
        [Range(0f, 1f)]
        [SerializeField] private float pingVolume = 0.7f;

        [Tooltip("Sound when scan is on cooldown.")]
        [SerializeField] private AudioClip cooldownClip;

        // ══════════════════════════════════════════════════════════
        //  STATIC BUFFER — Zero GC OverlapSphere
        // ══════════════════════════════════════════════════════════

        private static readonly Collider[] s_HitBuffer = new Collider[64];

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private float _lastScanTime = -999f;
        private Transform _cachedTransform;

        // ── Pulse state (read by ScannerPulseDrawer) ──
        internal bool  PulseActive    { get; private set; }
        internal float3 PulseOrigin   { get; private set; }
        internal float PulseStartTime { get; private set; }

        // ── Config accessors (read by ScannerPulseDrawer) ──
        internal float PulseDuration   => pulseDuration;
        internal float ScanRadius      => scanRadius;
        internal Color PulseColor      => pulseColor;
        internal float PulseThickness  => pulseThickness;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _cachedTransform = transform;

            // Auto-create the Shapes drawer component on this GameObject
            if (GetComponent<ScannerPulseDrawer>() == null)
            {
                var drawer = gameObject.AddComponent<ScannerPulseDrawer>();
                drawer.Init(this);
            }
        }

        public override void OnEquip()
        {
            base.OnEquip();
            PulseActive = false;
        }

        public override void OnUnequip()
        {
            base.OnUnequip();
            PulseActive = false;
        }

        // ══════════════════════════════════════════════════════════
        //  TOOL USAGE
        // ══════════════════════════════════════════════════════════

        public override void UsePrimary(float deltaTime)
        {
            if (!IsEquipped) return;

            float now = Time.time;

            if (now - _lastScanTime < scanCooldown)
                return;

            _lastScanTime = now;

            float3 origin = _cachedTransform.position;
            PerformScan(origin);

            PulseActive    = true;
            PulseOrigin    = origin;
            PulseStartTime = now;

            if (pingClip != null && SpatialAudioManager.Instance != null)
                SpatialAudioManager.Instance.PlayStatic2D(pingClip, pingVolume);

            ScanEvents.OnScanTriggered?.Invoke(origin, scanRadius);
        }

        public override void ToolTick(float deltaTime)
        {
            if (PulseActive)
            {
                float elapsed = Time.time - PulseStartTime;
                if (elapsed > pulseDuration)
                    PulseActive = false;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  SCAN LOGIC — Zero GC
        // ══════════════════════════════════════════════════════════

        private void PerformScan(float3 origin)
        {
            int hitCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                origin, scanRadius, s_HitBuffer, scanLayerMask,
                QueryTriggerInteraction.Collide);

            if (hitCount > s_HitBuffer.Length)
                hitCount = s_HitBuffer.Length;

            int foundCount = 0;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = s_HitBuffer[i];
                if (col == null) continue;

                if (col.TryGetComponent(out ResourceNode node))
                {
                    if (node.IsDepleted) continue;

                    float3 nodePos = col.transform.position;
                    ScanEvents.OnNodeFound?.Invoke(nodePos);
                    foundCount++;
                }

                s_HitBuffer[i] = null;
            }

#if UNITY_EDITOR
            Debug.Log($"[Scanner] Pulse at {origin}: {foundCount} nodes found " +
                      $"({hitCount} colliders checked, radius {scanRadius}m)");
#endif
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  SHAPES DRAWER — separate component on same GameObject
    //  Inherits ImmediateModeShapeDrawer (Shapes 4.x class).
    //  Reads pulse state from ScannerTool via internal properties.
    //  Zero GC: immediate mode drawing, no allocations.
    // ══════════════════════════════════════════════════════════════

    [DisallowMultipleComponent]
    public sealed class ScannerPulseDrawer : ImmediateModeShapeDrawer
    {
        private ScannerTool _scanner;

        /// <summary>
        /// Called by ScannerTool.Awake() after AddComponent.
        /// </summary>
        internal void Init(ScannerTool scanner)
        {
            _scanner = scanner;
        }

        private void Awake()
        {
            // If Init wasn't called (e.g. component already existed on prefab),
            // find ScannerTool on same GameObject.
            if (_scanner == null)
                _scanner = GetComponent<ScannerTool>();
        }

        public override void DrawShapes(Camera cam)
        {
            if (_scanner == null) return;
            if (!_scanner.PulseActive) return;
            if (!_scanner.IsEquipped) return;

            float elapsed = Time.time - _scanner.PulseStartTime;
            float t = math.saturate(elapsed / _scanner.PulseDuration);

            float currentRadius = math.lerp(0f, _scanner.ScanRadius, t);

            Color baseColor = _scanner.PulseColor;
            float alpha = baseColor.a * (1f - t * t);

            if (alpha < 0.01f) return;

            Color ringColor = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

            float baseThickness = _scanner.PulseThickness;
            float thickness = math.lerp(baseThickness, baseThickness * 0.3f, t);

            using (Draw.Command(cam))
            {
                Draw.Ring(
                    (Vector3)_scanner.PulseOrigin,
                    Quaternion.Euler(90f, 0f, 0f),
                    currentRadius,
                    thickness,
                    ringColor
                );

                if (t < 0.8f)
                {
                    float innerRadius = currentRadius * 0.85f;
                    float innerAlpha = alpha * 0.3f;
                    Color innerColor = new Color(
                        baseColor.r, baseColor.g, baseColor.b, innerAlpha);

                    Draw.Ring(
                        (Vector3)_scanner.PulseOrigin,
                        Quaternion.Euler(90f, 0f, 0f),
                        innerRadius,
                        thickness * 0.5f,
                        innerColor
                    );
                }
            }
        }
    }
}