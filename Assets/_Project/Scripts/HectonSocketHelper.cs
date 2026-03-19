// ============================================================================
// HECTON-8 — HectonSocketHelper.cs
// Editor-time socket visualizer for modular base building.
//
// Draws directional gizmos on snap sockets:
//   • Green  = Top socket
//   • Yellow = Side socket
//   • Red    = Under socket
//   • Blue   = Unknown/default
//
// Gizmos hide behind geometry (zTest = LessEqual).
// Context menu "Snap to Surface" auto-aligns socket to nearest mesh.
//
// [ExecuteInEditMode] — runs ONLY in Editor, zero runtime cost.
// ============================================================================

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
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
            // Запоминаем состояние для Undo
            Undo.RecordObject(transform, "Snap Socket to Surface");
#endif

            Vector3 origin = transform.position;
            Vector3 direction = -transform.forward;

            if (UnityEngine.Physics.Raycast(origin, direction, out RaycastHit hit,
                snapRayDistance, snapLayerMask, QueryTriggerInteraction.Ignore))
            {
                // Перемещаем на точку удара
                transform.position = hit.point;

                // Разворачиваем forward по нормали поверхности
                transform.rotation = Quaternion.LookRotation(hit.normal);

#if UNITY_EDITOR
                Debug.Log($"[SocketHelper] Snapped to {hit.collider.name} " +
                          $"at {hit.point}, normal {hit.normal}");
#endif
            }
            else
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"[SocketHelper] No surface found within {snapRayDistance}m " +
                    $"behind socket {name}.", this);
#endif
            }
        }

        // ══════════════════════════════════════════════════════════
        //  GIZMOS — скрываются за геометрией
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
            Vector3 pos = transform.position;
            Vector3 forward = transform.forward;
            Vector3 tipPos = pos + forward * arrowLength;

            // Цвет по типу сокета
            Color socketColor = socketType switch
            {
                SocketType.Top   => Color.green,
                SocketType.Side  => Color.yellow,
                SocketType.Under => Color.red,
                _                => Color.cyan
            };

            // Более яркий когда выделен
            if (!selected)
                socketColor.a = 0.6f;

            // ── Z-Test: гизмо скрываются за геометрией ──
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            // ── Стрелка: линия от позиции в направлении forward ──
            Handles.color = socketColor;
            Handles.DrawLine(pos, tipPos, 2f);

            // ── Конус на конце стрелки ──
            float coneLength = arrowLength * 0.25f;
            Vector3 coneBase = tipPos;
            Vector3 coneTip = tipPos + forward * coneLength;

            // Рисуем конус как набор линий
            Handles.color = socketColor;
            DrawCone(coneBase, coneTip, forward, coneLength * 0.4f, 8);

            // ── Сфера на кончике ──
            Handles.color = socketColor;
            Handles.SphereHandleCap(
                0, coneTip, Quaternion.identity,
                tipRadius * 2f, EventType.Repaint);

            // ── Подпись с типом ──
            GUIStyle style = new GUIStyle();
            style.normal.textColor = socketColor;
            style.fontSize = 10;
            style.fontStyle = FontStyle.Bold;

            Handles.Label(tipPos + Vector3.up * 0.1f,
                socketType.ToString(), style);

            // ── Направляющие оси (тонкие, для ориентации) ──
            if (selected)
            {
                float axisLen = arrowLength * 0.3f;

                Handles.color = new Color(1f, 0f, 0f, 0.4f); // Right = red
                Handles.DrawLine(pos, pos + transform.right * axisLen, 1f);

                Handles.color = new Color(0f, 1f, 0f, 0.4f); // Up = green
                Handles.DrawLine(pos, pos + transform.up * axisLen, 1f);
            }

            // Сброс z-test
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        }

        /// <summary>
        /// Рисует конус из линий. Дёшево, наглядно.
        /// </summary>
        private static void DrawCone(
            Vector3 baseCenter, Vector3 tip,
            Vector3 direction, float baseRadius, int segments)
        {
            // Вычисляем перпендикулярные оси
            Vector3 up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(direction, up)) > 0.99f)
                up = Vector3.right;

            Vector3 right = Vector3.Cross(direction, up).normalized;
            Vector3 localUp = Vector3.Cross(right, direction).normalized;

            Vector3 prevPoint = baseCenter + right * baseRadius;

            for (int i = 1; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                Vector3 point = baseCenter
                    + right * (Mathf.Cos(angle) * baseRadius)
                    + localUp * (Mathf.Sin(angle) * baseRadius);

                // Линия по окружности основания
                Handles.DrawLine(prevPoint, point, 1f);
                // Линия от основания к вершине
                Handles.DrawLine(point, tip, 1f);

                prevPoint = point;
            }
        }
#endif
    }
}