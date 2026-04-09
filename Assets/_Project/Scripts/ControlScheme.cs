// ============================================================================
// HECTON-8 — ControlScheme.cs
// ScriptableObject: единая точка настройки всех клавиш.
// Создать: Assets → Create → Hecton8 → Control Scheme
// Назначить в инспекторе HectonPlayerMovement и PlayerInteraction.
// ============================================================================

using UnityEngine;

namespace Hecton8.Gameplay
{
    [CreateAssetMenu(fileName = "ControlScheme_Default", menuName = "Hecton8/Control Scheme", order = 101)]
    public sealed class ControlScheme : ScriptableObject
    {
        // ══════════════════════════════════════════════════════════
        //  INTERACTION
        // ══════════════════════════════════════════════════════════

        [Header("── Interaction ──────────────────────────────")]
        [Tooltip("Взаимодействие с объектами. Стандартная клавиша: E.")]
        public KeyCode interactKey = KeyCode.E;

        // ══════════════════════════════════════════════════════════
        //  SWIM VERTICAL
        // ══════════════════════════════════════════════════════════

        [Header("── Swim Vertical ─────────────────────────────")]
        [Tooltip("Вверх в воде. Стандартная клавиша: Space.")]
        public KeyCode swimAscendPrimary   = KeyCode.Space;

        [Tooltip("Доп. клавиша вверх. None = отключено.")]
        public KeyCode swimAscendAlternate = KeyCode.None;

        [Tooltip("Вниз в воде. Основная клавиша: Left Ctrl.")]
        public KeyCode swimDescendPrimary  = KeyCode.LeftControl;

        [Tooltip("Вниз в воде. Альтернативная клавиша: C.")]
        public KeyCode swimDescendAlternate = KeyCode.C;

        [Tooltip("Запасной вниз (исторический Q).")]
        public KeyCode swimDescendLegacy   = KeyCode.Q;

        // ══════════════════════════════════════════════════════════
        //  TOOLS & INVENTORY
        // ══════════════════════════════════════════════════════════

        [Header("── Tools & Inventory ────────────────────────")]
        public KeyCode toolSlot1    = KeyCode.Alpha1;
        public KeyCode toolSlot2    = KeyCode.Alpha2;
        public KeyCode toolSlot3    = KeyCode.Alpha3;
        public KeyCode toolSlot4    = KeyCode.Alpha4;
        public KeyCode inventoryKey = KeyCode.Tab;

        [Tooltip("Модификатор деконструкции лазером (удерживать + ЛКМ).")]
        public KeyCode deconstructModifier = KeyCode.R;

        // ══════════════════════════════════════════════════════════
        //  FUTURE (задел — пока не подключены)
        // ══════════════════════════════════════════════════════════

        [Header("── Future (не подключены) ───────────────────")]
        public KeyCode flashlightKey = KeyCode.F;
        public KeyCode mapKey        = KeyCode.M;
        public KeyCode sprintKey     = KeyCode.LeftShift;
    }
}
