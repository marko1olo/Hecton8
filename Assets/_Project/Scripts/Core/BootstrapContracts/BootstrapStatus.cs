using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Shared bootstrap watchdog and timing state used by runtime telemetry and safe-halt protection.
    /// </summary>
    public enum BootstrapStepToken : byte
    {
        None = 0,
        HardwareCheck = 1,
        MemoryPreWarm = 2,
        Core = 3,
        CoreServices = Core,
        Environment = 4,
        Player = 5,
        UI = 6,
        SceneActivate = 7,
    }

    /// <summary>
    /// Deterministic bootstrap timing read-model for the 00_BOOTSTRAP -> 01_MAIN_MENU handoff.
    /// </summary>
    public static class BootstrapStatus
    {
        public delegate void BootstrapSafeHaltTelemetryReporter(
            BootstrapStepToken activeStep,
            BootstrapStepToken longestStep,
            double bootElapsedSeconds,
            double activeStepElapsedMilliseconds,
            uint recentStepMaskLow,
            uint recentStepMaskHigh,
            uint recentStepHash0,
            uint recentStepHash1,
            uint recentStepHash2,
            uint recentStepHash3,
            uint recentStepHash4,
            uint recentStepHash5,
            uint recentStepHash6,
            uint recentStepHash7,
            uint recentStepHash8,
            uint recentStepHash9);

        public const uint TelemetrySlowStepFlag = 1u << 9;
        public const uint TelemetrySafeHaltFlag = 1u << 10;

        private const double SlowStepBudgetMilliseconds = 500.0;
        private const double SafeHaltTimeoutSeconds = 10.0;
        private const int RecentStepCapacity = 10;
        private const string SafeHaltMessage =
            "BIOS ERROR 0xBOOT_TIMEOUT\nEXPECTED: ACTIVE BOOT STEP <= 10.0S\nDETECTED: BOOT STALL\nACTION: SAFE HALT";

        // COLD ALLOC: BootstrapStepToken[10] - safe-halt forensic step ring - owner: BootstrapStatus
        private static readonly BootstrapStepToken[] _recentSteps = new BootstrapStepToken[RecentStepCapacity];
        private static double _bootStartTimeSeconds;
        private static double _stepStartTimeSeconds;
        private static BootstrapStepToken _activeStep;
        private static BootstrapSafeHaltTelemetryReporter _safeHaltTelemetryReporter;
        private static int _recentStepWriteIndex;
        private static int _recentStepCount;
        private static bool _stepActive;

        /// <summary>
        /// True once 00_BOOTSTRAP has started its startup sequence.
        /// </summary>
        public static bool BootStarted { get; private set; }

        /// <summary>
        /// True once the main menu scene has been reached.
        /// </summary>
        public static bool MainMenuReached { get; private set; }

        /// <summary>
        /// True once the watchdog has entered safe halt.
        /// </summary>
        public static bool SafeHaltTriggered { get; private set; }

        /// <summary>
        /// BIOS-style safe-halt message for runtime overlays.
        /// </summary>
        public static string SafeHaltDisplayMessage => SafeHaltMessage;

        /// <summary>
        /// Bit mask of bootstrap steps that exceeded the timing budget.
        /// </summary>
        public static uint SlowStepMask { get; private set; }

        /// <summary>
        /// Longest observed step duration in milliseconds.
        /// </summary>
        public static double LongestStepMilliseconds { get; private set; }

        /// <summary>
        /// Longest observed step token.
        /// </summary>
        public static BootstrapStepToken LongestStep { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _bootStartTimeSeconds = 0d;
            _stepStartTimeSeconds = 0d;
            _activeStep = BootstrapStepToken.None;
            _recentStepWriteIndex = 0;
            _recentStepCount = 0;
            _stepActive = false;
            System.Array.Clear(_recentSteps, 0, _recentSteps.Length);
            BootStarted = false;
            MainMenuReached = false;
            SafeHaltTriggered = false;
            SlowStepMask = 0u;
            LongestStepMilliseconds = 0d;
            LongestStep = BootstrapStepToken.None;
            Time.timeScale = 1f;
            Physics.simulationMode = SimulationMode.FixedUpdate;
        }

        public static void RegisterSafeHaltTelemetryReporter(BootstrapSafeHaltTelemetryReporter reporter)
        {
            _safeHaltTelemetryReporter = reporter;
        }

        private static void EnsureEditorBootPump()
        {
#if UNITY_EDITOR
            if (!Application.runInBackground)
                Application.runInBackground = true;
#endif
        }

        /// <summary>
        /// Starts the bootstrap watchdog timer.
        /// </summary>
        public static void BeginBoot()
        {
            EnsureEditorBootPump();

            if (BootStarted && !SafeHaltTriggered && !MainMenuReached)
                return;

            BootStarted = true;
            MainMenuReached = false;
            SafeHaltTriggered = false;
            SlowStepMask = 0u;
            LongestStepMilliseconds = 0d;
            LongestStep = BootstrapStepToken.None;
            _activeStep = BootstrapStepToken.None;
            _stepActive = false;
            _stepStartTimeSeconds = 0d;
            _recentStepWriteIndex = 0;
            _recentStepCount = 0;
            System.Array.Clear(_recentSteps, 0, _recentSteps.Length);
            _bootStartTimeSeconds = Time.realtimeSinceStartupAsDouble;
            Time.timeScale = 1f;
            Physics.simulationMode = SimulationMode.FixedUpdate;
        }

        /// <summary>
        /// Begins timing one ordered bootstrap step.
        /// </summary>
        /// <param name="step">Bootstrap step token.</param>
        public static void BeginStep(BootstrapStepToken step)
        {
            if (step == BootstrapStepToken.None)
                return;

            BeginBoot();
            _activeStep = step;
            _stepStartTimeSeconds = Time.realtimeSinceStartupAsDouble;
            _stepActive = true;
            RecordRecentStep(step);
        }

        /// <summary>
        /// Completes timing for one ordered bootstrap step.
        /// </summary>
        /// <param name="step">Bootstrap step token.</param>
        public static void EndStep(BootstrapStepToken step)
        {
            if (!_stepActive || _activeStep != step || step == BootstrapStepToken.None)
                return;

            double elapsedMilliseconds = (Time.realtimeSinceStartupAsDouble - _stepStartTimeSeconds) * 1000.0;
            if (elapsedMilliseconds > LongestStepMilliseconds)
            {
                LongestStepMilliseconds = elapsedMilliseconds;
                LongestStep = step;
            }

            if (elapsedMilliseconds > SlowStepBudgetMilliseconds)
                SlowStepMask |= 1u << ((int)step - 1);

            _activeStep = BootstrapStepToken.None;
            _stepActive = false;
            _stepStartTimeSeconds = 0d;
        }

        /// <summary>
        /// Marks the main-menu handoff as completed.
        /// </summary>
        public static void MarkMainMenuReached()
        {
            if (!BootStarted)
                BeginBoot();

            MainMenuReached = true;
            SafeHaltTriggered = false;
            _activeStep = BootstrapStepToken.None;
            _stepActive = false;
            _stepStartTimeSeconds = 0d;
            Time.timeScale = 1f;
            Physics.simulationMode = SimulationMode.FixedUpdate;
        }

        /// <summary>
        /// Returns the telemetry error flags emitted by bootstrap timing state.
        /// </summary>
        /// <returns>Bootstrap-related telemetry error flags.</returns>
        public static uint GetTelemetryErrorFlags()
        {
            uint flags = 0u;
            if (SlowStepMask != 0u)
                flags |= TelemetrySlowStepFlag;
            if (SafeHaltTriggered)
                flags |= TelemetrySafeHaltFlag;

            return flags;
        }

        /// <summary>
        /// Evaluates the boot timeout watchdog and enters safe halt if the menu handoff never completes.
        /// </summary>
        /// <returns>True when a new safe halt was triggered this frame.</returns>
        public static bool TryTriggerSafeHalt()
        {
            if (!BootStarted || MainMenuReached || SafeHaltTriggered)
                return false;

            double nowSeconds = Time.realtimeSinceStartupAsDouble;
            double bootElapsedSeconds = nowSeconds - _bootStartTimeSeconds;
            double watchedElapsedSeconds = _stepActive
                ? nowSeconds - _stepStartTimeSeconds
                : bootElapsedSeconds;
            if (watchedElapsedSeconds < SafeHaltTimeoutSeconds)
                return false;

            SafeHaltTriggered = true;
            Time.timeScale = 0f;
            Physics.simulationMode = SimulationMode.Script;
            BuildRecentStepMasks(out uint recentStepMaskLow, out uint recentStepMaskHigh);
            BuildRecentStepHashes(
                out uint recentStepHash0,
                out uint recentStepHash1,
                out uint recentStepHash2,
                out uint recentStepHash3,
                out uint recentStepHash4,
                out uint recentStepHash5,
                out uint recentStepHash6,
                out uint recentStepHash7,
                out uint recentStepHash8,
                out uint recentStepHash9);
            double activeElapsedMilliseconds = _stepActive
                ? (nowSeconds - _stepStartTimeSeconds) * 1000.0
                : 0.0;
            _safeHaltTelemetryReporter?.Invoke(
                _activeStep,
                LongestStep,
                bootElapsedSeconds,
                activeElapsedMilliseconds,
                recentStepMaskLow,
                recentStepMaskHigh,
                recentStepHash0,
                recentStepHash1,
                recentStepHash2,
                recentStepHash3,
                recentStepHash4,
                recentStepHash5,
                recentStepHash6,
                recentStepHash7,
                recentStepHash8,
                recentStepHash9);
            Debug.LogError(SafeHaltMessage);
            return true;
        }

        private static void RecordRecentStep(BootstrapStepToken step)
        {
            _recentSteps[_recentStepWriteIndex] = step;
            _recentStepWriteIndex = (_recentStepWriteIndex + 1) % RecentStepCapacity;
            if (_recentStepCount < RecentStepCapacity)
                _recentStepCount++;
        }

        private static void BuildRecentStepMasks(out uint low, out uint high)
        {
            low = 0u;
            high = 0u;
            for (int i = 0; i < _recentStepCount; i++)
            {
                int sourceIndex = _recentStepWriteIndex - _recentStepCount + i;
                if (sourceIndex < 0)
                    sourceIndex += RecentStepCapacity;

                uint token = (uint)_recentSteps[sourceIndex] & 0xFu;
                int shift = i * 4;
                if (shift < 32)
                    low |= token << shift;
                else
                    high |= token << (shift - 32);
            }
        }

        private static void BuildRecentStepHashes(
            out uint hash0,
            out uint hash1,
            out uint hash2,
            out uint hash3,
            out uint hash4,
            out uint hash5,
            out uint hash6,
            out uint hash7,
            out uint hash8,
            out uint hash9)
        {
            hash0 = 0u;
            hash1 = 0u;
            hash2 = 0u;
            hash3 = 0u;
            hash4 = 0u;
            hash5 = 0u;
            hash6 = 0u;
            hash7 = 0u;
            hash8 = 0u;
            hash9 = 0u;

            for (int i = 0; i < _recentStepCount; i++)
            {
                int sourceIndex = _recentStepWriteIndex - _recentStepCount + i;
                if (sourceIndex < 0)
                    sourceIndex += RecentStepCapacity;

                uint hash = HashStep(_recentSteps[sourceIndex]);
                switch (i)
                {
                    case 0:
                        hash0 = hash;
                        break;
                    case 1:
                        hash1 = hash;
                        break;
                    case 2:
                        hash2 = hash;
                        break;
                    case 3:
                        hash3 = hash;
                        break;
                    case 4:
                        hash4 = hash;
                        break;
                    case 5:
                        hash5 = hash;
                        break;
                    case 6:
                        hash6 = hash;
                        break;
                    case 7:
                        hash7 = hash;
                        break;
                    case 8:
                        hash8 = hash;
                        break;
                    case 9:
                        hash9 = hash;
                        break;
                }
            }
        }

        private static uint HashStep(BootstrapStepToken step)
        {
            uint value = (uint)step;
            value ^= 0x9E3779B9u;
            value *= 0x85EBCA6Bu;
            value ^= value >> 13;
            value *= 0xC2B2AE35u;
            value ^= value >> 16;
            return value;
        }
    }
}
