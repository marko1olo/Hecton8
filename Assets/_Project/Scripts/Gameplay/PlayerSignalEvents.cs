using System;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// HUD-directed trauma packet raised by runtime damage owners without scene polling.
    /// </summary>
    public readonly struct TraumaHudSignal
    {
        public TraumaHudSignal(
            float glitchIntensity,
            float recoilScalar,
            float transportPower01,
            float hullIntegrity01,
            bool biosRecoveryMode)
        {
            GlitchIntensity = glitchIntensity;
            RecoilScalar = recoilScalar;
            TransportPower01 = transportPower01;
            HullIntegrity01 = hullIntegrity01;
            BiosRecoveryMode = biosRecoveryMode;
        }

        public float GlitchIntensity { get; }
        public float RecoilScalar { get; }
        public float TransportPower01 { get; }
        public float HullIntegrity01 { get; }
        public bool BiosRecoveryMode { get; }
    }

    /// <summary>
    /// Audio-directed internal stress packet for heartbeat / breathing consumers.
    /// </summary>
    public readonly struct InteractionSignal
    {
        public InteractionSignal(
            float stress01,
            float volume01,
            float pitchScale,
            float frequency01)
        {
            Stress01 = stress01;
            Volume01 = volume01;
            PitchScale = pitchScale;
            Frequency01 = frequency01;
        }

        public float Stress01 { get; }
        public float Volume01 { get; }
        public float PitchScale { get; }
        public float Frequency01 { get; }
    }

    /// <summary>
    /// Raised when the equipped tool is exhausted and removed from the inventory.
    /// </summary>
    public readonly struct ToolDepletedSignal
    {
        public ToolDepletedSignal(int toolHashId)
        {
            ToolHashId = toolHashId;
        }

        public int ToolHashId { get; }
    }

    /// <summary>
    /// Static zero-allocation signal bus for trauma, HUD, and internal audio coupling.
    /// </summary>
    public static class PlayerSignalEvents
    {
        public static event Action<TraumaHudSignal> OnTraumaHudSignal;
        public static event Action<InteractionSignal> OnInteractionSignal;
        public static event Action<ToolDepletedSignal> OnToolDepletedSignal;

        public static void RaiseTraumaHudSignal(in TraumaHudSignal signal)
        {
            Action<TraumaHudSignal> handler = OnTraumaHudSignal;
            handler?.Invoke(signal);
        }

        public static void RaiseInteractionSignal(in InteractionSignal signal)
        {
            Action<InteractionSignal> handler = OnInteractionSignal;
            handler?.Invoke(signal);
        }

        public static void RaiseToolDepletedSignal(in ToolDepletedSignal signal)
        {
            Action<ToolDepletedSignal> handler = OnToolDepletedSignal;
            handler?.Invoke(signal);
        }
    }
}
