using Unity.Profiling;

namespace Hecton8.Core
{
    /// <summary>
    /// Central zero-allocation registry for profiler markers used by major runtime systems.
    /// </summary>
    public static class ProfilerRegistry
    {
        /// <summary>Physics simulation and integration marker.</summary>
        public static readonly ProfilerMarker PhysicsTick = new ProfilerMarker("H8.Physics.Tick");

        /// <summary>Voxel generation and rebuild marker.</summary>
        public static readonly ProfilerMarker VoxelRebuild = new ProfilerMarker("H8.Voxel.Rebuild");

        /// <summary>AI update marker.</summary>
        public static readonly ProfilerMarker AiTick = new ProfilerMarker("H8.AI.Tick");

        /// <summary>Fluid solve marker.</summary>
        public static readonly ProfilerMarker FluidPressure = new ProfilerMarker("H8.Fluid.Pressure");

        /// <summary>Crash telemetry ring-buffer write marker.</summary>
        public static readonly ProfilerMarker TelemetryWrite = new ProfilerMarker("H8.Debug.TelemetryWrite");

        /// <summary>Crash telemetry binary export marker.</summary>
        public static readonly ProfilerMarker TelemetryExport = new ProfilerMarker("H8.Debug.TelemetryExport");
    }
}
