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
//     ThermalVentRuntime deliberately stops enabling the Light at all once
//     cullingProxy != null (:135-136), handing the enable decision to the proxy.
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
            /// profile is deliberately refused. Empty is a decision, not a gap -
            /// see ProfileDeclineReason.
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
                "last metre still bites at ~1.6/second. VisorGlitchBias 0.75 - an " +
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
                Marker + " APPLY " + ApplyFlag + " was passed explicitly, so the four production prefabs " +
                "WILL be rewritten through the Unity API. This is the opt-in path, not the default.");
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
