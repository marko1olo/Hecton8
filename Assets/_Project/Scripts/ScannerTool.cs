// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  ScannerTool.cs — Project HECTON-8 Hydroacoustic Scanner                   ║
// ║  Unity 6 (URP) | Shapes 4.x | Zero GC                                    ║
// ║  v1.0 — Sonar pulse with world-space ring visualization                    ║
// ║                                                                             ║
// ║  GAMEPLAY:                                                                  ║
// ║  ─────────                                                                  ║
// ║  Primary fire (LKM): Emits a sonar ping. Expanding spherical wavefront    ║
// ║  detects all ResourceNodes within radius. Results are broadcast via        ║
// ║  ScanEvents for HUD marker display. Cooldown prevents spam.               ║
// ║                                                                             ║
// ║  NASA-PUNK FEEL:                                                            ║
// ║  ───────────────                                                            ║
// ║  • Low-frequency sonar ping via SpatialAudioManager (2D, in-helmet).       ║
// ║  • Cyan expanding ring in world space via Shapes ImmediateModeShapeDrawer. ║
// ║  • Ring fades out as it expands — like a real sonar pulse dissipating.     ║
// ║                                                                             ║
// ║  ZERO GC:                                                                   ║
// ║  ─────────                                                                  ║
// ║  • OverlapSphereNonAlloc with static Collider[64] buffer.                  ║
// ║  • No List, no LINQ, no string operations in hot path.                     ║
// ║  • Shapes Draw.Ring — immediate mode, zero allocation.                     ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using Hecton8.Audio;
using Hecton8.Scavenging;
using Shapes;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ScannerTool : PlayerTool, ImmediateModeShapeDrawer
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

        /// <summary>
        /// Pre-allocated collider buffer for Physics.OverlapSphereNonAlloc.
        /// 64 entries = max 64 scannable objects per pulse.
        /// Static: shared across all ScannerTool instances (only one active at a time).
        /// </summary>
        private static readonly Collider[] s_HitBuffer = new Collider[64];

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>Time.time when last scan completed. Used for cooldown.</summary>
        private float _lastScanTime = -999f;

        /// <summary>Is a pulse animation currently playing.</summary>
        private bool _pulseActive;

        /// <summary>World-space origin of the current pulse.</summary>
        private float3 _pulseOrigin;

        /// <summary>Time.time when current pulse started.</summary>
        private float _pulseStartTime;

        /// <summary>Cached transform for world position.</summary>
        private Transform _cachedTransform;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _cachedTransform = transform;
        }

        public override void OnEquip()
        {
            base.OnEquip();
            _pulseActive = false;
        }

        public override void OnUnequip()
        {
            base.OnUnequip();
            _pulseActive = false;
        }

        // ══════════════════════════════════════════════════════════
        //  TOOL USAGE — Called by PlayerToolManager every frame
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Primary fire: Emit sonar pulse.
        /// Called every frame while Fire1 is held — we gate with cooldown.
        /// </summary>
        public override void UsePrimary(float deltaTime)
        {
            if (!IsEquipped) return;

            float now = Time.time;

            // ── Cooldown check ──
            if (now - _lastScanTime < scanCooldown)
            {
                // Optional: play "not ready" sound on first frame of press
                // (PlayerToolManager calls this every frame while held)
                return;
            }

            _lastScanTime = now;

            // ── Execute scan ──
            float3 origin = _cachedTransform.position;
            PerformScan(origin);

            // ── Start pulse visual ──
            _pulseActive = true;
            _pulseOrigin = origin;
            _pulseStartTime = now;

            // ── Audio ──
            if (pingClip != null && SpatialAudioManager.Instance != null)
            {
                SpatialAudioManager.Instance.PlayStatic2D(pingClip, pingVolume);
            }

            // ── Broadcast scan trigger ──
            ScanEvents.OnScanTriggered?.Invoke(origin, scanRadius);
        }

        /// <summary>
        /// Update pulse animation. Called every frame by PlayerToolManager.
        /// </summary>
        public override void ToolTick(float deltaTime)
        {
            // Auto-deactivate pulse after duration
            if (_pulseActive)
            {
                float elapsed = Time.time - _pulseStartTime;
                if (elapsed > pulseDuration)
                {
                    _pulseActive = false;
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  SCAN LOGIC — Zero GC
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Performs physics overlap and broadcasts found nodes.
        ///
        /// Zero GC:
        /// - OverlapSphereNonAlloc uses static s_HitBuffer
        /// - No List, no LINQ
        /// - TryGetComponent is non-allocating
        /// - ScanEvents.OnNodeFound passes float3 (struct)
        /// </summary>
        private void PerformScan(float3 origin)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                origin, scanRadius, s_HitBuffer, scanLayerMask,
                QueryTriggerInteraction.Collide);

            // Clamp to buffer size
            if (hitCount > s_HitBuffer.Length)
                hitCount = s_HitBuffer.Length;

            int foundCount = 0;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = s_HitBuffer[i];
                if (col == null) continue;

                // ── Check for ResourceNode ──
                // TryGetComponent checks the same GameObject — zero GC
                if (col.TryGetComponent(out ResourceNode node))
                {
                    if (node.IsDepleted) continue;

                    float3 nodePos = col.transform.position;
                    ScanEvents.OnNodeFound?.Invoke(nodePos);
                    foundCount++;
                }

                // Clear buffer slot to prevent stale references
                s_HitBuffer[i] = null;
            }

#if UNITY_EDITOR
            Debug.Log($"[Scanner] Pulse at {origin}: {foundCount} nodes found " +
                      $"({hitCount} colliders checked, radius {scanRadius}m)");
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  SHAPES VISUALIZATION — World-space sonar ring
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// ImmediateModeShapeDrawer callback. Called by Shapes for each camera.
        /// Draws expanding ring in world space — visible from all cameras.
        /// </summary>
        public override void DrawShapes(Camera cam)
        {
            if (!_pulseActive) return;
            if (!IsEquipped) return;

            float elapsed = Time.time - _pulseStartTime;
            float t = math.saturate(elapsed / pulseDuration);

            // ── Ring radius: 0 → scanRadius over duration ──
            float currentRadius = math.lerp(0f, scanRadius, t);

            // ── Alpha: full → 0 with easing ──
            float alpha = pulseColor.a * (1f - t * t); // Quadratic fade

            if (alpha < 0.01f) return;

            Color ringColor = new Color(pulseColor.r, pulseColor.g, pulseColor.b, alpha);

            // ── Thickness: starts thick, thins as it expands ──
            float thickness = math.lerp(pulseThickness, pulseThickness * 0.3f, t);

            // ── Draw ring in world space (horizontal plane at scan origin Y) ──
            using (Draw.Command(cam))
            {
                Draw.Ring(
                    (Vector3)_pulseOrigin,
                    Quaternion.Euler(90f, 0f, 0f), // Horizontal ring
                    currentRadius,
                    thickness,
                    ringColor
                );

                // Second thinner ring slightly behind for depth
                if (t < 0.8f)
                {
                    float innerRadius = currentRadius * 0.85f;
                    float innerAlpha = alpha * 0.3f;
                    Color innerColor = new Color(
                        pulseColor.r, pulseColor.g, pulseColor.b, innerAlpha);

                    Draw.Ring(
                        (Vector3)_pulseOrigin,
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