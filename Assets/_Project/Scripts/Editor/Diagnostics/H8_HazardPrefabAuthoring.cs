// ============================================================================
// HECTON-8 — H8_HazardPrefabAuthoring.cs
// Attaches the hazard emitter component to the four hazard prefabs that carry
// hazard geometry and no hazard behaviour.
//
// WHY THIS EXISTS
//   A census of the hazard content found the two halves of the feature fully
//   built and never joined:
//     • Four production prefabs are named, shaped and shipped as hazards
//       (pocket / nest / vent / support vent) and carry between zero and one
//       project scripts, none of them a hazard system. They emit nothing.
//     • HectonHazardSource is documented, in its own file header, as the
//       component you hang on prefabs ("Prikreplyaetsya k prefabam (geyzery,
//       oblomki reaktorov)") and exists on zero GameObjects in the project.
//   Nothing is missing. The authoring step between them was never taken.
//
// WHY HectonHazardSource AND NOT THE OTHER FOUR HAZARD SYSTEMS
//   The choice is not stylistic - three of the five reach nothing from these
//   prefabs, and attaching them would replace one dead system with two.
//
//   • HectonHazardSource  -> ATTACHED. Needs only a Transform. Registers a
//     radial hazard volume from OnEnable and re-resolves on service hot swap.
//     Both non-radiation destinations are reachable without touching a scene:
//       - Toxicity / Biohazard go to HectonHazardManager.Register
//         (HectonHazardSource.cs:154-161), which resolves its zone registry
//         through ResolveZoneManager (HectonHazardManager.cs:222-234) ->
//         EnvironmentRuntimeContextService, which SELF-CREATES the manager with
//         AddComponent<HazardZoneManager> (EnvironmentRuntimeContextService.cs:222).
//       - Heat goes to IThermodynamicsService.TryInjectTransientHeatSource
//         (HectonHazardSource.cs:145-152). That slot used to be permanently
//         null, but WorldRuntimeInstaller.cs:99-114 now installs
//         AbyssalThermalManager - and its own comment names this exact consumer
//         (HectonHazardSource.cs:280) and "hydrothermal vent heat" as what the
//         missing owner killed. The lane is live.
//
//   • RadiationHazardGrid -> NOT ATTACHED, and radiation is never authored
//     here. It is a world singleton, not a per-prefab component. Its
//     RegisterSource pushes onto SignalBus<RadiationSourceSignal>
//     (RadiationHazardGrid.cs:271-299), drained only by ActiveRuntimeInstance,
//     which is assigned in OnEnable (RadiationHazardGrid.cs:89, :390-392). A
//     GUID-unbounded search of Assets returns zero AddComponent<RadiationHazardGrid>
//     call sites and zero instances, so every radiation-typed source in the
//     project pushes into a buffer nobody reads. Authoring HazardType.Radiation
//     onto these prefabs would look correct and do literally nothing. That is
//     why the vents below are Heat and the pockets are Toxicity/Biohazard.
//
//   • ThermalVentRuntime -> CONDITIONALLY attached, never blindly. It is
//     presentation only: it drives Light.intensity/range and
//     DecalProjector.fadeFactor. TryRegisterLateFrame (ThermalVentRuntime.cs:196-202)
//     refuses to register unless HasPresentationTarget (:224-227) finds a Light
//     or a DecalProjector. None of the four prefabs currently contains either
//     (no !u!108 and no DecalProjector in their YAML), so attaching it today
//     produces a component that can never tick. This tool searches the
//     hierarchy including inactive objects, wires it through its public
//     ConfigureForEditor when a target exists, and otherwise declines out loud.
//     Re-run it after the art lane adds a vent light and it wires itself.
//
//   • EnvironmentalHazard -> NOT ATTACHED. It is the self-contained variant:
//     its own trigger collider, playerLayer/playerTag, UnityEvents and an
//     emission-driven indicator Renderer. These prefabs have no collider at
//     all, and stacking it on top of HectonHazardSource would register two
//     overlapping hazard volumes for one object and double the damage field.
//     One emitter per object.
//
//   • HostileFlora -> NOT ATTACHED. It is a shooting turret: projectilePrefab,
//     muzzlePoint, aimingBone, shootCooldown, projectileSpeed. A gas pocket and
//     a mineral vent are not turrets, and it has no authored projectile prefab
//     to fire. It needs a flora prefab and a projectile, not this lane.
//
// UNITS - THE TWO SCALES ARE NOT THE SAME AND MIXING THEM IS THE EASY BUG
//   HazardZoneManager path (Toxicity / Biohazard): intensity is a damage-rate
//   scale. EnvironmentalHazard.TryRegisterRadiationSource fixes the conversion
//   at `intensity = baseDamagePerSecond * 10f`, so intensity 30 reads as ~3
//   damage/second. HectonHazardManager caps it at HazardIntensityHardCap 1000.
//   Thermodynamics path (Heat): intensity is a TEMPERATURE magnitude. It is
//   forwarded straight into TemperatureChangedSignal
//   (AbyssalThermalManager.cs:1634-1652), so a value of 45 is a thermal
//   reading, not 4.5 damage/second. The Heat numbers below are degrees over an
//   abyssal ambient of a few degrees; the pocket numbers below are damage.
//
// WHY THIS TOOL IS ALLOWED TO WRITE PREFABS
//   AGENTS.md `Sandbox Firewall Rule` bans automated TEST runners and scripts
//   from PrefabUtility.SaveAsPrefabAsset / EditorUtility.SetDirty on production
//   assets. This is a deliberate authoring entry point, invoked by hand or by
//   -executeMethod, in the same class as the sanctioned
//   Editor/Authoring/StorageEndpointAuthoring.cs, and it takes the same route:
//   LoadPrefabContents -> mutate through the Unity API -> SaveAsPrefabAsset ->
//   UnloadPrefabContents. It never text-edits YAML (COMMON_SENSE #9), never
//   touches a scene, and skips the write entirely when nothing changed.
//
// BATCHMODE CONTRACT
//   No EditorUtility.DisplayDialog, no Selection, no EditorApplication.Exit, no
//   exception escapes the per-prefab loop. A missing or unopenable prefab is
//   reported and stepped over so it can never take the host job down with it.
//
//   -executeMethod Hecton8.EditorTools.Diagnostics.H8_HazardPrefabAuthoring.AttachHazardComponents
//   -executeMethod Hecton8.EditorTools.Diagnostics.H8_HazardPrefabAuthoring.ReportHazardPrefabs
// ============================================================================

