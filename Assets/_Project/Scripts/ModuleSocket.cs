// ============================================================================
// HECTON-8 — ModuleSocket.cs
// Компонент-маркер точки соединения модулей базы.
//
// РАЗМЕЩЕНИЕ:
//   На каждом префабе модуля (Corridor, Habitat, Airlock и т.д.)
//   создаются пустые дочерние GameObject в точках стыковки.
//   На каждом из них:
//     1. Компонент ModuleSocket (этот скрипт).
//     2. SphereCollider (isTrigger=true, radius=0.1).
//     3. Layer = "Sockets" (создать в Project Settings → Tags & Layers).
//
// ОРИЕНТАЦИЯ:
//   transform.forward = направление "наружу" (куда подключается следующий модуль).
//   transform.up = локальный "вверх" модуля.
//   Призрак постройки выравнивает свой forward по forward сокета.
//
// ЗАНЯТОСТЬ:
//   IsOccupied = true когда к сокету уже подключён другой модуль.
//   PlayerBuilder пропускает занятые сокеты при поиске snap-точки.
//   Устанавливается при размещении модуля (TryPlaceModule).
//
// ZERO GC:
//   Нет Update, нет аллокаций. Только хранилище данных + флаг.
//   Обнаружение через Physics.OverlapSphereNonAlloc на слое "Sockets".
// ============================================================================

using UnityEngine;

namespace Hecton8.Building
{
    [DisallowMultipleComponent]
    [AddComponentMenu("HECTON-8/Building/Module Socket")]
    public sealed class ModuleSocket : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Socket Settings ───────────────────────────")]
        [Tooltip("Тип модуля, который может подключиться к этому сокету.\n" +
                 "Пустая строка = универсальный сокет (принимает всё).")]
        [SerializeField] private string compatibleType = "";

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// true когда к сокету уже подключён модуль.
        /// Занятые сокеты пропускаются при поиске snap-точки.
        /// </summary>
        private bool _isOccupied;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>Сокет занят другим модулем.</summary>
        public bool IsOccupied => _isOccupied;

        /// <summary>Тип совместимого модуля (пустая строка = универсальный).</summary>
        public string CompatibleType => compatibleType;

        /// <summary>
        /// Помечает сокет как занятый.
        /// Вызывается PlayerBuilder после успешного размещения модуля.
        /// </summary>
        public void SetOccupied(bool occupied)
        {
            _isOccupied = occupied;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR — GIZMOS
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Цвет: зелёный = свободен, красный = занят
            Gizmos.color = _isOccupied
                ? new Color(1f, 0.2f, 0.2f, 0.6f)
                : new Color(0f, 1f, 0.5f, 0.6f);

            Gizmos.DrawWireSphere(transform.position, 0.15f);

            // Стрелка forward (направление стыковки)
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position,
                            transform.position + transform.forward * 0.5f);

            // Стрелка up
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position,
                            transform.position + transform.up * 0.3f);
        }

        private void OnDrawGizmosSelected()
        {
            // Радиус обнаружения (визуальный)
            Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, 2f);
        }
#endif
    }
}