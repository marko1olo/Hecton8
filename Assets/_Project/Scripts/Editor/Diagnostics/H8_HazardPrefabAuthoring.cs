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
//   • ThermalVentRuntime -> attached to the vents, and the presentation target
//     it needs is now authored HERE rather than waited on. It is presentation
//     only: it drives Light.intensity/range and DecalProjector.fadeFactor.
//     TryRegisterLateFrame (ThermalVentRuntime.cs:196-202) refuses to register
//     unless HasPresentationTarget (:224-227) finds a Light or a DecalProjector.
//     Neither vent prefab contained either (no !u!108, no DecalProjector), which
//     is why the previous run of this tool logged DECLINED-INERT twice. The tool
//     now authors the vent glow Light itself (see VENT GLOW LIGHT below), so the
//     runtime wires on the same pass instead of waiting for an art lane.
//
//   • LightCullingProxy -> NOT attached, and the decline is load-bearing rather
//     than lazy. Its LateFrameTick computes
//     `shouldEnable = quality > minimumQualityWeight && hasPlayer`
//     (LightCullingProxy.cs:98-101), so an unresolved IPlayerRuntimeContext
//     force-disables the Light forever. It also owns light.intensity/range when
//     managePresentationScalars is true, which is the same pair ThermalVentRuntime
//     writes - two ILateFrameTickables fighting over one Light. And
//     ThermalVentRuntime.LateFrameTick deliberately stops enabling the Light at all
//     once cullingProxy != null (ThermalVentRuntime.cs:135-136 - the
//     `!hasExternalCulling` guard), handing the enable decision to the proxy.
//     Stacking it would put a second independently-inertable gate in front of a
//     glow that has never once been visible. One presentation owner per Light,
//     for the same reason as one emitter per object. Add it later, deliberately,
//     against a measured GPU cost.
//
//   • HazardMetadata -> NOT attached. ThermalVentRuntime only uses it for
//     ResolvePresentationPhase (:229-232), a `HazardHash & 3` frame-stagger, and
//     Awake back-fills it when absent. Its real surface is a DamageRouter, a
//     VfxAnchorBinding[] and an effectHash that has to match a VFX registry
//     (HazardMetadata.cs:42-51). Attaching it with a zero hash and no router to
//     buy one frame of stagger is exactly the "looks configured, does nothing"
//     object this file exists to avoid. The cost is that both vents phase on 0.
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
// VENT GLOW LIGHT - WHY POINT, WHY WARM, WHY THESE NUMBERS
//   Pipeline first: GraphicsSettings.m_CustomRenderPipeline resolves to
//   Assets/_Project/Data/URP_Medium (PC_RPAsset).asset, so this is URP 17.5.0
//   with m_AdditionalLightsRenderingMode: 1 (per pixel),
//   m_AdditionalLightsPerObjectLimit: 2 and m_AdditionalLightShadowsSupported: 1.
//   A realtime Point light therefore renders per pixel and, left on Auto, would
//   claim an additional-light shadow slice. So the authored Light is Point,
//   Realtime, shadows None, bounce 0. ThermalVentRuntime.EnforceLightPolicy
//   (:190-194) also forces shadows off, but authoring it means the asset on disk
//   is already correct and the editor preview never pays for a shadow map.
//
//   Intensity is in candela: every authored Light in this project serialises
//   m_LightUnit: 1 (UnityEngine.LightUnit.Candela), which is the engine default
//   here rather than a hand edit, so this tool does not touch the unit and only
//   logs it back. The project's own calibration point is
//   PFB_FieldBeacon_Runtime.prefab: a Point light at 1.6 candela, range 4, colour
//   (0.25, 1, 0.95). A vent glow is brighter than a navigation marker but is not
//   a floodlight, so the authored range is 1.6..3.2 candela across the heat span.
//
//   RANGE IS MEASURED, NOT COPIED FROM THE HAZARD RADIUS. The earlier revision
//   passed plan.RadiusMeters straight through as baseLightRange. That is wrong
//   twice over. First, ThermalVentRuntime multiplies it: at the default
//   GlobalQualityWeight of 1f (HomeostasisBrain.ScalabilityDictator.cs:287) the
//   quality curve is 1 and targetRange = baseLightRange * Mathf.Lerp(0.7, 1.2, 1)
//   = baseLightRange * 1.2 (:142), so radius 12 would have produced a 14.4 m
//   light. Second, the hazard radius is a DAMAGE volume; a glow that spans it
//   reads as a debug sphere and floods the basin the vent is supposed to punctuate.
//   The range here is derived from the prefab's own measured geometry instead, and
//   deliberately lands well inside the damage radius so the light never
//   impersonates the field.
//
//   The measurement is taken from MeshFilter.sharedMesh.bounds transformed into
//   root space - not Renderer.bounds, which is unreliable for the inactive LOD1 and
//   LOD2 renderers on the hero vent. The plume origin prefers the highest
//   ParticleSystem in the hierarchy, because on the hero vent the tallest MESH is
//   a decorative spine tip at ~2.6 m while VentBubbleColumn_Main marks the actual
//   mouth at y 1.16. Where no ParticleSystem exists (the small vent) the mouth is
//   the top of the mesh bounds, pulled slightly inside so the chimney lip is lit
//   instead of open water.
//
//   Calibration check that this derivation is not arbitrary: the pulse it
//   computes for the 25-magnitude small vent is 0.45 Hz at amplitude 0.18, which
//   reproduces the two hand-tuned constants the previous revision hard-coded. The
//   hero vent extrapolates to 0.41 Hz / 0.215 - a bigger vent breathing slower and
//   deeper. Colour follows the same heat span, amber toward pale amber, because a
//   hotter body reads whiter; it stays warm rather than the project's cyan beacon
//   language, and it agrees with the Heat = Color.yellow gizmo this project
//   already draws (HectonHazardSource.cs:319-325). Caveat worth stating: seawater
//   absorbs long wavelengths fastest, so an amber source will read desaturated
//   in-game at range. That is an argument for keeping it saturated at the source,
//   which it is, not for making the vents blue.
//
// HAZARD ZONE PROFILES - TWO ASSETS, NOT FOUR, AND WHY THAT IS THE POINT
//   The project owned zero HazardZoneProfile assets, so _profile was null on all
//   four sources and ResolveHazardType fell back to the inline type. Creating
//   profiles is not free of risk: ResolveHazardType (HectonHazardSource.cs:175-178)
//   returns `_profile != null ? _profile.HazardType : _type`, so an assigned
//   profile OVERRIDES the inline type - and HazardZoneProfile's own default for
//   that field is HazardType.Radiation (HazardZoneProfile.cs:15), the one route in
//   this project that reaches no consumer. A carelessly authored profile does not
//   merely fail to help, it silently kills the hazard it is attached to. Every
//   assignment below is therefore interlocked against the plan's type.
//
//   Only the POCKETS get a profile. The Heat branch
//   (HectonHazardSource.cs:145-152) calls
//   IThermodynamicsService.TryInjectTransientHeatSource(position, radius,
//   intensity, sourceId) - the signature (GlobalRegistryContracts.cs:3621) takes
//   no profile, no glitch bias and no curve. So on a vent, every profile field
//   except HazardType is inert, and HazardType would only restate what _type
//   already says while adding a second place it can drift to the dead Radiation
//   default. A profile that changes nothing but looks configured is worse than
//   none, so the vents get an explicit logged refusal instead.
//
//   What the pocket profiles actually change, field by field:
//     • hazardType - decisive, consumed by ResolveHazardType. Authored to match
//       the plan exactly, and verified again at assignment time.
//     • intensityCurve / bakedIntensityLut - consumed. HazardZoneManager
//       .WriteVolumeCurveLut (:2917-2939) copies the baked LUT into the job's
//       native buffer and EvaluateAabbSphereContribution (:221-227) multiplies
//       intensity by it. THE AXIS IS SQUARED AND THIS IS THE EASY BUG: the profile
//       bakes lut[i] from curve.Evaluate(i / 63f) (HazardZoneProfile.cs:67-82),
//       but the runtime indexes that same table by normalised distance SQUARED
//       (HazardZoneManager.cs:230-254, fed from :221). Effective attenuation is
//       therefore curve.Evaluate(d * d), so a knee wanted at d = 0.55 must be
//       authored at x = 0.30. The engine's no-profile default is (1 - d²)²,
//       i.e. curve(x) = (1 - x)²; both curves below are measured against that.
//     • visorGlitchBias - consumed on the zone-manager path only. It rides
//       HazardVolumeData.VisorGlitchBias at FieldOffset(40) into the exposure job
//       and is max-folded per hazard type (HazardZoneManager.cs:127, :140).
//       Clamped to 0..2 by ClampGlitchBias (:3253-3256).
//     • acousticDreadID - DEAD. A whole-project search finds exactly one
//       reference, its own getter (HazardZoneProfile.cs:41). It is left at 0 on
//       purpose: writing a hand-picked id into a field nobody reads is the same
//       fake configuration this section refuses elsewhere.
//
// TWO DIFFERENT WRITE RISK CLASSES, NAMED PER CASE
//   • AssetDatabase.CreateAsset for the two profiles creates NEW files under
//     Assets/_Project/Data/World/HazardZoneProfiles. It cannot overwrite or wipe
//     anything: an existing asset at the same path is loaded and left untouched,
//     never rewritten, so a designer's edits to a profile always win.
//   • Assigning _profile, adding HectonHazardSource / ThermalVentRuntime and
//     adding the glow Light child all REWRITE a shipped production prefab through
//     LoadPrefabContents -> SaveAsPrefabAsset. That is the higher risk class and
//     it is why the whole apply path sits behind the opt-in flag below.
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
    /// Authors HectonHazardSource onto the four hazard prefabs, creates the two
    /// HazardZoneProfile assets the pockets need and assigns them, adds the vent
    /// glow Light the vents were missing, and wires ThermalVentRuntime on top of it.
    /// Idempotent throughout: an existing emitter, profile reference, Light or vent
    /// runtime is reported and skipped, never duplicated, and authored values are
    /// never overwritten unless they are degenerate. A second run writes nothing.
    /// </summary>
    public static class H8_HazardPrefabAuthoring
    {
        private const string Marker = "[H8_HAZARD_AUTHORING]";
        private const string AttachMenuPath = "Hecton8/Authoring/Attach Hazard Components To Hazard Prefabs";
        private const string ReportMenuPath = "Hecton8/Validation/Report Hazard Prefab Authoring";

        // Opt-in flag for the batchmode entry point. Naming matches the established convention in
        // H8_ScatterPlacementOwnerEnableAuthoring.cs:122 (-h8ApplyScatterOwnerEnable) on purpose, so the
        // pattern is learned once rather than per tool. See AuthorHazardComponentsFromCommandLine.
        private const string ApplyFlag = "-h8ApplyHazardComponents";

        // Serialized backing field names on HectonHazardSource.cs:29-45. The
        // fields are private with no ConfigureForEditor entry point, so
        // SerializedObject is the only supported way in. A rename must fail
        // loudly rather than silently author nothing.
        private const string TypePropertyName = "_type";
        private const string IntensityPropertyName = "_intensity";
        private const string RadiusPropertyName = "_radius";
        private const string IsStaticPropertyName = "_isStatic";
        private const string ProfilePropertyName = "_profile";

        // Serialized backing field names on HazardZoneProfile.cs:15-26. Note the
        // absence of the underscore prefix used by HectonHazardSource - these are
        // two different authors' conventions in the same feature and mixing them
        // up produces a null SerializedProperty, not a compile error.
        private const string ProfileHazardTypePropertyName = "hazardType";
        private const string ProfileIntensityCurvePropertyName = "intensityCurve";
        private const string ProfileVisorGlitchBiasPropertyName = "visorGlitchBias";
        private const string ProfileBakedLutPropertyName = "bakedIntensityLut";

        private const string ProfileFolderParent = "Assets/_Project/Data/World";
        private const string ProfileFolderName = "HazardZoneProfiles";
        private const string ProfileFolder = ProfileFolderParent + "/" + ProfileFolderName;

        // Naming follows the type's own CreateAssetMenu fileName hint,
        // "HazardZoneProfile_" (HazardZoneProfile.cs:8), so a hand-created asset and
        // a tool-created one are indistinguishable in the project window.
        private const string ChemicalSeepProfilePath =
            ProfileFolder + "/HazardZoneProfile_ChemicalSeep.asset";

        private const string SporeNestProfilePath =
            ProfileFolder + "/HazardZoneProfile_SporeNest.asset";

        // ThermalVentRuntime.ConfigureForEditor (ThermalVentRuntime.cs:37-59).
        // baseDecalFade is inert while no DecalProjector exists but is passed
        // through anyway so a later decal lane inherits a sane value instead of 0.
        private const float VentDecalFade = 0.7f;

        // Every light value below is derived, not typed in. These are the
        // endpoints of the derivation and the reference magnitude it normalises
        // against; see VENT GLOW LIGHT in the file header for the justification.
        // 60 is the reference "black smoker" heat magnitude: the authored vents at
        // 25 and 45 land at 0.42 and 0.75 of it, which is what makes them read as
        // a warm chimney and a hard-venting hero rather than two of the same thing.
        private const float VentHeatReferenceMagnitude = 60f;
        private const float VentGlowMinIntensityCandela = 1.6f;
        private const float VentGlowMaxIntensityCandela = 3.2f;
        private const float VentPulseFrequencyHzCool = 0.5f;
        private const float VentPulseFrequencyHzHot = 0.38f;
        private const float VentPulseAmplitudeCool = 0.14f;
        private const float VentPulseAmplitudeHot = 0.24f;

        // ThermalVentRuntime.LateFrameTick:142 applies
        // Mathf.Lerp(0.7f, 1.2f, curve) to baseLightRange, and the quality curve
        // is 1 at the default GlobalQualityWeight of 1f. Divide the range the vent
        // should actually project by this to get the value to author.
        private const float VentRangeGainAtFullQuality = 1.2f;
        private const float VentRuntimeRangeMinMeters = 3f;
        private const float VentRuntimeRangeMaxMeters = 9f;
        private const float VentMouthInsetMaxMeters = 0.35f;
        private const float VentMouthInsetHeightFraction = 0.08f;

        // Used only when a vent prefab contains no measurable mesh at all, so the
        // fallback is visible in the log rather than silently indistinguishable
        // from a measurement.
        private const float VentFallbackStandingHeightMeters = 2f;
        private const float VentFallbackHorizontalRadiusMeters = 0.5f;

        private const string VentGlowLightObjectName = "VentGlow_Light";

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

            /// <summary>
            /// Asset path of the profile this plan should own, or empty when a
            /// profile is deliberately refused. Empty is a decision, not a gap:
            /// AuthorHazardProfile logs the full reason as REFUSED-PROFILE.
            /// </summary>
            public readonly string ProfileAssetPath;

            public readonly string Rationale;

            public HazardPlan(
                string prefabPath,
                HazardType type,
                float intensity,
                float radiusMeters,
                bool isVent,
                string profileAssetPath,
                string rationale)
            {
                PrefabPath = prefabPath;
                Type = type;
                Intensity = intensity;
                RadiusMeters = radiusMeters;
                IsVent = isVent;
                ProfileAssetPath = profileAssetPath;
                Rationale = rationale;
            }
        }

        /// <summary>
        /// One authored HazardZoneProfile asset. CurveTimes/CurveValues are a
        /// polyline in the profile curve's own x space, which the runtime samples
        /// at normalised-distance SQUARED - see the header. Keep them the same
        /// length and strictly ascending in x.
        /// </summary>
        private readonly struct HazardProfilePlan
        {
            public readonly string AssetPath;
            public readonly HazardType Type;
            public readonly float VisorGlitchBias;
            public readonly float[] CurveTimes;
            public readonly float[] CurveValues;
            public readonly string Rationale;

            public HazardProfilePlan(
                string assetPath,
                HazardType type,
                float visorGlitchBias,
                float[] curveTimes,
                float[] curveValues,
                string rationale)
            {
                AssetPath = assetPath;
                Type = type;
                VisorGlitchBias = visorGlitchBias;
                CurveTimes = curveTimes;
                CurveValues = curveValues;
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
                ChemicalSeepProfilePath,
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
                SporeNestProfilePath,
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
                string.Empty,
                "Vent variant: three cylinder segments, ALL siblings of the root " +
                "(m_Father 8053698375052014182), stacked at localPosition y=1, 3.3 " +
                "and 5.3 with localScale y=1.02, 0.78 and 0.57 on 2-unit primitives. " +
                "That is a tapering spire reaching y~5.87, NOT the ~2 m this file " +
                "claimed before - the earlier pass measured the tallest single " +
                "segment and read it as the whole object. Heat, which reaches the " +
                "thermal grid rather than the damage registry. 25 is a temperature " +
                "magnitude over abyssal ambient: a warm proxy chimney, not a black " +
                "smoker. Worth noting for a later lane: the hazard sphere is centred " +
                "on the ROOT, so the plume mouth at y~5.87 sits at 73% of the 8 m " +
                "radius where (1-d^2)^2 has already fallen to ~0.21 of peak."),

            new HazardPlan(
                "Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_Pocket_Hazard.prefab",
                HazardType.Heat,
                45f,
                12f,
                true,
                string.Empty,
                "The hero vent - the only one of the four with a bubble-column " +
                "ParticleSystem, an LOD group and 2 m spines around a VentMass at " +
                "localScale 1.3 x 1.1 x 1.3. Hottest and widest of the set because " +
                "the art already says it is venting hard, and the plume it draws " +
                "should be backed by a field a player can measure. Unlike the small " +
                "vent it is SQUAT: VentBubbleColumn_Main sits at y 1.16 and the " +
                "tallest mesh is a decorative spine tip at ~2.6 m, so its glow is " +
                "hotter and tighter rather than taller."),
        };

        /// <summary>
        /// The two profile assets this tool owns. Both curves are authored in the
        /// profile's own x space, which the runtime samples at normalised distance
        /// SQUARED, so a knee wanted at distance d belongs at x = d * d. They are
        /// deliberately OPPOSITE shapes: the seep is a plateau with a cliff, the
        /// nest is a hot core that dies almost immediately.
        /// </summary>
        private static readonly HazardProfilePlan[] ProfilePlans =
        {
            new HazardProfilePlan(
                ChemicalSeepProfilePath,
                HazardType.Toxicity,
                1.65f,
                new[] { 0f, 0.30f, 0.70f, 1f },
                new[] { 1f, 0.96f, 0.42f, 0f },
                "Chemical seep: a dense cloud with an edge, not a point source. The " +
                "plateau to x=0.30 is distance 0.55 of the radius, so the gas stays " +
                "near full strength through the body of the plume and then falls off " +
                "a shoulder to exactly 0 at the rim. Measured against the engine " +
                "default of (1-x)^2 this roughly DOUBLES exposure mid-volume (0.96 vs " +
                "0.49 at d=0.55) and is ~4.7x at d=0.84 (0.42 vs 0.09). That is what " +
                "makes the authored '~3 damage/second, lethal to loiter in, " +
                "survivable to cross' true: under the default falloff most of the " +
                "6 m volume was harmless. VisorGlitchBias 1.65 of a possible 2 - " +
                "chemical vapour etches and refracts across the faceplate, and this " +
                "is the worst optical offender of the four."),

            new HazardProfilePlan(
                SporeNestProfilePath,
                HazardType.Biohazard,
                0.75f,
                new[] { 0f, 0.12f, 0.36f, 1f },
                new[] { 1f, 0.55f, 0.12f, 0f },
                "Spore nest: the inverse shape on purpose. Full strength only where " +
                "you are touching it, then gone - 0.55 at x=0.12 (distance 0.35) and " +
                "0.12 at x=0.36 (distance 0.6), both BELOW the engine default of 0.77 " +
                "and 0.41. At 3 m out that is ~0.24 damage/second instead of ~0.82, " +
                "so the authored intent that 'a nest is something you are meant to be " +
                "able to approach and salvage' becomes mechanically true, while the " +
                "last metre still bites at ~1.7/second. VisorGlitchBias 0.75 - an " +
                "organic film smears the visor, it does not corrupt the sensor."),
        };

        // ══════════════════════════════════════════════════════════
        //  ENTRY POINTS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Batchmode entry point. REPORTS BY DEFAULT and writes nothing; pass
        /// <c>-h8ApplyHazardComponents</c> to actually author the prefabs.
        ///
        /// WHY THE SPLIT, and why a bare -executeMethod must not write. AGENTS.md:126, the Sandbox
        /// Firewall Rule, forbids automated runners and scripts from calling
        /// PrefabUtility.SaveAsPrefabAsset or EditorUtility.SetDirty on production assets, so that no
        /// automated pass can wipe a level designer's work. These four hazard prefabs ARE production
        /// assets. A no-argument public static void is reachable by -executeMethod, so before this gate
        /// existed any batchmode invocation - including one aimed at something else entirely that merely
        /// listed this method - would have rewritten four shipped prefabs.
        ///
        /// A human clicking the MenuItem is a deliberate act and stays permitted; an automated pass is
        /// not, and now has to say so out loud. That is the same split
        /// H8_ScatterPlacementOwnerEnableAuthoring.cs:122,:166-202 uses with -h8ApplyScatterOwnerEnable,
        /// and the same one H8_WorldRootGraveyardRepair.cs:222-236 and H8_DuplicateSceneRootAudit.cs:303-316
        /// use. Matching the existing convention matters more than inventing a tidier one: an agent that
        /// learns the flag pattern once should not have to relearn it per tool.
        /// </summary>
        public static void AuthorHazardComponentsFromCommandLine()
        {
            bool apply = false;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], ApplyFlag, System.StringComparison.Ordinal))
                {
                    apply = true;
                    break;
                }
            }

            if (!apply)
            {
                Debug.Log(
                    Marker + " REPORT-ONLY no " + ApplyFlag + " argument was passed, so nothing will be " +
                    "written. AGENTS.md:126 forbids an automated pass from writing production prefabs. " +
                    "Re-run with " + ApplyFlag + " to author, or use the menu item '" + AttachMenuPath +
                    "'. The state report follows.");
                ReportHazardPrefabs();
                return;
            }

            Debug.Log(
                Marker + " APPLY " + ApplyFlag + " was passed explicitly, so two things WILL happen, in " +
                "two different risk classes. (1) Up to two NEW HazardZoneProfile assets are created " +
                "under " + ProfileFolder + " - new files, nothing overwritten, an existing asset is " +
                "left alone. (2) The four production prefabs are REWRITTEN through the Unity API " +
                "(LoadPrefabContents -> SaveAsPrefabAsset) to carry the emitter, the profile reference " +
                "and, on the vents, a glow Light child plus ThermalVentRuntime. This is the opt-in " +
                "path, not the default.");
            AttachHazardComponents();
        }

        /// <summary>
        /// Authors every plan above. Batchmode safe: each prefab is independent,
        /// a failure on one is reported and the run continues.
        ///
        /// This is the WRITING half. Reachable by a human through the menu item, and from batchmode only
        /// through <see cref="AuthorHazardComponentsFromCommandLine"/> with an explicit opt-in flag.
        /// </summary>
        [MenuItem(AttachMenuPath, priority = 219)]
        public static void AttachHazardComponents()
        {
            int wrote = 0;
            int unchanged = 0;
            int declined = 0;

            // Profile assets first, and outside the prefab loop: LoadPrefabContents
            // opens a hidden preview scene, and creating assets while one is open is
            // avoidable churn. This creates NEW files only - an existing asset is
            // loaded and left byte-for-byte alone.
            EnsureProfileAssets();

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
                " planned; profile assets reported above. Static authoring only - no scene was " +
                "touched and NOTHING HERE PROVES ANY OF IT RUNS. Three separate claims still need " +
                "play-mode evidence, and they fail independently: (1) the emitters register - sample " +
                "HazardZoneManager for the two pocket volumes and the thermal grid for the two vents; " +
                "(2) the authored falloff is the one being applied - read back " +
                "HazardVolumeData.VisorGlitchBias and the curve LUT, because a profile that fails to " +
                "load degrades silently to the built-in (1-d^2)^2; (3) the vent glow is actually " +
                "visible - ThermalVentRuntime disables the Light whenever the smoothstepped " +
                "GlobalQualityWeight falls to 0.02 or below (ThermalVentRuntime.cs:109-117), so a " +
                "capture on a throttled machine can show nothing while every asset is correct.");
        }

        /// <summary>
        /// Read-only state report over the same four prefabs. Writes nothing.
        /// </summary>
        [MenuItem(ReportMenuPath, priority = 219)]
        public static void ReportHazardPrefabs()
        {
            Debug.Log(
                Marker + " REPORT-FOLDER " + ProfileFolder + " exists=" +
                AssetDatabase.IsValidFolder(ProfileFolder) + " (parent '" + ProfileFolderParent +
                "' exists=" + AssetDatabase.IsValidFolder(ProfileFolderParent) +
                "). The apply path WOULD create the folder if absent.");

            for (int i = 0; i < ProfilePlans.Length; i++)
            {
                HazardProfilePlan profilePlan = ProfilePlans[i];
                var existingProfile = AssetDatabase.LoadAssetAtPath<HazardZoneProfile>(profilePlan.AssetPath);
                if (existingProfile != null)
                {
                    Debug.Log(
                        Marker + " REPORT-PROFILE-ASSET " + profilePlan.AssetPath + " EXISTS type=" +
                        existingProfile.HazardType + " visorGlitchBias=" +
                        existingProfile.VisorGlitchBias.ToString("0.##") + " curve=" +
                        DescribeCurveAgainstEngineDefault(existingProfile.IntensityCurve) +
                        ". The apply path WOULD leave it untouched.",
                        existingProfile);
                    continue;
                }

                AnimationCurve plannedCurve = BuildPolylineFalloffCurve(
                    profilePlan.CurveTimes,
                    profilePlan.CurveValues);

                Debug.Log(
                    Marker + " REPORT-PROFILE-ASSET " + profilePlan.AssetPath +
                    " ABSENT. The apply path WOULD create it as a NEW file (nothing overwritten): " +
                    "type=" + profilePlan.Type + " visorGlitchBias=" +
                    profilePlan.VisorGlitchBias.ToString("0.##") + " curve=" +
                    (plannedCurve != null
                        ? DescribeCurveAgainstEngineDefault(plannedCurve)
                        : "MALFORMED - the apply path would refuse and create nothing") +
                    " acousticDreadID=0 (no consumer exists). " + profilePlan.Rationale);
            }

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

                Debug.Log(
                    Marker + " REPORT-PROFILE " + plan.PrefabPath + " current=" +
                    DescribeAssignedProfile(source) + "  WOULD " +
                    (plan.ProfileAssetPath.Length == 0
                        ? "assign nothing - Heat consumes no profile field but HazardType, so a " +
                          "profile here would only add a second place the type can drift to the dead " +
                          "Radiation default"
                        : "assign '" + plan.ProfileAssetPath + "' if _profile is null and the asset's " +
                          "type matches " + plan.Type + "; an existing reference is never repointed"));

                if (!plan.IsVent)
                    continue;

                VentPresentationPlan presentation = ResolveVentPresentationPlan(asset, plan);
                Debug.Log(
                    Marker + " REPORT-LIGHT " + plan.PrefabPath + " current=" +
                    (light != null
                        ? "'" + light.name + "' type=" + light.type + " intensity=" +
                          light.intensity.ToString("0.##") + " range=" + light.range.ToString("0.##") + "m"
                        : "NONE, so ThermalVentRuntime.HasPresentationTarget is false and the runtime " +
                          "cannot register") +
                    "  WOULD " + (light != null
                        ? "add nothing - any existing Light suppresses this step"
                        : "add child '" + VentGlowLightObjectName + "' at local " +
                          FormatVector(presentation.MouthLocalPosition) + ": Point, " +
                          presentation.IntensityCandela.ToString("0.##") + "cd, range " +
                          presentation.RuntimeRangeMeters.ToString("0.##") + "m, colour " +
                          FormatColor(presentation.GlowColor) + ", shadows None, Realtime; then wire " +
                          "ThermalVentRuntime with baseLightRange=" +
                          presentation.BaseLightRangeMeters.ToString("0.##") + "m pulseHz=" +
                          presentation.PulseFrequencyHz.ToString("0.###") + " pulseAmplitude=" +
                          presentation.PulseAmplitude.ToString("0.###")) +
                    ". " + presentation.Derivation,
                    asset);
            }
        }

        private static string DescribeAssignedProfile(HectonHazardSource source)
        {
            if (source == null)
                return "n/a (no HectonHazardSource)";

            var serialized = new SerializedObject(source);
            SerializedProperty profileProperty = serialized.FindProperty(ProfilePropertyName);
            if (profileProperty == null)
                return "unknown ('" + ProfilePropertyName + "' renamed - tool needs updating)";

            UnityEngine.Object assigned = profileProperty.objectReferenceValue;
            if (assigned == null)
                return "none (ResolveHazardType falls back to the inline type, VisorGlitchBias 1)";

            var profile = assigned as HazardZoneProfile;
            return profile != null
                ? "'" + profile.name + "' type=" + profile.HazardType + " bias=" +
                  profile.VisorGlitchBias.ToString("0.##")
                : "'" + assigned.name + "' which is NOT a HazardZoneProfile";
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
                // Order matters. AuthorHazardSource must run first because the other
                // two need the component to exist; AuthorHazardProfile must be its
                // own step rather than a branch inside it, because the source is
                // already authored on all four prefabs and that method deliberately
                // returns early without touching an already-live payload - a profile
                // folded into it would never be reached again. AuthorVentGlowLight
                // must precede AuthorVentPresentation, which is the consumer that
                // needs a Light to exist before it will wire anything.
                changed = AuthorHazardSource(root, plan);
                changed |= AuthorHazardProfile(root, plan);
                changed |= AuthorVentGlowLight(root, plan);
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
                    " - no HazardZoneProfile assigned yet, so ResolveHazardType falls back to the " +
                    "inline type just authored and VisorGlitchBias stays at 1. " +
                    (plan.ProfileAssetPath.Length > 0
                        ? "AuthorHazardProfile runs next in this same pass and will assign " +
                          plan.ProfileAssetPath + "."
                        : "That is the intended end state for this plan - see the logged refusal " +
                          "from AuthorHazardProfile below."));
            }

            return true;
        }

        /// <summary>
        /// Assigns the plan's HazardZoneProfile asset to the emitter, or refuses out
        /// loud. This is a separate step from <see cref="AuthorHazardSource"/> on
        /// purpose: that method returns early on an already-live payload, so a
        /// profile assignment folded into it would be unreachable on every prefab
        /// this tool has already authored once.
        ///
        /// THE INTERLOCK IS THE WHOLE POINT. ResolveHazardType
        /// (HectonHazardSource.cs:175-178) lets the profile OVERRIDE the inline type,
        /// and HazardZoneProfile's default for that field is HazardType.Radiation
        /// (HazardZoneProfile.cs:15), which reaches no consumer in this project. So a
        /// profile whose authored type disagrees with the plan is refused rather than
        /// assigned: assigning it would silently reroute a working hazard into a dead
        /// grid while looking more configured than before.
        ///
        /// An already-assigned profile is never replaced, for the same reason
        /// authored intensity is never overwritten - a designer's reference wins.
        /// </summary>
        private static bool AuthorHazardProfile(GameObject root, HazardPlan plan)
        {
            if (!root.TryGetComponent(out HectonHazardSource source))
            {
                Debug.LogWarning(
                    Marker + " SKIP-PROFILE " + plan.PrefabPath +
                    " - no HectonHazardSource on the root, so there is nothing to assign a profile " +
                    "to. The source authoring step above declined; that is the failure to fix.");
                return false;
            }

            if (plan.ProfileAssetPath.Length == 0)
            {
                Debug.Log(
                    Marker + " REFUSED-PROFILE " + plan.PrefabPath +
                    " - deliberately NO HazardZoneProfile, and this is a decision rather than a gap. " +
                    "This plan is " + plan.Type + ", and the Heat branch " +
                    "(HectonHazardSource.cs:145-152) calls TryInjectTransientHeatSource(position, " +
                    "radius, intensity, sourceId) - a signature (GlobalRegistryContracts.cs:3621) " +
                    "that accepts no profile, no VisorGlitchBias and no falloff curve. So every " +
                    "profile field except HazardType would be inert here, and HazardType would only " +
                    "restate the inline _type while adding a second field that can drift to the " +
                    "HazardZoneProfile default of Radiation - the one route in this project with no " +
                    "consumer. An asset that looks configured and changes nothing is worse than " +
                    "none. Revisit if Heat ever gains a HazardZoneManager route.");
                return false;
            }

            var serialized = new SerializedObject(source);
            SerializedProperty profileProperty = serialized.FindProperty(ProfilePropertyName);
            if (profileProperty == null)
            {
                Debug.LogError(
                    Marker + " DECLINED-PROFILE " + plan.PrefabPath +
                    " - HectonHazardSource no longer exposes '" + ProfilePropertyName +
                    "'. The field was renamed; fix this tool rather than guessing. Nothing written.");
                return false;
            }

            if (profileProperty.objectReferenceValue != null)
            {
                var existingProfile = profileProperty.objectReferenceValue as HazardZoneProfile;
                Debug.Log(
                    Marker + " SKIP-PROFILE " + plan.PrefabPath + " - already references '" +
                    profileProperty.objectReferenceValue.name + "'" +
                    (existingProfile != null
                        ? " (type=" + existingProfile.HazardType + " bias=" +
                          existingProfile.VisorGlitchBias.ToString("0.##") + ")"
                        : " which is NOT a HazardZoneProfile") +
                    ". Left exactly as found; this tool does not repoint an authored profile " +
                    "reference.");
                return false;
            }

            var profile = AssetDatabase.LoadAssetAtPath<HazardZoneProfile>(plan.ProfileAssetPath);
            if (profile == null)
            {
                Debug.LogWarning(
                    Marker + " DECLINED-PROFILE " + plan.PrefabPath + " - profile asset '" +
                    plan.ProfileAssetPath + "' could not be loaded, so there is nothing to assign. " +
                    "EnsureProfileAssets should have created it; check its log lines above for the " +
                    "reason. The inline type stays authoritative, which is a working fallback.");
                return false;
            }

            if (profile.HazardType != plan.Type)
            {
                Debug.LogError(
                    Marker + " DECLINED-PROFILE " + plan.PrefabPath + " - INTERLOCK TRIPPED. '" +
                    plan.ProfileAssetPath + "' is authored as " + profile.HazardType +
                    " but this plan is " + plan.Type + ". ResolveHazardType " +
                    "(HectonHazardSource.cs:175-178) would let the profile win, so assigning it " +
                    "would silently reroute this hazard" +
                    (profile.HazardType == HazardType.Radiation
                        ? " into RadiationHazardGrid, whose signal buffer nothing in this project " +
                          "drains - the hazard would go completely dead while looking configured."
                        : " to the wrong consumer.") +
                    " Nothing written. Fix the asset or the plan, do not remove this check.");
                return false;
            }

            profileProperty.objectReferenceValue = profile;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log(
                Marker + " AUTHORED-PROFILE " + plan.PrefabPath + " -> _profile = '" +
                plan.ProfileAssetPath + "' (type=" + profile.HazardType + " matches plan, " +
                "visorGlitchBias=" + profile.VisorGlitchBias.ToString("0.##") +
                "). This REWRITES a shipped production prefab. Runtime effect: " +
                "HazardZoneManager.WriteVolumeCurveLut (:2917-2939) now copies this profile's baked " +
                "falloff into the exposure job instead of the built-in (1-d^2)^2, and " +
                "HazardVolumeData.VisorGlitchBias carries " +
                profile.VisorGlitchBias.ToString("0.##") + " instead of 1 into the max-fold at " +
                "HazardZoneManager.cs:127.",
                root);

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
                // Reaching this now means AuthorVentGlowLight failed on the step before,
                // because that step's whole job is to guarantee a Light exists. The
                // decline itself is unchanged and still correct: without a target,
                // HasPresentationTarget (ThermalVentRuntime.cs:224-227) is false,
                // TryRegisterLateFrame (:196-202) refuses, and the component could
                // never tick.
                Debug.LogWarning(
                    Marker + " DECLINED-INERT " + plan.PrefabPath +
                    " - ThermalVentRuntime NOT attached: the prefab still owns no Light and no " +
                    "DecalProjector, so HasPresentationTarget (ThermalVentRuntime.cs:224-227) is " +
                    "false and TryRegisterLateFrame (:196-202) would never register it. It would be " +
                    "a component that cannot tick. This is no longer a waiting state - " +
                    "AuthorVentGlowLight runs immediately before this and should have created one, " +
                    "so read ITS log line for the real failure. The hazard field itself is " +
                    "unaffected: ThermalVentRuntime is presentation only.");
                return false;
            }

            if (root.TryGetComponent(out ThermalVentRuntime existingRuntime))
            {
                Debug.Log(
                    Marker + " SKIP " + plan.PrefabPath +
                    " - ThermalVentRuntime already present (validFactoryConfiguration=" +
                    existingRuntime.HasValidFactoryConfiguration +
                    "; that property also wants a DecalProjector and a LightCullingProxy, neither of " +
                    "which this tool authors - see the header). Not duplicated, not reconfigured.");
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
            // enable/disable instead of deferring to external culling. Neither is
            // authored here - the header records why.
            root.TryGetComponent(out HazardMetadata metadata);
            LightCullingProxy cullingProxy = root.GetComponentInChildren<LightCullingProxy>(true);

            VentPresentationPlan presentation = ResolveVentPresentationPlan(root, plan);

            runtime.ConfigureForEditor(
                metadata,
                keyLight,
                primaryDecal,
                cullingProxy,
                presentation.IntensityCandela,
                presentation.BaseLightRangeMeters,
                VentDecalFade,
                presentation.PulseFrequencyHz,
                presentation.PulseAmplitude);

            Debug.Log(
                Marker + " AUTHORED " + plan.PrefabPath + " -> ThermalVentRuntime added: keyLight=" +
                (keyLight != null ? keyLight.name : "none") + " decal=" +
                (primaryDecal != null ? primaryDecal.name : "none") + " cullingProxy=" +
                (cullingProxy != null ? cullingProxy.name : "none") + " metadata=" +
                (metadata != null ? "bound" : "none") + " baseLightIntensity=" +
                presentation.IntensityCandela.ToString("0.##") + "cd baseLightRange=" +
                presentation.BaseLightRangeMeters.ToString("0.##") + "m (projects " +
                presentation.RuntimeRangeMeters.ToString("0.##") + "m at quality 1, x" +
                VentRangeGainAtFullQuality.ToString("0.##") + " per ThermalVentRuntime.cs:142) " +
                "baseDecalFade=" + VentDecalFade.ToString("0.##") + " pulseHz=" +
                presentation.PulseFrequencyHz.ToString("0.###") + " pulseAmplitude=" +
                presentation.PulseAmplitude.ToString("0.###") + ". " + presentation.Derivation,
                root);

            return true;
        }

        /// <summary>
        /// Creates the vent glow Light when the prefab owns no Light at all. This is
        /// the step that closes the DECLINED-INERT loop the previous revision
        /// reported: without a Light or DecalProjector, HasPresentationTarget
        /// (ThermalVentRuntime.cs:224-227) is false and TryRegisterLateFrame
        /// (:196-202) never registers, so the runtime could not tick.
        ///
        /// This REWRITES a shipped production prefab - it adds a child GameObject,
        /// which is the same risk class as adding a component, not the same class as
        /// creating a new asset.
        ///
        /// IDEMPOTENT: ANY Light anywhere in the hierarchy, active or not, means this
        /// does nothing. That is deliberately broader than "a child named
        /// VentGlow_Light" - if an art lane adds its own vent light under a different
        /// name, a second one from this tool would double the exposure and blow the
        /// per-object additional-light limit of 2 on its own.
        /// </summary>
        private static bool AuthorVentGlowLight(GameObject root, HazardPlan plan)
        {
            if (!plan.IsVent)
                return false;

            Light existingLight = root.GetComponentInChildren<Light>(true);
            if (existingLight != null)
            {
                Debug.Log(
                    Marker + " SKIP-LIGHT " + plan.PrefabPath + " - '" + existingLight.name +
                    "' already provides a Light (type=" + existingLight.type + " intensity=" +
                    existingLight.intensity.ToString("0.##") + " range=" +
                    existingLight.range.ToString("0.##") + "m). Not duplicated and not " +
                    "reconfigured: an authored light is content, and a second one would double the " +
                    "glow and exhaust the per-object additional-light budget of 2 by itself.");
                return false;
            }

            VentPresentationPlan presentation = ResolveVentPresentationPlan(root, plan);

            var lightObject = new GameObject(VentGlowLightObjectName);
            lightObject.layer = root.layer;
            Transform lightTransform = lightObject.transform;
            lightTransform.SetParent(root.transform, false);
            lightTransform.localPosition = presentation.MouthLocalPosition;
            lightTransform.localRotation = Quaternion.identity;
            lightTransform.localScale = Vector3.one;

            var light = lightObject.AddComponent<Light>();
            if (light == null)
            {
                UnityEngine.Object.DestroyImmediate(lightObject, true);
                Debug.LogError(
                    Marker + " DECLINED-LIGHT " + plan.PrefabPath +
                    " - AddComponent<Light> returned null, so the empty child was removed again " +
                    "rather than left behind as a decoy. Hazard source authoring still stands.");
                return false;
            }

            // Point: a vent mouth radiates in every direction and a Spot would need an
            // aim nobody authored. Realtime is not cosmetic - ThermalVentRuntime writes
            // intensity every late frame, and a Baked or Mixed light would ignore it.
            // Shadows off before the pipeline ever sees it: this URP asset DOES support
            // additional-light shadows, so Auto would silently claim a shadow slice for
            // a glow that casts nothing meaningful.
            light.type = LightType.Point;
            light.color = presentation.GlowColor;
            light.intensity = presentation.IntensityCandela;
            light.range = presentation.RuntimeRangeMeters;
            light.shadows = LightShadows.None;
            light.bounceIntensity = 0f;
            light.renderMode = LightRenderMode.Auto;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.useColorTemperature = false;

            // Not cosmetic tidiness: Light.GetUniversalAdditionalLightData()
            // (UniversalAdditionalLightData.cs:17-25) calls AddComponent when the
            // component is missing, and its callers are on the render path -
            // ForwardLights.cs:629, DeferredLights.cs:1196,
            // UniversalRenderPipelineCore.cs:1639. A Light shipped without it therefore
            // buys a managed AddComponent the first time the pipeline sees it.
            // Authoring it here keeps the prefab self-contained. Its defaults
            // (usePipelineSettings, shadow tier 2, soft shadow quality 0) are exactly
            // what every other authored light in this project serialises, so nothing
            // here is tuned - only made explicit.
            if (lightObject.GetComponent<UniversalAdditionalLightData>() == null)
                lightObject.AddComponent<UniversalAdditionalLightData>();

            Debug.Log(
                Marker + " AUTHORED-LIGHT " + plan.PrefabPath + " -> added child '" +
                VentGlowLightObjectName + "' at local " + FormatVector(presentation.MouthLocalPosition) +
                ": Point, " + presentation.IntensityCandela.ToString("0.##") + DescribeLightUnit(light) +
                ", range " + presentation.RuntimeRangeMeters.ToString("0.##") + "m, colour " +
                FormatColor(presentation.GlowColor) + ", shadows None, bounce 0, Realtime. " +
                presentation.Derivation + " This REWRITES a shipped production prefab (a new child " +
                "GameObject), and it is what makes ThermalVentRuntime registerable on the very next " +
                "step of this same pass.",
                root);

            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  VENT PRESENTATION DERIVATION
        // ══════════════════════════════════════════════════════════

        // Endpoints of the glow colour ramp. Amber at the cool end, paler amber at
        // the hot end, because a hotter body radiates whiter - the same reason a
        // forge scale runs red to yellow to white. Kept saturated at the source
        // because seawater strips long wavelengths with distance.
        private static readonly Color VentGlowColorCool = new Color(1f, 0.42f, 0.16f, 1f);
        private static readonly Color VentGlowColorHot = new Color(1f, 0.66f, 0.38f, 1f);

        /// <summary>
        /// Everything the glow Light and ThermalVentRuntime must agree on, derived
        /// once from the prefab's measured geometry and the plan's heat magnitude so
        /// the two steps cannot drift apart.
        /// </summary>
        private readonly struct VentPresentationPlan
        {
            public readonly Vector3 MouthLocalPosition;
            public readonly float IntensityCandela;
            public readonly Color GlowColor;
            public readonly float RuntimeRangeMeters;
            public readonly float BaseLightRangeMeters;
            public readonly float PulseFrequencyHz;
            public readonly float PulseAmplitude;
            public readonly string Derivation;

            public VentPresentationPlan(
                Vector3 mouthLocalPosition,
                float intensityCandela,
                Color glowColor,
                float runtimeRangeMeters,
                float baseLightRangeMeters,
                float pulseFrequencyHz,
                float pulseAmplitude,
                string derivation)
            {
                MouthLocalPosition = mouthLocalPosition;
                IntensityCandela = intensityCandela;
                GlowColor = glowColor;
                RuntimeRangeMeters = runtimeRangeMeters;
                BaseLightRangeMeters = baseLightRangeMeters;
                PulseFrequencyHz = pulseFrequencyHz;
                PulseAmplitude = pulseAmplitude;
                Derivation = derivation;
            }
        }

        /// <summary>
        /// Derives the whole glow from two measured numbers and one authored one.
        /// Nothing here is a magic constant that survived from a first draft: range
        /// and position come from the prefab's own meshes and particle systems,
        /// brightness / colour / pulse come from the plan's heat magnitude.
        /// </summary>
        private static VentPresentationPlan ResolveVentPresentationPlan(GameObject root, HazardPlan plan)
        {
            bool measured = TryMeasureRootSpaceMeshBounds(root, out Bounds bounds);

            float standingHeightMeters = measured
                ? Mathf.Max(0f, bounds.max.y)
                : VentFallbackStandingHeightMeters;
            float horizontalRadiusMeters = measured
                ? Mathf.Max(
                    Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x)),
                    Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z)))
                : VentFallbackHorizontalRadiusMeters;

            if (!IsFiniteFloat(standingHeightMeters) || standingHeightMeters <= 0f)
                standingHeightMeters = VentFallbackStandingHeightMeters;
            if (!IsFiniteFloat(horizontalRadiusMeters) || horizontalRadiusMeters <= 0f)
                horizontalRadiusMeters = VentFallbackHorizontalRadiusMeters;

            // The mouth is where the plume leaves the rock, and the art already marks
            // it on any vent that has a bubble column. Falling back to the top of the
            // mesh bounds would put the hero vent's light on a decorative spine tip
            // instead, about 1.4 m above the hole it is supposed to be shining out of.
            bool hasPlume = TryResolvePlumeOrigin(root, out Vector3 plumeLocalPosition, out string plumeName);
            float mouthInset = Mathf.Min(
                VentMouthInsetMaxMeters,
                standingHeightMeters * VentMouthInsetHeightFraction);
            Vector3 mouthLocalPosition = hasPlume
                ? plumeLocalPosition
                : new Vector3(
                    measured ? bounds.center.x : 0f,
                    Mathf.Max(0f, standingHeightMeters - mouthInset),
                    measured ? bounds.center.z : 0f);

            float heat01 = Mathf.Clamp01(
                IsFiniteFloat(plan.Intensity) ? plan.Intensity / VentHeatReferenceMagnitude : 0f);

            float intensityCandela = Mathf.Lerp(
                VentGlowMinIntensityCandela,
                VentGlowMaxIntensityCandela,
                heat01);

            // Range covers the chimney it belongs to plus a body width of water either
            // side, then stops. It is NOT the hazard radius: this deliberately lands
            // far inside it so the glow never reads as the damage volume.
            float runtimeRangeMeters = Mathf.Clamp(
                standingHeightMeters + (horizontalRadiusMeters * 2f),
                VentRuntimeRangeMinMeters,
                VentRuntimeRangeMaxMeters);
            float baseLightRangeMeters = Mathf.Max(
                0.5f,
                runtimeRangeMeters / VentRangeGainAtFullQuality);

            float pulseFrequencyHz = Mathf.Lerp(
                VentPulseFrequencyHzCool,
                VentPulseFrequencyHzHot,
                heat01);
            float pulseAmplitude = Mathf.Lerp(
                VentPulseAmplitudeCool,
                VentPulseAmplitudeHot,
                heat01);

            string derivation =
                "DERIVED FROM GEOMETRY: standingHeight=" + standingHeightMeters.ToString("0.##") +
                "m horizontalRadius=" + horizontalRadiusMeters.ToString("0.##") + "m (" +
                (measured
                    ? "measured from MeshFilter.sharedMesh.bounds in root space"
                    : "NO MEASURABLE MESH - fallback constants used, treat these numbers as a guess") +
                "), mouth from " + (hasPlume
                    ? "ParticleSystem '" + plumeName + "'"
                    : "mesh top inset " + mouthInset.ToString("0.##") + "m") +
                ". FROM HEAT: plan intensity " + plan.Intensity.ToString("0.##") + " / reference " +
                VentHeatReferenceMagnitude.ToString("0.##") + " = heat01 " + heat01.ToString("0.###") +
                ", which sets brightness, colour and pulse. Sanity anchor: heat01 0.417 reproduces the " +
                "0.45Hz/0.18 pulse that was hand-tuned before this derivation existed.";

            return new VentPresentationPlan(
                mouthLocalPosition,
                intensityCandela,
                Color.Lerp(VentGlowColorCool, VentGlowColorHot, heat01),
                runtimeRangeMeters,
                baseLightRangeMeters,
                pulseFrequencyHz,
                pulseAmplitude,
                derivation);
        }

        /// <summary>
        /// Combined mesh bounds in the ROOT's local space, including inactive objects.
        /// Deliberately not Renderer.bounds: the hero vent keeps its LOD1 and LOD2
        /// renderers switched off, and a disabled renderer's world bounds are not a
        /// number worth trusting in batchmode. Mesh bounds plus the transform chain
        /// is the same measurement without the dependency on activation state.
        /// </summary>
        private static bool TryMeasureRootSpaceMeshBounds(GameObject root, out Bounds rootSpaceBounds)
        {
            rootSpaceBounds = default;
            Matrix4x4 worldToRoot = root.transform.worldToLocalMatrix;
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            bool encapsulatedAny = false;

            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter == null)
                    continue;

                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                    continue;

                Matrix4x4 meshToRoot = worldToRoot * filter.transform.localToWorldMatrix;
                Bounds meshBounds = mesh.bounds;
                Vector3 min = meshBounds.min;
                Vector3 max = meshBounds.max;

                for (int corner = 0; corner < 8; corner++)
                {
                    var meshCorner = new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z);
                    Vector3 rootSpaceCorner = meshToRoot.MultiplyPoint3x4(meshCorner);
                    if (!IsFiniteVector(rootSpaceCorner))
                        continue;

                    if (!encapsulatedAny)
                    {
                        rootSpaceBounds = new Bounds(rootSpaceCorner, Vector3.zero);
                        encapsulatedAny = true;
                        continue;
                    }

                    rootSpaceBounds.Encapsulate(rootSpaceCorner);
                }
            }

            return encapsulatedAny;
        }

        /// <summary>
        /// Highest ParticleSystem in the hierarchy, in root local space. Highest
        /// rather than first, so an LOD duplicate of a bubble column cannot win over
        /// the main one, and so a ground-level ambient emitter cannot be mistaken for
        /// the plume.
        /// </summary>
        private static bool TryResolvePlumeOrigin(
            GameObject root,
            out Vector3 plumeLocalPosition,
            out string plumeName)
        {
            plumeLocalPosition = Vector3.zero;
            plumeName = string.Empty;

            Transform rootTransform = root.transform;
            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
            bool found = false;

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                if (system == null)
                    continue;

                Vector3 localPosition = rootTransform.InverseTransformPoint(system.transform.position);
                if (!IsFiniteVector(localPosition))
                    continue;

                if (found && localPosition.y <= plumeLocalPosition.y)
                    continue;

                plumeLocalPosition = localPosition;
                plumeName = system.name;
                found = true;
            }

            return found;
        }

        // ══════════════════════════════════════════════════════════
        //  PROFILE ASSETS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Creates the profile assets this tool owns. NEW FILES ONLY: an asset that
        /// already exists at the target path is loaded, reported and left untouched,
        /// so this can never overwrite a designer's tuning. Called only from the
        /// apply path.
        /// </summary>
        private static void EnsureProfileAssets()
        {
            if (!AssetDatabase.IsValidFolder(ProfileFolder))
            {
                if (!AssetDatabase.IsValidFolder(ProfileFolderParent))
                {
                    Debug.LogError(
                        Marker + " DECLINED-PROFILE-ASSETS - parent folder '" + ProfileFolderParent +
                        "' does not exist, so no profile can be created. The Data tree moved; fix " +
                        "this tool rather than creating folders at a guessed path. Prefab authoring " +
                        "continues without profiles, which leaves the working inline types in place.");
                    return;
                }

                string folderGuid = AssetDatabase.CreateFolder(ProfileFolderParent, ProfileFolderName);
                if (string.IsNullOrEmpty(folderGuid))
                {
                    Debug.LogError(
                        Marker + " DECLINED-PROFILE-ASSETS - CreateFolder('" + ProfileFolderParent +
                        "', '" + ProfileFolderName + "') returned no GUID. Nothing created.");
                    return;
                }

                Debug.Log(
                    Marker + " CREATED-FOLDER " + ProfileFolder +
                    " - new folder, nothing overwritten. It sits beside the existing ZoneProfiles " +
                    "and ContentProfiles folders, which hold unrelated ScriptableObject types.");
            }

            int created = 0;
            for (int i = 0; i < ProfilePlans.Length; i++)
            {
                if (EnsureProfileAsset(ProfilePlans[i]))
                    created++;
            }

            if (created > 0)
                AssetDatabase.SaveAssets();
        }

        private static bool EnsureProfileAsset(HazardProfilePlan plan)
        {
            var existing = AssetDatabase.LoadAssetAtPath<HazardZoneProfile>(plan.AssetPath);
            if (existing != null)
            {
                Debug.Log(
                    Marker + " SKIP-PROFILE-ASSET " + plan.AssetPath + " - already exists (type=" +
                    existing.HazardType + " visorGlitchBias=" +
                    existing.VisorGlitchBias.ToString("0.##") + " curveKeys=" +
                    (existing.IntensityCurve != null ? existing.IntensityCurve.length : 0) +
                    "). Left exactly as found - this tool creates profiles, it never rewrites one, " +
                    "so any hand tuning survives every re-run.");
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(plan.AssetPath) != null)
            {
                Debug.LogError(
                    Marker + " DECLINED-PROFILE-ASSET " + plan.AssetPath +
                    " - something that is NOT a HazardZoneProfile already occupies that path. " +
                    "Refusing to overwrite an unrelated asset. Nothing written.");
                return false;
            }

            var profile = ScriptableObject.CreateInstance<HazardZoneProfile>();
            if (profile == null)
            {
                Debug.LogError(
                    Marker + " DECLINED-PROFILE-ASSET " + plan.AssetPath +
                    " - CreateInstance<HazardZoneProfile> returned null. Nothing written.");
                return false;
            }

            AnimationCurve curve = BuildPolylineFalloffCurve(plan.CurveTimes, plan.CurveValues);
            if (curve == null)
            {
                UnityEngine.Object.DestroyImmediate(profile);
                Debug.LogError(
                    Marker + " DECLINED-PROFILE-ASSET " + plan.AssetPath +
                    " - the authored curve keys are malformed (mismatched lengths, fewer than two " +
                    "keys, a non-finite value, or x values that are not strictly ascending). No " +
                    "asset was created, because a profile with a broken curve is worse than none.");
                return false;
            }

            AssetDatabase.CreateAsset(profile, plan.AssetPath);

            var serialized = new SerializedObject(profile);
            SerializedProperty typeProperty = serialized.FindProperty(ProfileHazardTypePropertyName);
            SerializedProperty curveProperty = serialized.FindProperty(ProfileIntensityCurvePropertyName);
            SerializedProperty biasProperty = serialized.FindProperty(ProfileVisorGlitchBiasPropertyName);
            SerializedProperty lutProperty = serialized.FindProperty(ProfileBakedLutPropertyName);
            if (typeProperty == null || curveProperty == null || biasProperty == null || lutProperty == null)
            {
                // Deleting an asset created three lines ago, never a pre-existing one -
                // the early return above guarantees nothing was here. Leaving it would
                // ship a profile stuck on the HazardType.Radiation default, i.e. a
                // silent kill switch for whatever prefab later referenced it.
                AssetDatabase.DeleteAsset(plan.AssetPath);
                Debug.LogError(
                    Marker + " DECLINED-PROFILE-ASSET " + plan.AssetPath +
                    " - HazardZoneProfile no longer exposes all of '" + ProfileHazardTypePropertyName +
                    "', '" + ProfileIntensityCurvePropertyName + "', '" +
                    ProfileVisorGlitchBiasPropertyName + "', '" + ProfileBakedLutPropertyName +
                    "'. The fields were renamed; fix this tool rather than guessing. The asset just " +
                    "created was deleted again so no Radiation-defaulted profile is left behind.");
                return false;
            }

            typeProperty.intValue = (int)plan.Type;
            curveProperty.animationCurveValue = curve;
            biasProperty.floatValue = plan.VisorGlitchBias;
            WriteBakedIntensityLut(lutProperty, curve);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);

            Debug.Log(
                Marker + " CREATED-PROFILE-ASSET " + plan.AssetPath + " - NEW FILE via " +
                "AssetDatabase.CreateAsset, so nothing was overwritten or wiped. type=" + plan.Type +
                " visorGlitchBias=" + plan.VisorGlitchBias.ToString("0.##") + " curve=" +
                DescribeCurveAgainstEngineDefault(curve) + " acousticDreadID=0 (left unset on " +
                "purpose: the only reference to it in the entire project is its own getter at " +
                "HazardZoneProfile.cs:41, so a hand-picked value would fake configuration). " +
                plan.Rationale,
                profile);

            return true;
        }

        /// <summary>
        /// Mirrors HazardZoneProfile.BakeIntensityCurveLut (HazardZoneProfile.cs:67-82)
        /// so the asset on disk is self-consistent the moment it is written. The
        /// profile also re-bakes in OnEnable (:55-59), so this is belt-and-braces
        /// rather than the load-bearing path - the authored CURVE is what matters.
        /// Note the axis asymmetry documented in the header: the bake is linear in the
        /// curve's own x, and the runtime reads the table at distance SQUARED.
        /// </summary>
        private static void WriteBakedIntensityLut(SerializedProperty lutProperty, AnimationCurve curve)
        {
            int sampleCount = HazardZoneProfile.IntensityLutSampleCount;
            if (lutProperty == null || !lutProperty.isArray || sampleCount <= 1 || curve == null)
                return;

            lutProperty.arraySize = sampleCount;
            for (int i = 0; i < sampleCount; i++)
            {
                SerializedProperty element = lutProperty.GetArrayElementAtIndex(i);
                if (element == null)
                    continue;

                float normalized = i / (float)(sampleCount - 1);
                float sample = curve.Evaluate(normalized);
                element.floatValue = IsFiniteFloat(sample) ? Mathf.Clamp01(sample) : 0f;
            }
        }

        /// <summary>
        /// Builds a strict polyline. Cubic Hermite with both endpoint tangents set to
        /// the segment slope reduces exactly to the straight line between the two
        /// keys, so this has no overshoot - which matters for a falloff that must never
        /// exceed 1 or dip below 0, and which is then resampled into a 64-entry LUT
        /// and linearly interpolated again at runtime anyway.
        /// Returns null rather than a plausible-but-wrong curve on bad input.
        /// </summary>
        private static AnimationCurve BuildPolylineFalloffCurve(float[] times, float[] values)
        {
            if (times == null || values == null || times.Length != values.Length || times.Length < 2)
                return null;

            var keys = new Keyframe[times.Length];
            for (int i = 0; i < times.Length; i++)
            {
                if (!IsFiniteFloat(times[i]) || !IsFiniteFloat(values[i]))
                    return null;

                // Unity silently reorders keys with equal or descending time, which
                // would quietly produce a different curve than the one authored.
                if (i > 0 && times[i] <= times[i - 1])
                    return null;

                keys[i] = new Keyframe(times[i], values[i]);
            }

            for (int i = 0; i < keys.Length; i++)
            {
                Keyframe key = keys[i];
                key.inTangent = i > 0 ? ResolveSegmentSlope(keys[i - 1], keys[i]) : 0f;
                key.outTangent = i < keys.Length - 1 ? ResolveSegmentSlope(keys[i], keys[i + 1]) : 0f;
                key.weightedMode = WeightedMode.None;
                keys[i] = key;
            }

            return new AnimationCurve(keys);
        }

        private static float ResolveSegmentSlope(Keyframe from, Keyframe to)
        {
            float deltaTime = to.time - from.time;
            return deltaTime > 0f ? (to.value - from.value) / deltaTime : 0f;
        }

        /// <summary>
        /// Reports the authored curve against the falloff the runtime would use with
        /// no profile at all, at the distances a player actually feels. Without this
        /// comparison a log line saying "curve authored" cannot distinguish a real
        /// change from a copy of the default.
        /// </summary>
        private static string DescribeCurveAgainstEngineDefault(AnimationCurve curve)
        {
            if (curve == null)
                return "none";

            string description = curve.length.ToString() + " keys; attenuation vs no-profile default";
            for (int i = 1; i <= 4; i++)
            {
                float normalizedDistance = i * 0.2f;
                float squared = normalizedDistance * normalizedDistance;
                float authored = Mathf.Clamp01(curve.Evaluate(squared));

                // HazardZoneManager.ResolveSquaredDefaultCurveSample: (1 - dSq)^2.
                float engineDefault = 1f - squared;
                engineDefault = engineDefault > 0f ? engineDefault * engineDefault : 0f;

                description += "  d=" + normalizedDistance.ToString("0.#") + ":" +
                               authored.ToString("0.00") + "/" + engineDefault.ToString("0.00");
            }

            return description;
        }

        private static string DescribeLightUnit(Light light)
        {
            if (light == null)
                return "?";

            var serialized = new SerializedObject(light);
            SerializedProperty unitProperty = serialized.FindProperty("m_LightUnit");
            if (unitProperty == null)
                return "? (m_LightUnit not found - Light serialization changed, log only)";

            switch (unitProperty.intValue)
            {
                case 0:
                    return "lm";
                case 1:
                    return "cd";
                case 2:
                    return "lx";
                case 3:
                    return "nit";
                case 4:
                    return "EV100";
                default:
                    return "unit#" + unitProperty.intValue.ToString();
            }
        }

        private static string FormatVector(Vector3 value)
        {
            return "(" + value.x.ToString("0.##") + ", " + value.y.ToString("0.##") + ", " +
                   value.z.ToString("0.##") + ")";
        }

        private static string FormatColor(Color value)
        {
            return "rgb(" + value.r.ToString("0.##") + ", " + value.g.ToString("0.##") + ", " +
                   value.b.ToString("0.##") + ")";
        }

        private static bool IsFiniteFloat(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return IsFiniteFloat(value.x) && IsFiniteFloat(value.y) && IsFiniteFloat(value.z);
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
