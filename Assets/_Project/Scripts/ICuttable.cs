// ============================================================================
// HECTON-8 — ICuttable.cs
// Контракт для объектов, которые можно резать лазером.
//
// РЕАЛИЗАЦИИ:
//   • ResourceNode  — ресурсный узел, делегирует в TakeDamage.
//   • BaseModule    — модуль базы, делегирует в ApplyDamage.
//
// ПОТРЕБИТЕЛИ:
//   • LaserCutter.UsePrimary() — вызывает ApplyCutDamage через
//     TryGetComponent<ICuttable> на рейкаст-цели.
//
// КОНТРАКТ:
//   • damage — урон за кадр (damagePerSecond × deltaTime).
//     Гарантия вызывающей стороны: damage > 0.
//   • hitPoint — мировая точка попадания луча (Vector3).
//     Реализация может использовать для декалей, VFX, направленных
//     повреждений. Может игнорировать.
//
// ZERO GC:
//   • Интерфейс без свойств — TryGetComponent<ICuttable> не вызывает
//     boxing (Unity кэширует интерфейсные запросы на MonoBehaviour).
//   • Параметры — value types (float, Vector3).
// ============================================================================

using UnityEngine;

namespace Hecton8.Gameplay
{
    public interface ICuttable
    {
        /// <summary>
        /// Применяет урон от режущего инструмента.
        /// </summary>
        /// <param name="damage">
        /// Урон за текущий кадр. Положительное значение.
        /// Типичный источник: damagePerSecond × deltaTime.
        /// </param>
        /// <param name="hitPoint">
        /// Мировая позиция точки попадания луча / инструмента.
        /// Используется для локализации повреждений, спавна декалей,
        /// направленных VFX.
        /// </param>
        void ApplyCutDamage(float damage, Vector3 hitPoint);
    }
}