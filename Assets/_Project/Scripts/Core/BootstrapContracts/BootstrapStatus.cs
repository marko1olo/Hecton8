using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Shared bootstrap watchdog and timing state used by runtime telemetry and safe-halt protection.
    /// </summary>
    public enum BootstrapStepToken : byte
    {
        None = 0,
        Core = 1,
        Environment = 2,
        Player = 3,
        UI = 4,
    }

    /// <summary>
    /// Deterministic bootstrap timing read-model for the 00_BOOTSTRAP -> 01_MAIN_MENU handoff.
    /// </summary>
    public static class BootstrapStatus
    {
        public const uint TelemetrySlowStepFlag = 1u << 9;
        public const uint TelemetrySafeHaltFlag = 1u << 10;

        private const double SlowStepBudgetMilliseconds = 500.0;
        private const double SafeHaltTimeoutSeconds = 10.0;
        private const string SafeHaltMessage =
            "BIOS ERROR 0xBOOT_TIMEOUT\nEXPECTED: 01_MAIN_MENU <= 10.0S\nDETECTED: BOOT STALL\nACTION: SAFE HALT";

        private static double _bootStartTimeSeconds;
        private static double _stepStartTimeSeconds;
        private static BootstrapStepToken _activeStep;
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
            _stepActive = false;
            BootStarted = false;
            MainMenuReached = false;
            SafeHaltTriggered = false;
            SlowStepMask = 0u;
            LongestStepMilliseconds = 0d;
            LongestStep = BootstrapStepToken.None;
            Time.timeScale = 1f;
            Physics.simulationMode = SimulationMode.FixedUpdate;
        }

        /// <summary>
        /// Starts the bootstrap watchdog timer.
        /// </summary>
        public static void BeginBoot()
        {
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

            double elapsedSeconds = Time.realtimeSinceStartupAsDouble - _bootStartTimeSeconds;
            if (elapsedSeconds < SafeHaltTimeoutSeconds)
                return false;

            SafeHaltTriggered = true;
            Time.timeScale = 0f;
            Physics.simulationMode = SimulationMode.Script;
            Debug.LogError(SafeHaltMessage);
            return true;
        }
    }
}
