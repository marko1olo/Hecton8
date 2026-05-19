namespace Hecton8.Modding
{
    /// <summary>
    /// Legacy managed HECTON-8 code mod contract retained for source compatibility.
    /// Runtime UGC execution is quarantined; the loader disables managed entries while
    /// envelope-only mode is enforced, and live commands must use 64-byte
    /// <see cref="FutureCommandEnvelope"/> packets.
    /// </summary>
    public interface IHectonMod
    {
        /// <summary>
        /// Called once immediately after the mod assembly is loaded and instantiated.
        /// Subscribe to events and register static content here.
        /// </summary>
        void OnLoad();

        /// <summary>
        /// Called once after bootstrap published a live runtime world and player object.
        /// Resolve world-facing state here instead of during <see cref="OnLoad"/>.
        /// </summary>
        void OnInitialize();

        /// <summary>
        /// Called during shutdown or domain reset so the mod can dispose subscriptions and transient state.
        /// </summary>
        void OnUnload();
    }

    /// <summary>
    /// Legacy version marker for managed code mods.
    /// It is ignored by the runtime UGC path while envelope-only mode is enforced.
    /// </summary>
    public interface IHectonVersionedMod : IHectonMod
    {
        /// <summary>
        /// Minimum HECTON-8 modding API version required by this mod.
        /// </summary>
        int RequiredAPIVersion { get; }
    }
}
