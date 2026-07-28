using Hecton8.Caves;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Cold-path installer for the scene-level world domain owners.
    /// </summary>
    /// <remarks>
    /// The world domain was the only gameplay domain with no runtime-construction path.
    /// GameBootstrapper.PublishPlayerRuntimeReference calls an installer for meta, economy, ecosystem,
    /// PDA, progression, narrative and atmospheric audio, so when 02_HECTON_WORLD lost its authored
    /// owners every one of those domains rebuilt itself and the world domain did not. A headless probe
    /// of the running world reported instances=0 for HectonVoxelEngine, HectonFluidEngine and
    /// FloraInteractionManager, which means GlobalRegistry.VoxelEngine, GlobalRegistry.Fluid and the
    /// wake-displacement service were null for the whole session while every consumer of them
    /// degraded silently rather than failing.
    ///
    /// This installer restores the three owners a bare runtime root can genuinely stand up. It does
    /// NOT install HectonWorldGenerator, HectonIndirectVegetationRenderer or GpuScatterLodManager -
    /// each of those needs authored assets or an authored producer that no runtime root can supply,
    /// and the per-system proof is recorded at the call sites below rather than summarised here.
    /// </remarks>
    public static class WorldRuntimeInstaller
    {
        private const string RuntimeRootName = "__HECTON_WORLD_RUNTIME";
        private const string FluidRuntimeRootName = "__HECTON_WORLD_FLUID_RUNTIME";

        /// <summary>
        /// Ensures the voxel volume owner, its carve/delta owner, the fluid owner and the flora
        /// wake-displacement owner exist and are live in the loaded gameplay scene.
        /// </summary>
        public static void EnsureRuntimeSystems()
        {
            // Fluid first. HectonFluidEngine carries [DefaultExecutionOrder(-5000)]
            // (HectonFluidEngine.cs:192), the earliest of anything installed here, and it publishes
            // the water level and ambient current read models that the voxel and flora owners sample.
            EnsureFluidRuntime();
            EnsureWorldVolumeRuntime();
        }

        /// <summary>
        /// Installs the fluid simulation owner on a root of its own.
        /// </summary>
        private static void EnsureFluidRuntime()
        {
            // GlobalRegistry.Fluid (GlobalRegistry.cs:1546) is checked before the install rather than
            // after, because HectonFluidEngine does not merely decline to register when another owner
            // holds the slot - it calls Destroy(gameObject), not Destroy(this), at
            // HectonFluidEngine.cs:2100-2105 in Awake and again at :2225-2229 in OnEnable.
            if (GlobalRegistry.Fluid != null)
                return;

            // The engine also gets a root of its own for the same reason. AddComponent runs Awake and
            // OnEnable synchronously at the call site, so a self-destruct on the shared world root
            // would take HectonVoxelEngine, VoxelDeltaProcessor and FloraInteractionManager down with
            // it - at end of frame, after this method has already returned cleanly, with no
            // exception and no error line naming the cause. The registry guard above should make that
            // unreachable; a separate root makes it harmless if the guard is ever wrong.
            GameObject fluidRuntimeRoot = ResolveOrCreateRuntimeRoot(FluidRuntimeRootName);
            ActivateRuntimeRoot(fluidRuntimeRoot);

            if (!fluidRuntimeRoot.TryGetComponent<Hecton8.Physics.HectonFluidEngine>(out _))
                fluidRuntimeRoot.AddComponent<Hecton8.Physics.HectonFluidEngine>();
        }

        /// <summary>
        /// Installs the localized voxel volume owner, its carve/delta owner and the flora
        /// wake-displacement owner on the shared world runtime root.
        /// </summary>
        private static void EnsureWorldVolumeRuntime()
        {
            GameObject runtimeRoot = ResolveOrCreateRuntimeRoot(RuntimeRootName);

            // Two guards per owner, and both are load-bearing.
            //
            // The published-instance guard: HectonVoxelEngine claims a static ActiveRuntimeInstance at
            // HectonVoxelEngine.cs:6015 and FloraInteractionManager claims its own at
            // FloraInteractionManager.cs:1751 and :1865 - unconditionally, and the flora owner also
            // rebinds DroneFleetManager to itself. Neither coexists with a scene-authored peer; a
            // second instance silently demotes the first. So a live published owner means no install
            // is wanted at all, not that this root needs one.
            //
            // The TryGetComponent guard: HectonVoxelEngine declares no [DisallowMultipleComponent], so
            // the published-instance guard alone would double-install onto a root that already carries
            // a deactivated engine - TryResolveVoxelEngine filters on isActiveAndEnabled and would
            // report it as absent.
            HectonVoxelEngine publishedVoxelEngine = null;
            bool installVoxelEngine =
                !WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref publishedVoxelEngine) &&
                !runtimeRoot.TryGetComponent<HectonVoxelEngine>(out _);

            FloraInteractionManager publishedFloraInteractionManager = null;
            bool installFloraInteractionManager =
                !WorldRuntimeReferenceUtility.TryResolveFloraInteractionManager(ref publishedFloraInteractionManager) &&
                !runtimeRoot.TryGetComponent<FloraInteractionManager>(out _);

            // AbyssalThermalManager is the project's only IThermodynamicsService and was never constructed
            // by anything, so depth cold, hydrothermal vent heat and laser-cutter heat events had no
            // gameplay effect at all. Eight live consumers already cache the slot and got null:
            // HectonSurvivalSystem.cs:741, HectonPlayerMovement.cs:4218, FaunaDirector.cs:822,
            // DebrisManager.cs:1069, EnvironmentalHazard.cs:288, HectonHazardSource.cs:280,
            // HectonFluidEngine.cs:2512, CrashTelemetryBuffer.cs:2672.
            //
            // GameBootstrapper.TryEnsureThermodynamicsRegistryCoverage cannot rescue it: that helper
            // bridges the concrete Thermodynamics slot into the ThermodynamicsService slot, and both are
            // empty when no owner exists. It reads, it never constructs.
            //
            // Safe as a sibling on this root, verified rather than assumed: zero Destroy(gameObject) in
            // AbyssalThermalManager.cs, so it cannot take the root down the way HectonFluidEngine would.
            // It carries [DisallowMultipleComponent] and [DefaultExecutionOrder(-102)], which places it
            // between FloraInteractionManager's -105 and the voxel pair's implicit 0.
            bool installAbyssalThermalManager = !runtimeRoot.TryGetComponent<AbyssalThermalManager>(out _);

            // WorldChunkResidencyManager is deliberately NOT installed here even though it is the only
            // IStreamingBackpressureService and its slot is read by a live prologue consumer.
            //
            // Its slot is GlobalRegistryServiceSlot.StreamingBackpressureRuntime, which
            // GlobalRegistry.IsSceneRuntimeHotSwapSlot HARD-DENIES (GlobalRegistry.cs:7182). The
            // publication gate this installer runs inside cannot issue a token for a denied slot, so its
            // OnEnable registration at WorldChunkResidencyManager.cs:2491 would throw
            // CriticalBootException - and because the installer calls run in sequence with no try/catch
            // between them, that throw would abort every installer after it. Adding it here is strictly
            // worse than leaving it out. It needs a pre-Ready bootstrap lane, not this one.
            int deferredActivationOwnerCount =
                (installVoxelEngine ? 1 : 0) +
                (installFloraInteractionManager ? 1 : 0) +
                (installAbyssalThermalManager ? 1 : 0);

            if (installVoxelEngine || deferredActivationOwnerCount > 1)
            {
                // The voxel pair is installed against a deactivated root, then activated once below.
                //
                // HectonVoxelEngine.OnEnable caches its sibling carve owner with
                // TryGetComponent(out _deltaProcessor) at HectonVoxelEngine.cs:6039 and has no second
                // path to it: _deltaProcessor is a [SerializeField] at :5559 exposed only through an
                // internal getter at :5561, with no setter. A VoxelDeltaProcessor added after that
                // OnEnable is therefore never seen, the engine logs the missing-authored-processor
                // error at :6041-6046, and runtime voxel carving plus delta-save replay stay dead at
                // :10479 and :10535 while the engine itself looks healthy.
                //
                // Adding the processor first does not fix it either: its
                // [RequireComponent(typeof(HectonVoxelEngine))] at VoxelDeltaProcessor.cs:70 makes
                // Unity insert the engine as a dependency BEFORE the processor exists, so the engine's
                // OnEnable still misses it. Deferred activation is the only ordering that satisfies
                // both directions - every component exists before any Awake or OnEnable runs.
                //
                // It is also what makes [DefaultExecutionOrder] mean anything here. AddComponent onto
                // a live GameObject runs Awake and OnEnable at the call site and ignores execution
                // order entirely; activation runs them in attribute order, which is what puts
                // FloraInteractionManager's -105 (FloraInteractionManager.cs:34) ahead of the voxel
                // pair's implicit 0 when both install in the same pass.
                runtimeRoot.SetActive(false);
            }
            else
            {
                // Exactly one owner installs and it is not the voxel pair, so there is nothing to order
                // against and nothing that resolves a sibling. FloraInteractionManager's consumers reach
                // it through GlobalRegistry's wake-displacement slot (FloraInteractionManager.cs:1872) or
                // the static at :1701; AbyssalThermalManager has zero GetComponent/TryGetComponent calls
                // and resolves everything through GlobalRegistry. Deactivating a live root to add one
                // component would fire OnDisable on whatever is already running, and on the voxel engine
                // that means TeardownRuntimeState disposing native streaming scratch for nothing.
                ActivateRuntimeRoot(runtimeRoot);
            }

            if (installFloraInteractionManager)
                runtimeRoot.AddComponent<FloraInteractionManager>();

            if (installAbyssalThermalManager)
                runtimeRoot.AddComponent<AbyssalThermalManager>();

            if (installVoxelEngine)
            {
                runtimeRoot.AddComponent<HectonVoxelEngine>();

                if (!runtimeRoot.TryGetComponent<VoxelDeltaProcessor>(out _))
                    runtimeRoot.AddComponent<VoxelDeltaProcessor>();
            }

            ActivateRuntimeRoot(runtimeRoot);
            RewireVoxelRuntimeReferences(runtimeRoot);

            // HectonWorldGenerator is deliberately NOT installed here, and wiring its voxelEngine
            // field would not revive caves or rifts.
            //
            // Its cave and rift spawns at HectonWorldGenerator.cs:2131 and :2151 live inside
            // SpawnVoxelPOIs, which is reachable only from the generator's own legacy terrain-chunk
            // mesh pipeline - it consumes that pipeline's vertex and cave-mask NativeArrays. That
            // pipeline only starts when StartStreaming passes its viewer check at :1103-1109, and
            // `viewer` (:613) has no runtime self-heal. Wiring it would stream a second terrain
            // surface, with a null terrainMaterial (:636, consumed at :1944 and :2740), directly over
            // MapMagic's - which GameBootstrapper.StartWorldGeneration:7527-7529 exists to prevent by
            // returning early whenever GlobalRegistry.Terrain is available.
            //
            // Leaving `viewer` null instead keeps the streamer off but is not free either: OnEnable
            // still registers the generator as IWorldSeedProvider at :944 and publishes its static
            // seed at :965-970. Registration runs inside the caller's publication gate, so
            // GlobalRegistry.Register takes the Interlocked.Exchange branch at GlobalRegistry.cs:7342
            // and hijacks the slot outright, and FaunaGeneticsManager yields to an initialised
            // incumbent at FaunaGeneticsManager.cs:185-187 rather than taking it back. The world seed
            // would then come from ComputeRuntimeWorldSeed (:517-532) hashing the inline noise
            // defaults at :598-610 - a per-build constant - replacing FaunaGeneticsManager's per-run
            // seed on a surface SaveManager.ValidateRuntimeWorldSeed reads.
            //
            // The MapMagic-era voxel consumers do not need the generator. WorldCaveDirector
            // (WorldCaveDirector.cs:1106-1107) and WorldGenerativeGeologyIntegrationDirector
            // (:1035) resolve the engine through WorldRuntimeReferenceUtility.TryResolveVoxelEngine,
            // and all three of them - including WorldGenerativeGeologyVoxelBridgeDirector, which owns
            // the live cave generation calls at :1227 and :1235 - also rebind from the registry
            // hot-swap callback (WorldCaveDirector.cs:393, WorldGenerativeGeologyIntegrationDirector.cs:164,
            // WorldGenerativeGeologyVoxelBridgeDirector.cs:447). Publishing the engine above is what
            // reaches them; the generator is not on that path.
        }

        /// <summary>
        /// Re-attempts the runtime references the voxel owner cannot resolve for itself.
        /// </summary>
        /// <param name="runtimeRoot">Shared world runtime root that hosts the voxel owner.</param>
        private static void RewireVoxelRuntimeReferences(GameObject runtimeRoot)
        {
            if (!runtimeRoot.TryGetComponent(out HectonVoxelEngine voxelEngine))
                return;

            if (voxelEngine.mapMagicBridge != null)
                return;

            // Re-attempted on every pass rather than only at install, because the bridge can come up
            // after this installer's first pass. MapMagicBridge carries [DefaultExecutionOrder(-7000)]
            // (MapMagicBridge.cs:977) and publishes itself from MapMagicRuntimeBridge.cs:363, and the
            // caller runs this block more than once per activation.
            //
            // Resolution goes through the base type on purpose. MapMagicRuntimeBridge lives in the
            // Hecton8.Plugins assembly, which references Hecton8.Core and not the reverse, so it
            // cannot be named from here at all - but it derives from MapMagicBridge
            // (MapMagicRuntimeBridge.cs:57), which is the exact type of the field
            // (HectonVoxelEngine.cs:5001), and TryResolveMapMagicBridge returns that base
            // (WorldRuntimeReferenceUtility.cs:424-427). The assignment needs no cast.
            //
            // Left null when nothing is live yet. The engine then logs "[HectonVoxel] No
            // MapMagicBridge assigned!" and returns null from every height query at :6347, :6790 and
            // :7011, which disables the voxel-to-terrain stitching contract in
            // VoxelSeamDirector.cs:12-16 loudly instead of stitching against a fabricated surface.
            MapMagicBridge mapMagicBridge = null;
            if (WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge))
                voxelEngine.mapMagicBridge = mapMagicBridge;
        }

        /// <summary>
        /// Resolves a world runtime root across loaded scenes, creating it when no scene owns one.
        /// </summary>
        /// <param name="runtimeRootName">Scene root name owned by this installer.</param>
        /// <returns>Runtime root with editor hide flags cleared. Activation is the caller's call.</returns>
        private static GameObject ResolveOrCreateRuntimeRoot(string runtimeRootName)
        {
            GameObject runtimeRoot = null;
            WorldRuntimeReferenceUtility.TryResolveScenePath(ref runtimeRoot, runtimeRootName);
            if (runtimeRoot == null)
                runtimeRoot = new GameObject(runtimeRootName); // COLD ALLOC: GameObject[1] - one runtime root per world owner group per gameplay scene - owner: WorldRuntimeInstaller

            // A resolved root can come back hidden from an earlier scene state. Resolution is by scene
            // path through WorldRuntimeReferenceUtility rather than GameObject.Find, which skips
            // inactive objects and produced the duplicate-root defect in WorldRuntimeBootstrapAuthoring.
            runtimeRoot.hideFlags = HideFlags.None;
            return runtimeRoot;
        }

        /// <summary>
        /// Guarantees a runtime root is live so its owners actually run Awake and OnEnable.
        /// </summary>
        /// <param name="runtimeRoot">Runtime root to activate.</param>
        private static void ActivateRuntimeRoot(GameObject runtimeRoot)
        {
            // A root left deactivated is the whole defect this installer exists to fix: the owners
            // would exist, report a live instance count, and never reach the registry. These roots are
            // scene roots, so activeSelf is the entire hierarchy state. Same handling as
            // EcosystemRuntimeInstaller.cs:69-71 and PrologueOrbitSceneBootstrap.cs:186-193.
            if (!runtimeRoot.activeSelf)
                runtimeRoot.SetActive(true);
        }
    }
}
