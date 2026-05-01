using System;

namespace Hecton8.Core
{
    /// <summary>
    /// Registry-owned input binding service used by UI rebinding panels.
    /// Kept in bootstrap contracts so input runtime can publish the service without depending on Core.
    /// </summary>
    public interface IInputBindingService
    {
        /// <summary>True while an interactive rebind operation is active.</summary>
        bool IsRebinding { get; }

        /// <summary>Raised when a binding rebind starts.</summary>
        event Action<string, string, int> OnRebindStarted;

        /// <summary>Raised when a binding rebind completes.</summary>
        event Action<string, string, int, string> OnRebindCompleted;

        /// <summary>Raised when a binding rebind is canceled.</summary>
        event Action<string, string, int> OnRebindCanceled;

        /// <summary>Raised when a new binding conflicts with an existing binding.</summary>
        event Action<string, string, string, Action, Action> OnConflictDetected;

        /// <summary>Raised after binding overrides are loaded.</summary>
        event Action OnOverridesLoaded;

        /// <summary>Raised after binding overrides are saved.</summary>
        event Action OnOverridesSaved;

        /// <summary>Raised after binding overrides are cleared.</summary>
        event Action OnOverridesCleared;

        /// <summary>
        /// Starts an interactive rebind for a binding index.
        /// </summary>
        bool StartInteractiveRebind(
            string actionName,
            string actionMap = "Player",
            int bindingIndex = 0,
            string expectedControlType = null,
            string cancelPath = "<Keyboard>/escape",
            string[] excludedControlPaths = null);

        /// <summary>
        /// Starts an interactive rebind for a binding identifier.
        /// </summary>
        bool StartInteractiveRebindById(
            string actionName,
            string bindingId,
            string actionMap = "Player",
            string expectedControlType = null,
            string cancelPath = "<Keyboard>/escape",
            string[] excludedControlPaths = null);

        /// <summary>Cancels the active rebind operation, if any.</summary>
        void CancelRebind();

        /// <summary>Saves current binding overrides.</summary>
        void SaveOverrides();

        /// <summary>Loads persisted binding overrides.</summary>
        void LoadOverrides();

        /// <summary>Clears persisted binding overrides.</summary>
        void ClearOverrides(bool clearPlayerPrefs = true);
    }
}
