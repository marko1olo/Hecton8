// ============================================================================
// HECTON-8 — CraftingEvents.cs
// Глобальный статический Event Bus для системы крафта.
//
// Zero-instance, zero-GC dispatch.
// HUD, UI, аудио-системы подписываются здесь вместо прямых ссылок.
//
// Паттерн: кэширование делегата перед Invoke для thread-safety.
// ============================================================================

using System;
using Hecton8.Items;

namespace Hecton8.Crafting
{
    public static class CraftingEvents
    {
        // ════════════════════════════════════════════════════════
        //  EVENT: OnFabricatorOpened
        //  UI крафта подписывается, чтобы показать меню рецептов.
        //  Param: Fabricator — ссылка на конкретный верстак.
        // ════════════════════════════════════════════════════════

        public static event Action<Fabricator> OnFabricatorOpened;

        /// <summary>Фабрикатор открыт игроком (Interact).</summary>
        public static void RaiseFabricatorOpened(Fabricator fabricator)
        {
            var handler = OnFabricatorOpened;
            handler?.Invoke(fabricator);
        }

        // ════════════════════════════════════════════════════════
        //  EVENT: OnFabricatorClosed
        //  UI крафта скрывается.
        // ════════════════════════════════════════════════════════

        public static event Action OnFabricatorClosed;

        /// <summary>Фабрикатор закрыт (отход, ESC, завершение крафта).</summary>
        public static void RaiseFabricatorClosed()
        {
            var handler = OnFabricatorClosed;
            handler?.Invoke();
        }

        // ════════════════════════════════════════════════════════
        //  EVENT: OnCraftStarted
        //  HUD запускает анимацию прогресс-бара.
        //  Param: RecipeData — рецепт, который начал крафтиться.
        // ════════════════════════════════════════════════════════

        public static event Action<RecipeData> OnCraftStarted;

        /// <summary>Процесс крафта запущен.</summary>
        public static void RaiseCraftStarted(RecipeData recipe)
        {
            var handler = OnCraftStarted;
            handler?.Invoke(recipe);
        }

        // ════════════════════════════════════════════════════════
        //  EVENT: OnCraftProgressUpdated
        //  HUD обновляет полосу прогресса.
        //  Param: float (0.0 … 1.0) — нормализованный прогресс.
        //
        //  ЧАСТОТА: каждый кадр во время крафта.
        //  ZERO GC: float — value type, no boxing.
        // ════════════════════════════════════════════════════════

        public static event Action<float> OnCraftProgressUpdated;

        /// <summary>Прогресс крафта обновлён (0..1).</summary>
        public static void RaiseCraftProgressUpdated(float progress01)
        {
            var handler = OnCraftProgressUpdated;
            handler?.Invoke(progress01);
        }

        // ════════════════════════════════════════════════════════
        //  EVENT: OnCraftCompleted
        //  HUD показывает уведомление о готовности.
        //  Param: ItemData — что было скрафчено.
        // ════════════════════════════════════════════════════════

        public static event Action<ItemData> OnCraftCompleted;

        /// <summary>Крафт завершён успешно, предмет добавлен в инвентарь.</summary>
        public static void RaiseCraftCompleted(ItemData resultItem)
        {
            var handler = OnCraftCompleted;
            handler?.Invoke(resultItem);
        }

        // ════════════════════════════════════════════════════════
        //  EVENT: OnCraftCancelled
        //  HUD скрывает прогресс-бар, ресурсы возвращаются.
        // ════════════════════════════════════════════════════════

        public static event Action OnCraftCancelled;

        /// <summary>Крафт отменён (игрок отошёл / нажал ESC).</summary>
        public static void RaiseCraftCancelled()
        {
            var handler = OnCraftCancelled;
            handler?.Invoke();
        }
    }
}