#if UNITY_EDITOR
// ============================================================================
// HECTON-8 - ProductFaceToolMeshSourceAuthoring.cs
// Batch 18 / 1874 static source route for future product-face tool mesh assets.
//
// This is editor-only mesh source authoring. It writes Mesh assets only when a
// future Unity owner executes the menu/method. It does not edit prefabs,
// anchors, gameplay origins, colliders, materials, scenes, or runtime code.
// ============================================================================

namespace Hecton8.Editor.ProductFace
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Rendering;

    public static class ProductFaceToolMeshSourceAuthoring
    {
        private const string OutputFolder = "Assets/_Project/Art/Generated/ProductFace/Tools";
        private const string ToolDecayShaderPath = "Assets/_Project/Art/Shaders/Hecton_ToolDecayLit.shader";
        private const string ToolScreenShaderPath = "Assets/_Project/Art/Shaders/Hecton_ToolScreenDiegetic.shader";
        private const float MinimumTriangleArea = 0.0000001f;
        private const int MaterialSlotCount = 4;
        private const int SlotCasing = 0;
        private const int SlotWear = 1;
        private const int SlotTrim = 2;
        private const int SlotGlassEmission = 3;

        private static readonly ToolMeshSpec[] ToolSpecs =
        {
            new ToolMeshSpec(
                "Tool_BeaconDeployer",
                ToolSilhouette.BeaconDeployer,
                "GEN_Tool_BeaconDeployer_Source_LOD0",
                new Vector3(0.58f, 0.16f, 0.24f),
                "VIS_Body,VIS_FoldedBeaconCartridge,VIS_AntennaSpool,VIS_EmitterLens,VIS_BatteryLatch,VIS_RubberGrip,VIS_LabelDecals",
                "ANCHOR_Grip_R,ANCHOR_DeployRayOrigin,ANCHOR_Emitter,ANCHOR_CartridgeLatch,ANCHOR_Pickup,ANCHOR_AUP_LocalOrigin"),
            new ToolMeshSpec(
                "Tool_Builder",
                ToolSilhouette.Builder,
                "GEN_Tool_Builder_Source_LOD0",
                new Vector3(0.54f, 0.19f, 0.28f),
                "VIS_PistolGrip,VIS_ProjectorFork,VIS_MaterialFeedSlot,VIS_HeatVents,VIS_StatusGauge,VIS_ServiceScrews",
                "ANCHOR_Grip_R,ANCHOR_BuildRayOrigin,ANCHOR_ProjectorNozzle,ANCHOR_FeedSlot,ANCHOR_Pickup,ANCHOR_AUP_LocalOrigin"),
            new ToolMeshSpec(
                "Tool_EnvAnalyzer",
                ToolSilhouette.EnvAnalyzer,
                "GEN_Tool_EnvAnalyzer_Source_LOD0",
                new Vector3(0.50f, 0.17f, 0.24f),
                "VIS_RuggedCasing,VIS_SensorHead,VIS_SamplePort,VIS_RibbedGrip,VIS_SmallScreen,VIS_LensCluster",
                "ANCHOR_Grip_R,ANCHOR_ScanOrigin,ANCHOR_SamplePort,ANCHOR_ScreenPlane,ANCHOR_Pickup,ANCHOR_AUP_LocalOrigin"),
            new ToolMeshSpec(
                "Tool_Flashlight",
                ToolSilhouette.Flashlight,
                "GEN_Tool_Flashlight_Source_LOD0",
                new Vector3(0.64f, 0.14f, 0.14f),
                "VIS_LensBezel,VIS_BatteryTube,VIS_ClampRail,VIS_HandStrap,VIS_HeatFins,VIS_RearCap",
                "ANCHOR_Grip_R,ANCHOR_BeamOrigin,ANCHOR_LensCenter,ANCHOR_BatteryCap,ANCHOR_Pickup,ANCHOR_AUP_LocalOrigin"),
            new ToolMeshSpec(
                "Tool_HarpoonLauncher",
                ToolSilhouette.HarpoonLauncher,
                "GEN_Tool_HarpoonLauncher_Source_LOD0",
                new Vector3(0.78f, 0.22f, 0.25f),
                "VIS_BarrelRail,VIS_TetherSpool,VIS_PressureTank,VIS_GripStock,VIS_SafetyCage,VIS_HarpoonTipProxy",
                "ANCHOR_Grip_R,ANCHOR_Muzzle,ANCHOR_TetherAnchor,ANCHOR_SpoolAxis,ANCHOR_Pickup,ANCHOR_AUP_LocalOrigin"),
            new ToolMeshSpec(
                "Tool_Knife",
                ToolSilhouette.Knife,
                "GEN_Tool_Knife_Source_LOD0",
                new Vector3(0.72f, 0.08f, 0.12f),
                "VIS_BladeSerrated,VIS_PrySpine,VIS_Guard,VIS_WrappedGrip,VIS_SheathClip",
                "ANCHOR_Grip_R,ANCHOR_BladeEdgeStart,ANCHOR_BladeEdgeEnd,ANCHOR_CutProxy,ANCHOR_Pickup"),
            new ToolMeshSpec(
                "Tool_LaserCutter",
                ToolSilhouette.LaserCutter,
                "GEN_Tool_LaserCutter_Source_LOD0",
                new Vector3(0.62f, 0.20f, 0.24f),
                "VIS_EmitterHead,VIS_CeramicNozzle,VIS_HeatSinkFins,VIS_PowerCell,VIS_SafetyShield,VIS_StatusDisplay,VIS_Grip",
                "ANCHOR_Grip_R,ANCHOR_BeamOrigin,ANCHOR_Emitter,ANCHOR_HeatVent,ANCHOR_StatusScreen,ANCHOR_Pickup,ANCHOR_AUP_LocalOrigin"),
            new ToolMeshSpec(
                "Tool_Propulsion",
                ToolSilhouette.Propulsion,
                "GEN_Tool_Propulsion_Source_LOD0",
                new Vector3(0.66f, 0.22f, 0.26f),
                "VIS_IntakeGrille,VIS_DuctedNozzle,VIS_BatteryPumpBody,VIS_GripRails,VIS_CageFins,VIS_ServiceLatch",
                "ANCHOR_Grip_LR,ANCHOR_ThrustOrigin,ANCHOR_Intake,ANCHOR_ServiceLatch,ANCHOR_Pickup,ANCHOR_AUP_LocalOrigin"),
            new ToolMeshSpec(
                "Tool_Repair",
                ToolSilhouette.Repair,
                "GEN_Tool_Repair_Source_LOD0",
                new Vector3(0.58f, 0.19f, 0.24f),
                "VIS_WelderNozzle,VIS_HeatShield,VIS_CableBattery,VIS_Grip,VIS_PressureGauge,VIS_PatchFeedSlot",
                "ANCHOR_Grip_R,ANCHOR_WeldRayOrigin,ANCHOR_Nozzle,ANCHOR_GaugeScreen,ANCHOR_Pickup,ANCHOR_AUP_LocalOrigin"),
            new ToolMeshSpec(
                "Tool_SalvageSampler",
                ToolSilhouette.SalvageSampler,
                "GEN_Tool_SalvageSampler_Source_LOD0",
                new Vector3(0.62f, 0.19f, 0.25f),
                "VIS_ClampJaws,VIS_SampleTubeGlass,VIS_SensorFork,VIS_CartridgeSlot,VIS_Grip,VIS_Readout",
                "ANCHOR_Grip_R,ANCHOR_SampleContact,ANCHOR_JawPivot,ANCHOR_CartridgeSlot,ANCHOR_Pickup,ANCHOR_AUP_LocalOrigin"),
            new ToolMeshSpec(
                "Tool_Scanner",
                ToolSilhouette.Scanner,
                "GEN_Tool_Scanner_Source_LOD0",
                new Vector3(0.52f, 0.18f, 0.25f),
                "VIS_RuggedScannerBody,VIS_LensArray,VIS_DisplayGlass,VIS_AntennaGrille,VIS_Grip,VIS_StatusLights",
                "ANCHOR_Grip_R,ANCHOR_ScanOrigin,ANCHOR_LensCenter,ANCHOR_DisplayPlane,ANCHOR_Pickup,ANCHOR_AUP_LocalOrigin"),
            new ToolMeshSpec(
                "Tool_StunPistol",
                ToolSilhouette.StunPistol,
                "GEN_Tool_StunPistol_Source_LOD0",
                new Vector3(0.60f, 0.19f, 0.23f),
                "VIS_InsulatedGrip,VIS_TwinElectrodeMuzzle,VIS_CapacitorPack,VIS_SafetyLatch,VIS_WornRails,VIS_StatusLight",
                "ANCHOR_Grip_R,ANCHOR_Muzzle,ANCHOR_ElectrodeTips,ANCHOR_CapacitorLatch,ANCHOR_Pickup,ANCHOR_AUP_LocalOrigin")
        };

        [MenuItem("HECTON-8/Product Face/Author Tool Mesh Sources 1874", false, 1874)]
        public static void AuthorAllFromMenu()
        {
            AuthorAll(0.72f);
        }

        public static ToolMeshAuthoringReport AuthorAll(float globalQualityWeight)
        {
            ToolMeshAuthoringReport report = new ToolMeshAuthoringReport();
            float quality = Mathf.Clamp01(globalQualityWeight);

            if (!ValidateSourceAssumptions(out string assumptionFailure))
            {
                report.FailureReason = assumptionFailure;
                Debug.LogError("[ProductFaceToolMeshSourceAuthoring1874] " + assumptionFailure);
                return report;
            }

            EnsureAssetFolder(OutputFolder);

            for (int i = 0; i < ToolSpecs.Length; i++)
            {
                ToolMeshSpec spec = ToolSpecs[i];
                MeshBuilder builder = new MeshBuilder();
                BuildToolMesh(spec, quality, builder);

                if (!TryCreateMesh(spec, builder, out Mesh mesh, out string failure))
                {
                    report.FailureReason = spec.ToolId + ": " + failure;
                    Debug.LogError("[ProductFaceToolMeshSourceAuthoring1874] " + report.FailureReason);
                    return report;
                }

                string meshPath = OutputFolder + "/" + spec.MeshAssetName + ".asset";
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
                Mesh countedMesh = existing != null ? existing : mesh;
                report.VertexCount += countedMesh.vertexCount;
                report.TriangleCount += ResolveTriangleCount(countedMesh);
            }

            AssetDatabase.SaveAssets();
            report.Success = true;
            report.OutputFolder = OutputFolder;
            return report;
        }

        public static ToolMeshSpec[] GetSpecsForStaticAudit()
        {
            ToolMeshSpec[] copy = new ToolMeshSpec[ToolSpecs.Length];
            Array.Copy(ToolSpecs, copy, ToolSpecs.Length);
            return copy;
        }

        private static bool ValidateSourceAssumptions(out string failure)
        {
            if (ToolSpecs.Length != 12)
            {
                failure = "Expected exactly 12 tool mesh specs.";
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<Shader>(ToolDecayShaderPath) == null)
            {
                failure = "Required casing shader source missing: " + ToolDecayShaderPath;
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<Shader>(ToolScreenShaderPath) == null)
            {
                failure = "Required screen shader source missing: " + ToolScreenShaderPath;
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static void BuildToolMesh(ToolMeshSpec spec, float quality, MeshBuilder builder)
        {
            ResolveQuality(quality, out int radialSegments, out int finCount, out float bevel);
            Vector3 half = spec.BodySize * 0.5f;

            builder.AppendBeveledBox(Vector3.zero, half, bevel, SlotCasing, new Color32(80, 88, 84, 18));

            switch (spec.Silhouette)
            {
                case ToolSilhouette.BeaconDeployer:
                    builder.AppendBeveledBox(new Vector3(0.03f, 0.12f, 0f), new Vector3(0.18f, 0.045f, 0.11f), bevel * 0.7f, SlotWear, new Color32(112, 98, 58, 40));
                    builder.AppendCylinder(new Vector3(-0.23f, 0.13f, 0f), new Vector3(-0.23f, 0.13f, 0.20f), 0.055f, radialSegments, SlotTrim, new Color32(45, 42, 38, 80));
                    builder.AppendLens(new Vector3(0.32f, 0.02f, 0f), Vector3.right, 0.075f, 0.016f, radialSegments, new Color32(45, 220, 185, 255));
                    builder.AppendSpool(new Vector3(-0.17f, -0.03f, -0.14f), Vector3.forward, 0.068f, 0.045f, radialSegments, SlotTrim);
                    break;
                case ToolSilhouette.Builder:
                    builder.AppendGrip(new Vector3(-0.17f, -0.18f, -0.02f), 0.16f, 0.08f, radialSegments);
                    builder.AppendRailPair(new Vector3(0.28f, 0.03f, 0f), 0.28f, 0.13f, 0.025f, radialSegments);
                    builder.AppendScreen(new Vector3(-0.03f, 0.105f, 0.126f), new Vector2(0.18f, 0.075f), new Color32(24, 180, 220, 255));
                    builder.AppendFins(new Vector3(0.08f, 0.12f, -0.13f), Vector3.forward, finCount, 0.14f, 0.075f, 0.012f);
                    break;
                case ToolSilhouette.EnvAnalyzer:
                    builder.AppendGrip(new Vector3(-0.18f, -0.16f, -0.02f), 0.15f, 0.075f, radialSegments);
                    builder.AppendCylinder(new Vector3(0.28f, 0.02f, -0.065f), new Vector3(0.28f, 0.02f, 0.065f), 0.082f, radialSegments, SlotWear, new Color32(55, 64, 64, 80));
                    builder.AppendLensCluster(new Vector3(0.35f, 0.02f, 0f), radialSegments);
                    builder.AppendScreen(new Vector3(-0.09f, 0.09f, 0.122f), new Vector2(0.15f, 0.065f), new Color32(60, 210, 150, 255));
                    break;
                case ToolSilhouette.Flashlight:
                    builder.AppendCylinder(new Vector3(-0.18f, 0f, 0f), new Vector3(0.24f, 0f, 0f), 0.092f, radialSegments, SlotCasing, new Color32(74, 82, 82, 35));
                    builder.AppendLens(new Vector3(0.36f, 0f, 0f), Vector3.right, 0.125f, 0.025f, radialSegments, new Color32(210, 245, 255, 210));
                    builder.AppendRailPair(new Vector3(0f, 0.11f, 0f), 0.36f, 0.11f, 0.018f, radialSegments);
                    builder.AppendFins(new Vector3(0.16f, -0.105f, 0f), Vector3.up, finCount, 0.18f, 0.065f, 0.01f);
                    break;
                case ToolSilhouette.HarpoonLauncher:
                    builder.AppendGrip(new Vector3(-0.23f, -0.18f, 0f), 0.18f, 0.085f, radialSegments);
                    builder.AppendCylinder(new Vector3(-0.32f, 0.03f, 0f), new Vector3(0.43f, 0.03f, 0f), 0.052f, radialSegments, SlotWear, new Color32(65, 68, 64, 40));
                    builder.AppendRailPair(new Vector3(0.10f, 0.10f, 0f), 0.58f, 0.16f, 0.018f, radialSegments);
                    builder.AppendSpool(new Vector3(-0.08f, -0.03f, -0.17f), Vector3.forward, 0.11f, 0.075f, radialSegments, SlotTrim);
                    builder.AppendNozzle(new Vector3(0.48f, 0.03f, 0f), Vector3.right, 0.07f, 0.045f, radialSegments);
                    break;
                case ToolSilhouette.Knife:
                    builder.AppendBlade(new Vector3(0.20f, 0f, 0f), 0.46f, 0.105f, 0.018f);
                    builder.AppendGrip(new Vector3(-0.25f, -0.005f, 0f), 0.22f, 0.055f, radialSegments);
                    builder.AppendBeveledBox(new Vector3(-0.04f, 0f, 0f), new Vector3(0.025f, 0.075f, 0.09f), bevel * 0.7f, SlotTrim, new Color32(92, 78, 58, 30));
                    builder.AppendFins(new Vector3(0.12f, -0.065f, 0f), Vector3.up, Mathf.Max(3, finCount - 1), 0.20f, 0.035f, 0.007f);
                    break;
                case ToolSilhouette.LaserCutter:
                    builder.AppendGrip(new Vector3(-0.19f, -0.18f, 0f), 0.16f, 0.075f, radialSegments);
                    builder.AppendNozzle(new Vector3(0.35f, 0.03f, 0f), Vector3.right, 0.095f, 0.055f, radialSegments);
                    builder.AppendFins(new Vector3(0.13f, 0.12f, 0f), Vector3.up, finCount + 1, 0.18f, 0.06f, 0.011f);
                    builder.AppendScreen(new Vector3(-0.08f, 0.10f, 0.123f), new Vector2(0.12f, 0.05f), new Color32(230, 165, 55, 255));
                    break;
                case ToolSilhouette.Propulsion:
                    builder.AppendGrip(new Vector3(-0.20f, -0.19f, -0.05f), 0.17f, 0.07f, radialSegments);
                    builder.AppendGrip(new Vector3(-0.20f, -0.19f, 0.08f), 0.17f, 0.07f, radialSegments);
                    builder.AppendNozzle(new Vector3(0.35f, 0f, 0f), Vector3.right, 0.14f, 0.105f, radialSegments);
                    builder.AppendFins(new Vector3(0.22f, 0f, 0f), Vector3.right, finCount + 2, 0.18f, 0.10f, 0.014f);
                    builder.AppendRailPair(new Vector3(-0.05f, 0.12f, 0f), 0.35f, 0.18f, 0.02f, radialSegments);
                    break;
                case ToolSilhouette.Repair:
                    builder.AppendGrip(new Vector3(-0.18f, -0.17f, 0f), 0.16f, 0.075f, radialSegments);
                    builder.AppendNozzle(new Vector3(0.33f, 0.00f, 0f), Vector3.right, 0.075f, 0.042f, radialSegments);
                    builder.AppendCylinder(new Vector3(-0.18f, 0.13f, -0.11f), new Vector3(0.18f, 0.13f, -0.11f), 0.04f, radialSegments, SlotTrim, new Color32(70, 62, 54, 70));
                    builder.AppendLens(new Vector3(-0.04f, 0.12f, 0.13f), Vector3.forward, 0.055f, 0.012f, radialSegments, new Color32(255, 180, 65, 255));
                    break;
                case ToolSilhouette.SalvageSampler:
                    builder.AppendGrip(new Vector3(-0.18f, -0.17f, 0f), 0.16f, 0.075f, radialSegments);
                    builder.AppendCylinder(new Vector3(-0.05f, 0.12f, 0f), new Vector3(0.28f, 0.12f, 0f), 0.045f, radialSegments, SlotGlassEmission, new Color32(120, 220, 205, 160));
                    builder.AppendRailPair(new Vector3(0.28f, 0.02f, 0f), 0.22f, 0.16f, 0.023f, radialSegments);
                    builder.AppendBeveledBox(new Vector3(0.42f, 0.02f, 0.065f), new Vector3(0.075f, 0.026f, 0.026f), bevel * 0.6f, SlotWear, new Color32(118, 104, 72, 70));
                    builder.AppendBeveledBox(new Vector3(0.42f, 0.02f, -0.065f), new Vector3(0.075f, 0.026f, 0.026f), bevel * 0.6f, SlotWear, new Color32(118, 104, 72, 70));
                    break;
                case ToolSilhouette.Scanner:
                    builder.AppendGrip(new Vector3(-0.18f, -0.16f, 0f), 0.15f, 0.075f, radialSegments);
                    builder.AppendLensCluster(new Vector3(0.31f, 0.02f, 0f), radialSegments);
                    builder.AppendScreen(new Vector3(-0.07f, 0.10f, 0.128f), new Vector2(0.17f, 0.075f), new Color32(45, 205, 235, 255));
                    builder.AppendFins(new Vector3(-0.12f, 0.12f, -0.13f), Vector3.forward, finCount, 0.13f, 0.045f, 0.009f);
                    break;
                case ToolSilhouette.StunPistol:
                    builder.AppendGrip(new Vector3(-0.18f, -0.18f, 0f), 0.16f, 0.075f, radialSegments);
                    builder.AppendCylinder(new Vector3(0.12f, 0.04f, -0.055f), new Vector3(0.39f, 0.04f, -0.055f), 0.027f, radialSegments, SlotWear, new Color32(85, 82, 74, 80));
                    builder.AppendCylinder(new Vector3(0.12f, 0.04f, 0.055f), new Vector3(0.39f, 0.04f, 0.055f), 0.027f, radialSegments, SlotWear, new Color32(85, 82, 74, 80));
                    builder.AppendLens(new Vector3(0.44f, 0.04f, -0.055f), Vector3.right, 0.034f, 0.012f, radialSegments, new Color32(90, 150, 255, 255));
                    builder.AppendLens(new Vector3(0.44f, 0.04f, 0.055f), Vector3.right, 0.034f, 0.012f, radialSegments, new Color32(90, 150, 255, 255));
                    builder.AppendBeveledBox(new Vector3(-0.02f, 0.12f, 0f), new Vector3(0.13f, 0.045f, 0.08f), bevel * 0.7f, SlotTrim, new Color32(55, 58, 62, 140));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            builder.AppendLens(new Vector3(-half.x * 0.42f, half.y + 0.018f, -half.z * 0.42f), Vector3.up, 0.018f, 0.006f, radialSegments, new Color32(40, 220, 190, 255));
        }

        private static void ResolveQuality(float quality, out int radialSegments, out int finCount, out float bevel)
        {
            float q = Mathf.Clamp01(quality);
            radialSegments = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(10f, 24f, q)), 8, 32);
            finCount = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(4f, 9f, q)), 3, 12);
            bevel = Mathf.Lerp(0.006f, 0.018f, q);
        }

        private static bool TryCreateMesh(ToolMeshSpec spec, MeshBuilder builder, out Mesh mesh, out string failure)
        {
            mesh = null;
            failure = string.Empty;

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
                failure = "Unity mesh validation failed after assembly.";
                return false;
            }

            if (!IsFiniteNonZero(mesh.bounds))
            {
                failure = "mesh bounds are invalid.";
                return false;
            }

            return true;
        }

        private static int ResolveTriangleCount(Mesh mesh)
        {
            int total = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
                total += (int)mesh.GetIndexCount(i) / 3;
            return total;
        }

        private static bool IsFiniteNonZero(Bounds bounds)
        {
            return IsFinite(bounds.center) &&
                   IsFinite(bounds.extents) &&
                   bounds.extents.sqrMagnitude > 0.000001f;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Vector4 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
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
            private readonly List<Vector3> vertices = new List<Vector3>(4096);
            private readonly List<Vector3> normals = new List<Vector3>(4096);
            private readonly List<Vector4> tangents = new List<Vector4>(4096);
            private readonly List<Vector2> uvs = new List<Vector2>(4096);
            private readonly List<Color32> colors = new List<Color32>(4096);
            private readonly List<int>[] submeshIndices =
            {
                new List<int>(4096),
                new List<int>(2048),
                new List<int>(2048),
                new List<int>(1024)
            };

            public int VertexCount => vertices.Count;
            public int IndexCount
            {
                get
                {
                    int count = 0;
                    for (int i = 0; i < submeshIndices.Length; i++)
                        count += submeshIndices[i].Count;
                    return count;
                }
            }

            public int SubmeshCount => submeshIndices.Length;

            public void AppendBeveledBox(Vector3 center, Vector3 half, float bevel, int slot, Color32 color)
            {
                float b = Mathf.Clamp(bevel, 0.001f, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.45f);
                Vector3 inner = new Vector3(Mathf.Max(0.001f, half.x - b), Mathf.Max(0.001f, half.y - b), Mathf.Max(0.001f, half.z - b));

                AppendBoxFaces(center, inner, slot, color);

                AppendBoxFaces(center + new Vector3(half.x - b * 0.5f, 0f, 0f), new Vector3(b * 0.5f, inner.y, inner.z), slot, color);
                AppendBoxFaces(center + new Vector3(-half.x + b * 0.5f, 0f, 0f), new Vector3(b * 0.5f, inner.y, inner.z), slot, color);
                AppendBoxFaces(center + new Vector3(0f, half.y - b * 0.5f, 0f), new Vector3(inner.x, b * 0.5f, inner.z), slot, color);
                AppendBoxFaces(center + new Vector3(0f, -half.y + b * 0.5f, 0f), new Vector3(inner.x, b * 0.5f, inner.z), slot, color);
                AppendBoxFaces(center + new Vector3(0f, 0f, half.z - b * 0.5f), new Vector3(inner.x, inner.y, b * 0.5f), slot, color);
                AppendBoxFaces(center + new Vector3(0f, 0f, -half.z + b * 0.5f), new Vector3(inner.x, inner.y, b * 0.5f), slot, color);
            }

            public void AppendCylinder(Vector3 start, Vector3 end, float radius, int segments, int slot, Color32 color)
            {
                Vector3 axis = end - start;
                float length = axis.magnitude;
                if (length <= 0.0001f)
                    return;

                Vector3 forward = axis / length;
                ResolveBasis(forward, out Vector3 right, out Vector3 up);
                int baseIndex = vertices.Count;

                for (int i = 0; i < segments; i++)
                {
                    float angle = (Mathf.PI * 2f * i) / segments;
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

            public void AppendNozzle(Vector3 center, Vector3 axis, float length, float radius, int segments)
            {
                Vector3 forward = axis.normalized;
                AppendCylinder(center - forward * length * 0.5f, center + forward * length * 0.5f, radius, segments, SlotWear, new Color32(82, 78, 70, 70));
                AppendCylinder(center + forward * length * 0.30f, center + forward * length * 0.62f, radius * 0.62f, segments, SlotTrim, new Color32(132, 120, 96, 70));
            }

            public void AppendLens(Vector3 center, Vector3 axis, float radius, float depth, int segments, Color32 color)
            {
                Vector3 forward = axis.normalized;
                AppendCylinder(center - forward * depth, center + forward * depth, radius, segments, SlotGlassEmission, color);
                AppendCylinder(center - forward * depth * 1.35f, center - forward * depth * 0.65f, radius * 1.16f, segments, SlotWear, new Color32(72, 76, 74, 60));
            }

            public void AppendScreen(Vector3 center, Vector2 size, Color32 color)
            {
                Vector3 right = Vector3.right * size.x * 0.5f;
                Vector3 up = Vector3.up * size.y * 0.5f;
                AppendQuad(center - right - up, center + right - up, center + right + up, center - right + up, Vector3.forward, SlotGlassEmission, color);
                AppendBeveledBox(center - Vector3.forward * 0.011f, new Vector3(size.x * 0.55f, size.y * 0.58f, 0.008f), 0.004f, SlotTrim, new Color32(38, 44, 44, 120));
            }

            public void AppendLensCluster(Vector3 center, int segments)
            {
                AppendLens(center + new Vector3(0f, 0.038f, 0f), Vector3.right, 0.052f, 0.012f, segments, new Color32(60, 210, 235, 230));
                AppendLens(center + new Vector3(0f, -0.035f, 0.044f), Vector3.right, 0.034f, 0.010f, segments, new Color32(90, 235, 180, 220));
                AppendLens(center + new Vector3(0f, -0.035f, -0.044f), Vector3.right, 0.034f, 0.010f, segments, new Color32(90, 185, 255, 220));
            }

            public void AppendRailPair(Vector3 center, float length, float spread, float radius, int segments)
            {
                Vector3 a = center - Vector3.right * length * 0.5f;
                Vector3 b = center + Vector3.right * length * 0.5f;
                AppendCylinder(a + Vector3.forward * spread * 0.5f, b + Vector3.forward * spread * 0.5f, radius, segments, SlotTrim, new Color32(54, 56, 54, 90));
                AppendCylinder(a - Vector3.forward * spread * 0.5f, b - Vector3.forward * spread * 0.5f, radius, segments, SlotTrim, new Color32(54, 56, 54, 90));
            }

            public void AppendGrip(Vector3 center, float length, float radius, int segments)
            {
                AppendCylinder(center - Vector3.up * length * 0.5f, center + Vector3.up * length * 0.5f, radius, segments, SlotTrim, new Color32(34, 34, 30, 170));
                int ribs = 5;
                for (int i = 0; i < ribs; i++)
                {
                    float t = (i + 1f) / (ribs + 1f);
                    Vector3 rib = center + Vector3.up * Mathf.Lerp(-length * 0.42f, length * 0.42f, t);
                    AppendCylinder(rib - Vector3.up * 0.006f, rib + Vector3.up * 0.006f, radius * 1.08f, segments, SlotWear, new Color32(50, 47, 38, 110));
                }
            }

            public void AppendFins(Vector3 center, Vector3 normalAxis, int count, float span, float height, float thickness)
            {
                Vector3 axis = normalAxis.normalized;
                for (int i = 0; i < count; i++)
                {
                    float t = count <= 1 ? 0.5f : i / (float)(count - 1);
                    Vector3 offset = Vector3.right * Mathf.Lerp(-span * 0.5f, span * 0.5f, t);
                    Vector3 half = new Vector3(thickness, height, 0.045f);
                    if (Mathf.Abs(axis.x) > 0.5f)
                        half = new Vector3(0.035f, height, thickness);
                    else if (Mathf.Abs(axis.z) > 0.5f)
                        half = new Vector3(thickness, height, 0.035f);
                    AppendBeveledBox(center + offset, half, thickness * 0.35f, SlotWear, new Color32(64, 61, 54, 95));
                }
            }

            public void AppendSpool(Vector3 center, Vector3 axis, float radius, float width, int segments, int slot)
            {
                Vector3 forward = axis.normalized;
                AppendCylinder(center - forward * width * 0.5f, center + forward * width * 0.5f, radius, segments, slot, new Color32(42, 42, 38, 120));
                AppendCylinder(center - forward * width * 0.68f, center - forward * width * 0.54f, radius * 1.14f, segments, SlotWear, new Color32(78, 72, 60, 80));
                AppendCylinder(center + forward * width * 0.54f, center + forward * width * 0.68f, radius * 1.14f, segments, SlotWear, new Color32(78, 72, 60, 80));
            }

            public void AppendBlade(Vector3 center, float length, float width, float thickness)
            {
                Vector3 p0 = center + new Vector3(-length * 0.5f, -width * 0.5f, -thickness);
                Vector3 p1 = center + new Vector3(length * 0.32f, -width * 0.42f, -thickness);
                Vector3 p2 = center + new Vector3(length * 0.5f, 0f, -thickness);
                Vector3 p3 = center + new Vector3(length * 0.32f, width * 0.42f, -thickness);
                Vector3 p4 = center + new Vector3(-length * 0.5f, width * 0.5f, -thickness);
                Vector3 q0 = p0 + Vector3.forward * thickness * 2f;
                Vector3 q1 = p1 + Vector3.forward * thickness * 2f;
                Vector3 q2 = p2 + Vector3.forward * thickness * 2f;
                Vector3 q3 = p3 + Vector3.forward * thickness * 2f;
                Vector3 q4 = p4 + Vector3.forward * thickness * 2f;

                AppendPolygon(new[] { p0, p1, p2, p3, p4 }, Vector3.back, SlotWear, new Color32(155, 160, 150, 55));
                AppendPolygon(new[] { q4, q3, q2, q1, q0 }, Vector3.forward, SlotWear, new Color32(175, 182, 170, 45));
                AppendQuad(p0, q0, q1, p1, Vector3.down, SlotTrim, new Color32(120, 118, 104, 65));
                AppendQuad(p1, q1, q2, p2, Vector3.down, SlotWear, new Color32(195, 198, 180, 40));
                AppendQuad(p2, q2, q3, p3, Vector3.up, SlotWear, new Color32(195, 198, 180, 40));
                AppendQuad(p3, q3, q4, p4, Vector3.up, SlotTrim, new Color32(120, 118, 104, 65));
                AppendQuad(p4, q4, q0, p0, Vector3.left, SlotTrim, new Color32(80, 76, 66, 75));
            }

            public bool ValidateTopology(float minimumTriangleArea, out string failure)
            {
                failure = string.Empty;
                for (int slot = 0; slot < submeshIndices.Length; slot++)
                {
                    List<int> indices = submeshIndices[slot];
                    if (indices.Count == 0)
                    {
                        failure = "empty material slot " + slot + ".";
                        return false;
                    }

                    for (int i = 0; i < indices.Count; i += 3)
                    {
                        int ia = indices[i];
                        int ib = indices[i + 1];
                        int ic = indices[i + 2];
                        if ((uint)ia >= (uint)vertices.Count || (uint)ib >= (uint)vertices.Count || (uint)ic >= (uint)vertices.Count)
                        {
                            failure = "index out of range.";
                            return false;
                        }

                        Vector3 a = vertices[ia];
                        Vector3 b = vertices[ib];
                        Vector3 c = vertices[ic];
                        Vector3 normalA = normals[ia];
                        Vector3 tangentA = tangents[ia];
                        Vector2 uvA = uvs[ia];
                        if (!IsFinite(a) || !IsFinite(b) || !IsFinite(c) || !IsFinite(normalA) || !IsFinite(tangentA) || !IsFinite(uvA))
                        {
                            failure = "non-finite vertex stream.";
                            return false;
                        }

                        float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                        if (area <= minimumTriangleArea)
                        {
                            failure = "degenerate triangle.";
                            return false;
                        }

                        float normalLength = normalA.magnitude;
                        if (normalLength < 0.995f || normalLength > 1.005f)
                        {
                            failure = "normal length outside tolerance.";
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
                    indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
                };

                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetTangents(tangents);
                mesh.SetUVs(0, uvs);
                mesh.SetColors(colors);
                mesh.subMeshCount = submeshIndices.Length;
                for (int i = 0; i < submeshIndices.Length; i++)
                    mesh.SetTriangles(submeshIndices[i], i, true);
                mesh.RecalculateBounds();
                return mesh;
            }

            private void AppendBoxFaces(Vector3 center, Vector3 half, int slot, Color32 color)
            {
                Vector3 min = center - half;
                Vector3 max = center + half;
                AppendQuad(new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), Vector3.forward, slot, color);
                AppendQuad(new Vector3(max.x, min.y, min.z), new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), Vector3.back, slot, color);
                AppendQuad(new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), Vector3.left, slot, color);
                AppendQuad(new Vector3(max.x, min.y, max.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), Vector3.right, slot, color);
                AppendQuad(new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z), Vector3.up, slot, color);
                AppendQuad(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), Vector3.down, slot, color);
            }

            private void AppendPolygon(Vector3[] points, Vector3 normal, int slot, Color32 color)
            {
                int root = AddVertex(points[0], normal, Vector3.right, Vector2.zero, color);
                for (int i = 1; i < points.Length - 1; i++)
                {
                    int b = AddVertex(points[i], normal, Vector3.right, new Vector2(i / (float)points.Length, 0f), color);
                    int c = AddVertex(points[i + 1], normal, Vector3.right, new Vector2((i + 1f) / points.Length, 1f), color);
                    AddTriangle(slot, root, b, c);
                }
            }

            private void AppendQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, int slot, Color32 color)
            {
                Vector3 tangent = ResolveTangent(normal);
                int ia = AddVertex(a, normal, tangent, new Vector2(0f, 0f), color);
                int ib = AddVertex(b, normal, tangent, new Vector2(1f, 0f), color);
                int ic = AddVertex(c, normal, tangent, new Vector2(1f, 1f), color);
                int id = AddVertex(d, normal, tangent, new Vector2(0f, 1f), color);
                AddTriangle(slot, ia, ib, ic);
                AddTriangle(slot, ia, ic, id);
            }

            private int AddVertex(Vector3 position, Vector3 normal, Vector3 tangent, Vector2 uv, Color32 color)
            {
                vertices.Add(position);
                normals.Add(normal.normalized);
                tangents.Add(new Vector4(tangent.normalized.x, tangent.normalized.y, tangent.normalized.z, 1f));
                uvs.Add(uv);
                colors.Add(color);
                return vertices.Count - 1;
            }

            private void AddTriangle(int slot, int a, int b, int c)
            {
                submeshIndices[slot].Add(a);
                submeshIndices[slot].Add(b);
                submeshIndices[slot].Add(c);
            }

            private static void ResolveBasis(Vector3 forward, out Vector3 right, out Vector3 up)
            {
                Vector3 helper = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.92f ? Vector3.right : Vector3.up;
                right = Vector3.Cross(helper, forward).normalized;
                up = Vector3.Cross(forward, right).normalized;
            }

            private static Vector3 ResolveTangent(Vector3 normal)
            {
                return Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.92f ? Vector3.right : Vector3.Cross(Vector3.up, normal).normalized;
            }
        }

        public readonly struct ToolMeshSpec
        {
            public readonly string ToolId;
            public readonly ToolSilhouette Silhouette;
            public readonly string MeshAssetName;
            public readonly Vector3 BodySize;
            public readonly string VisualParts;
            public readonly string FutureAnchors;

            public ToolMeshSpec(string toolId, ToolSilhouette silhouette, string meshAssetName, Vector3 bodySize, string visualParts, string futureAnchors)
            {
                ToolId = toolId;
                Silhouette = silhouette;
                MeshAssetName = meshAssetName;
                BodySize = bodySize;
                VisualParts = visualParts;
                FutureAnchors = futureAnchors;
            }
        }

        public struct ToolMeshAuthoringReport
        {
            public bool Success;
            public string OutputFolder;
            public string FailureReason;
            public int MeshesWritten;
            public int VertexCount;
            public int TriangleCount;
        }

        public enum ToolSilhouette
        {
            BeaconDeployer,
            Builder,
            EnvAnalyzer,
            Flashlight,
            HarpoonLauncher,
            Knife,
            LaserCutter,
            Propulsion,
            Repair,
            SalvageSampler,
            Scanner,
            StunPistol
        }
    }
}
#endif
