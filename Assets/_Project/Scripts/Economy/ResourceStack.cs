using System;
using Hecton8.Items;
using UnityEngine;

namespace Hecton8.Economy
{
    /// <summary>
    /// Immutable resource/quantity pair used by runtime recycling overlays.
    /// </summary>
    [Serializable]
    public struct ResourceStack
    {
        [Tooltip("Resolved item granted or consumed by the stack.")]
        public ItemData Item;

        [Tooltip("Amount of the referenced item.")]
        [Min(1)]
        public int Amount;
    }
}
