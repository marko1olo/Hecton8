using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class WreckagePrefabFactory1735EditTests
    {
        private const string FactoryPath = "Assets/_Project/Editor/Assembly/WreckagePrefabFactory.cs";
        private const string CarveVolumePath = "Assets/_Project/Scripts/World/VoxelCarveVolume.cs";
        private const string ScatterManagerPath = "Assets/_Project/Scripts/World/WreckageScatterManager.cs";

        [Test]
        public void FactoryOwnsOfflineDebrisCombineAndPrefabGate()
        {
            string source = ReadProjectFile(FactoryPath);

            StringAssert.Contains("Mesh.CombineMeshes", source);
            StringAssert.Contains("BuildMaterialSlotsFromBuckets", source);
            StringAssert.Contains("PrefabUtility.SaveAsPrefabAsset", source);
            StringAssert.Contains("VoxelCarveVolume", source);
            StringAssert.Contains("TryBuildCombinedVisualMesh", source);
            StringAssert.Contains("VIS_HullCombined", source);
            StringAssert.Contains("MESH_Wreckage_", source);
            StringAssert.Contains("_HullCombined.asset", source);
            StringAssert.Contains("ConfigureMergedLodGroup", source);
            StringAssert.Contains("LODGroup", source);
            StringAssert.Contains("WreckageVoxelCarveInstruction.FlattenAndBury", source);
            StringAssert.Contains("LowestVertexPercent = 20", source);
            StringAssert.Contains("COL_", source);
            StringAssert.Contains("COL_ proxy must not contain Renderer components", source);
            StringAssert.Contains("TRIG_SalvageNode", source);
            StringAssert.Contains("ReceiveGI.Lightmaps", source);
            StringAssert.Contains("CBUFFER_START(UnityPerMaterial)", source);
            StringAssert.Contains("MAT_Wreckage_Exterior", source);
            StringAssert.Contains("MAT_Wreckage_Burned_Interior", source);
            StringAssert.Contains("MaterialNameMatchesCandidate", source);
            StringAssert.Contains("Agent 1727 burned PBR material set missing", source);
            StringAssert.Contains("Factory refuses to create fallback materials", source);
            StringAssert.Contains("required World_Static layer missing", source);
            StringAssert.Contains("visual renderer count exceeds merged wreck budget", source);
            StringAssert.Contains("DefaultWriteReportToDisk = false", source);
            StringAssert.Contains("WriteReportToDisk = DefaultWriteReportToDisk", source);
            StringAssert.Contains("if (report.writeReportToDisk)", source);
            StringAssert.Contains("MaxExpectedDebrisSegmentsPerGroup = 512", source);
            StringAssert.Contains("MaxExpectedCombineInstancesPerMaterial = 512", source);
            Assert.IsFalse(source.Contains("new Material(", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("renderer.material", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("RuntimeInitializeOnLoadMethod", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("\"Blackened\", \"Charred\"", StringComparison.Ordinal));
        }

        [Test]
        public void RuntimeCarveVolumeIsSerializedMetadataAndSpawnCarveBridge()
        {
            string source = ReadProjectFile(CarveVolumePath);

            StringAssert.Contains("TryReadDescriptor", source);
            StringAssert.Contains("ReadLocalAabb", source);
            StringAssert.Contains("SetEditorBakeData", source);
            StringAssert.Contains("WreckageVoxelCarveDescriptor", source);
            StringAssert.Contains("BurialDepthMeters", source);
            StringAssert.Contains("ValidateDescriptorLayout", source);
            StringAssert.Contains("UnsafeUtility.SizeOf<WreckageVoxelCarveDescriptor>", source);
            StringAssert.Contains("DescriptorStrideBytes = 56", source);
            StringAssert.Contains("ILateFrameTickable", source);
            StringAssert.Contains("TryQueueSpawnCarve", source);
            StringAssert.Contains("TryPrimeRuntimeBridge", source);
            StringAssert.Contains("TryReadCachedVoxelBridge", source);
            StringAssert.Contains("CacheVoxelBridgeCold", source);
            StringAssert.Contains("TryQueueCarveEvent", source);
            StringAssert.Contains("GlobalRegistry.VoxelEngine", source);
            StringAssert.Contains("IGlobalRegistryHotSwapListener", source);
            StringAssert.Contains("TryRegisterHotSwapListener", source);
            StringAssert.Contains("GlobalRegistryServiceSlot.VoxelEngineRuntime", source);
            StringAssert.Contains("GlobalRegistryServiceSlot.Dispatcher", source);
            StringAssert.Contains("_registeredLateFrameTick = 0;", source);
            AssertMethodBodyDoesNotContain(source, "public void LateFrameTick()", "GlobalRegistry.");
            AssertMethodBodyDoesNotContain(source, "private bool TryQueueSpawnCarve()", "GlobalRegistry.");
            AssertMethodBodyDoesNotContain(source, "private bool TryQueueSpawnCarve()", "TryGetNearestActiveVolume");
            Assert.IsFalse(source.Contains("Update" + "()", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("FixedUpdate", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("LateUpdate", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("GetComponentsInChildren", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("GetComponent", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("Mesh.CombineMeshes", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("GlobalRegistry.Get<", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("GlobalDataVault", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("new Mesh", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("new Material(", StringComparison.Ordinal));
        }

        [Test]
        public void ScatterManagerConsumesContinuousQualityWithoutSceneSearch()
        {
            string source = ReadProjectFile(ScatterManagerPath);

            StringAssert.Contains("HomeostasisBrain.GlobalQualityWeight", source);
            StringAssert.Contains("Smooth01", source);
            StringAssert.Contains("shadowSurvivalFloor01", source);
            StringAssert.Contains("twoSidedShadowWeight01", source);
            StringAssert.Contains("SetEditorBakeData", source);
            StringAssert.Contains("ILateFrameTickable", source);
            StringAssert.Contains("LateFrameTick", source);
            StringAssert.Contains("TryRegisterLateFrameTickable", source);
            StringAssert.Contains("IGlobalRegistryHotSwapListener", source);
            StringAssert.Contains("TryRegisterHotSwapListener", source);
            StringAssert.Contains("GlobalRegistryServiceSlot.Dispatcher", source);
            StringAssert.Contains("_registeredLateFrameTick = 0;", source);
            Assert.IsFalse(source.Contains("GetComponentsInChildren", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("FindObject", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("GameObject." + "Find", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("Update" + "()", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("GlobalRegistry.Get<", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("GetComponent", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("Mesh.CombineMeshes", StringComparison.Ordinal));
            Assert.IsFalse(source.Contains("new Material(", StringComparison.Ordinal));
        }

        private static string ReadProjectFile(string relativePath)
        {
            string fullPath = Path.GetFullPath(relativePath);
            Assert.IsTrue(File.Exists(fullPath), "Missing project file: " + relativePath);
            return File.ReadAllText(fullPath);
        }

        private static void AssertMethodBodyDoesNotContain(string source, string methodSignature, string forbiddenToken)
        {
            int signatureIndex = source.IndexOf(methodSignature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, "Missing method signature: " + methodSignature);

            int openBrace = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(openBrace, 0, "Missing method opening brace: " + methodSignature);

            int depth = 0;
            for (int i = openBrace; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        string body = source.Substring(openBrace, i - openBrace + 1);
                        Assert.IsFalse(
                            body.Contains(forbiddenToken, StringComparison.Ordinal),
                            methodSignature + " contains forbidden token: " + forbiddenToken);
                        return;
                    }
                }
            }

            Assert.Fail("Unbalanced method body: " + methodSignature);
        }
    }
}
