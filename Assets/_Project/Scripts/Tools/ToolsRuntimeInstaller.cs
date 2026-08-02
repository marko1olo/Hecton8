using Hecton8.World;
using UnityEngine;

namespace Hecton8.Tools
{
    /// <summary>
    /// Cold-path installer for scene-level tool owners that a live consumer already reads out of
    /// <see cref="Hecton8.Core.GlobalRegistry"/> and receives as null.
    /// </summary>
    public static class ToolsRuntimeInstaller
    {
        private const string RuntimeRootName = "__HECTON_TOOLS_RUNTIME";

        /// <summary>
        /// Ensures the tool durability owner exists in the active gameplay scene.
        /// <para>
        /// <see cref="ToolDurabilitySystem"/> is the only <c>IToolDurabilityService</c> implementation and
        /// it had no construction site of any kind: no <c>AddComponent</c>, <c>new</c>,
        /// <c>GetOrAddComponent</c> or <c>CreateInstance</c> for the type exists anywhere under Assets/,
        /// the type declares no static factory, and a GUID sweep of every .unity, .prefab and .asset file
        /// under Assets/ (4093 files, nibble-swapped byte scan for the binary scenes) found its script GUID
        /// 818e05db9f8b78843b2663109ab8aeb3 in none of them. The
        /// <c>[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]</c> at
        /// ToolDurabilitySystem.cs:121 is not a self-install - its body only clears three static fields
        /// (<c>s_x001ToolDurabilitySystemSignalPushDropCount</c>, <c>s_nativeLayoutValidated</c>,
        /// <c>s_nativeLayoutValid</c>) for domain-reload-disabled play sessions. So
        /// <c>GlobalRegistry.ToolDurabilityService</c> is permanently null and no tool in the shipped build
        /// ever wears out.
        /// </para>
        /// <para>
        /// Seven live read sites already take that null: PlayerToolManager.cs:1477 and PlayerTool.cs:966
        /// (PlayerToolManager's GUID is authored into Player.prefab, and PlayerTool is the base of
        /// BuilderTool, whose GUID is authored into Tool_Builder_Held.prefab), ModularEquipmentEngine.cs:3333
        /// (constructed from PlayerTool.cs:907 via <c>ModularEquipmentEngine.EnsureRuntimeInstance()</c>),
        /// MaintenanceStationModule.cs:484, HUDQuickBar.cs:958 and PDALoadoutTab.cs:140/:165.
        /// </para>
        /// <para>
        /// Registration happens in <c>ToolDurabilitySystem.OnEnable</c> at ToolDurabilitySystem.cs:252-264,
        /// which reaches <c>GlobalRegistry.RegisterToolDurabilityRuntime</c> at :1942, so the root must be
        /// live before the AddComponent below - see <see cref="ResolveOrCreateRuntimeRoot"/>. The owner
        /// carries no asset-typed <c>[SerializeField]</c> at all, so it stands up complete on a bare runtime
        /// root with no authored dependency; every dependency it takes (<c>DataVault</c>, <c>Save</c>,
        /// <c>Player</c>, <c>BrineFluidDensity</c>) is resolved from GlobalRegistry and rebound through
        /// <c>IGlobalRegistryHotSwapRefListener</c>.
        /// </para>
        /// </summary>
        public static void EnsureRuntimeSystems()
        {
            GameObject runtimeRoot = ResolveOrCreateRuntimeRoot();

            // Guarded so the installer is idempotent across the multiple passes the bootstrap makes. The
            // owner also self-guards: ToolDurabilitySystem.Awake/OnEnable/Start each open with
            // TryAbortForUsableExistingRuntime (:245, :254, :269), so a second instance cannot take the
            // slot from a live one.
            //
            // ToolDurabilitySystem declares no [DefaultExecutionOrder], and only one owner is installed on
            // this root, so there is no intra-root ordering to respect. It contains no GetComponent or
            // TryGetComponent call, so the sibling-coupling that forces WorldRuntimeInstaller's deferred
            // activation idiom does not apply.
            if (!runtimeRoot.TryGetComponent<ToolDurabilitySystem>(out _))
                runtimeRoot.AddComponent<ToolDurabilitySystem>();
        }

        /// <summary>
        /// Resolves the tools runtime root across loaded scenes, creating it when no scene owns one, and
        /// guarantees it is live so the install above actually runs Awake/OnEnable.
        /// </summary>
        /// <returns>Active runtime root that hosts the tool owners.</returns>
        private static GameObject ResolveOrCreateRuntimeRoot()
        {
            GameObject runtimeRoot = null;
            WorldRuntimeReferenceUtility.TryResolveScenePath(ref runtimeRoot, RuntimeRootName);
            if (runtimeRoot == null)
                // Player-build construction path: no authored/bootstrap instance reachable.
                // Must construct in player builds when bootstrap reorders or skips registration.
                runtimeRoot = new GameObject(RuntimeRootName); // COLD ALLOC: GameObject[1] - one runtime root for tool owners per gameplay scene - owner: ToolsRuntimeInstaller

            // A resolved root can come back hidden or deactivated from an earlier scene state.
            // AddComponent on an inactive GameObject never runs Awake/OnEnable, so
            // ToolDurabilitySystem.cs:1942 would never register and all seven consumers listed above would
            // still cache null - the fix would look applied and change nothing.
            // WorldRuntimeReferenceUtility.TryResolveScenePath (WorldRuntimeReferenceUtility.cs:179)
            // resolves scene roots, so activeSelf is the whole hierarchy state here. Same handling as
            // EcosystemRuntimeInstaller.cs:63-71.
            runtimeRoot.hideFlags = HideFlags.None;
            if (!runtimeRoot.activeSelf)
                runtimeRoot.SetActive(true);

            return runtimeRoot;
        }
    }
}
