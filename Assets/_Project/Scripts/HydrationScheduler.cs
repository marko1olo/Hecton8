using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace Hecton8.SaveSystem
{
    public static class HydrationScheduler
    {
        public const double FrameBudgetMilliseconds = 4.0d;

        public static readonly long FrameBudgetTicks = Math.Max(1L, Stopwatch.Frequency / 250L);

        public static long CreateDeadlineTicks()
        {
            return Stopwatch.GetTimestamp() + FrameBudgetTicks;
        }

        public static Awaitable NextFrameAsync(CancellationToken cancellationToken = default)
        {
            return Awaitable.NextFrameAsync(cancellationToken: cancellationToken);
        }
    }
}
