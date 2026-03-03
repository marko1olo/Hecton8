namespace Hecton.Interaction
{
    using System;
    using Hecton.Items;

    /// <summary>
    /// Глобальная шина событий взаимодействия.
    /// Инвентарь, квестовая система, аналитика — подписываются здесь.
    /// Ни один из модулей не знает друг о друге.
    /// </summary>
    public static class InteractionEvents
    {
        // ─── Предметы ────────────────────────────────────────────
        /// <summary>Предмет подобран. (данные, количество, кто подобрал)</summary>
        public static event Action<ItemData, int, UnityEngine.Transform> OnItemCollected;

        public static void RaiseItemCollected(ItemData data, int quantity,
                                               UnityEngine.Transform collector)
        {
            #if UNITY_EDITOR
            UnityEngine.Debug.Log(
                $"<color=#5cf>▶ ItemCollected</color>  " +
                $"{data.itemName} ×{quantity}  weight: {data.weight * quantity}");
            #endif
            OnItemCollected?.Invoke(data, quantity, collector);
        }

        // ─── Расширяй по мере необходимости ─────────────────────
        // public static event Action<DoorData> OnDoorOpened;
        // public static event Action<TerminalData> OnTerminalAccessed;
    }
}