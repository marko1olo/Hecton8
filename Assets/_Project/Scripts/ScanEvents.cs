// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  ScanEvents.cs — Project HECTON-8 Scanner Event Bus                        ║
// ║  Static events for decoupled communication between ScannerTool,            ║
// ║  HectonScanMarkerSystem, and any future scan-reactive systems.             ║
// ║  v1.0                                                                       ║
// ║                                                                             ║
// ║  ZERO GC: Static delegates, no allocations on Invoke.                      ║
// ║  THREAD SAFETY: Main thread only (Unity event pattern).                    ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using Unity.Mathematics;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Static event bus for the scanning system.
    ///
    /// ScannerTool fires events → HectonScanMarkerSystem listens.
    /// No direct coupling between scanner and HUD.
    ///
    /// Usage:
    ///   ScanEvents.OnScanTriggered?.Invoke(center, radius);
    ///   ScanEvents.OnNodeFound?.Invoke(worldPos);
    /// </summary>
    public static class ScanEvents
    {
        /// <summary>
        /// Fired once when a scan pulse is triggered.
        /// Parameters: world-space center of scan, scan radius in meters.
        /// Used by VFX systems to render the expanding ring.
        /// </summary>
        public static Action<float3, float> OnScanTriggered;

        /// <summary>
        /// Fired for each ResourceNode detected by a scan pulse.
        /// Parameter: world-space position of the detected node.
        /// HectonScanMarkerSystem creates a timed HUD marker at this position.
        /// </summary>
        public static Action<float3> OnNodeFound;
    }
}