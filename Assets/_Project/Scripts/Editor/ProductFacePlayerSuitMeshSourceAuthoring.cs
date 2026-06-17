#if UNITY_EDITOR
// ============================================================================
// HECTON-8 - ProductFacePlayerSuitMeshSourceAuthoring.cs
// Batch 18 / 1877 static source route for future player suit mesh assets.
//
// Editor-only mesh source authoring. When a future Unity owner runs this menu
// or calls AuthorAll(), it writes Mesh assets only. It does not edit prefabs,
// colliders, HandAnchor, Swim_*Attachment transforms, Suit_Visor, HUD roots,
// movement, camera, tools, survival state, scenes, materials, or runtime code.
// ============================================================================

namespace Hecton8.Editor.ProductFace
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Rendering;

    public static class ProductFacePlayerSuitMeshSourceAuthoring
    {
        public const string OutputFolder = "Assets/_Project/Art/Generated/ProductFace/PlayerSuit";

        private const float DefaultGlobalQualityWeight = 0.72f;
        private const float MinimumTriangleArea = 0.0000001f;
        private const int MaterialSlotCount = 4;
        private const int SlotGraphiteRubber = 0;
        private const int SlotWetPlate = 1;
        private const int SlotTrimLatch = 2;
        private const int SlotGlassEmission = 3;

        private static readonly string[] RequiredMaterialSources =
            ProductFacePlayerSuitGeminiMaterialApplier.GetRequiredMaterialPathsForStaticAudit();

        private static readonly SuitPartSpec[] SuitPartSpecs =
        {
            new SuitPartSpec(
                "FirstPerson_LeftGloveForearm",
                SuitPartKind.LeftGloveForearm,
                "GEN_PlayerSuit_FP_LeftGloveForearm_Source_LOD0",
                new Vector3(-0.20f, -0.08f, 0.34f),
                new Vector3(0.15f, 0.12f, 0.58f),
                "Maps to Swim_LeftForearmAttachment, Swim_LeftHandAttachment, HandAnchor support hand; visual-only, no HandAnchor mutation.",
                "VIS_LeftPressureGlove,VIS_LeftForearmShell,VIS_LeftToolSupportKnuckles,VIS_LeftWristSeal,VIS_LeftCyanTrim,VIS_LeftHoseGuide"),
            new SuitPartSpec(
                "FirstPerson_RightGloveForearm",
                SuitPartKind.RightGloveForearm,
                "GEN_PlayerSuit_FP_RightGloveForearm_Source_LOD0",
                new Vector3(0.20f, -0.08f, 0.34f),
                new Vector3(0.15f, 0.12f, 0.58f),
                "Maps to Swim_RightForearmAttachment, Swim_RightHandAttachment, HandAnchor active tool hand; visual-only, no HandAnchor mutation.",
                "VIS_RightPressureGlove,VIS_RightForearmShell,VIS_RightToolGripPads,VIS_RightWristSeal,VIS_RightCyanTrim,VIS_RightHoseGuide"),
            new SuitPartSpec(
                "LeftShoulderChestEdge",
                SuitPartKind.LeftShoulderChestEdge,
                "GEN_PlayerSuit_LeftShoulderChestEdge_Source_LOD0",
                new Vector3(-0.23f, 0.12f, 0.05f),
                new Vector3(0.30f, 0.20f, 0.34f),
                "Maps to Swim_LeftShoulderAttachment and torso edge; keeps first-person arm connected to chest without owning movement.",
                "VIS_LeftClaviclePlate,VIS_LeftShoulderCup,VIS_LeftChestSeal,VIS_LeftHarnessLatch,VIS_LeftTrimStrip"),
            new SuitPartSpec(
                "RightShoulderChestEdge",
                SuitPartKind.RightShoulderChestEdge,
                "GEN_PlayerSuit_RightShoulderChestEdge_Source_LOD0",
                new Vector3(0.23f, 0.12f, 0.05f),
                new Vector3(0.30f, 0.20f, 0.34f),
                "Maps to Swim_RightShoulderAttachment and torso edge; keeps first-person arm connected to chest without owning movement.",
                "VIS_RightClaviclePlate,VIS_RightShoulderCup,VIS_RightChestSeal,VIS_RightHarnessLatch,VIS_RightTrimStrip"),
            new SuitPartSpec(
                "TorsoHardShell",
                SuitPartKind.TorsoHardShell,
                "GEN_PlayerSuit_TorsoHardShell_Source_LOD0",
                new Vector3(0f, 0.02f, 0f),
                new Vector3(0.56f, 0.74f, 0.32f),
                "Maps to Swim_TorsoAttachment, chest straps, Suit_Diegetic_HUD_V4_Projection clearance; no collider ownership.",
                "VIS_TorsoPressureShell,VIS_BreathingManifold,VIS_ServicePanels,VIS_CyanChestTrim,VIS_AmberLatches,VIS_HosePorts"),
            new SuitPartSpec(
                "PelvisHarness",
                SuitPartKind.PelvisHarness,
                "GEN_PlayerSuit_PelvisHarness_Source_LOD0",
                new Vector3(0f, -0.54f, 0f),
                new Vector3(0.48f, 0.30f, 0.30f),
                "Maps to Swim_PelvisAttachment and lower harness; visual strap geometry only, no inventory or survival authority.",
                "VIS_PelvisBelt,VIS_CrotchHarness,VIS_LeftHipLatch,VIS_RightHipLatch,VIS_ServiceLoops"),
            new SuitPartSpec(
                "LeftThighCalfFin",
                SuitPartKind.LeftThighCalfFin,
                "GEN_PlayerSuit_LeftThighCalfFin_Source_LOD0",
                new Vector3(-0.16f, -1.03f, 0.05f),
                new Vector3(0.16f, 0.94f, 0.82f),
                "Maps to Swim_LeftThighAttachment, Swim_LeftCalfAttachment, Swim_LeftFinAttachment; fin is visual source only.",
                "VIS_LeftThighShell,VIS_LeftKneeStrap,VIS_LeftCalfShell,VIS_LeftFinBlade,VIS_LeftFinRibTrim"),
            new SuitPartSpec(
                "RightThighCalfFin",
                SuitPartKind.RightThighCalfFin,
                "GEN_PlayerSuit_RightThighCalfFin_Source_LOD0",
                new Vector3(0.16f, -1.03f, 0.05f),
                new Vector3(0.16f, 0.94f, 0.82f),
                "Maps to Swim_RightThighAttachment, Swim_RightCalfAttachment, Swim_RightFinAttachment; fin is visual source only.",
                "VIS_RightThighShell,VIS_RightKneeStrap,VIS_RightCalfShell,VIS_RightFinBlade,VIS_RightFinRibTrim"),
            new SuitPartSpec(
                "HelmetVisorHousing",
                SuitPartKind.HelmetVisorHousing,
                "GEN_PlayerSuit_HelmetVisorHousing_Source_LOD0",
                new Vector3(0f, 0.58f, 0.18f),
                new Vector3(0.48f, 0.34f, 0.38f),
                "Maps to Suit_Visor visual housing and visor/HUD roots; replacement owner must preserve existing visor controller references.",
                "VIS_HelmetPressureCollar,VIS_VisorHousingShell,VIS_SideServiceCaps,VIS_HUDProjectorClearance,VIS_DirtyGlassSeat"),
            new SuitPartSpec(
                "VisorGlassSupportRim",
                SuitPartKind.VisorGlassSupportRim,
                "GEN_PlayerSuit_VisorGlassSupportRim_Source_LOD0",
                new Vector3(0f, 0.58f, 0.39f),
                new Vector3(0.42f, 0.20f, 0.08f),
                "Maps to Suit_Visor rim and HUD projection roots; rim only, no glass material creation and no SphereCollider change.",
                "VIS_VisorGlassRim,VIS_GasketLip,VIS_CyanInstrumentRim,VIS_AmberSealTabs,VIS_ScratchGuideTrim")
        };

        [MenuItem("Hecton8/Product Face/Author Player Suit Mesh Sources 1877", false, 1877)]
        public static void AuthorAllFromMenu()
        {
            AuthorAll(DefaultGlobalQualityWeight);
        }

        public static PlayerSuitMeshAuthoringReport AuthorAll(float globalQualityWeight)
        {
            PlayerSuitMeshAuthoringReport report = new PlayerSuitMeshAuthoringReport();
            float quality = SanitizeQuality(globalQualityWeight);

            if (!ValidateSourceAssumptions(out string assumptionFailure))
            {
                report.FailureReason = assumptionFailure;
                Debug.LogError("[ProductFacePlayerSuitMeshSourceAuthoring1877] " + assumptionFailure);
                return report;
            }

            EnsureAssetFolder(OutputFolder);

            HashSet<string> sourceNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < SuitPartSpecs.Length; i++)
            {
                SuitPartSpec spec = SuitPartSpecs[i];
                MeshBuilder builder = new MeshBuilder();
                BuildPartMesh(spec, quality, builder);

                if (!TryCreateMesh(spec, builder, sourceNames, out Mesh mesh, out string failure))
                {
                    report.FailureReason = spec.SourceName + ": " + failure;
                    Debug.LogError("[ProductFacePlayerSuitMeshSourceAuthoring1877] " + report.FailureReason);
                    return report;
                }

                string meshPath = OutputFolder + "/" + spec.MeshAssetName + ".asset";
                int vertexCount = mesh.vertexCount;
                int triangleCount = ResolveTriangleCount(mesh);
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(mesh, existing);
                    existing.name = spec.MeshAssetName;
                    EditorUtility.SetDirty(existing);
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
                else
                {
                    AssetDatabase.CreateAsset(mesh, meshPath);
                }

                report.MeshesWritten++;
                report.VertexCount += vertexCount;
                report.TriangleCount += triangleCount;
            }

            AssetDatabase.SaveAssets();
            report.Success = true;
            report.OutputFolder = OutputFolder;
            report.GlobalQualityWeight = quality;
            return report;
        }

        public static SuitPartSpec[] GetSpecsForStaticAudit()
        {
            SuitPartSpec[] copy = new SuitPartSpec[SuitPartSpecs.Length];
            Array.Copy(SuitPartSpecs, copy, SuitPartSpecs.Length);
            return copy;
        }

        private static bool ValidateSourceAssumptions(out string failure)
        {
            if (SuitPartSpecs.Length != 10)
            {
                failure = "Expected exactly 10 player suit mesh specs.";
                return false;
            }

            HashSet<string> sourceNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < SuitPartSpecs.Length; i++)
            {
                SuitPartSpec spec = SuitPartSpecs[i];
                if (string.IsNullOrWhiteSpace(spec.SourceName) ||
                    string.IsNullOrWhiteSpace(spec.MeshAssetName) ||
                    string.IsNullOrWhiteSpace(spec.FutureMappingComment) ||
                    string.IsNullOrWhiteSpace(spec.VisualPartNames))
                {
                    failure = "Player suit spec has missing identity or mapping metadata at index " + i.ToString();
                    return false;
                }

                if (!sourceNames.Add(spec.SourceName))
                {
                    failure = "Duplicate player suit source name: " + spec.SourceName;
                    return false;
                }
            }

            for (int i = 0; i < RequiredMaterialSources.Length; i++)
            {
                string path = RequiredMaterialSources[i];
                if (AssetDatabase.LoadAssetAtPath<Material>(path) == null)
                {
                    failure = "Required material source missing; no material will be created by this task: " + path;
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private static void BuildPartMesh(SuitPartSpec spec, float quality, MeshBuilder builder)
        {
            ResolveQuality(quality, out int radialSegments, out int hoseSegments, out int trimCount, out float bevel);
            Vector3 c = spec.Center;
            Vector3 s = spec.Size;
            float side = ResolveSideSign(spec.Kind);

            switch (spec.Kind)
            {
                case SuitPartKind.LeftGloveForearm:
                case SuitPartKind.RightGloveForearm:
                    builder.AppendTaperedLimbShell(c + new Vector3(side * 0.02f, 0.02f, -s.z * 0.35f), c + new Vector3(side * 0.05f, -0.02f, s.z * 0.28f), s.x * 0.42f, s.x * 0.30f, radialSegments, SlotGraphiteRubber, new Color32(36, 42, 42, 210));
                    builder.AppendBeveledHardPlate(c + new Vector3(side * 0.015f, 0.075f, -s.z * 0.02f), new Vector3(s.x * 0.38f, s.y * 0.20f, s.z * 0.20f), bevel, SlotWetPlate, new Color32(76, 88, 88, 150));
                    builder.AppendBeveledHardPlate(c + new Vector3(side * 0.03f, -s.y * 0.34f, s.z * 0.36f), new Vector3(s.x * 0.56f, s.y * 0.30f, s.z * 0.16f), bevel, SlotGraphiteRubber, new Color32(24, 24, 22, 230));
                    builder.AppendStrapOrHose(c + new Vector3(side * s.x * 0.43f, -s.y * 0.03f, -s.z * 0.17f), c + new Vector3(side * s.x * 0.47f, -s.y * 0.05f, s.z * 0.23f), 0.018f, hoseSegments, SlotTrimLatch, new Color32(58, 56, 48, 180));
                    builder.AppendInstrumentTrimStrip(c + new Vector3(side * s.x * 0.03f, s.y * 0.52f, -s.z * 0.08f), new Vector3(s.x * 0.52f, 0.010f, 0.018f), side, SlotGlassEmission, new Color32(40, 220, 195, 255));
                    builder.AppendLatchBlock(c + new Vector3(side * s.x * 0.42f, s.y * 0.20f, s.z * 0.05f), new Vector3(0.024f, 0.032f, 0.042f), bevel, new Color32(220, 150, 55, 240));
                    break;

                case SuitPartKind.LeftShoulderChestEdge:
                case SuitPartKind.RightShoulderChestEdge:
                    builder.AppendTaperedLimbShell(c + new Vector3(side * s.x * 0.23f, -s.y * 0.05f, -s.z * 0.23f), c + new Vector3(side * s.x * 0.47f, 0.02f, s.z * 0.08f), s.y * 0.44f, s.y * 0.34f, radialSegments, SlotGraphiteRubber, new Color32(38, 42, 40, 210));
                    builder.AppendBeveledHardPlate(c + new Vector3(side * -s.x * 0.08f, s.y * 0.02f, -s.z * 0.02f), new Vector3(s.x * 0.32f, s.y * 0.34f, s.z * 0.34f), bevel, SlotWetPlate, new Color32(82, 94, 92, 150));
                    builder.AppendStrapOrHose(c + new Vector3(side * -s.x * 0.27f, s.y * 0.30f, -s.z * 0.36f), c + new Vector3(side * s.x * 0.34f, -s.y * 0.18f, s.z * 0.24f), 0.016f, hoseSegments, SlotTrimLatch, new Color32(50, 48, 42, 190));
                    builder.AppendInstrumentTrimStrip(c + new Vector3(side * s.x * 0.10f, s.y * 0.44f, s.z * 0.20f), new Vector3(s.x * 0.42f, 0.012f, 0.016f), side, SlotGlassEmission, new Color32(38, 205, 190, 255));
                    builder.AppendLatchBlock(c + new Vector3(side * s.x * 0.28f, -s.y * 0.30f, s.z * 0.18f), new Vector3(0.040f, 0.026f, 0.040f), bevel, new Color32(220, 155, 55, 230));
                    break;

                case SuitPartKind.TorsoHardShell:
                    builder.AppendBeveledHardPlate(c + new Vector3(0f, s.y * 0.05f, -s.z * 0.02f), new Vector3(s.x * 0.52f, s.y * 0.42f, s.z * 0.46f), bevel, SlotWetPlate, new Color32(72, 84, 84, 150));
                    builder.AppendBeveledHardPlate(c + new Vector3(0f, -s.y * 0.23f, s.z * 0.16f), new Vector3(s.x * 0.44f, s.y * 0.20f, s.z * 0.30f), bevel, SlotGraphiteRubber, new Color32(30, 34, 32, 220));
                    builder.AppendStrapOrHose(c + new Vector3(-s.x * 0.36f, s.y * 0.42f, s.z * 0.12f), c + new Vector3(s.x * 0.36f, -s.y * 0.32f, s.z * 0.18f), 0.020f, hoseSegments, SlotTrimLatch, new Color32(46, 44, 38, 210));
                    builder.AppendStrapOrHose(c + new Vector3(s.x * 0.36f, s.y * 0.42f, s.z * 0.12f), c + new Vector3(-s.x * 0.36f, -s.y * 0.32f, s.z * 0.18f), 0.020f, hoseSegments, SlotTrimLatch, new Color32(46, 44, 38, 210));
                    for (int i = 0; i < trimCount; i++)
                    {
                        float x = Mathf.Lerp(-s.x * 0.34f, s.x * 0.34f, trimCount <= 1 ? 0.5f : i / (float)(trimCount - 1));
                        builder.AppendInstrumentTrimStrip(c + new Vector3(x, s.y * 0.26f, s.z * 0.52f), new Vector3(0.012f, 0.055f, 0.010f), 1f, SlotGlassEmission, new Color32(35, 215, 190, 255));
                    }
                    builder.AppendLatchBlock(c + new Vector3(-s.x * 0.45f, -s.y * 0.05f, s.z * 0.42f), new Vector3(0.040f, 0.060f, 0.032f), bevel, new Color32(220, 150, 55, 235));
                    builder.AppendLatchBlock(c + new Vector3(s.x * 0.45f, -s.y * 0.05f, s.z * 0.42f), new Vector3(0.040f, 0.060f, 0.032f), bevel, new Color32(220, 150, 55, 235));
                    break;

                case SuitPartKind.PelvisHarness:
                    builder.AppendBeveledHardPlate(c, new Vector3(s.x * 0.48f, s.y * 0.28f, s.z * 0.40f), bevel, SlotGraphiteRubber, new Color32(34, 36, 34, 220));
                    builder.AppendStrapOrHose(c + new Vector3(-s.x * 0.50f, s.y * 0.08f, s.z * 0.16f), c + new Vector3(s.x * 0.50f, s.y * 0.08f, s.z * 0.16f), 0.024f, hoseSegments, SlotTrimLatch, new Color32(56, 50, 42, 210));
                    builder.AppendStrapOrHose(c + new Vector3(-s.x * 0.26f, -s.y * 0.30f, s.z * 0.12f), c + new Vector3(-s.x * 0.05f, s.y * 0.28f, s.z * 0.20f), 0.018f, hoseSegments, SlotTrimLatch, new Color32(48, 46, 40, 200));
                    builder.AppendStrapOrHose(c + new Vector3(s.x * 0.26f, -s.y * 0.30f, s.z * 0.12f), c + new Vector3(s.x * 0.05f, s.y * 0.28f, s.z * 0.20f), 0.018f, hoseSegments, SlotTrimLatch, new Color32(48, 46, 40, 200));
                    builder.AppendLatchBlock(c + new Vector3(-s.x * 0.38f, s.y * 0.16f, s.z * 0.38f), new Vector3(0.038f, 0.036f, 0.032f), bevel, new Color32(220, 150, 55, 230));
                    builder.AppendLatchBlock(c + new Vector3(s.x * 0.38f, s.y * 0.16f, s.z * 0.38f), new Vector3(0.038f, 0.036f, 0.032f), bevel, new Color32(220, 150, 55, 230));
                    break;

                case SuitPartKind.LeftThighCalfFin:
                case SuitPartKind.RightThighCalfFin:
                    builder.AppendTaperedLimbShell(c + new Vector3(0f, s.y * 0.34f, -s.z * 0.02f), c + new Vector3(0f, -s.y * 0.02f, s.z * 0.02f), s.x * 0.48f, s.x * 0.38f, radialSegments, SlotGraphiteRubber, new Color32(34, 38, 36, 215));
                    builder.AppendTaperedLimbShell(c + new Vector3(0f, -s.y * 0.05f, s.z * 0.01f), c + new Vector3(0f, -s.y * 0.42f, s.z * 0.07f), s.x * 0.36f, s.x * 0.30f, radialSegments, SlotGraphiteRubber, new Color32(32, 36, 34, 215));
                    builder.AppendStrapOrHose(c + new Vector3(side * s.x * 0.38f, s.y * 0.15f, s.z * 0.07f), c + new Vector3(side * s.x * 0.40f, -s.y * 0.32f, s.z * 0.13f), 0.015f, hoseSegments, SlotTrimLatch, new Color32(50, 48, 42, 180));
                    builder.AppendFinBlade(c + new Vector3(0f, -s.y * 0.57f, s.z * 0.32f), side, s.x * 1.36f, s.z * 0.52f, Mathf.RoundToInt(Mathf.Lerp(4f, 9f, quality)), SlotWetPlate);
                    builder.AppendInstrumentTrimStrip(c + new Vector3(side * s.x * 0.20f, -s.y * 0.14f, s.z * 0.22f), new Vector3(0.010f, 0.10f, 0.012f), side, SlotGlassEmission, new Color32(38, 210, 190, 255));
                    builder.AppendLatchBlock(c + new Vector3(side * s.x * 0.36f, s.y * 0.18f, s.z * 0.18f), new Vector3(0.024f, 0.034f, 0.032f), bevel, new Color32(215, 145, 55, 230));
                    break;

                case SuitPartKind.HelmetVisorHousing:
                    builder.AppendBeveledHardPlate(c + new Vector3(0f, 0f, -s.z * 0.10f), new Vector3(s.x * 0.50f, s.y * 0.48f, s.z * 0.44f), bevel, SlotWetPlate, new Color32(72, 84, 84, 150));
                    builder.AppendCurvedVisorRimBand(c + new Vector3(0f, 0f, s.z * 0.35f), s.x * 0.78f, s.y * 0.56f, 0.050f, radialSegments + 6, SlotTrimLatch, new Color32(48, 48, 42, 220));
                    builder.AppendCurvedVisorRimBand(c + new Vector3(0f, 0f, s.z * 0.40f), s.x * 0.60f, s.y * 0.38f, 0.026f, radialSegments + 4, SlotGlassEmission, new Color32(42, 210, 190, 245));
                    builder.AppendStrapOrHose(c + new Vector3(-s.x * 0.42f, -s.y * 0.20f, -s.z * 0.28f), c + new Vector3(s.x * 0.42f, -s.y * 0.20f, -s.z * 0.28f), 0.020f, hoseSegments, SlotTrimLatch, new Color32(46, 44, 40, 210));
                    builder.AppendLatchBlock(c + new Vector3(-s.x * 0.42f, -s.y * 0.35f, s.z * 0.30f), new Vector3(0.035f, 0.035f, 0.032f), bevel, new Color32(220, 150, 55, 230));
                    builder.AppendLatchBlock(c + new Vector3(s.x * 0.42f, -s.y * 0.35f, s.z * 0.30f), new Vector3(0.035f, 0.035f, 0.032f), bevel, new Color32(220, 150, 55, 230));
                    break;

                case SuitPartKind.VisorGlassSupportRim:
                    builder.AppendCurvedVisorRimBand(c, s.x, s.y, 0.040f, radialSegments + 8, SlotTrimLatch, new Color32(52, 54, 48, 230));
                    builder.AppendCurvedVisorRimBand(c + new Vector3(0f, 0f, 0.018f), s.x * 0.82f, s.y * 0.70f, 0.022f, radialSegments + 6, SlotGlassEmission, new Color32(36, 215, 198, 250));
                    for (int i = 0; i < Mathf.Max(4, trimCount); i++)
                    {
                        float t = i / (float)Mathf.Max(1, trimCount - 1);
                        float angle = Mathf.Lerp(205f, 335f, t) * Mathf.Deg2Rad;
                        Vector3 p = c + new Vector3(Mathf.Cos(angle) * s.x * 0.54f, Mathf.Sin(angle) * s.y * 0.54f, 0.042f);
                        builder.AppendLatchBlock(p, new Vector3(0.018f, 0.014f, 0.012f), bevel * 0.6f, new Color32(220, 150, 55, 230));
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static bool TryCreateMesh(
            SuitPartSpec spec,
            MeshBuilder builder,
            HashSet<string> sourceNames,
            out Mesh mesh,
            out string failure)
        {
            mesh = null;
            failure = string.Empty;

            if (!sourceNames.Add(spec.SourceName))
            {
                failure = "duplicate source name.";
                return false;
            }

            if (builder.VertexCount == 0 || builder.IndexCount == 0)
            {
                failure = "empty mesh source.";
                return false;
            }

            if (builder.IndexCount % 3 != 0)
            {
                failure = "index count is not divisible by 3.";
                return false;
            }

            if (builder.SubmeshCount != MaterialSlotCount)
            {
                failure = "material slot/submesh count mismatch.";
                return false;
            }

            if (!builder.ValidateTopology(MinimumTriangleArea, out failure))
                return false;

            mesh = builder.ToMesh(spec.MeshAssetName);
            if (mesh.vertexCount == 0 || ResolveTriangleCount(mesh) == 0 || mesh.subMeshCount != MaterialSlotCount)
            {
                failure = "created mesh failed final non-empty/submesh validation.";
                UnityEngine.Object.DestroyImmediate(mesh);
                mesh = null;
                return false;
            }

            Bounds bounds = mesh.bounds;
            if (!IsFinite(bounds.center) || !IsFinite(bounds.size) || bounds.size.sqrMagnitude <= 0.0001f)
            {
                failure = "created mesh failed finite non-zero bounds validation.";
                UnityEngine.Object.DestroyImmediate(mesh);
                mesh = null;
                return false;
            }

            return true;
        }

        private static void ResolveQuality(float quality, out int radialSegments, out int hoseSegments, out int trimCount, out float bevel)
        {
            float q = SanitizeQuality(quality);
            radialSegments = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(10f, 28f, q)), 8, 32);
            hoseSegments = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(5f, 14f, q)), 4, 18);
            trimCount = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(4f, 12f, q)), 3, 16);
            bevel = Mathf.Lerp(0.006f, 0.024f, q);
        }

        private static float ResolveSideSign(SuitPartKind kind)
        {
            switch (kind)
            {
                case SuitPartKind.LeftGloveForearm:
                case SuitPartKind.LeftShoulderChestEdge:
                case SuitPartKind.LeftThighCalfFin:
                    return -1f;
                case SuitPartKind.RightGloveForearm:
                case SuitPartKind.RightShoulderChestEdge:
                case SuitPartKind.RightThighCalfFin:
                    return 1f;
                default:
                    return 1f;
            }
        }

        private static float SanitizeQuality(float value)
        {
            return IsFinite(value) ? Mathf.Clamp01(value) : 0f;
        }

        private static int ResolveTriangleCount(Mesh mesh)
        {
            if (mesh == null)
                return 0;

            int count = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
                count += (int)mesh.GetIndexCount(i) / 3;
            return count;
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Vector4 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void EnsureAssetFolder(string folder)
        {
            string normalized = folder.Replace('\\', '/').Trim('/');
            string[] parts = normalized.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                throw new InvalidOperationException("Asset folder must start with Assets/: " + folder);

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private sealed class MeshBuilder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>(4096);
            private readonly List<Vector3> _normals = new List<Vector3>(4096);
            private readonly List<Vector4> _tangents = new List<Vector4>(4096);
            private readonly List<Vector2> _uvs = new List<Vector2>(4096);
            private readonly List<Color32> _colors = new List<Color32>(4096);
            private readonly List<int>[] _submeshIndices =
            {
                new List<int>(4096),
                new List<int>(2048),
                new List<int>(2048),
                new List<int>(2048)
            };

            public int VertexCount => _vertices.Count;
            public int SubmeshCount => _submeshIndices.Length;

            public int IndexCount
            {
                get
                {
                    int count = 0;
                    for (int i = 0; i < _submeshIndices.Length; i++)
                        count += _submeshIndices[i].Count;
                    return count;
                }
            }

            public void AppendTaperedLimbShell(Vector3 start, Vector3 end, float radiusStart, float radiusEnd, int segments, int slot, Color32 color)
            {
                Vector3 axis = end - start;
                float length = axis.magnitude;
                if (length <= 0.0001f)
                    return;

                Vector3 forward = axis / length;
                ResolveBasis(forward, out Vector3 right, out Vector3 up);
                int rings = 5;
                int[,] ids = new int[rings + 1, segments];
                for (int r = 0; r <= rings; r++)
                {
                    float t = r / (float)rings;
                    float radius = Mathf.Lerp(radiusStart, radiusEnd, t);
                    Vector3 center = Vector3.Lerp(start, end, t);
                    for (int i = 0; i < segments; i++)
                    {
                        float angle = Mathf.PI * 2f * i / segments;
                        float oval = 1f + Mathf.Sin(angle * 2f) * 0.08f;
                        Vector3 radial = (right * Mathf.Cos(angle) * 0.82f) + (up * Mathf.Sin(angle) * 1.10f);
                        Vector3 position = center + radial * radius * oval;
                        Vector3 normal = radial.normalized;
                        ids[r, i] = AddVertex(position, normal, forward, new Vector2(i / (float)segments, t), color);
                    }
                }

                for (int r = 0; r < rings; r++)
                {
                    for (int i = 0; i < segments; i++)
                    {
                        int next = (i + 1) % segments;
                        AppendQuad(slot, ids[r, i], ids[r, next], ids[r + 1, next], ids[r + 1, i]);
                    }
                }

                AppendCap(ids, 0, -forward, slot, color);
                AppendCap(ids, rings, forward, slot, color);
            }

            public void AppendBeveledHardPlate(Vector3 center, Vector3 half, float bevel, int slot, Color32 color)
            {
                float b = Mathf.Clamp(bevel, 0.001f, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.42f);
                Vector3 core = new Vector3(Mathf.Max(0.001f, half.x - b), Mathf.Max(0.001f, half.y - b), Mathf.Max(0.001f, half.z - b));
                AppendBoxFaces(center, core, slot, color);
                AppendBoxFaces(center + new Vector3(half.x - b * 0.5f, 0f, 0f), new Vector3(b * 0.5f, core.y, core.z), slot, color);
                AppendBoxFaces(center + new Vector3(-half.x + b * 0.5f, 0f, 0f), new Vector3(b * 0.5f, core.y, core.z), slot, color);
                AppendBoxFaces(center + new Vector3(0f, half.y - b * 0.5f, 0f), new Vector3(core.x, b * 0.5f, core.z), slot, color);
                AppendBoxFaces(center + new Vector3(0f, -half.y + b * 0.5f, 0f), new Vector3(core.x, b * 0.5f, core.z), slot, color);
                AppendBoxFaces(center + new Vector3(0f, 0f, half.z - b * 0.5f), new Vector3(core.x, core.y, b * 0.5f), slot, color);
                AppendBoxFaces(center + new Vector3(0f, 0f, -half.z + b * 0.5f), new Vector3(core.x, core.y, b * 0.5f), slot, color);
            }

            public void AppendCurvedVisorRimBand(Vector3 center, float radiusX, float radiusY, float thickness, int segments, int slot, Color32 color)
            {
                int clampedSegments = Mathf.Max(8, segments);
                float start = 205f * Mathf.Deg2Rad;
                float end = 335f * Mathf.Deg2Rad;
                int lastOuterFront = -1;
                int lastInnerFront = -1;
                int lastOuterBack = -1;
                int lastInnerBack = -1;
                for (int i = 0; i <= clampedSegments; i++)
                {
                    float t = i / (float)clampedSegments;
                    float a = Mathf.Lerp(start, end, t);
                    Vector3 radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    Vector3 outer = center + new Vector3(radial.x * radiusX, radial.y * radiusY, thickness * 0.5f);
                    Vector3 inner = center + new Vector3(radial.x * Mathf.Max(0.001f, radiusX - thickness), radial.y * Mathf.Max(0.001f, radiusY - thickness), thickness * 0.5f);
                    Vector3 outerBack = outer - Vector3.forward * thickness;
                    Vector3 innerBack = inner - Vector3.forward * thickness;
                    Vector3 normal = radial.normalized;
                    int of = AddVertex(outer, normal, Vector3.right, new Vector2(t, 1f), color);
                    int inf = AddVertex(inner, -normal, Vector3.right, new Vector2(t, 0f), color);
                    int ob = AddVertex(outerBack, normal, Vector3.right, new Vector2(t, 1f), color);
                    int ib = AddVertex(innerBack, -normal, Vector3.right, new Vector2(t, 0f), color);
                    if (lastOuterFront >= 0)
                    {
                        AppendQuad(slot, lastOuterFront, of, inf, lastInnerFront);
                        AppendQuad(slot, lastOuterBack, lastInnerBack, ib, ob);
                        AppendQuad(slot, lastOuterFront, lastOuterBack, ob, of);
                        AppendQuad(slot, lastInnerFront, inf, ib, lastInnerBack);
                    }

                    lastOuterFront = of;
                    lastInnerFront = inf;
                    lastOuterBack = ob;
                    lastInnerBack = ib;
                }
            }

            public void AppendStrapOrHose(Vector3 start, Vector3 end, float radius, int segments, int slot, Color32 color)
            {
                AppendCylinder(start, end, radius, segments, slot, color);
            }

            public void AppendFinBlade(Vector3 root, float side, float span, float length, int ribs, int slot)
            {
                int ribCount = Mathf.Max(3, ribs);
                Vector3 tip = root + new Vector3(side * span * 0.05f, -length * 0.12f, length);
                Vector3 left = root + new Vector3(-span * 0.5f, 0f, -length * 0.10f);
                Vector3 right = root + new Vector3(span * 0.5f, 0f, -length * 0.10f);
                Vector3 normal = Vector3.up;
                int i0 = AddVertex(left, normal, Vector3.right, new Vector2(0f, 0f), new Color32(42, 54, 56, 190));
                int i1 = AddVertex(right, normal, Vector3.right, new Vector2(1f, 0f), new Color32(42, 54, 56, 190));
                int i2 = AddVertex(tip, normal, Vector3.right, new Vector2(0.5f, 1f), new Color32(54, 72, 74, 190));
                AddTriangle(slot, i0, i1, i2);
                int ib0 = AddVertex(left - Vector3.up * 0.012f, -normal, Vector3.right, new Vector2(0f, 0f), new Color32(32, 42, 44, 190));
                int ib1 = AddVertex(right - Vector3.up * 0.012f, -normal, Vector3.right, new Vector2(1f, 0f), new Color32(32, 42, 44, 190));
                int ib2 = AddVertex(tip - Vector3.up * 0.012f, -normal, Vector3.right, new Vector2(0.5f, 1f), new Color32(32, 42, 44, 190));
                AddTriangle(slot, ib1, ib0, ib2);

                for (int r = 0; r < ribCount; r++)
                {
                    float t = ribCount <= 1 ? 0.5f : r / (float)(ribCount - 1);
                    float x = Mathf.Lerp(-span * 0.38f, span * 0.38f, t);
                    AppendBeveledHardPlate(root + new Vector3(x, 0.010f, length * 0.30f), new Vector3(0.010f, 0.012f, length * 0.36f), 0.004f, SlotTrimLatch, new Color32(72, 82, 78, 150));
                }
            }

            public void AppendLatchBlock(Vector3 center, Vector3 half, float bevel, Color32 color)
            {
                AppendBeveledHardPlate(center, half, bevel, SlotTrimLatch, color);
            }

            public void AppendInstrumentTrimStrip(Vector3 center, Vector3 half, float side, int slot, Color32 color)
            {
                Quaternion rotation = Quaternion.Euler(0f, 0f, side * 8f);
                AppendOrientedBox(center, half, rotation, slot, color);
            }

            public bool ValidateTopology(float minimumTriangleArea, out string failure)
            {
                failure = string.Empty;
                for (int slot = 0; slot < _submeshIndices.Length; slot++)
                {
                    List<int> indices = _submeshIndices[slot];
                    if (indices.Count % 3 != 0)
                    {
                        failure = "submesh index count not divisible by 3.";
                        return false;
                    }

                    for (int i = 0; i < indices.Count; i += 3)
                    {
                        int ia = indices[i];
                        int ib = indices[i + 1];
                        int ic = indices[i + 2];
                        if ((uint)ia >= (uint)_vertices.Count || (uint)ib >= (uint)_vertices.Count || (uint)ic >= (uint)_vertices.Count)
                        {
                            failure = "index out of range.";
                            return false;
                        }

                        Vector3 a = _vertices[ia];
                        Vector3 b = _vertices[ib];
                        Vector3 c = _vertices[ic];
                        Vector3 n = _normals[ia];
                        Vector4 t = _tangents[ia];
                        Vector2 uv = _uvs[ia];
                        if (!IsFinite(a) || !IsFinite(b) || !IsFinite(c) || !IsFinite(n) || !IsFinite(t) || !IsFinite(uv))
                        {
                            failure = "non-finite vertex stream.";
                            return false;
                        }

                        float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                        if (!IsFinite(area) || area <= minimumTriangleArea)
                        {
                            failure = "degenerate triangle.";
                            return false;
                        }

                        float normalLength = n.magnitude;
                        if (normalLength < 0.995f || normalLength > 1.005f)
                        {
                            failure = "normal length outside tolerance.";
                            return false;
                        }

                        float tangentLength = new Vector3(t.x, t.y, t.z).magnitude;
                        if (tangentLength < 0.995f || tangentLength > 1.005f || Mathf.Abs(Mathf.Abs(t.w) - 1f) > 0.001f)
                        {
                            failure = "tangent outside tolerance.";
                            return false;
                        }
                    }
                }

                return true;
            }

            public Mesh ToMesh(string name)
            {
                Mesh mesh = new Mesh
                {
                    name = name,
                    indexFormat = _vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
                };

                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetTangents(_tangents);
                mesh.SetUVs(0, _uvs);
                mesh.SetColors(_colors);
                mesh.subMeshCount = _submeshIndices.Length;
                for (int i = 0; i < _submeshIndices.Length; i++)
                    mesh.SetTriangles(_submeshIndices[i], i, true);
                mesh.RecalculateBounds();
                return mesh;
            }

            private void AppendCylinder(Vector3 start, Vector3 end, float radius, int segments, int slot, Color32 color)
            {
                Vector3 axis = end - start;
                float length = axis.magnitude;
                if (length <= 0.0001f)
                    return;

                Vector3 forward = axis / length;
                ResolveBasis(forward, out Vector3 right, out Vector3 up);
                int baseIndex = _vertices.Count;
                for (int i = 0; i < segments; i++)
                {
                    float angle = Mathf.PI * 2f * i / segments;
                    Vector3 normal = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                    AddVertex(start + normal * radius, normal, forward, new Vector2(i / (float)segments, 0f), color);
                    AddVertex(end + normal * radius, normal, forward, new Vector2(i / (float)segments, 1f), color);
                }

                int startCenter = AddVertex(start, -forward, right, new Vector2(0.5f, 0.5f), color);
                int endCenter = AddVertex(end, forward, right, new Vector2(0.5f, 0.5f), color);
                for (int i = 0; i < segments; i++)
                {
                    int next = (i + 1) % segments;
                    int a = baseIndex + i * 2;
                    int b = baseIndex + next * 2;
                    int c = baseIndex + next * 2 + 1;
                    int d = baseIndex + i * 2 + 1;
                    AddTriangle(slot, a, b, c);
                    AddTriangle(slot, a, c, d);
                    AddTriangle(slot, startCenter, b, a);
                    AddTriangle(slot, endCenter, d, c);
                }
            }

            private void AppendOrientedBox(Vector3 center, Vector3 half, Quaternion rotation, int slot, Color32 color)
            {
                Vector3[] p =
                {
                    center + rotation * new Vector3(-half.x, -half.y, -half.z),
                    center + rotation * new Vector3(half.x, -half.y, -half.z),
                    center + rotation * new Vector3(half.x, half.y, -half.z),
                    center + rotation * new Vector3(-half.x, half.y, -half.z),
                    center + rotation * new Vector3(-half.x, -half.y, half.z),
                    center + rotation * new Vector3(half.x, -half.y, half.z),
                    center + rotation * new Vector3(half.x, half.y, half.z),
                    center + rotation * new Vector3(-half.x, half.y, half.z)
                };

                AppendQuad(slot, p[4], p[5], p[6], p[7], rotation * Vector3.forward, color);
                AppendQuad(slot, p[1], p[0], p[3], p[2], rotation * Vector3.back, color);
                AppendQuad(slot, p[0], p[4], p[7], p[3], rotation * Vector3.left, color);
                AppendQuad(slot, p[5], p[1], p[2], p[6], rotation * Vector3.right, color);
                AppendQuad(slot, p[7], p[6], p[2], p[3], rotation * Vector3.up, color);
                AppendQuad(slot, p[0], p[1], p[5], p[4], rotation * Vector3.down, color);
            }

            private void AppendBoxFaces(Vector3 center, Vector3 half, int slot, Color32 color)
            {
                Vector3 min = center - half;
                Vector3 max = center + half;
                AppendQuad(slot, new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), Vector3.forward, color);
                AppendQuad(slot, new Vector3(max.x, min.y, min.z), new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), Vector3.back, color);
                AppendQuad(slot, new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), Vector3.left, color);
                AppendQuad(slot, new Vector3(max.x, min.y, max.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), Vector3.right, color);
                AppendQuad(slot, new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z), Vector3.up, color);
                AppendQuad(slot, new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), Vector3.down, color);
            }

            private void AppendCap(int[,] ids, int ring, Vector3 normal, int slot, Color32 color)
            {
                int count = ids.GetLength(1);
                Vector3 center = Vector3.zero;
                for (int i = 0; i < count; i++)
                    center += _vertices[ids[ring, i]];
                center /= count;
                int centerId = AddVertex(center, normal, ResolveTangent(normal), new Vector2(0.5f, 0.5f), color);
                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;
                    if (normal.z >= 0f || normal.y >= 0f)
                        AddTriangle(slot, centerId, ids[ring, i], ids[ring, next]);
                    else
                        AddTriangle(slot, centerId, ids[ring, next], ids[ring, i]);
                }
            }

            private void AppendQuad(int slot, int a, int b, int c, int d)
            {
                AddTriangle(slot, a, b, c);
                AddTriangle(slot, a, c, d);
            }

            private void AppendQuad(int slot, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color32 color)
            {
                Vector3 n = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
                Vector3 tangent = ResolveTangent(n);
                int ia = AddVertex(a, n, tangent, new Vector2(0f, 0f), color);
                int ib = AddVertex(b, n, tangent, new Vector2(1f, 0f), color);
                int ic = AddVertex(c, n, tangent, new Vector2(1f, 1f), color);
                int id = AddVertex(d, n, tangent, new Vector2(0f, 1f), color);
                AddTriangle(slot, ia, ib, ic);
                AddTriangle(slot, ia, ic, id);
            }

            private int AddVertex(Vector3 position, Vector3 normal, Vector3 tangent, Vector2 uv, Color32 color)
            {
                Vector3 n = normal.sqrMagnitude > 0.0001f && IsFinite(normal) ? normal.normalized : Vector3.up;
                Vector3 t = tangent.sqrMagnitude > 0.0001f && IsFinite(tangent) ? tangent.normalized : ResolveTangent(n);
                int index = _vertices.Count;
                _vertices.Add(position);
                _normals.Add(n);
                _tangents.Add(new Vector4(t.x, t.y, t.z, 1f));
                _uvs.Add(uv);
                _colors.Add(color);
                return index;
            }

            private void AddTriangle(int slot, int a, int b, int c)
            {
                _submeshIndices[slot].Add(a);
                _submeshIndices[slot].Add(b);
                _submeshIndices[slot].Add(c);
            }

            private static void ResolveBasis(Vector3 forward, out Vector3 right, out Vector3 up)
            {
                Vector3 helper = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.92f ? Vector3.right : Vector3.up;
                right = Vector3.Cross(helper, forward).normalized;
                up = Vector3.Cross(forward, right).normalized;
            }

            private static Vector3 ResolveTangent(Vector3 normal)
            {
                return Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.92f
                    ? Vector3.right
                    : Vector3.Cross(Vector3.up, normal).normalized;
            }
        }

        public readonly struct SuitPartSpec
        {
            public readonly string SourceName;
            public readonly SuitPartKind Kind;
            public readonly string MeshAssetName;
            public readonly Vector3 Center;
            public readonly Vector3 Size;
            public readonly string FutureMappingComment;
            public readonly string VisualPartNames;

            public SuitPartSpec(
                string sourceName,
                SuitPartKind kind,
                string meshAssetName,
                Vector3 center,
                Vector3 size,
                string futureMappingComment,
                string visualPartNames)
            {
                SourceName = sourceName;
                Kind = kind;
                MeshAssetName = meshAssetName;
                Center = center;
                Size = size;
                FutureMappingComment = futureMappingComment;
                VisualPartNames = visualPartNames;
            }
        }

        public struct PlayerSuitMeshAuthoringReport
        {
            public bool Success;
            public string OutputFolder;
            public string FailureReason;
            public float GlobalQualityWeight;
            public int MeshesWritten;
            public int VertexCount;
            public int TriangleCount;
        }

        public enum SuitPartKind
        {
            LeftGloveForearm,
            RightGloveForearm,
            LeftShoulderChestEdge,
            RightShoulderChestEdge,
            TorsoHardShell,
            PelvisHarness,
            LeftThighCalfFin,
            RightThighCalfFin,
            HelmetVisorHousing,
            VisorGlassSupportRim
        }
    }
}
#endif
