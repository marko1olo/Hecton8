// ============================================================================
// HECTON-8 — IBatteryTool.cs
// Interface for tools that use batteries.
//
// ARCHITECTURE:
//   • Interface for battery-powered tools
//   • Used by BatteryCharger for battery swapping
//   • Zero GC: no allocations in interface methods
// ============================================================================

using Hecton8.Items;
using UnityEngine;

namespace Hecton8.Tools
{
    /// <summary>
    /// Interface for tools that use batteries.
    /// Implement this on PlayerTool-derived classes that have battery functionality.
    /// </summary>
    public interface IBatteryTool
    {
        /// <summary>True if the tool currently has a battery installed.</summary>
        bool HasBattery { get; }

        /// <summary>Current battery charge level (0-1). Returns 0 if no battery.</summary>
        float BatteryCharge { get; }

        /// <summary>The battery item currently installed (null if none).</summary>
        ItemData BatteryItem { get; }

        /// <summary>
        /// Removes the battery from the tool.
        /// </summary>
        /// <returns>The removed battery item, or null if no battery.</returns>
        ItemData RemoveBattery();

        /// <summary>
        /// Inserts a battery into the tool.
        /// </summary>
        /// <param name="battery">The battery item to insert.</param>
        /// <param name="charge">Initial charge level (0-1).</param>
        /// <returns>True if the battery was inserted successfully.</returns>
        bool InsertBattery(ItemData battery, float charge);
    }

    /// <summary>
    /// Optional narrow bridge for battery tools that also publish central equipment state.
    /// </summary>
    public interface IRuntimeEquipmentIdProvider
    {
        /// <summary>Runtime equipment/tool hash owned by the tool runtime. Zero means unavailable.</summary>
        uint RuntimeEquipmentId { get; }
    }
}
