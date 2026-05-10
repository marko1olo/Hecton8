// ============================================================================
// HECTON-8 — ControlScheme.cs
// ScriptableObject: edinaya tochka nastroyki vseh klavish.
// Sozdat: Assets → Create → Hecton8 → Control Scheme
// Naznachit v inspektore HectonPlayerMovement i PlayerInteraction.
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
        [Tooltip("Vzaimodeystvie s obektami. Standartnaya klavisha: E.")]
        public KeyCode interactKey = KeyCode.E;

        // ══════════════════════════════════════════════════════════
        //  SWIM VERTICAL
        // ══════════════════════════════════════════════════════════

        [Header("── Swim Vertical ─────────────────────────────")]
        [Tooltip("Vverh v vode. Standartnaya klavisha: Space.")]
        public KeyCode swimAscendPrimary   = KeyCode.Space;

        [Tooltip("Dop. klavisha vverh. None = otklyucheno.")]
        public KeyCode swimAscendAlternate = KeyCode.None;

        [Tooltip("Vniz v vode. Osnovnaya klavisha: Left Ctrl.")]
        public KeyCode swimDescendPrimary  = KeyCode.LeftControl;

        [Tooltip("Vniz v vode. Alternativnaya klavisha: C.")]
        public KeyCode swimDescendAlternate = KeyCode.C;

        [Tooltip("Zapasnoy vniz (istoricheskiy Q).")]
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

        [Tooltip("Modifikator dekonstruktsii lazerom (uderzhivat + LKM).")]
        public KeyCode deconstructModifier = KeyCode.R;

        // ══════════════════════════════════════════════════════════
        //  FUTURE (zadel — poka ne podklyucheny)
        // ══════════════════════════════════════════════════════════

        [Header("── Future (ne podklyucheny) ───────────────────")]
        public KeyCode flashlightKey = KeyCode.F;
        public KeyCode mapKey        = KeyCode.M;
        public KeyCode sprintKey     = KeyCode.LeftShift;
    }
}
