namespace Hecton.Interaction
{
    using UnityEngine;

    /// <summary>
    /// Контракт для любого объекта, с которым игрок может взаимодействовать.
    /// Реализуйте на дверях, терминалах, предметах, NPC и т.д.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Курсор навёлся — включить подсветку/обводку.</summary>
        void OnHoverStart();

        /// <summary>Курсор ушёл — выключить подсветку.</summary>
        void OnHoverEnd();

        /// <summary>Игрок нажал клавишу взаимодействия.</summary>
        /// <param name="interactor">Transform того, кто взаимодействует (игрок).</param>
        void Interact(Transform interactor);

        /// <summary>Текст подсказки: "Открыть шлюз", "Забрать титан" и т.д.</summary>
        string GetInteractText();
    }
}