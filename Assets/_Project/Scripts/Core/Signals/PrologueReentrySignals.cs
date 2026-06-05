namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>
    /// Cold-start prewarm for orbital drop signal lanes.
    /// </summary>
    public static class PrologueReentrySignalLanes
    {
        /// <summary>
        /// Ensures the prologue signal lanes allocate before the whiteout moment.
        /// </summary>
        public static void Warm()
        {
            Hecton8.Core.SignalCorridorRuntime.EnsureInitialized();
            SignalBus<AtmosphericReentrySignal>.EnsureInitialized();
            SignalBus<ReentryAcousticStressSignal>.EnsureInitialized();
            SignalBus<PrologueCompleteSignal>.EnsureInitialized();
            SignalBus<ReentryVfxStateSignal>.EnsureInitialized();
            SignalBus<AcousticPingSignal>.EnsureInitialized();
            SignalBus<DebrisSpawnSignal>.EnsureInitialized();
            SignalBus<VisorDropletSignal>.EnsureInitialized();
            SignalBus<StreamingTurbulenceSignal>.EnsureInitialized();
            SignalBus<TelemetryAnomalySignal>.EnsureInitialized();
            SignalBus<SystemPauseSignal>.EnsureInitialized();
            SignalBus<MixerStateSignal>.EnsureInitialized();
            SignalBus<VocalWarningSignal>.EnsureInitialized();
            SignalBus<DiegeticHudSignal>.EnsureInitialized();
            SignalBus<HUDNotificationSignal>.EnsureInitialized();
            SignalBus<SectorResidencyHydratedSignal>.EnsureInitialized();
            SignalBus<HapticRequest>.EnsureInitialized();
            Hecton8.Core.CameraJuiceSignals.EnsurePrewarmed();
        }
    }
}
