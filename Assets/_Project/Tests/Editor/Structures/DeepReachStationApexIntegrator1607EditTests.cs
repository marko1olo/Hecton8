#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor.Structures
{
    public sealed class DeepReachStationApexIntegrator1607EditTests
    {
        private static readonly string[] StationGeneratorFiles =
        {
            "Assets/_Project/Editor/Generators/Structures/DeepReachStationContracts.cs",
            "Assets/_Project/Editor/Generators/Structures/DeepReachStationModuleLibrary.cs",
            "Assets/_Project/Editor/Generators/Structures/DeepReachStationFabricator.cs",
            "Assets/_Project/Editor/Generators/Structures/DeepReachStationArchitectWindow.cs"
        };

        private static readonly string[] HotMethodNames =
        {
            "Tick",
            "FixedUpdate",
            "LateFrameTick",
            "Update",
            "Execute"
        };

        [Test]
        public void StationSources_HaveBalancedLexicalBlocks()
        {
            for (int i = 0; i < StationGeneratorFiles.Length; i++)
            {
                string source = Read(StationGeneratorFiles[i]);
                Assert.That(HasBalancedBlocks(source), Is.True, StationGeneratorFiles[i]);
            }
        }

        [Test]
        public void HotPhaseMethods_DoNotResolveColdDependencies()
        {
            for (int f = 0; f < StationGeneratorFiles.Length; f++)
            {
                string relativePath = StationGeneratorFiles[f];
                string source = Read(relativePath);
                for (int hot = 0; hot < HotMethodNames.Length; hot++)
                {
                    string[] bodies = ExtractMethodBodies(source, HotMethodNames[hot]);
                    for (int m = 0; m < bodies.Length; m++)
                    {
                        AssertForbidden(bodies[m], "GlobalRegistry.Get<", relativePath + "::" + HotMethodNames[hot]);
                        AssertForbidden(bodies[m], "Get" + "Component(", relativePath + "::" + HotMethodNames[hot]);
                        AssertForbidden(bodies[m], "Get" + "Component<", relativePath + "::" + HotMethodNames[hot]);
                        AssertForbidden(bodies[m], "TryGet" + "Component(", relativePath + "::" + HotMethodNames[hot]);
                        AssertForbidden(bodies[m], "TryGet" + "Component<", relativePath + "::" + HotMethodNames[hot]);
                    }
                }
            }
        }

        [Test]
        public void BurstExecuteMethods_DoNotUseManagedContainersOrFormatting()
        {
            string contractsPath = "Assets/_Project/Editor/Generators/Structures/DeepReachStationContracts.cs";
            string[] executeMethods = ExtractMethodBodies(Read(contractsPath), "Execute");
            Assert.GreaterOrEqual(executeMethods.Length, 3);

            for (int i = 0; i < executeMethods.Length; i++)
            {
                string body = executeMethods[i];
                AssertForbidden(body, "new List<", contractsPath);
                AssertForbidden(body, "new Dictionary<", contractsPath);
                AssertForbidden(body, ".ToList(", contractsPath);
                AssertForbidden(body, ".Select(", contractsPath);
                AssertForbidden(body, ".Where(", contractsPath);
                AssertForbidden(body, ".ToString(", contractsPath);
                AssertForbidden(body, "string.Format", contractsPath);
                AssertForbidden(body, "$\"", contractsPath);
                AssertForbidden(body, "GameObject.Find", contractsPath);
                AssertForbidden(body, "FindObjectOfType", contractsPath);
            }
        }

        [Test]
        public void DeterministicSetBitSelection_UsesMultiplyHighMapping()
        {
            string contracts = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationContracts.cs");
            string body = ExtractSingleMethodBody(contracts, "SelectNthSetBit", "DeepReachStationContracts.cs");
            Assert.That(contracts.Contains("public static uint MultiplyHighToRange", StringComparison.Ordinal), Is.True);
            Assert.That(body.Contains("MultiplyHighToRange(ordinal, (uint)count)", StringComparison.Ordinal), Is.True);
            AssertForbidden(body, "ordinal %", "DeepReachStationContracts.cs");
            AssertForbidden(body, "% (uint)count", "DeepReachStationContracts.cs");
        }

        [Test]
        public void VertexWelding_UsesPowerOfTwoBucketMaskInsteadOfModuloDivision()
        {
            string contracts = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationContracts.cs");
            string hotBody =
                ExtractSingleMethodBody(contracts, "TryFindExistingVertex", "DeepReachStationContracts.cs") +
                ExtractSingleMethodBody(contracts, "InsertVertex", "DeepReachStationContracts.cs");

            Assert.That(contracts.Contains("private static bool IsPowerOfTwo(int value)", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("private static int BucketSlot(uint key, int bucketCount)", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("private static int ProbeSlot(int start, int probe, int bucketCount)", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("!IsPowerOfTwo(bucketCount)", StringComparison.Ordinal), Is.True);
            Assert.That(hotBody.Contains("BucketSlot(key, bucketCount)", StringComparison.Ordinal), Is.True);
            Assert.That(hotBody.Contains("ProbeSlot(start, probe, bucketCount)", StringComparison.Ordinal), Is.True);
            AssertForbidden(hotBody, "% bucketCount", "DeepReachStationContracts.cs");
            AssertForbidden(hotBody, "% (uint)bucketCount", "DeepReachStationContracts.cs");
        }

        [Test]
        public void StationGenerator_IsEditorOnlyAndHasNoRuntimePhaseRegistration()
        {
            for (int i = 0; i < StationGeneratorFiles.Length; i++)
            {
                string source = Read(StationGeneratorFiles[i]);
                Assert.That(source.TrimStart().StartsWith("#if UNITY_EDITOR", StringComparison.Ordinal), Is.True, StationGeneratorFiles[i]);
                AssertForbidden(source, "ITickable", StationGeneratorFiles[i]);
                AssertForbidden(source, "IUpdatable", StationGeneratorFiles[i]);
                AssertForbidden(source, "IFixedTickable", StationGeneratorFiles[i]);
                AssertForbidden(source, "ILateFrameTickable", StationGeneratorFiles[i]);
                AssertForbidden(source, "TryRegisterLateFrameTickable", StationGeneratorFiles[i]);
                AssertForbidden(source, "GlobalRegistry.TryRegister", StationGeneratorFiles[i]);
                AssertForbidden(source, "SignalBus<", StationGeneratorFiles[i]);
                AssertForbidden(source, "GlobalSignals", StationGeneratorFiles[i]);
                AssertForbidden(source, "HectonEventBus", StationGeneratorFiles[i]);
            }
        }

        [Test]
        public void StationArchitectWindow_UsesRetainedModeEditorUi()
        {
            string window = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationArchitectWindow.cs");
            Assert.That(window.Contains("CreateGUI()", StringComparison.Ordinal), Is.True);
            Assert.That(window.Contains("UnityEngine.UIElements", StringComparison.Ordinal), Is.True);
            AssertForbidden(window, "OnGUI(", "DeepReachStationArchitectWindow.cs");
            AssertForbidden(window, "EditorGUILayout", "DeepReachStationArchitectWindow.cs");
            AssertForbidden(window, "GUILayout", "DeepReachStationArchitectWindow.cs");
            AssertForbidden(window, "EditorGUI.DisabledScope", "DeepReachStationArchitectWindow.cs");
        }

        [Test]
        public void StationArchitectWindow_PreservesFullUintSeedSpace()
        {
            string window = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationArchitectWindow.cs");
            Assert.That(window.Contains("private TextField _seedField", StringComparison.Ordinal), Is.True);
            Assert.That(window.Contains("_seed.ToString(CultureInfo.InvariantCulture)", StringComparison.Ordinal), Is.True);
            Assert.That(window.Contains("uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsed)", StringComparison.Ordinal), Is.True);
            Assert.That(window.Contains("parsed != 0u", StringComparison.Ordinal), Is.True);
            AssertForbidden(window, "IntegerField _seedField", "DeepReachStationArchitectWindow.cs");
            AssertForbidden(window, "int.MaxValue", "DeepReachStationArchitectWindow.cs");
            AssertForbidden(window, "(int)_seed", "DeepReachStationArchitectWindow.cs");
        }

        [Test]
        public void StationCode_HasNoDataVaultWriteLocksOrManagedLocks()
        {
            for (int i = 0; i < StationGeneratorFiles.Length; i++)
            {
                string source = Read(StationGeneratorFiles[i]);
                AssertForbidden(source, "GlobalDataVault", StationGeneratorFiles[i]);
                AssertForbidden(source, "IDataVault", StationGeneratorFiles[i]);
                AssertForbidden(source, "TryAcquireWriteLock", StationGeneratorFiles[i]);
                AssertForbidden(source, "ReleaseWriteLock", StationGeneratorFiles[i]);
                AssertForbidden(source, "lock (", StationGeneratorFiles[i]);
                AssertForbidden(source, "Monitor.Enter", StationGeneratorFiles[i]);
                AssertForbidden(source, "SpinLock", StationGeneratorFiles[i]);
            }
        }

        [Test]
        public void StationSource_DoesNotSpawnBuildProcessesOrWriteProofReports()
        {
            for (int i = 0; i < StationGeneratorFiles.Length; i++)
            {
                string source = Read(StationGeneratorFiles[i]);
                AssertForbiddenIgnoreCase(source, "dotnet build", StationGeneratorFiles[i]);
                AssertForbidden(source, "ProcessStartInfo", StationGeneratorFiles[i]);
                AssertForbidden(source, "BuildPipeline.BuildPlayer", StationGeneratorFiles[i]);
                AssertForbiddenIgnoreCase(source, "csc.exe", StationGeneratorFiles[i]);
                AssertForbiddenIgnoreCase(source, "MSBuild", StationGeneratorFiles[i]);
                AssertForbidden(source, "Get" + "Component(", StationGeneratorFiles[i]);
                AssertForbidden(source, "Get" + "Component<", StationGeneratorFiles[i]);
                AssertForbidden(source, "TryGet" + "Component(", StationGeneratorFiles[i]);
                AssertForbidden(source, "TryGet" + "Component<", StationGeneratorFiles[i]);
                AssertForbidden(source, "GameObject.Find", StationGeneratorFiles[i]);
                AssertForbidden(source, "FindObjectOfType", StationGeneratorFiles[i]);
                AssertForbidden(source, "FindAnyObjectByType", StationGeneratorFiles[i]);
                AssertForbidden(source, "FindObjectsOfType", StationGeneratorFiles[i]);
                AssertForbidden(source, "Resources.FindObjectsOfTypeAll", StationGeneratorFiles[i]);
                AssertForbidden(source, "File.WriteAllText", StationGeneratorFiles[i]);
                AssertForbidden(source, "StreamWriter", StationGeneratorFiles[i]);
                AssertForbidden(source, "Docs/Reports", StationGeneratorFiles[i]);
                AssertForbidden(source, ".json", StationGeneratorFiles[i]);
            }
        }

        [Test]
        public void StationFabricator_DoesNotForceGlobalAssetRefresh()
        {
            string fabricator = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationFabricator.cs");
            Assert.That(fabricator.Contains("AssetDatabase.CreateFolder(current, segments[i])", StringComparison.Ordinal), Is.True);
            AssertForbidden(fabricator, "AssetDatabase.Refresh", "DeepReachStationFabricator.cs");
            AssertForbidden(fabricator, "Directory.CreateDirectory", "DeepReachStationFabricator.cs");
        }

        [Test]
        public void QualityWeight_IsContinuousFidelityInputOnly()
        {
            string contracts = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationContracts.cs");
            Assert.That(contracts.Contains("math.saturate(rawQuality)", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("float rawQuality = GlobalQualityWeight", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("!math.isfinite(rawQuality) || !math.isfinite(CellSize)", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("float quality = GlobalQualityWeight", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("DeepReachStationMath.Smooth01(quality)", StringComparison.Ordinal), Is.True);
            AssertForbidden(contracts, "GlobalQualityWeight >", "DeepReachStationContracts.cs");
            AssertForbidden(contracts, "GlobalQualityWeight <", "DeepReachStationContracts.cs");
            AssertForbidden(contracts, "GlobalQualityWeight ==", "DeepReachStationContracts.cs");
            AssertForbidden(contracts, "GlobalQualityWeight !=", "DeepReachStationContracts.cs");
        }

        [Test]
        public void SocketCompatibility_UsesGenericUniversalLane()
        {
            string contracts = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationContracts.cs");
            Assert.That(contracts.Contains("SocketsCompatible", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("lhs == DeepReachStationConstants.GenericConnectorMask", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("DeepReachStationMath.SocketsCompatible(currentSocket, candidateSocket)", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("DeepReachStationMath.SocketsCompatible(currentSocket, neighborSocket)", StringComparison.Ordinal), Is.True);
        }

        [Test]
        public void WfcSolver_RotatesSocketContractsBeforeMeshFusion()
        {
            string contracts = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationContracts.cs");
            Assert.That(contracts.Contains("SocketAtRotated", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("SelectRotationForCell", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("RotationFitsCollapsedNeighbors", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("ScoreRotationAgainstStationVolume", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("RotationFromQuarterTurns(rotation)", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("UnrotateHorizontalDirection(direction, rotation)", StringComparison.Ordinal), Is.True);
            AssertForbidden(contracts, "RotationQuarterTurns = 0;", "DeepReachStationContracts.cs");
        }

        [Test]
        public void WfcSolver_RejectsExteriorSocketLeaks()
        {
            string contracts = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationContracts.cs");
            Assert.That(contracts.Contains("BuildVolumeCompatibleMask", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("ModuleFitsStationVolumeAt", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("RotationKeepsSocketsInsideStationVolume", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("CellInsideFlag", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("neighborCell.CollapsedModuleId == DeepReachStationConstants.EmptyModuleId", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("counters.FaultFlags |= DeepReachStationConstants.FaultContradiction", StringComparison.Ordinal), Is.True);
        }

        [Test]
        public void WfcSolver_PropagatesCollapsedNeighborConstraints()
        {
            string contracts = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationContracts.cs");
            Assert.That(contracts.Contains("PropagateCollapsedConstraints", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("ReduceMaskAgainstCollapsedNeighbors", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("BuildClosedFaceMask", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("while (changed && pass < cellCount)", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("if (changed)\r\n                counters.FaultFlags |= DeepReachStationConstants.FaultContradiction", StringComparison.Ordinal) ||
                        contracts.Contains("if (changed)\n                counters.FaultFlags |= DeepReachStationConstants.FaultContradiction", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("counters.FaultFlags & DeepReachStationConstants.FaultContradiction", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("counters.PlacementCount = 0;\r\n                Counters[0] = counters;\r\n                return;", StringComparison.Ordinal) ||
                        contracts.Contains("counters.PlacementCount = 0;\n                Counters[0] = counters;\n                return;", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("neighborToCellDirection", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("BuildCompatibleMask(", StringComparison.Ordinal), Is.True);
        }

        [Test]
        public void WfcSolver_AllowsHorizontalClosedFaceAbutmentButRejectsVerticalStacks()
        {
            string contracts = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationContracts.cs");
            Assert.That(contracts.Contains("CanClosedFacesAbut", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("ModuleHasClosedFace(candidate, opposite)", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("currentSocket == 0 && neighborSocket == 0 && CanClosedFacesAbut(direction)", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("direction != DeepReachStationDirections.Top", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("direction != DeepReachStationDirections.Bottom", StringComparison.Ordinal), Is.True);
            AssertForbidden(contracts, "currentSocket == 0 && module == DeepReachStationConstants.EmptyModuleId", "DeepReachStationContracts.cs");
        }

        [Test]
        public void WfcSolver_GrowsFromCollapsedStructuralFrontier()
        {
            string contracts = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationContracts.cs");
            Assert.That(contracts.Contains("SelectNextCell(cellCount, dims, collapsedStructural > 0", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("frontierOnly && !HasCollapsedStructuralNeighbor", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("HasCollapsedStructuralNeighbor", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("neighbor.CollapsedModuleId != 255 && neighbor.CollapsedModuleId != DeepReachStationConstants.EmptyModuleId", StringComparison.Ordinal), Is.True);
        }

        [Test]
        public void WfcSolver_ValidatesSingleStructuralComponentBeforeEmission()
        {
            string contracts = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationContracts.cs");
            string structuralCompatibilityBody = ExtractSingleMethodBody(contracts, "StructuralSocketsCompatible", "DeepReachStationContracts.cs");
            Assert.That(contracts.Contains("ValidateStructuralConnectivity", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("HasVisitedCompatibleNeighbor", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("StructuralSocketsCompatible", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("cell.ParentIndex = uint.MaxValue", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("counters.FaultFlags |= DeepReachStationConstants.FaultInvalidTopology", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("counters.PlacementCount = 0;", StringComparison.Ordinal), Is.True);
            Assert.That(structuralCompatibilityBody.Contains("DeepReachStationMath.SocketsCompatible(currentSocket, neighborSocket)", StringComparison.Ordinal), Is.True);
            AssertForbidden(structuralCompatibilityBody, "CanClosedFacesAbut", "DeepReachStationContracts.cs::StructuralSocketsCompatible");
        }

        [Test]
        public void DamageBake_UsesDistanceSquaredFalloff()
        {
            string contracts = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationContracts.cs");
            Assert.That(contracts.Contains("math.lengthsq(vertex.Position - center)", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("radiusSq", StringComparison.Ordinal), Is.True);
            AssertForbidden(contracts, "math.distance(vertex.Position, center)", "DeepReachStationContracts.cs");
        }

        [Test]
        public void DamageBake_FailsClosedOnNonFiniteInputs()
        {
            string contracts = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationContracts.cs");
            Assert.That(contracts.Contains("!math.isfinite(quality)", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("Counters[0] = counters;\r\n                return;", StringComparison.Ordinal) ||
                        contracts.Contains("Counters[0] = counters;\n                return;", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("!DeepReachStationMath.IsFinite(StationHalfExtents)", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("!DeepReachStationMath.IsFinite(vertex.Position)", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("counters.DamageVertexCount = invalidInput ? 0u : damaged", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("counters.FaultFlags |= DeepReachStationConstants.FaultNonFinite", StringComparison.Ordinal), Is.True);
        }

        [Test]
        public void DamageBake_EncodesWearForWreckShaderVertexColorContract()
        {
            string contracts = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationContracts.cs");
            string shader = Read("Assets/_Project/Art/Shaders/Hecton_WreckIndirectLit.shader");
            Assert.That(shader.Contains("input.vertexColor.r * (half)_WreckVertexRustInfluence", StringComparison.Ordinal), Is.True);
            Assert.That(shader.Contains("input.vertexColor.g * (half)_WreckVertexAlgaeInfluence", StringComparison.Ordinal), Is.True);
            Assert.That(shader.Contains("1.0h - saturate(input.vertexColor.b)", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("byte wearBlocker", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("DeepReachStationMath.EncodeColor(rust, algae, wearBlocker, 255)", StringComparison.Ordinal), Is.True);
            AssertForbidden(contracts, "DeepReachStationMath.EncodeColor(255, rust, algae, 255)", "DeepReachStationContracts.cs");
        }

        [Test]
        public void DamageBake_NormalizesSourceNormalsBeforeCrushAndMask()
        {
            string contracts = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationContracts.cs");
            Assert.That(contracts.Contains("float3 unitNormal = math.normalizesafe(vertex.Normal", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("vertex.Normal = unitNormal", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("vertex.Position -= unitNormal * crush", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("unitNormal.y * 0.35f", StringComparison.Ordinal), Is.True);
            AssertForbidden(contracts, "vertex.Position -= vertex.Normal * crush", "DeepReachStationContracts.cs");
            AssertForbidden(contracts, "vertex.Normal.y * 0.35f", "DeepReachStationContracts.cs");
        }

        [Test]
        public void VertexWelding_SearchesNeighborQuantizedCells()
        {
            string contracts = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationContracts.cs");
            Assert.That(contracts.Contains("QuantizedPosition(float3 position, float epsilon)", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("QuantizedPositionHash(int3 quantized)", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("TryFindExistingVertex", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("neighborQuantized = quantized + new int3(x, y, z)", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("math.all(bucket.QuantizedCoord == quantized)", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("bucket.QuantizedCoord = quantized", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("CanWeldVertices", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("WeldNormalDotMin", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("WeldUvDistanceSqMax", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("!math.isfinite(WeldEpsilon)", StringComparison.Ordinal), Is.True);
        }

        [Test]
        public void StationMeshAsset_WritesPackedNativeBuffers()
        {
            string fabricator = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationFabricator.cs");
            Assert.That(fabricator.Contains("SetVertexBufferParams(vertexCount, s_vertexLayout)", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("SetVertexBufferData(renderVertices", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("SetIndexBufferData(sortedIndices", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("UploadMeshData(true)", StringComparison.Ordinal), Is.True);
            AssertForbidden(fabricator, "mesh.vertices =", "DeepReachStationFabricator.cs");
            AssertForbidden(fabricator, "mesh.SetIndices(", "DeepReachStationFabricator.cs");
            AssertForbidden(fabricator, "new Vector3[", "DeepReachStationFabricator.cs");
            AssertForbidden(fabricator, "new int[", "DeepReachStationFabricator.cs");
        }

        [Test]
        public void StationMeshAsset_SortsIndicesIntoActiveMaterialSubMeshes()
        {
            string contracts = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationContracts.cs");
            string fabricator = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationFabricator.cs");
            Assert.That(contracts.Contains("RawTriangleMaterials", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("SourceTriangleMaterials", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("WeldedTriangleMaterials", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("BuildMaterialSortedIndexBuffer", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("ResolveTriangleMaterialSlot", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("ResolveActiveMaterials", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("ResolveStationMaterials", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("mesh.subMeshCount = activeSubMeshCount", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("mesh.SetSubMesh(sub", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("renderer.sharedMaterials = materials", StringComparison.Ordinal), Is.True);
            AssertForbidden(fabricator, "mesh.subMeshCount = 1;", "DeepReachStationFabricator.cs");
            AssertForbidden(fabricator, "renderer.sharedMaterial = material", "DeepReachStationFabricator.cs");
        }

        [Test]
        public void StationMeshAsset_ManuallyValidatesBuffersBeforeUnsafeUpload()
        {
            string fabricator = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationFabricator.cs");
            Assert.That(fabricator.Contains("ValidateIndexBuffer", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("vertexCount > vertices.Length", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("indexCount > indices.Length", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("indexCount % 3 != 0", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("Station render vertex is non-finite", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("!DeepReachStationMath.IsFinite(vertex.Position)", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("DontValidateIndices", StringComparison.Ordinal), Is.True);
        }

        [Test]
        public void StationFallbackMaterial_FailsClosedWhenShaderIsUnavailable()
        {
            string fabricator = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationFabricator.cs");
            Assert.That(fabricator.Contains("Shader.Find(\"Universal Render Pipeline/Lit\")", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("Shader.Find(\"Standard\")", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("Unable to resolve fallback station shader", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.IndexOf("Unable to resolve fallback station shader", StringComparison.Ordinal),
                Is.LessThan(fabricator.IndexOf("new Material(shader)", StringComparison.Ordinal)));
        }

        [Test]
        public void StationMaterialResolver_DoesNotMutateAuthoredSourceMaterials()
        {
            string fabricator = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationFabricator.cs");
            string body = ExtractSingleMethodBody(fabricator, "ResolveStationMaterials", "DeepReachStationFabricator.cs");
            AssertForbidden(body, "enableInstancing", "DeepReachStationFabricator.cs::ResolveStationMaterials");
            AssertForbidden(body, "library.PrimaryMaterial.enableInstancing", "DeepReachStationFabricator.cs");
            Assert.That(fabricator.Contains("new Material(shader)", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("enableInstancing = true", StringComparison.Ordinal), Is.True);
        }

        [Test]
        public void StationFabricator_ClampsEditorAllocationBudgetsBeforeNativeAllocation()
        {
            string fabricator = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationFabricator.cs");
            Assert.That(fabricator.Contains("MaxEditorSourceVertexCapacity", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("MaxEditorSourceIndexCapacity", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("Station source vertex budget exceeded", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("Station source index budget exceeded", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.IndexOf("ResolveMeshCapacity", StringComparison.Ordinal), Is.LessThan(fabricator.IndexOf("new NativeArray<StationMeshVertexDTO>(sourceVertexCapacity", StringComparison.Ordinal)));
        }

        [Test]
        public void StationFabricator_RejectsNonFiniteSettingsBeforeNativeAllocation()
        {
            string fabricator = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationFabricator.cs");
            Assert.That(fabricator.Contains("Station cell size is non-finite", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("Station quality weight is non-finite", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("Station weld epsilon is non-finite", StringComparison.Ordinal), Is.True);
            int sanitizeCell = fabricator.IndexOf("!math.isfinite(settings.CellSize)", StringComparison.Ordinal);
            int firstCellUse = fabricator.IndexOf("settings.CellSize = math.clamp", StringComparison.Ordinal);
            Assert.That(sanitizeCell, Is.GreaterThanOrEqualTo(0));
            Assert.That(firstCellUse, Is.GreaterThanOrEqualTo(0));
            Assert.That(sanitizeCell, Is.LessThan(firstCellUse));
        }

        [Test]
        public void StationAssetPaths_AreClampedToProjectAssets()
        {
            string fabricator = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationFabricator.cs");
            Assert.That(fabricator.Contains("SanitizeAssetFolder", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("StartsWith(\"Assets/\"", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("Path.GetInvalidFileNameChars()", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("Station asset folder contains invalid character", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("SanitizeAssetName", StringComparison.Ordinal), Is.True);
        }

        [Test]
        public void StationSockets_DoNotHashConnectorTypesIntoCollisionBits()
        {
            string moduleLibrary = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationModuleLibrary.cs");
            Assert.That(moduleLibrary.Contains("Dictionary<string, ushort>", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("connectorMasks.Count + 1", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("exceeded the 15 explicit connector bits", StringComparison.Ordinal), Is.True);
            AssertForbidden(moduleLibrary, "hash % 15", "DeepReachStationModuleLibrary.cs");
        }

        [Test]
        public void StationSocketDtos_AreMaterializedWithModuleRanges()
        {
            string contracts = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationContracts.cs");
            string moduleLibrary = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationModuleLibrary.cs");
            Assert.That(contracts.Contains("SourceSocketStart", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("NativeArray<StationSocketDTO> Sockets", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("BuildSocketDTO", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("dto.LocalPosition", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("dto.LocalRotation", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("Station socket transform is invalid", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("math.normalize(rotation)", StringComparison.Ordinal), Is.True);
        }

        [Test]
        public void StationSocketDirections_AreInferredFromMarkerPositions()
        {
            string moduleLibrary = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationModuleLibrary.cs");
            Assert.That(moduleLibrary.Contains("ResolveSocketDirection", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("ResolveSocketLocalPosition", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("strongest < 0.45f", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("ConvertDirection(socket.Direction)", StringComparison.Ordinal), Is.True);
        }

        [Test]
        public void StationModuleBoundsTransform_DoesNotAllocateCornerArrays()
        {
            string moduleLibrary = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationModuleLibrary.cs");
            Assert.That(moduleLibrary.Contains("private static Bounds TransformBounds", StringComparison.Ordinal), Is.True);
            AssertForbidden(moduleLibrary, "Vector3[] corners", "DeepReachStationModuleLibrary.cs");
            AssertForbidden(moduleLibrary, "Vector3[] p", "DeepReachStationModuleLibrary.cs");
            Assert.That(moduleLibrary.Contains("AppendBoxSurrogateVertex", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("result.Encapsulate(matrix.MultiplyPoint3x4", StringComparison.Ordinal), Is.True);
        }

        [Test]
        public void HiddenSurfaceCulling_IsSocketWindowBounded()
        {
            string moduleLibrary = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationModuleLibrary.cs");
            Assert.That(moduleLibrary.Contains("IsNearSocketWindow", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("TangentialDistanceSq", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("nearFace && nearSocket", StringComparison.Ordinal), Is.True);
        }

        [Test]
        public void StationModuleScan_IncludesRuinedStructuralPrefabs()
        {
            string moduleLibrary = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationModuleLibrary.cs");
            Assert.That(moduleLibrary.Contains("PFB_Module_", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("PFB_Ruin_", StringComparison.Ordinal), Is.True);
            AssertForbidden(moduleLibrary, "PFB_Debris_", "DeepReachStationModuleLibrary.cs");
        }

        [Test]
        public void StationModuleScan_DoesNotSilentlyDropStructuralPrefabs()
        {
            string moduleLibrary = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationModuleLibrary.cs");
            Assert.That(moduleLibrary.Contains("Refusing to silently drop prefab", StringComparison.Ordinal), Is.True);
            AssertForbidden(moduleLibrary, "rules.Count < DeepReachStationConstants.MaxModuleRules", "DeepReachStationModuleLibrary.cs");
        }

        [Test]
        public void StationModuleScan_UsesListMeshAccessorsInsteadOfCopyArrays()
        {
            string moduleLibrary = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationModuleLibrary.cs");
            Assert.That(moduleLibrary.Contains("mesh.GetVertices(meshVertices)", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("mesh.GetNormals(meshNormals)", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("mesh.GetUVs(0, meshUvs)", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("mesh.GetTriangles(meshIndices, sub, true)", StringComparison.Ordinal), Is.True);
            AssertForbidden(moduleLibrary, "mesh.vertices", "DeepReachStationModuleLibrary.cs");
            AssertForbidden(moduleLibrary, "mesh.normals", "DeepReachStationModuleLibrary.cs");
            AssertForbidden(moduleLibrary, "mesh.uv", "DeepReachStationModuleLibrary.cs");
            AssertForbidden(moduleLibrary, "mesh.GetTriangles(sub, true)", "DeepReachStationModuleLibrary.cs");
        }

        [Test]
        public void StationModuleScan_RejectsNonFiniteBoundsBeforeDtoMaterialization()
        {
            string moduleLibrary = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationModuleLibrary.cs");
            Assert.That(moduleLibrary.Contains("EnsureFiniteBounds(localBounds, prefab.name)", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("private static bool IsFiniteBounds(Bounds bounds)", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("Station module bounds are non-finite", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("!IsFiniteBounds(renderer.bounds)", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("!IsFiniteBounds(local)", StringComparison.Ordinal), Is.True);
        }

        [Test]
        public void StationPrimaryMaterial_RejectsTransparentLeakAndGhostMaterials()
        {
            string moduleLibrary = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationModuleLibrary.cs");
            Assert.That(moduleLibrary.Contains("IsPreferredStructuralMaterial", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("IsRejectedPrimaryMaterial", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("Mat_Module_", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("MAT_family_ruin", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("material.IsKeywordEnabled(\"_ALPHABLEND_ON\")", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("material.IsKeywordEnabled(\"_SURFACE_TYPE_TRANSPARENT\")", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("material.renderQueue >= 3000", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("material.GetTag(\"RenderType\", true, string.Empty)", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("IndexOf(\"Leak\"", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("IndexOf(\"Glass\"", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("IndexOf(\"Ghost\"", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("IndexOf(\"Scan\"", StringComparison.Ordinal), Is.True);
            AssertForbidden(moduleLibrary, "LeakWetSheen", "DeepReachStationModuleLibrary.cs");
        }

        [Test]
        public void StationModuleScan_PreservesStructuralMaterialSlotsButRejectsTransparentSlots()
        {
            string moduleLibrary = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationModuleLibrary.cs");
            string contracts = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationContracts.cs");
            Assert.That(contracts.Contains("MaxMaterialSlots", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("public Material[] Materials", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("Materials = materials.ToArray()", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("ResolveMaterialSlot", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("ReserveFallbackMaterialSlot(materials)", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("materials.Add(null)", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("IsRejectedPrimaryMaterial(material)", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("return 0;", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("SubMesh = materialSlot", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("ResolveSurrogateMaterialSlot", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("AppendBoxSurrogate(localBounds, sockets, vertexStart, triangleStart, vertices, triangles, surrogateMaterialSlot)", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("var renderers = new List<Renderer>(16)", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("Station structural material vocabulary exceeds", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("out uint primaryMaterialHash", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("slice.MaterialHash = materialHash", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("primaryMaterialHash = HashString(structuralMaterial.name)", StringComparison.Ordinal), Is.True);
            AssertForbidden(moduleLibrary, "SubMesh = (ushort)Mathf.Clamp(sub", "DeepReachStationModuleLibrary.cs");
        }

        [Test]
        public void StationModuleScan_CullsSocketCapsIndependentOfTriangleWinding()
        {
            string moduleLibrary = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationModuleLibrary.cs");
            Assert.That(moduleLibrary.Contains("SocketCapNormalDotThreshold = 0.72f", StringComparison.Ordinal), Is.True);
            Assert.That(moduleLibrary.Contains("math.abs(math.dot(normal, axis)) >= SocketCapNormalDotThreshold", StringComparison.Ordinal), Is.True);
            AssertForbidden(moduleLibrary, "math.dot(normal, axis) > 0.32f", "DeepReachStationModuleLibrary.cs");
            AssertForbidden(moduleLibrary, "math.abs(math.dot(normal, axis)) > 0.32f", "DeepReachStationModuleLibrary.cs");
        }

        [Test]
        public void StationFabricator_DoesNotExposeFatalFaultBypass()
        {
            string fabricator = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationFabricator.cs");
            string window = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationArchitectWindow.cs");
            AssertForbidden(fabricator, "FailOnFault", "DeepReachStationFabricator.cs");
            AssertForbidden(window, "FailOnFault", "DeepReachStationArchitectWindow.cs");
            AssertForbidden(window, "fatal faults", "DeepReachStationArchitectWindow.cs");
        }

        [Test]
        public void StationFabricator_TreatsInvalidSourceTopologyAsFatal()
        {
            string contracts = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationContracts.cs");
            string fabricator = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationFabricator.cs");
            Assert.That(contracts.Contains("FaultInvalidTopology", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("IsValidTriangle", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("IsDegenerateTriangle", StringComparison.Ordinal), Is.True);
            Assert.That(contracts.Contains("math.cross(b - a, c - a)", StringComparison.Ordinal), Is.True);
            Assert.That(fabricator.Contains("FaultInvalidTopology", StringComparison.Ordinal), Is.True);
        }

        [Test]
        public void StationFabricator_TreatsWfcContradictionAsFatal()
        {
            string fabricator = Read("Assets/_Project/Editor/Generators/Structures/DeepReachStationFabricator.cs");
            Assert.That(fabricator.Contains("FaultContradiction", StringComparison.Ordinal), Is.True);
            int faultCheck = fabricator.IndexOf("FailClosedIfRequired(counters[0], \"WFC\")", StringComparison.Ordinal);
            int zeroPlacementCheck = fabricator.IndexOf("HasUsablePlacement(counters[0])", StringComparison.Ordinal);
            Assert.That(faultCheck, Is.GreaterThanOrEqualTo(0));
            Assert.That(zeroPlacementCheck, Is.GreaterThanOrEqualTo(0));
            Assert.That(faultCheck, Is.LessThan(zeroPlacementCheck));
        }

        private static bool HasBalancedBlocks(string source)
        {
            string code = StripNonCode(source);
            int depth = 0;
            for (int i = 0; i < code.Length; i++)
            {
                char c = code[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                    depth--;

                if (depth < 0)
                    return false;
            }

            return depth == 0;
        }

        private static string ExtractSingleMethodBody(string source, string methodName, string label)
        {
            string[] bodies = ExtractMethodBodies(source, methodName);
            Assert.That(bodies.Length, Is.GreaterThanOrEqualTo(1), label + " missing method " + methodName);
            return bodies[0];
        }

        private static string[] ExtractMethodBodies(string source, string methodName)
        {
            var bodies = new List<string>(4);
            string pattern = @"\b(?:public|private|internal|protected)\s+(?:static\s+)?[\w<>\[\],\s]+\s+" + Regex.Escape(methodName) + @"\s*\(";
            MatchCollection matches = Regex.Matches(source, pattern);
            for (int i = 0; i < matches.Count; i++)
            {
                int brace = source.IndexOf('{', matches[i].Index);
                if (brace < 0)
                    continue;

                bodies.Add(ExtractBlock(source, brace));
            }

            return bodies.ToArray();
        }

        private static string ExtractBlock(string source, int startBrace)
        {
            string code = StripNonCode(source);
            int depth = 0;
            for (int i = startBrace; i < code.Length; i++)
            {
                char c = code[i];
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(startBrace, i - startBrace + 1);
                }
            }

            Assert.Fail("Unbalanced method block at " + startBrace);
            return string.Empty;
        }

        private static string StripNonCode(string source)
        {
            char[] code = source.ToCharArray();
            bool inLineComment = false;
            bool inBlockComment = false;
            bool inString = false;
            bool inVerbatimString = false;
            bool inChar = false;

            for (int i = 0; i < code.Length; i++)
            {
                char c = code[i];
                char next = i + 1 < code.Length ? code[i + 1] : '\0';

                if (inLineComment)
                {
                    if (c == '\n' || c == '\r')
                        inLineComment = false;
                    else
                        code[i] = ' ';
                    continue;
                }

                if (inBlockComment)
                {
                    if (c == '*' && next == '/')
                    {
                        code[i] = ' ';
                        code[i + 1] = ' ';
                        i++;
                        inBlockComment = false;
                    }
                    else
                    {
                        code[i] = ' ';
                    }

                    continue;
                }

                if (inString)
                {
                    if (inVerbatimString)
                    {
                        if (c == '"' && next == '"')
                        {
                            code[i] = ' ';
                            code[i + 1] = ' ';
                            i++;
                        }
                        else if (c == '"')
                        {
                            code[i] = ' ';
                            inString = false;
                            inVerbatimString = false;
                        }
                        else
                        {
                            code[i] = ' ';
                        }
                    }
                    else
                    {
                        bool end = c == '"' && !IsEscaped(source, i);
                        code[i] = ' ';
                        if (end)
                            inString = false;
                    }

                    continue;
                }

                if (inChar)
                {
                    bool end = c == '\'' && !IsEscaped(source, i);
                    code[i] = ' ';
                    if (end)
                        inChar = false;
                    continue;
                }

                if (c == '/' && next == '/')
                {
                    code[i] = ' ';
                    code[i + 1] = ' ';
                    i++;
                    inLineComment = true;
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    code[i] = ' ';
                    code[i + 1] = ' ';
                    i++;
                    inBlockComment = true;
                    continue;
                }

                if (c == '@' && next == '"')
                {
                    code[i] = ' ';
                    code[i + 1] = ' ';
                    i++;
                    inString = true;
                    inVerbatimString = true;
                    continue;
                }

                if (c == '"')
                {
                    code[i] = ' ';
                    inString = true;
                    inVerbatimString = false;
                    continue;
                }

                if (c == '\'')
                {
                    code[i] = ' ';
                    inChar = true;
                }
            }

            return new string(code);
        }

        private static bool IsEscaped(string source, int index)
        {
            int slashCount = 0;
            for (int i = index - 1; i >= 0 && source[i] == '\\'; i--)
                slashCount++;
            return (slashCount & 1) != 0;
        }

        private static string Read(string relativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(projectRoot, relativePath));
        }

        private static void AssertForbidden(string source, string value, string label)
        {
            Assert.That(source.Contains(value, StringComparison.Ordinal), Is.False, label + " contains " + value);
        }

        private static void AssertForbiddenIgnoreCase(string source, string value, string label)
        {
            Assert.That(source.Contains(value, StringComparison.OrdinalIgnoreCase), Is.False, label + " contains " + value);
        }
    }
}
#endif
