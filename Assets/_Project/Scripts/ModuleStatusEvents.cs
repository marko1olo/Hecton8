// ============================================================================
// HECTON-8 — ModuleStatusEvents.cs
// Static event bus for BaseModule → HUD communication.
// v1.0
//
// ZERO GC: Static Action delegates. No closure captures.
// THREAD SAFETY: Main thread only.
// ============================================================================

using System;
using Hecton8.Gameplay;

/// <summary>
/// Decoupled event bus between BaseModule (interior trigger)
/// and HectonSuitHUD (module status panel).
///
/// BaseModule fires → HUD listens. No direct reference needed.
/// </summary>
public static class ModuleStatusEvents
{
    /// <summary>
    /// Player entered a module's interior zone.
    /// Parameter: the BaseModule entered.
    /// </summary>
    public static event Action<BaseModule> OnModuleEnter;

    /// <summary>
    /// Player exited a module's interior zone.
    /// Parameter: the BaseModule exited.
    /// </summary>
    public static event Action<BaseModule> OnModuleExit;

    /// <summary>Fire OnModuleEnter. Called from BaseModule.OnTriggerEnter.</summary>
    public static void NotifyEnter(BaseModule module)
        => OnModuleEnter?.Invoke(module);

    /// <summary>Fire OnModuleExit. Called from BaseModule.OnTriggerExit.</summary>
    public static void NotifyExit(BaseModule module)
        => OnModuleExit?.Invoke(module);
}