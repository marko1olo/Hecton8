// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  HectonScanMarkerSystem.cs — Project HECTON-8 Scan HUD Markers            ║
// ║  Unity 6 (URP) | Shapes 4.x | Zero GC                                    ║
// ║  v1.0 — NASA-Punk scan markers with edge clamping and distance readout    ║
// ║                                                                             ║
// ║  PURPOSE:                                                                   ║
// ║  ─────────                                                                  ║
// ║  Renders diamond-shaped HUD markers at detected resource positions.        ║
// ║  Markers are visible through walls and murky water — critical for          ║
// ║  Abyss-depth cave navigation.                                              ║
// ║                                                                             ║
// ║  ARCHITECTURE:                                                              ║
// ║  ─────────────                                                              ║
// ║  • Subscribes to ScanEvents.OnNodeFound (decoupled from ScannerTool).     ║
// ║  • Pre-allocated ActiveMarker[64] array — zero GC on scan.                ║
// ║  • Draws in HUD_Render_Camera only (overlay on helmet visor).             ║
// ║  • ImmediateModeShapeDrawer: Shapes renders diamonds + distance text.     ║
// ║  • ITickable: timer countdown via GameTickManager.                         ║
// ║                                                                             ║
// ║  NASA-PUNK FEEL:                                                            ║
// ║  ───────────────                                                            ║
// ║  • Cyan diamond markers with scan-line flicker (sin-based alpha noise).    ║
// ║  • Distance in meters below each marker (IntStrings zero-GC cache).       ║
// ║  • Fade-out in last second of marker lifetime.                             ║
// ║  • Off-screen markers clamp to viewport edges with directional indicator.  ║
// ║                                                                             ║
// ║  ZERO GC:                                                                   ║
// ║  ─────────                                                                  ║
// ║  • No List.Add/Remove — index cycling over fixed array.                   ║
// ║  • Distance text from HudNumericStringCache.IntStrings (pre-allocated).   ║
// ║  • All math via Unity.Mathematics (float3, math.sin, etc.)                ║
// ║  • Shapes immediate mode — no GameObjects, no materials allocated.         ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.UI;
using NASAPunk.Visor;
using Shapes;
using Unity.Mathematics;
using UnityEngine;
using TMPro;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class HectonScanMarkerSystem : ImmediateModeShapeDrawer, ITickable, IUpdatable
    {
        private static readonly List<VisorHUDController> s_controllerResolveBuffer = new List<VisorHUDController>(2);
        // ══════════════════════════════════════════════════════════
        //  MARKER DATA — Pre-allocated, Zero GC
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Single HUD marker. Struct — no heap allocation.
        /// Stored in fixed-size array, cycled via write index.
        /// </summary>
        private struct ActiveMarker
        {
            /// <summary>World-space position of the detected resource.</summary>
            public Unity.Mathematics.float3 worldPos;

            /// <summary>Remaining display time in seconds. ≤ 0 = inactive.</summary>
            public float timer;

            /// <summary>Is this slot occupied by an active marker.</summary>
            public bool active;
        }

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("═══ HUD CAMERA ═══")]

        [Tooltip("Reference to HUD_Render_Camera.\n" +
                 "Markers are drawn ONLY in this camera.\n" +
                 "Assign in Inspector or auto-found by tag.")]
        [SerializeField] private Camera hudCamera;

        [Header("═══ MARKER APPEARANCE ═══")]

        [Tooltip("Base color of scan markers.")]
        [SerializeField] private Color markerColor = new Color(0f, 0.9f, 1f, 0.9f);

        [Tooltip("Base size of marker diamond in pixels (at 1m distance).")]
        [SerializeField] private float markerBaseSize = 24f;

        [Tooltip("Minimum marker size in pixels (at max distance).")]
        [SerializeField] private float markerMinSize = 8f;

        [Tooltip("Maximum marker size in pixels (at point-blank).")]
        [SerializeField] private float markerMaxSize = 40f;

        [Tooltip("Duration markers stay visible after scan (seconds).")]
        [SerializeField] private float markerLifetime = 5f;

        [Tooltip("Font for distance readout.")]
        [SerializeField] private TMP_FontAsset distanceFont;

        [Tooltip("Font size for distance text.")]
        [SerializeField] private float distanceFontSize = 12f;

        [Header("═══ EDGE CLAMPING ═══")]

        [Tooltip("Pixel margin from screen edges for clamped markers.")]
        [SerializeField] private float edgeMargin = 40f;

        [Header("═══ NASA-PUNK FLICKER ═══")]

        [Tooltip("Flicker frequency (Hz). Higher = faster scan-line effect.")]
        [SerializeField] private float flickerFrequency = 25f;

        [Tooltip("Flicker intensity (0-1). 0 = no flicker.")]
        [Range(0f, 0.4f)]
        [SerializeField] private float flickerIntensity = 0.15f;

        // ══════════════════════════════════════════════════════════
        //  CONSTANTS
        // ══════════════════════════════════════════════════════════

        /// <summary>Maximum simultaneous markers. Fixed array size.</summary>
        const int MAX_MARKERS = 64;

        /// <summary>Fade-out begins this many seconds before marker expires.</summary>
        const float FADE_DURATION = 1.0f;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>Fixed-size marker array. Never resized.</summary>
        private ActiveMarker[] _markers;

        /// <summary>Write index for next marker. Cycles 0..MAX_MARKERS-1.</summary>
        private int _writeIndex;

        /// <summary>Cached player transform for distance calculation.</summary>
        private Transform _playerTransform;

        /// <summary>Cached screen dimensions for edge clamping.</summary>
        private float _screenWidth;
        private float _screenHeight;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _markers = new ActiveMarker[MAX_MARKERS];
            _writeIndex = 0;

            // ── Find HUD camera if not assigned ──
            if (hudCamera == null)
            {
                VisorHUDController.CopyActiveControllersTo(s_controllerResolveBuffer);
                for (int i = 0; i < s_controllerResolveBuffer.Count; i++)
                {
                    VisorHUDController controller = s_controllerResolveBuffer[i];
                    if (controller != null && controller.HudCamera != null)
                    {
                        hudCamera = controller.HudCamera;
                        break;
                    }
                }

                s_controllerResolveBuffer.Clear();
            }

            // ── Find player ──
            SceneBootstrap.TryGetCurrentPlayerTransform(out _playerTransform);
        }

        public override void OnEnable()
        {
            base.OnEnable(); // Добавить это!
            ScanEvents.OnNodeFound += HandleNodeFound;
            if (Application.isPlaying)
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
        }

        public override void OnDisable()
        {
            base.OnDisable(); // Добавить это!
            ScanEvents.OnNodeFound -= HandleNodeFound;
            if (Application.isPlaying)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
        }

        // ══════════════════════════════════════════════════════════
        //  EVENT HANDLER — Zero GC
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called by ScanEvents.OnNodeFound for each detected resource.
        /// Writes into pre-allocated array at cycling index.
        /// Zero GC: struct assignment to array slot.
        /// </summary>
        private void HandleNodeFound(Unity.Mathematics.float3 worldPos)
        {
            // ── Check for duplicate (same position already active) ──
            // Prevents stacking markers on same resource if scanned twice
            for (int i = 0; i < MAX_MARKERS; i++)
            {
                if (_markers[i].active &&
                    math.distancesq(_markers[i].worldPos, worldPos) < 1f)
                {
                    // Refresh timer instead of adding duplicate
                    _markers[i].timer = markerLifetime;
                    return;
                }
            }

            // ── Write new marker ──
            _markers[_writeIndex] = new ActiveMarker
            {
                worldPos = worldPos,
                timer = markerLifetime,
                active = true
            };

            _writeIndex = (_writeIndex + 1) % MAX_MARKERS;
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable — Timer countdown
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            for (int i = 0; i < MAX_MARKERS; i++)
            {
                if (!_markers[i].active) continue;

                _markers[i].timer -= deltaTime;

                if (_markers[i].timer <= 0f)
                {
                    _markers[i].active = false;
                }
            }

            // Cache screen size (may change with resolution)
            if (hudCamera != null)
            {
                _screenWidth = hudCamera.pixelWidth;
                _screenHeight = hudCamera.pixelHeight;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  SHAPES RENDERING — HUD markers
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// ImmediateModeShapeDrawer callback.
        /// Draws only in HUD_Render_Camera.
        /// </summary>
        public override void DrawShapes(Camera cam)
        {
            // ── HUD camera filter ──
            if (hudCamera == null) return;
            if (cam != hudCamera) return;
            if (_playerTransform == null) return;

            Unity.Mathematics.float3 playerPos = _playerTransform.position;
            float time = Time.time;

            using (Draw.Command(cam))
            {
                // Set up screen-space drawing
                Draw.Matrix = cam.cameraToWorldMatrix.inverse;
                Draw.BlendMode = ShapesBlendMode.Transparent;

                for (int i = 0; i < MAX_MARKERS; i++)
                {
                    if (!_markers[i].active) continue;

                    DrawSingleMarker(
                        cam, _markers[i].worldPos, _markers[i].timer,
                        playerPos, time);
                }

                // Reset matrix
                Draw.ResetMatrix();
            }
        }

        /// <summary>
        /// Renders one marker: diamond + distance text.
        /// All math is struct-based — zero GC.
        /// </summary>
        private void DrawSingleMarker(Camera cam, Unity.Mathematics.float3 worldPos, float timer,
                                       Unity.Mathematics.float3 playerPos, float time)
        {
            // ── World → Screen projection ──
            Vector3 screenPos = cam.WorldToScreenPoint((Vector3)worldPos);

            // ── Behind camera handling ──
            bool isBehind = screenPos.z < 0f;
            if (isBehind)
            {
                // Invert to get correct edge direction
                screenPos.x = _screenWidth - screenPos.x;
                screenPos.y = _screenHeight - screenPos.y;
            }

            // ── Edge clamping ──
            bool isClamped = isBehind
                || screenPos.x < edgeMargin
                || screenPos.x > _screenWidth - edgeMargin
                || screenPos.y < edgeMargin
                || screenPos.y > _screenHeight - edgeMargin;

            if (isClamped)
            {
                screenPos.x = math.clamp(screenPos.x, edgeMargin, _screenWidth - edgeMargin);
                screenPos.y = math.clamp(screenPos.y, edgeMargin, _screenHeight - edgeMargin);
            }

            // ── Distance calculation ──
            float distance = math.distance(worldPos, playerPos);

            // ── Marker size: inversely proportional to distance ──
            float size = markerBaseSize / math.max(distance * 0.1f, 0.5f);
            size = math.clamp(size, markerMinSize, markerMaxSize);

            // ── Alpha: fade out in last FADE_DURATION seconds ──
            float alpha = markerColor.a;
            if (timer < FADE_DURATION)
            {
                alpha *= math.saturate(timer / FADE_DURATION);
            }

            // ── NASA-Punk flicker ──
            float flicker = 1f - flickerIntensity +
                             flickerIntensity * math.sin(time * flickerFrequency * math.PI * 2f);
            alpha *= flicker;

            if (alpha < 0.01f) return;

            Color color = new Color(markerColor.r, markerColor.g, markerColor.b, alpha);
            Color textColor = new Color(markerColor.r, markerColor.g, markerColor.b, alpha * 0.8f);

            // ── Convert screen position to world position on HUD near plane ──
            Vector3 worldMarkerPos = cam.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, cam.nearClipPlane + 0.01f));

            // ── Draw diamond (4-sided regular polygon, hollow) ──
            Draw.RegularPolygonBorder(
                worldMarkerPos,
                cam.transform.rotation,
                4,                              // sides = diamond
                size * 0.001f,                  // radius (world units, scaled for near plane)
                size * 0.0002f,                 // thickness
                color
            );

            // ── Distance text below diamond ──
            int distInt = math.clamp((int)distance, 0, 5000);
            string distText = HudNumericStringCache.IntStrings[distInt];

            // Offset below diamond
            Vector3 textScreenPos = new Vector3(screenPos.x, screenPos.y - size * 0.8f, screenPos.z);
            Vector3 worldTextPos = cam.ScreenToWorldPoint(
                new Vector3(textScreenPos.x, textScreenPos.y, cam.nearClipPlane + 0.01f));

            Draw.Text(
                worldTextPos,
                cam.transform.rotation,
                distText,
                TextAlign.Center,
                distanceFontSize * 0.001f,      // Scaled for near plane
                distanceFont,
                textColor
            );

            // ── Clamped indicator: smaller dot when off-screen ──
            if (isClamped)
            {
                // Draw a small dot at clamp position to indicate direction
                Color dotColor = new Color(color.r, color.g, color.b, alpha * 0.5f);
                Draw.Disc(worldMarkerPos, cam.transform.rotation, size * 0.0003f, dotColor);
            }
        }
    }
}
