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
// [ExecuteInEditMode] — runs ONLY in Editor, zero runtime cost.
// ============================================================================

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Rendering;
#endif

namespace Hecton8.Building
{
    [ExecuteInEditMode]
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
        [Tooltip("Тип сокета. Определяет цвет гизмо.\n" +
                 "Top = зелёный, Side = жёлтый, Under = красный.")]
        [SerializeField] private SocketType socketType = SocketType.Side;

        [Tooltip("Длина визуальной стрелки в метрах.")]
        [SerializeField] private float arrowLength = 0.5f;

        [Tooltip("Радиус сферы на конце стрелки.")]
        [SerializeField] private float tipRadius = 0.05f;

        [Tooltip("Максимальная дистанция рейкаста для Snap to Surface.")]
        [SerializeField] private float snapRayDistance = 2f;

        [Tooltip("Layer mask для рейкаста при Snap to Surface.")]
        [SerializeField] private LayerMask snapLayerMask = ~0;

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
#endif

        // ══════════════════════════════════════════════════════════
        //  CONTEXT MENU — SNAP TO SURFACE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Пускает луч из позиции сокета в направлении -forward.
        /// Если попадает в меш — перемещает сокет на точку удара
        /// и разворачивает forward по нормали поверхности.
        ///
        /// Использование: ПКМ на компоненте → Snap to Surface.
        /// </summary>
        [ContextMenu("Snap to Surface")]
        private void SnapToSurface()
        {
#if UNITY_EDITOR
            Undo.RecordObject(transform, "Snap Socket to Surface");
#endif

            Vector3 origin = transform.position;
            Vector3 direction = -transform.forward;

            if (UnityEngine.Physics.Raycast(
                origin,
                direction,
                out RaycastHit hit,
                snapRayDistance,
                snapLayerMask,
                QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point;
                transform.rotation = Quaternion.LookRotation(hit.normal);

#if UNITY_EDITOR
                Debug.Log($"[SocketHelper] Snapped to {hit.collider.name} at {hit.point}, normal {hit.normal}");
#endif
            }
            else
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"[SocketHelper] No surface found within {snapRayDistance}m behind socket {name}.",
                    this);
#endif
            }
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

            float sqrDistanceToCamera = (cam.transform.position - pos).sqrMagnitude;
            Color socketColor = GetSocketColor(socketType, selected);

            Handles.zTest = CompareFunction.LessEqual;

            // ── LOD 2: Far (> 20m) ────────────────────────────────
            // Только основная линия
            Handles.color = socketColor;
            Handles.DrawLine(pos, tipPos, 2f);

            if (sqrDistanceToCamera > FarSimpleDistanceSqr)
            {
                Handles.zTest = CompareFunction.Always;
                return;
            }

            // ── LOD 1: Mid (10m..20m) ─────────────────────────────
            // Линия + cone + sphere, но без текста
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
            // Полный набор, включая текст
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