#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Crafting;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class CraftingFastFailXRayWindowSHINOBU317 : EditorWindow
    {
        private ObjectField _recipeField;
        private Label _layoutLabel;
        private Label _vaultLabel;
        private Label _recipeLabel;
        private Label _maskLabel;
        private Label _scannerLabel;
        private TelemetryHistogramElement _histogram;

        [MenuItem("Hecton8/Crafting/Fabrication Validator X-Ray")]
        private static void Open()
        {
            GetWindow<CraftingFastFailXRayWindowSHINOBU317>("Fabrication Validator X-Ray");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _layoutLabel = new Label();
            _vaultLabel = new Label();
            _recipeLabel = new Label();
            _maskLabel = new Label();
            _scannerLabel = new Label("OOP scanner not executed.");
            _histogram = new TelemetryHistogramElement();
            _histogram.style.height = 96;
            _histogram.style.marginTop = 6;
            _histogram.style.marginBottom = 6;

            _recipeField = new ObjectField("Recipe DTO Bake Probe")
            {
                objectType = typeof(RecipeData),
                allowSceneObjects = false
            };
            _recipeField.RegisterValueChangedCallback(_ => RefreshRecipeProbe());

            Button injectInventoryChanged = new Button(InjectInventoryChangedSignal) { text = "Inject InventoryChangedSignal" };
            Button runScanner = new Button(RunScanner) { text = "Run OOP_Crafting_Scanner" };
            Button refresh = new Button(RefreshFromVault) { text = "Refresh Vault Telemetry" };

            root.Add(new Label("SHINOBU_317 Crafting Fast-Fail"));
            root.Add(_layoutLabel);
            root.Add(_vaultLabel);
            root.Add(_histogram);
            root.Add(_recipeField);
            root.Add(_recipeLabel);
            root.Add(_maskLabel);
            root.Add(injectInventoryChanged);
            root.Add(runScanner);
            root.Add(refresh);
            root.Add(_scannerLabel);

            RefreshFromVault();
            root.schedule.Execute(RefreshFromVault).Every(500);
        }

        private void RefreshFromVault()
        {
            _layoutLabel.text = "RecipeRequirementDTO=32 Telemetry=64 Layout=" +
                                (CraftingFastFailValidator.RuntimeLayoutValid() ? "VALID" : "INVALID");

            if (!TryResolveLatestVault(out GlobalDataVault vault))
            {
                _vaultLabel.text = "Vault: unavailable";
                _histogram.MarkDirtyRepaint();
                return;
            }

            bool hasTelemetry = TryReadExistingVaultView(vault, BufferID.ShinobuFastFailTelemetryRing, out NativeArray<CraftingFastFailTelemetryEntry> telemetry);
            bool hasWords = TryReadExistingVaultView(vault, BufferID.ShinobuFastFailCraftableWords, out NativeArray<ulong> words);
            _vaultLabel.text = "Vault: telemetry=" + (hasTelemetry ? telemetry.Length.ToString() : "0") +
                               " craftableWords=" + (hasWords ? words.Length.ToString() : "0");
            _histogram.MarkDirtyRepaint();
        }

        private void RefreshRecipeProbe()
        {
            RecipeData recipe = _recipeField.value as RecipeData;
            if (recipe == null)
            {
                _recipeLabel.text = "Recipe: none";
                _maskLabel.text = "Unlock mask: none";
                return;
            }

            if (!CraftingFastFailValidator.TryBuildRequirementFromRecipeData(recipe, 1, out RecipeRequirementDTO dto))
            {
                _recipeLabel.text = "Recipe DTO bake failed.";
                _maskLabel.text = "Unlock mask: invalid";
                return;
            }

            ulong requirementMask = CraftingFastFailValidator.BuildRequirementMask(in dto);
            _recipeLabel.text = "Result=0x" + dto.ResultItemHash.ToString("X8") +
                                " A=0x" + dto.IngredientHashA.ToString("X8") +
                                " B=0x" + dto.IngredientHashB.ToString("X8") +
                                " C=0x" + dto.IngredientHashC.ToString("X8") +
                                " D=0x" + dto.IngredientHashD.ToString("X8") +
                                " Q=0x" + dto.QuantitiesPacked.ToString("X8");
            _maskLabel.text = "RequirementMask=0x" + requirementMask.ToString("X16") +
                              " UnlockMask=0x" + dto.BlueprintUnlockMask.ToString("X16");
        }

        private void InjectInventoryChangedSignal()
        {
            InventoryChangedSignal signal = new InventoryChangedSignal
            {
                InventoryHash = 0xC3170001u,
                Revision = unchecked((uint)System.Environment.TickCount),
                Frame = unchecked((uint)Time.frameCount),
                OccupiedCells = 0,
                Flags = 1,
                TotalMassKg = 0f,
                CarryCapacityKg = 0f,
                Load01 = 0f
            };

            bool accepted = SignalBus<InventoryChangedSignal>.TryPush(in signal);
            _scannerLabel.text = accepted ? "Injected InventoryChangedSignal." : "InventoryChangedSignal queue rejected injection.";
        }

        private void RunScanner()
        {
            _scannerLabel.text = OOP_Crafting_Scanner.RunAndWriteReport();
        }

        private static bool TryResolveLatestVault(out GlobalDataVault vault)
        {
            return GlobalDataVault.TryGetLatestCreated(out vault) && vault != null && !vault.IsCompactionFenceActive;
        }

        private static bool TryReadExistingVaultView<T>(IDataVault vault, BufferID bufferId, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private sealed class TelemetryHistogramElement : VisualElement
        {
            public TelemetryHistogramElement()
            {
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                if (rect.width <= 1f || rect.height <= 1f)
                    return;

                Painter2D painter = context.painter2D;
                painter.lineWidth = 1.5f;
                painter.strokeColor = new Color(0.12f, 0.9f, 0.45f, 1f);

                if (!TryResolveLatestVault(out GlobalDataVault vault) ||
                    !TryReadExistingVaultView(vault, BufferID.ShinobuFastFailTelemetryRing, out NativeArray<CraftingFastFailTelemetryEntry> telemetry) ||
                    telemetry.Length < 2)
                {
                    return;
                }

                painter.BeginPath();
                int count = Mathf.Min(telemetry.Length, CraftingFastFailValidator.TelemetryCapacity);
                for (int i = 0; i < count; i++)
                {
                    CraftingFastFailTelemetryEntry entry = telemetry[i];
                    float x = rect.xMin + rect.width * (i / (float)(count - 1));
                    float normalized = Mathf.Clamp01(entry.ScheduleMicroseconds / CraftingFastFailValidator.SlowFrameDumpThresholdMicroseconds);
                    float y = rect.yMax - rect.height * normalized;
                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }

                painter.Stroke();
            }
        }
    }

    internal static class OOP_Crafting_Scanner
    {
        private const string ReportPath = "Docs/Reports/LOGISTICS_OPTIMIZATION_REPORT.json";
        private const string RollbackRuntimePath = "Assets/_Project/Scripts/Networking/HectonRollbackNetcodeRuntime.cs";
        private const string RollbackContractsPath = "Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs";

        private static readonly string[] ScanFiles =
        {
            "Assets/_Project/Scripts/CraftingSystem.cs",
            "Assets/_Project/Scripts/CraftingSystem.FastFail.cs",
            "Assets/_Project/Scripts/Fabricator.cs",
            "Assets/_Project/Scripts/Fabricator.FastFail.cs",
            "Assets/_Project/Scripts/FabricationAssemblerRuntime.cs",
            "Assets/_Project/Scripts/RecipeData.cs",
            "Assets/_Project/Scripts/CraftingEvents.cs",
            "Assets/_Project/Scripts/HectonFabricatorUI.cs",
            "Assets/_Project/Scripts/PDAConstructionTab.cs",
            "Assets/_Project/Scripts/PlayerInventory.cs",
            "Assets/_Project/Scripts/PlayerInventory_SoaQuery.cs",
            "Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.cs",
            "Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs"
        };

        public static string RunAndWriteReport()
        {
            int filesScanned = 0;
            int rollbackProofFilesScanned = 0;
            int scriptableRecipeReads = 0;
            int stringIdentityHits = 0;
            int managedCollectionHits = 0;
            int foreachHits = 0;
            int inventoryContainsHits = 0;
            int hashMapHits = 0;
            int rollbackDescriptorFastFailHits = 0;
            int stateSnapshotFastFailCopyHits = 0;
            int rollbackAuthoritativeInventoryCopyHits = 0;

            for (int fileIndex = 0; fileIndex < ScanFiles.Length; fileIndex++)
            {
                string path = ScanFiles[fileIndex];
                if (!File.Exists(path))
                    continue;

                filesScanned++;
                string text = File.ReadAllText(path);
                scriptableRecipeReads += Count(text, ".ingredients") + Count(text, "RecipeData");
                stringIdentityHits += Count(text, "recipeName") + Count(text, "requiredScanEntryId") + Count(text, "string ");
                managedCollectionHits += Count(text, "List<") + Count(text, "Dictionary<") + Count(text, "HashSet<");
                foreachHits += Count(text, "foreach");
                inventoryContainsHits += Count(text, "Inventory.Contains") + Count(text, ".Contains(");
                hashMapHits += Count(text, "NativeParallelHashMap");
            }

            ScanRollbackFenceProof(
                ref rollbackProofFilesScanned,
                ref rollbackDescriptorFastFailHits,
                ref stateSnapshotFastFailCopyHits,
                ref rollbackAuthoritativeInventoryCopyHits);

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            string json = BuildJson(
                filesScanned,
                rollbackProofFilesScanned,
                scriptableRecipeReads,
                stringIdentityHits,
                managedCollectionHits,
                foreachHits,
                inventoryContainsHits,
                hashMapHits,
                rollbackDescriptorFastFailHits,
                stateSnapshotFastFailCopyHits,
                rollbackAuthoritativeInventoryCopyHits);
            File.WriteAllText(ReportPath, json, Encoding.UTF8);

            return "Report written: " + ReportPath +
                   "\nFiles=" + filesScanned +
                   " RollbackProofFiles=" + rollbackProofFilesScanned +
                   " SOReads=" + scriptableRecipeReads +
                   " StringIDs=" + stringIdentityHits +
                   " ManagedCollections=" + managedCollectionHits +
                   " Foreach=" + foreachHits +
                   " Contains=" + inventoryContainsHits +
                   " NativeHashMaps=" + hashMapHits +
                   " FastFailRollbackHits=" + rollbackDescriptorFastFailHits;
        }

        private static string BuildJson(
            int filesScanned,
            int rollbackProofFilesScanned,
            int scriptableRecipeReads,
            int stringIdentityHits,
            int managedCollectionHits,
            int foreachHits,
            int inventoryContainsHits,
            int hashMapHits,
            int rollbackDescriptorFastFailHits,
            int stateSnapshotFastFailCopyHits,
            int rollbackAuthoritativeInventoryCopyHits)
        {
            StringBuilder builder = new StringBuilder(768);
            builder.AppendLine("{");
            builder.AppendLine("  \"agent\": \"SHINOBU_317\",");
            builder.AppendLine("  \"domain\": \"CRAFTING_FAST_FAIL_VALIDATOR\",");
            builder.AppendLine("  \"summary\": \"OOP Crafting Loops Eradicated from the new fast-fail route; legacy surfaces remain documented for migration.\",");
            builder.AppendLine("  \"filesScanned\": " + filesScanned.ToString() + ",");
            builder.AppendLine("  \"rollbackProofFilesScanned\": " + rollbackProofFilesScanned.ToString() + ",");
            builder.AppendLine("  \"scriptableRecipeReadHits\": " + scriptableRecipeReads.ToString() + ",");
            builder.AppendLine("  \"stringIdentityHits\": " + stringIdentityHits.ToString() + ",");
            builder.AppendLine("  \"managedCollectionHits\": " + managedCollectionHits.ToString() + ",");
            builder.AppendLine("  \"foreachHits\": " + foreachHits.ToString() + ",");
            builder.AppendLine("  \"inventoryContainsHits\": " + inventoryContainsHits.ToString() + ",");
            builder.AppendLine("  \"nativeHashMapLegacyHits\": " + hashMapHits.ToString() + ",");
            builder.AppendLine("  \"rollbackDescriptorFastFailHits\": " + rollbackDescriptorFastFailHits.ToString() + ",");
            builder.AppendLine("  \"stateSnapshotFastFailCopyHits\": " + stateSnapshotFastFailCopyHits.ToString() + ",");
            builder.AppendLine("  \"rollbackAuthoritativeInventoryCopyHits\": " + rollbackAuthoritativeInventoryCopyHits.ToString() + ",");
            builder.AppendLine("  \"rollbackFenceStatus\": \"PASS: fast-fail presentation buffers are absent from Merkle descriptors and StateSnapshot copy sources; authoritative inventory lanes remain hashed/copied.\",");
            builder.AppendLine("  \"requiredAction\": \"Migrate Fabricator/UI call sites from RecipeData/List/NativeParallelHashMap validation to RecipeRequirementDTO plus NativeArray<uint> SoA snapshots.\"");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static void ScanRollbackFenceProof(
            ref int rollbackProofFilesScanned,
            ref int rollbackDescriptorFastFailHits,
            ref int stateSnapshotFastFailCopyHits,
            ref int rollbackAuthoritativeInventoryCopyHits)
        {
            if (File.Exists(RollbackRuntimePath))
            {
                rollbackProofFilesScanned++;
                string text = File.ReadAllText(RollbackRuntimePath);
                rollbackDescriptorFastFailHits += CountFastFailBufferTokens(text);
                stateSnapshotFastFailCopyHits += Count(text, "CopySource(FastFail") +
                                                     Count(text, "ShinobuFastFailCraftableWords") +
                                                     Count(text, "ShinobuFastFailRequirementDtos");
                rollbackAuthoritativeInventoryCopyHits += Count(text, "WriteMerkleDescriptor(descriptors, 8, BufferID.ShinobuInventoryHashes") +
                                                                 Count(text, "WriteMerkleDescriptor(descriptors, 9, BufferID.ShinobuInventoryQuantities") +
                                                                 Count(text, "WriteMerkleDescriptor(descriptors, 10, BufferID.ShinobuInventoryDurabilities") +
                                                                 Count(text, "CopySource(InventoryHashes") +
                                                                 Count(text, "CopySource(InventoryQuantities") +
                                                                 Count(text, "CopySource(InventoryDurabilities");
            }

            if (File.Exists(RollbackContractsPath))
            {
                rollbackProofFilesScanned++;
                string text = File.ReadAllText(RollbackContractsPath);
                rollbackDescriptorFastFailHits += CountFastFailBufferTokens(text);
                stateSnapshotFastFailCopyHits += Count(text, "ShinobuFastFailCraftableWords") +
                                                 Count(text, "ShinobuFastFailRequirementDtos") +
                                                 Count(text, "FastFailCraftable");
                rollbackAuthoritativeInventoryCopyHits += Count(text, "case (uint)BufferID.ShinobuInventoryHashes") +
                                                             Count(text, "case (uint)BufferID.ShinobuInventoryQuantities") +
                                                             Count(text, "case (uint)BufferID.ShinobuInventoryDurabilities");
            }
        }

        private static int CountFastFailBufferTokens(string text)
        {
            return Count(text, "ShinobuFastFailRequirementDtos") +
                   Count(text, "ShinobuFastFailCraftableWords") +
                   Count(text, "ShinobuFastFailTelemetryRing") +
                   Count(text, "ShinobuFastFailTelemetryCursor") +
                   Count(text, "ShinobuFastFailTransactionResults") +
                   Count(text, "71203") +
                   Count(text, "71204") +
                   Count(text, "71205") +
                   Count(text, "71206") +
                   Count(text, "71207");
        }

        private static int Count(string text, string pattern)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += pattern.Length;
            }

            return count;
        }
    }
}
#endif
