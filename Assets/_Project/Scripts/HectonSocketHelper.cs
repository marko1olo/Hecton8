// ============================================================================
// HECTON-8 — HectonSocketHelper.cs
// Optimized editor-time socket visualizer for modular base building.
//
// Draws directional gizmos on snap sockets:
//   • Green  = Top socket
//   • Yellow = Side socket
//   • Red    = Under socket
//   • Blue   = Unknown/default
//
// Optimizations:
//   • Cached GUIStyle (no per-frame allocations)
//   • Distance-based gizmo LOD in Scene View
//   • Native ConeHandleCap instead of manual line-cone
//
// Gizmos hide behind geometry (zTest = LessEqual).
// Context menu "Snap to Surface" auto-aligns socket to nearest mesh.
//
// Does not need edit-mode lifecycle; gizmos and context actions work without it.
// ============================================================================

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Rendering;
#endif

namespace Hecton8.Building
{
    [DisallowMultipleComponent]
    public sealed class HectonSocketHelper : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        public enum SocketType
        {
            Top,
            Side,
            Under
        }

        [Header("═══ SOCKET CONFIG ═══")]
        [Tooltip("Tip soketa. Opredelyaet tsvet gizmo.\n" +
                 "Top = zelenyy, Side = zheltyy, Under = krasnyy.")]
        [SerializeField] private SocketType socketType = SocketType.Side;

        [Tooltip("Dlina vizualnoy strelki v metrah.")]
        [SerializeField] private float arrowLength = 0.5f;

        [Tooltip("Radius sfery na kontse strelki.")]
        [SerializeField] private float tipRadius = 0.05f;

        [Tooltip("Maksimalnaya distantsiya poverhnostnogo proba dlya Snap to Surface.")]
        [SerializeField] private float snapRayDistance = 2f;

        [Tooltip("Layer mask dlya poverhnostnogo proba pri Snap to Surface.")]
        [SerializeField] private LayerMask snapLayerMask = Hecton8.Core.HectonLayerMasks.FieldToolSurfaceLayerMask;

#if UNITY_EDITOR
        // ══════════════════════════════════════════════════════════
        //  GIZMO LOD SETTINGS
        // ══════════════════════════════════════════════════════════

        // sqr distances:
        // 100  = 10m
        // 400  = 20m
        private const float NearLabelDistanceSqr = 100f;
        private const float FarSimpleDistanceSqr = 400f;

        private static GUIStyle s_LabelStyle;
        private static Color s_LastLabelColor = new Color(-1f, -1f, -1f, -1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_LabelStyle = null;
            s_LastLabelColor = new Color(-1f, -1f, -1f, -1f);
        }
#endif

        // ══════════════════════════════════════════════════════════
        //  CONTEXT MENU — SNAP TO SURFACE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Puskaet luch iz pozitsii soketa v napravlenii -forward.
        /// Esli popadaet v mesh — peremeschaet soket na tochku udara
        /// i razvorachivaet forward po normali poverhnosti.
        ///
        /// Ispolzovanie: PKM na komponente → Snap to Surface.
        /// </summary>
        [ContextMenu("Snap to Surface")]
        private void SnapToSurface()
        {
#if UNITY_EDITOR
            Undo.RecordObject(transform, "Snap Socket to Surface");
#endif

            Hecton8.Core.H8Debug.LogWarning(
                $"[SocketHelper] Snap to Surface is disabled for X_005 PhysX hygiene. " +
                $"Route this editor action through the construction surface owner before re-enabling. " +
                $"Configured probe: {snapRayDistance:0.###}m, mask {snapLayerMask.value}.",
                this);
        }

        // ══════════════════════════════════════════════════════════
        //  GIZMOS — optimized, hidden behind geometry
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            DrawSocketGizmo(false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawSocketGizmo(true);
        }

        private void DrawSocketGizmo(bool selected)
        {
            Camera cam = Camera.current;
            if (cam == null)
                return;

            Vector3 pos = transform.position;
            Vector3 forward = transform.forward;
            Vector3 tipPos = pos + forward * arrowLength;

            Vector3 cameraVisualPosition = cam.transform.position;
            Vector3 visualDeltaToCamera = cameraVisualPosition - pos;
            float sqrDistanceToCamera = visualDeltaToCamera.sqrMagnitude;
            Color socketColor = GetSocketColor(socketType, selected);

            Handles.zTest = CompareFunction.LessEqual;

            // ── LOD 2: Far (> 20m) ────────────────────────────────
            // Tolko osnovnaya liniya
            Handles.color = socketColor;
            Handles.DrawLine(pos, tipPos, 2f);

            if (sqrDistanceToCamera > FarSimpleDistanceSqr)
            {
                Handles.zTest = CompareFunction.Always;
                return;
            }

            // ── LOD 1: Mid (10m..20m) ─────────────────────────────
            // Liniya + cone + sphere, no bez teksta
            float coneSize = Mathf.Max(arrowLength * 0.18f, tipRadius * 2f);
            Quaternion coneRotation = Quaternion.LookRotation(forward);

            Handles.color = socketColor;
            Handles.ConeHandleCap(0, tipPos, coneRotation, coneSize, EventType.Repaint);

            Handles.color = socketColor;
            Handles.SphereHandleCap(0, tipPos, Quaternion.identity, tipRadius * 2f, EventType.Repaint);

            if (sqrDistanceToCamera > NearLabelDistanceSqr)
            {
                Handles.zTest = CompareFunction.Always;
                return;
            }

            // ── LOD 0: Near (<= 10m) ──────────────────────────────
            // Polnyy nabor, vklyuchaya tekst
            Handles.color = socketColor;
            Handles.Label(tipPos + Vector3.up * 0.1f, socketType.ToString(), GetLabelStyle(socketColor));

            if (selected)
            {
                float axisLen = arrowLength * 0.3f;

                Handles.color = new Color(1f, 0f, 0f, 0.4f); // Right = red
                Handles.DrawLine(pos, pos + transform.right * axisLen, 1f);

                Handles.color = new Color(0f, 1f, 0f, 0.4f); // Up = green
                Handles.DrawLine(pos, pos + transform.up * axisLen, 1f);
            }

            Handles.zTest = CompareFunction.Always;
        }

        private static Color GetSocketColor(SocketType type, bool selected)
        {
            Color socketColor = type switch
            {
                SocketType.Top   => Color.green,
                SocketType.Side  => Color.yellow,
                SocketType.Under => Color.red,
                _                => Color.cyan
            };

            if (!selected)
                socketColor.a = 0.6f;

            return socketColor;
        }

        private static GUIStyle GetLabelStyle(Color color)
        {
            if (s_LabelStyle == null)
            {
                s_LabelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Overflow
                };
            }

            if (s_LastLabelColor != color)
            {
                s_LabelStyle.normal.textColor = color;
                s_LastLabelColor = color;
            }

            return s_LabelStyle;
        }
#endif
    }
}
