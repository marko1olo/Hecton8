using System.Diagnostics;
using System.Threading;

namespace Hecton8.Core
{
    /// <summary>
    /// Background monitor that dumps telemetry if the main thread stops pinging.
    /// </summary>
    internal static class BlackBoxHeartbeatThread
    {
        private const int StallMilliseconds = 2000;
        private const int ProbeSleepMilliseconds = 50;
        private const int StopJoinMilliseconds = 100;
        private const string ThreadName = "H8.MainThreadHeartbeat";

        // COLD ALLOC: object[1] - heartbeat thread lifecycle gate - owner: BlackBoxHeartbeatThread
        private static readonly object _gate = new object();

        private static Thread _thread;
        private static long _lastPingTimestamp;
        private static int _running;

        /// <summary>
        /// Starts the background heartbeat monitor.
        /// </summary>
        public static void Start()
        {
            lock (_gate)
            {
                if (Volatile.Read(ref _running) != 0)
                    return;

                Volatile.Write(ref _lastPingTimestamp, Stopwatch.GetTimestamp());
                Volatile.Write(ref _running, 1);
                _thread = new Thread(Run) // COLD ALLOC: Thread[1] - background main-thread heartbeat monitor - owner: BlackBoxHeartbeatThread
                {
                    IsBackground = true,
                    Name = ThreadName,
                    Priority = ThreadPriority.BelowNormal
                };
                _thread.Start();
            }
        }

        /// <summary>
        /// Stops the background heartbeat monitor during normal shutdown.
        /// </summary>
        public static void Stop()
        {
            Thread thread;
            lock (_gate)
            {
                Volatile.Write(ref _running, 0);
                thread = _thread;
                _thread = null;
            }

            if (thread != null && thread.IsAlive)
                thread.Join(StopJoinMilliseconds);
        }

        /// <summary>
        /// Marks one main-thread frame as alive.
        /// </summary>
        public static void Ping()
        {
            Volatile.Write(ref _lastPingTimestamp, Stopwatch.GetTimestamp());
        }

        private static void Run()
        {
            while (Volatile.Read(ref _running) != 0)
            {
                Thread.Sleep(ProbeSleepMilliseconds);

                long lastPing = Volatile.Read(ref _lastPingTimestamp);
                if (lastPing <= 0L)
                    continue;

                long elapsedMilliseconds = ((Stopwatch.GetTimestamp() - lastPing) * 1000L) / Stopwatch.Frequency;
                if (elapsedMilliseconds < StallMilliseconds)
                    continue;

                Volatile.Write(ref _running, 0);
                GlobalTelemetryBus.TryEmergencyFlushFromBackground();
#if !UNITY_EDITOR
                Process.GetCurrentProcess().Kill();
#endif
                return;
            }
        }
    }
}
