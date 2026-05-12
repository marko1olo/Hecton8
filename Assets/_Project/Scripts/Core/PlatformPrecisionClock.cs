using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Hecton8.Core
{
    /// <summary>
    /// Cross-platform monotonic precision clock for replay and platform diagnostics.
    /// </summary>
    public static class PlatformPrecisionClock
    {
        private static readonly double _tickToSeconds = 1.0d / Stopwatch.Frequency;

        /// <summary>
        /// Monotonic timestamp in seconds. Windows maps to QPC; POSIX runtimes map Stopwatch to monotonic time.
        /// </summary>
        public static double NowSeconds
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Stopwatch.GetTimestamp() * _tickToSeconds;
        }

        /// <summary>
        /// Stopwatch ticks per second for serialization tools that need native clock units.
        /// </summary>
        public static long Frequency => Stopwatch.Frequency;
    }
}
