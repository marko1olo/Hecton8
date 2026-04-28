// ============================================================================
// HECTON-8 — ITickable.cs
// Интерфейсы-контракты для централизованной системы обновления.
//
// Любой скрипт, реализующий один или несколько интерфейсов, обязан:
//   1. В OnEnable()  → GlobalRegistry registration
//   2. В OnDisable() → matching GlobalRegistry unregistration
//   3. НИКОГДА не объявлять собственный Update/FixedUpdate/LateUpdate.
//
// GameTickManager вызывает методы централизованно — один Update
// на весь проект вместо сотен вызовов через Unity Message System.
// ============================================================================

namespace Hecton8.Core
{
    /// <summary>
    /// Каждый кадр. Замена Update().
    /// Используй для: ввода, движения, анимации, UI-логики.
    /// </summary>
    public interface ITickable : IUpdatable
    {
        /// <param name="deltaTime">Time.deltaTime — передаётся напрямую,
        /// без лишнего обращения к Time API.</param>
        new void Tick(float deltaTime);
    }

    /// <summary>
    /// Фиксированный шаг физики. Замена FixedUpdate().
    /// Используй для: Rigidbody-движения, физических проверок.
    /// </summary>
    public interface IFixedTickable
    {
        /// <param name="fixedDeltaTime">Time.fixedDeltaTime — передаётся
        /// напрямую для удобства.</param>
        void FixedTick(float fixedDeltaTime);
    }

    /// <summary>
    /// Медленный тик (по умолчанию 2 раза в секунду).
    /// Используй для: жизнеобеспечения базы, AI-решений, авто-сохранения,
    /// любых тяжёлых вычислений, которые не нужны каждый кадр.
    /// </summary>
    public interface ISlowTickable
    {
        /// <summary>
        /// Вызывается с фиксированной периодичностью
        /// (настраивается в GameTickManager.slowTickInterval).
        /// DeltaTime не передаётся — используй интервал напрямую
        /// или Time.time, если нужна дельта.
        /// </summary>
        void SlowTick();
    }
}