using System;
using Hecton8.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Hecton8.EditorTools.Diagnostics
{
    /// <summary>
    /// Authors HectonHazardSource onto the four hazard prefabs, and wires
    /// ThermalVentRuntime on the vents when a presentation target exists.
    /// Idempotent: an existing emitter is never duplicated and its authored
    /// values are never overwritten unless they are degenerate.
    /// </summary>
    public static class H8_HazardPrefabAuthoring
    {
        private const string Marker = "[H8_HAZARD_AUTHORING]";
        private const string AttachMenuPath = "Hecton8/Authoring/Attach Hazard Components To Hazard Prefabs";
        private const string ReportMenuPath = "Hecton8/Validation/Report Hazard Prefab Authoring";

        // Serialized backing field names on HectonHazardSource.cs:29-45. The
        // fields are private with no ConfigureForEditor entry point, so
        // SerializedObject is the only supported way in. A rename must fail
        // loudly rather than silently author nothing.
        private const string TypePropertyName = "_type";
        private const string IntensityPropertyName = "_intensity";
        private const string RadiusPropertyName = "_radius";
        private const string IsStaticPropertyName = "_isStatic";
        private const string ProfilePropertyName = "_profile";

        // ThermalVentRuntime.ConfigureForEditor defaults (ThermalVentRuntime.cs:19-23).
        // The pulse shape is already tuned, so only the light range is derived
        // from the hazard, so that the visible glow matches the heat field
        // instead of contradicting it.
        private const float VentLightIntensity = 2.5f;
        private const float VentDecalFade = 0.7f;
        private const float VentPulseFrequencyHz = 0.45f;
        private const float VentPulseAmplitude = 0.18f;

        /// <summary>
        /// One authored hazard. Radius is metres. Intensity is damage-scale for
        /// the zone-manager types and temperature-scale for Heat - see the units
        /// note in the file header.
        /// </summary>
        private readonly struct HazardPlan
        {
            public readonly string PrefabPath;
            public readonly HazardType Type;
            public readonly float Intensity;
            public readonly float RadiusMeters;
            public readonly bool IsVent;
            public readonly string Rationale;

            public HazardPlan(
                string prefabPath,
                HazardType type,
                float intensity,
                float radiusMeters,
                bool isVent,
                string rationale)
            {
                PrefabPath = prefabPath;
                Type = type;
                Intensity = intensity;
                RadiusMeters = radiusMeters;
                IsVent = isVent;
                Rationale = rationale;
            }
        }

        /// <summary>
        /// The four prefabs that are shaped as hazards and emit nothing. Radii
        /// are sized off the authored geometry in each prefab, not copied from
        /// the component default of 15 m, which is wider than every one of these
        /// objects by an order of magnitude.
        /// </summary>
        private static readonly HazardPlan[] Plans =
        {
            new HazardPlan(
                "Assets/_Project/Prefabs/WorldProceduralProxy/PFB_family_pocket_hazard.prefab",
                HazardType.Toxicity,
                30f,
                6f,
                false,
                "Single-mesh gas pocket, root localScale 1.8 x 1.2 x 1.8. Chemical " +
                "seep, so Toxicity routes it to HazardZoneManager. ~3 damage/second " +
                "over a plume about three times the source width - lethal to loiter " +
                "in, survivable to cross."),

            new HazardPlan(
                "Assets/_Project/Prefabs/WorldProceduralProxy/PFB_family_pocket_hazard__nest.prefab",
                HazardType.Biohazard,
                20f,
                5f,
                false,
                "Nest variant: three clustered cube bodies, largest localScale " +
                "1.1 x 0.9 x 1.1, no scripts at all. Biological, so Biohazard - " +
                "spores and pathogens, not chemistry. Weakest of the four (~2 " +
                "damage/second) and the tightest cloud, because a nest is something " +
                "you are meant to be able to approach and salvage."),

            new HazardPlan(
                "Assets/_Project/Prefabs/WorldProceduralProxy/PFB_family_pocket_hazard__vent.prefab",
                HazardType.Heat,
                25f,
                8f,
                true,
                "Vent variant: three cylinder chimneys, tallest at localScale y=1.02 " +
                "on a 2-unit primitive, so roughly 2 m of standing chimney. Heat, " +
                "which reaches the thermal grid rather than the damage registry. 25 " +
                "is a temperature magnitude over abyssal ambient: a warm proxy " +
                "chimney, not a black smoker."),

            new HazardPlan(
                "Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_Pocket_Hazard.prefab",
                HazardType.Heat,
                45f,
                12f,
                true,
                "The hero vent - the only one of the four with a bubble-column " +
                "ParticleSystem, an LOD group and 2 m spines around a VentMass at " +
                "localScale 1.3 x 1.1 x 1.3. Hottest and widest of the set because " +
                "the art already says it is venting hard, and the plume it draws " +
                "should be backed by a field a player can measure."),
        };

        // ══════════════════════════════════════════════════════════
        //  ENTRY POINTS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Authors every plan above. Batchmode safe: each prefab is independent,
        /// a failure on one is reported and the run continues.
        /// </summary>
        [MenuItem(AttachMenuPath, priority = 219)]
        public static void AttachHazardComponents()
        {
            int wrote = 0;
            int unchanged = 0;
            int declined = 0;

            for (int i = 0; i < Plans.Length; i++)
            {
                switch (ProcessPlan(Plans[i]))
                {
                    case PlanOutcome.Wrote:
                        wrote++;
                        break;
                    case PlanOutcome.Unchanged:
                        unchanged++;
                        break;
                    default:
                        declined++;
                        break;
                }
            }

            if (wrote > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log(
                Marker + " SUMMARY " + wrote + " prefab(s) written, " + unchanged +
                " already authored, " + declined + " declined, out of " + Plans.Length +
                " planned. Static authoring only - no scene was touched and nothing here " +
                "proves the emitters register at runtime. That needs a play-mode probe " +
                "that samples HazardZoneManager and the thermal grid.");
        }

        /// <summary>
        /// Read-only state report over the same four prefabs. Writes nothing.
        /// </summary>
        [MenuItem(ReportMenuPath, priority = 219)]
        public static void ReportHazardPrefabs()
        {
            for (int i = 0; i < Plans.Length; i++)
            {
                HazardPlan plan = Plans[i];
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(plan.PrefabPath);
                if (asset == null)
                {
                    Debug.LogWarning(Marker + " REPORT MISSING " + plan.PrefabPath);
                    continue;
                }

                bool hasSource = asset.TryGetComponent(out HectonHazardSource source);
                bool hasVentRuntime = asset.TryGetComponent(out ThermalVentRuntime _);
                Light light = asset.GetComponentInChildren<Light>(true);
                DecalProjector decal = asset.GetComponentInChildren<DecalProjector>(true);

                Debug.Log(
                    Marker + " REPORT " + plan.PrefabPath +
                    "  HectonHazardSource=" + (hasSource ? DescribeSource(source) : "ABSENT") +
                    "  ThermalVentRuntime=" + (hasVentRuntime ? "present" : "absent") +
                    "  presentationTargets: light=" + (light != null ? light.name : "none") +
                    " decal=" + (decal != null ? decal.name : "none") +
                    "  plan=" + plan.Type + " intensity=" + plan.Intensity.ToString("0.##") +
                    " radius=" + plan.RadiusMeters.ToString("0.##") + "m",
                    asset);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private enum PlanOutcome
        {
            Declined,
            Unchanged,
            Wrote,
        }

        private static PlanOutcome ProcessPlan(HazardPlan plan)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(plan.PrefabPath) == null)
            {
                Debug.LogWarning(
                    Marker + " DECLINED " + plan.PrefabPath +
                    " - prefab not found at that path (moved or renamed). Nothing written, " +
                    "run continues.");
                return PlanOutcome.Declined;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(plan.PrefabPath);
            if (root == null)
            {
                Debug.LogError(
                    Marker + " DECLINED " + plan.PrefabPath +
                    " - could not be opened as prefab contents. Nothing written, run continues.");
                return PlanOutcome.Declined;
            }

            bool changed = false;
            try
            {
                changed = AuthorHazardSource(root, plan);
                changed |= AuthorVentPresentation(root, plan);

                if (!changed)
                    return PlanOutcome.Unchanged;

                EditorUtility.SetDirty(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, plan.PrefabPath) == null)
                {
                    Debug.LogError(
                        Marker + " FAILED " + plan.PrefabPath +
                        " - SaveAsPrefabAsset returned null. The prefab on disk is unchanged.");
                    return PlanOutcome.Declined;
                }

                return PlanOutcome.Wrote;
            }
            catch (Exception exception)
            {
                // One malformed prefab must not abort the other three or the host
                // batchmode job. Report and step over.
                Debug.LogError(
                    Marker + " DECLINED " + plan.PrefabPath + " - threw " +
                    exception.GetType().Name + ": " + exception.Message +
                    ". Nothing written for this prefab, run continues.");
                return PlanOutcome.Declined;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Adds the emitter when absent and authors the full plan. When it is
        /// already present the component is never duplicated
        /// (HectonHazardSource is [DisallowMultipleComponent] anyway) and
        /// authored values are left alone - EXCEPT when they are degenerate.
        /// IsValidHazardSourcePayload (HectonHazardSource.cs:204-210) rejects any
        /// non-finite or non-positive intensity or radius, and rejection is
        /// silent: the component unregisters itself and reads as fine in the
        /// inspector. Repairing that specific case is the difference between an
        /// idempotent tool and one that leaves inert components behind.
        /// </summary>
        private static bool AuthorHazardSource(GameObject root, HazardPlan plan)
        {
            bool existed = root.TryGetComponent(out HectonHazardSource source);
            if (!existed)
            {
                source = root.AddComponent<HectonHazardSource>();
                if (source == null)
                {
                    Debug.LogError(
                        Marker + " DECLINED " + plan.PrefabPath +
                        " - AddComponent<HectonHazardSource> returned null. Nothing written.");
                    return false;
                }
            }

            var serialized = new SerializedObject(source);
            SerializedProperty typeProperty = serialized.FindProperty(TypePropertyName);
            SerializedProperty intensityProperty = serialized.FindProperty(IntensityPropertyName);
            SerializedProperty radiusProperty = serialized.FindProperty(RadiusPropertyName);
            SerializedProperty isStaticProperty = serialized.FindProperty(IsStaticPropertyName);
            if (typeProperty == null ||
                intensityProperty == null ||
                radiusProperty == null ||
                isStaticProperty == null)
            {
                // A component we just added but cannot configure must not survive
                // into the save. Its inspector defaults are intensity 50, radius
                // 15 and - decisively - HazardType.Radiation, which is the one
                // route in this project that reaches no consumer at all. Leaving
                // it behind would author the exact dead emitter this file exists
                // to avoid, under the banner of a declined run.
                if (!existed)
                    UnityEngine.Object.DestroyImmediate(source, true);

                Debug.LogError(
                    Marker + " DECLINED " + plan.PrefabPath +
                    " - HectonHazardSource no longer exposes all of '" + TypePropertyName + "', '" +
                    IntensityPropertyName + "', '" + RadiusPropertyName + "', '" + IsStaticPropertyName +
                    "'. The fields were renamed; fix this tool rather than guessing. Nothing written" +
                    (existed ? string.Empty : ", and the component just added was removed again") + ".");
                return false;
            }

            if (existed)
            {
                // enumValueIndex is deliberately avoided: intValue stores the
                // serialized underlying value, which stays correct if HazardType
                // ever gets explicit numbering.
                var authoredType = (HazardType)typeProperty.intValue;
                float authoredIntensity = intensityProperty.floatValue;
                float authoredRadius = radiusProperty.floatValue;

                if (IsLiveHazardPayload(authoredIntensity, authoredRadius))
                {
                    Debug.Log(
                        Marker + " SKIP " + plan.PrefabPath +
                        " - HectonHazardSource already authored as " + authoredType + " intensity=" +
                        authoredIntensity.ToString("0.##") + " radius=" + authoredRadius.ToString("0.##") +
                        "m. Left exactly as found; this tool does not overwrite authored hazard values.");
                    return false;
                }

                Debug.LogWarning(
                    Marker + " REPAIR " + plan.PrefabPath +
                    " - HectonHazardSource was present but DEGENERATE (intensity=" +
                    authoredIntensity.ToString("0.##") + " radius=" + authoredRadius.ToString("0.##") +
                    "). IsValidHazardSourcePayload rejects that and the source silently unregisters, " +
                    "so it emitted nothing. Overwriting with the authored plan.");
            }

            typeProperty.intValue = (int)plan.Type;
            intensityProperty.floatValue = plan.Intensity;
            radiusProperty.floatValue = plan.RadiusMeters;

            // Static is correct for scattered world geometry and is not just a
            // default: with _isStatic true the source skips the SlowTick
            // registration entirely and registers once from OnEnable, so a
            // hazard field costs no per-tick CPU (COMMON_SENSE #8).
            isStaticProperty.boolValue = true;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedProperty profileProperty = serialized.FindProperty(ProfilePropertyName);
            bool hasProfile = profileProperty != null && profileProperty.objectReferenceValue != null;

            Debug.Log(
                Marker + " AUTHORED " + plan.PrefabPath + " -> HectonHazardSource " +
                (existed ? "repaired" : "added") + ": type=" + plan.Type + " intensity=" +
                plan.Intensity.ToString("0.##") + " radius=" + plan.RadiusMeters.ToString("0.##") +
                "m isStatic=true profile=" + (hasProfile ? "authored" : "none") + ". " +
                plan.Rationale,
                root);

            if (!hasProfile)
            {
                Debug.Log(
                    Marker + " NOTE " + plan.PrefabPath +
                    " - no HazardZoneProfile assigned, so ResolveHazardType falls back to the inline " +
                    "type and VisorGlitchBias stays at 1. A GUID search of Assets for the " +
                    "HazardZoneProfile script found only its own .meta, so the project owns zero " +
                    "profile assets to assign; inline values are the only option today.");
            }

            return true;
        }

        /// <summary>
        /// Wires ThermalVentRuntime on vent plans, and only when the prefab
        /// actually owns a Light or DecalProjector. Without one,
        /// HasPresentationTarget is false, TryRegisterLateFrame refuses, and the
        /// component would be dead weight - the exact failure this whole file
        /// exists to stop repeating.
        /// </summary>
        private static bool AuthorVentPresentation(GameObject root, HazardPlan plan)
        {
            if (!plan.IsVent)
                return false;

            Light keyLight = root.GetComponentInChildren<Light>(true);
            DecalProjector primaryDecal = root.GetComponentInChildren<DecalProjector>(true);
            if (keyLight == null && primaryDecal == null)
            {
                Debug.Log(
                    Marker + " DECLINED-INERT " + plan.PrefabPath +
                    " - ThermalVentRuntime NOT attached: the prefab owns no Light and no " +
                    "DecalProjector, so HasPresentationTarget (ThermalVentRuntime.cs:224-227) is " +
                    "false and TryRegisterLateFrame (:196-202) would never register it. It would be " +
                    "a component that cannot tick. Add a vent glow light to this prefab and re-run " +
                    "this tool - it will wire itself. The hazard field itself is unaffected: " +
                    "ThermalVentRuntime is presentation only.");
                return false;
            }

            if (root.TryGetComponent(out ThermalVentRuntime existingRuntime))
            {
                Debug.Log(
                    Marker + " SKIP " + plan.PrefabPath +
                    " - ThermalVentRuntime already present (validFactoryConfiguration=" +
                    existingRuntime.HasValidFactoryConfiguration + "). Not duplicated, not reconfigured.");
                return false;
            }

            ThermalVentRuntime runtime = root.AddComponent<ThermalVentRuntime>();
            if (runtime == null)
            {
                Debug.LogError(
                    Marker + " DECLINED " + plan.PrefabPath +
                    " - AddComponent<ThermalVentRuntime> returned null. Hazard source authoring above " +
                    "still stands.");
                return false;
            }

            // Metadata and the culling proxy are optional and are only passed if
            // the prefab already owns them: Awake back-fills metadata itself, and
            // a null cullingProxy simply means this component owns its own
            // enable/disable instead of deferring to external culling.
            root.TryGetComponent(out HazardMetadata metadata);
            LightCullingProxy cullingProxy = root.GetComponentInChildren<LightCullingProxy>(true);

            // Light range is tied to the hazard radius so the glow the player
            // reads and the heat field that hurts them describe the same volume.
            float lightRange = Mathf.Max(0.5f, plan.RadiusMeters);

            runtime.ConfigureForEditor(
                metadata,
                keyLight,
                primaryDecal,
                cullingProxy,
                VentLightIntensity,
                lightRange,
                VentDecalFade,
                VentPulseFrequencyHz,
                VentPulseAmplitude);

            Debug.Log(
                Marker + " AUTHORED " + plan.PrefabPath + " -> ThermalVentRuntime added: keyLight=" +
                (keyLight != null ? keyLight.name : "none") + " decal=" +
                (primaryDecal != null ? primaryDecal.name : "none") + " cullingProxy=" +
                (cullingProxy != null ? cullingProxy.name : "none") + " metadata=" +
                (metadata != null ? "bound" : "none") + " baseLightIntensity=" +
                VentLightIntensity.ToString("0.##") + " baseLightRange=" + lightRange.ToString("0.##") +
                "m (matched to hazard radius) baseDecalFade=" + VentDecalFade.ToString("0.##") +
                " pulseHz=" + VentPulseFrequencyHz.ToString("0.##") + " pulseAmplitude=" +
                VentPulseAmplitude.ToString("0.##") + ".",
                root);

            return true;
        }

        /// <summary>
        /// Mirrors HectonHazardSource.IsValidHazardSourcePayload
        /// (HectonHazardSource.cs:204-210). Anything this rejects is a source
        /// that unregisters itself without complaining.
        /// </summary>
        private static bool IsLiveHazardPayload(float intensity, float radiusMeters)
        {
            return !float.IsNaN(intensity) &&
                   !float.IsInfinity(intensity) &&
                   intensity > 0f &&
                   !float.IsNaN(radiusMeters) &&
                   !float.IsInfinity(radiusMeters) &&
                   radiusMeters > 0f;
        }

        private static string DescribeSource(HectonHazardSource source)
        {
            if (source == null)
                return "ABSENT";

            var serialized = new SerializedObject(source);
            SerializedProperty typeProperty = serialized.FindProperty(TypePropertyName);
            SerializedProperty intensityProperty = serialized.FindProperty(IntensityPropertyName);
            SerializedProperty radiusProperty = serialized.FindProperty(RadiusPropertyName);
            if (typeProperty == null || intensityProperty == null || radiusProperty == null)
                return "present (serialized fields renamed - tool needs updating)";

            float intensity = intensityProperty.floatValue;
            float radius = radiusProperty.floatValue;
            return (HazardType)typeProperty.intValue + " intensity=" + intensity.ToString("0.##") +
                   " radius=" + radius.ToString("0.##") + "m live=" +
                   (IsLiveHazardPayload(intensity, radius) ? "yes" : "NO (silently unregisters)");
        }
    }
}
