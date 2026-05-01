namespace Hecton8.Modding
{
    /// <summary>
    /// Entry contract for a managed HECTON-8 code mod.
    /// The loader creates one instance of the implementing type, calls <see cref="OnLoad"/>,
    /// later calls <see cref="OnInitialize"/> once gameplay bootstrap is ready, and finally
    /// calls <see cref="OnUnload"/> during shutdown or domain reset.
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
    /// Required contract for managed code mods.
    /// Loader rejects mods whose required API is newer than the engine facade.
    /// Older API versions are accepted only through registered compatibility shims.
    /// </summary>
    public interface IHectonVersionedMod : IHectonMod
    {
        /// <summary>
        /// Minimum HECTON-8 modding API version required by this mod.
        /// </summary>
        int RequiredAPIVersion { get; }
    }
}
