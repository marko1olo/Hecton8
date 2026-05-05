// ============================================================================
// HECTON-8 — IFabricator.cs
// Контракт для верстаков (фабрикаторов), с которыми взаимодействует UI.
//
// РЕАЛИЗАЦИИ:
//   • Fabricator — машина-верстак с IPowerComponent, IInteractable, ITickable.
//
// ПОТРЕБИТЕЛИ:
//   • HectonFabricatorUI — subscribes through ICraftingEventListener for FabricatorOpened payloads,
//     читает AvailableRecipes, IsCrafting, вызывает StartCraft/CancelCraft.
//   • CraftingEvents — передаёт IFabricator в событии открытия.
//
// КОНТРАКТ:
//   • AvailableRecipes — список рецептов, доступных на этом верстаке.
//     IReadOnlyList гарантирует, что UI не может мутировать коллекцию.
//     Реализация возвращает List<RecipeData> через implicit cast.
//
//   • IsCrafting — true, пока идёт процесс крафта (таймер тикает
//     или заморожен из-за отсутствия питания).
//
//   • StartCraft(recipe) — запускает крафт. Реализация:
//     проверяет CanCraft, списывает ингредиенты, запускает таймер.
//     UI вызывает только если IsCrafting == false.
//
//   • CancelCraft() — отменяет текущий крафт, возвращает ингредиенты.
//     Безопасен при вызове без активного крафта (no-op).
//
// ZERO GC:
//   • IReadOnlyList<T> — интерфейс над List<T>, zero allocation
//     (implicit cast, без создания обёртки).
//   • Свойства возвращают value types (bool) и ссылочный тип
//     (IReadOnlyList) без boxing.
// ============================================================================

using System.Collections.Generic;

namespace Hecton8.Crafting
{
    public interface IFabricator
    {
        /// <summary>
        /// Список рецептов, доступных на этом верстаке.
        /// UI использует для отображения меню крафта.
        /// Не может быть null — реализация гарантирует пустой список.
        /// </summary>
        IReadOnlyList<RecipeData> AvailableRecipes { get; }

        /// <summary>
        /// Идёт ли процесс крафта.
        /// true — таймер активен (или заморожен при отсутствии питания).
        /// false — верстак свободен для нового крафта.
        /// </summary>
        bool IsCrafting { get; }

        /// <summary>
        /// Запускает процесс крафта указанного рецепта.
        /// Реализация проверяет наличие ингредиентов, питания,
        /// списывает ресурсы и запускает таймер.
        /// </summary>
        /// <param name="recipe">
        /// Рецепт для крафта. Не null.
        /// Должен присутствовать в AvailableRecipes.
        /// </param>
        void StartCraft(RecipeData recipe);

        void StartCraft(RecipeData recipe, int multiplier);

        /// <summary>
        /// Отменяет текущий крафт.
        /// Возвращает списанные ингредиенты в инвентарь игрока.
        /// Безопасен при вызове без активного крафта (no-op).
        /// </summary>
        void CancelCraft();
    }
}
