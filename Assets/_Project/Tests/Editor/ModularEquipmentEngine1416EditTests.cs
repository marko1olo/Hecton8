using System;
using System.IO;
using System.Text.RegularExpressions;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Tools;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ModularEquipmentEngine1416EditTests
    {
        private const string EquipmentSourcePath = "Assets/_Project/Scripts/ModularEquipmentEngine.cs";
        private const string EquipmentViewSourcePath = "Assets/_Project/Scripts/Tools/EquipmentVaultView.cs";
        private const int StressIterations = 1024;

        [Test]
        public void StaticPurge_NoPersistentNativeCollectionFieldsRemain()
        {
            string source = ReadProjectFile(EquipmentSourcePath);
            string viewSource = ReadProjectFile(EquipmentViewSourcePath);

            int managerNativeFields = Regex.Matches(
                source,
                @"^\s{8}(?:private|public|internal|protected)\s+(?:readonly\s+)?Native(?:Array|List|Queue|HashMap|ParallelHashMap)<.*;",
                RegexOptions.Multiline).Count;
            int handleFields = Regex.Matches(source, @"private\s+VaultGenerationHandle<").Count;
            int stackViewFields = Regex.Matches(source, @"public\s+EquipmentVaultView<").Count;

            Assert.AreEqual(0, managerNativeFields);
            Assert.AreEqual(28, handleFields);
            Assert.AreEqual(28, stackViewFields);
            StringAssert.Contains("internal ref struct EquipmentVaultView<T>", viewSource);
            StringAssert.DoesNotContain("implicit operator NativeArray", viewSource);
        }

        [Test]
        public void StaticLockDiscipline_UsesCapturedJobLocksAndReleaseMask()
        {
            string source = ReadProjectFile(EquipmentSourcePath);
            string acquireViews = ExtractMethod(source, "private bool TryAcquireEquipmentViewsWriteLock");
            string ensureBuffer = ExtractMethod(source, "private static bool EnsureEquipmentBuffer");
            string acquireBuffer = ExtractMethod(source, "private static bool TryAcquireEquipmentWriteBuffer");
            string lateFrame = ExtractMethod(source, "public void LateFrameTick");
            string complete = ExtractMethod(source, "private unsafe void CompleteActiveEquipmentJob");
            string releaseMask = ExtractMethod(source, "private uint ReleaseEquipmentWriteLockMask");
            string contentionTelemetry = ExtractMethod(source, "private void TryRecordEquipmentWriteLockContention");
            string onDisable = ExtractMethod(source, "private void OnDisable");
            string applyRebind = ExtractMethod(source, "private void ApplyDataVaultRebind");
            string disposeNative = ExtractMethod(source, "private void DisposeNativeState");
            string lifecycleDrain = ExtractMethod(source, "private bool DrainEquipmentIntegrationLocksForLifecycle");
            string lifecycleRelease = ExtractMethod(source, "private bool TryReleaseEquipmentVaultHandlesForLifecycle");
            string releaseHandles = ExtractMethod(source, "private bool ReleaseEquipmentVaultHandles");
            string releaseHandle = ExtractMethod(source, "private static bool ReleaseEquipmentVaultHandle");
            string registerTool = ExtractMethod(source, "public uint RegisterTool");
            string installModule = ExtractMethod(source, "public bool TryInstallModule");
            string removeModule = ExtractMethod(source, "public bool TryRemoveModule");
            string rebuildState = ExtractMethod(source, "private bool RebuildCompiledState");
            string upgradeStaging = ExtractMethod(source, "private bool TryWriteUpgradeMatrixStaging");

            Assert.AreEqual(28, Regex.Matches(acquireViews, @"TryAcquireEquipmentWriteBuffer\(").Count);
            StringAssert.Contains("finally", acquireViews);
            StringAssert.Contains("ReleaseEquipmentWriteLocks(vault, acquiredCount)", acquireViews);
            StringAssert.Contains("EnsureEquipmentViews(vault, out _, createIfMissing: true)", acquireViews);
            StringAssert.DoesNotContain("CountAcquiredWriteLock", source);
            StringAssert.Contains("if (!ReleaseEquipmentVaultHandle(vault, ref handle))", ensureBuffer);
            StringAssert.DoesNotContain("handle = default;", ensureBuffer);
            StringAssert.Contains("ref int acquiredCount", acquireBuffer);
            StringAssert.Contains("acquiredCount++;", acquireBuffer);
            StringAssert.DoesNotContain("ReleaseWriteLock(in handle", acquireBuffer);
            StringAssert.Contains("CompleteActiveEquipmentJob();", lateFrame);
            StringAssert.DoesNotContain("forceComplete: true", lateFrame);
            StringAssert.Contains("TryFinalizeCompleted", complete);
            StringAssert.Contains("TryResolveCapturedEquipmentIntegrationViews", complete);
            StringAssert.Contains("finally", complete);
            StringAssert.Contains("ReleaseEquipmentIntegrationWriteLocks", complete);
            StringAssert.Contains("public IDataVault Vault;", source);
            StringAssert.Contains("IDataVault vault = views.Vault;", source);
            StringAssert.Contains("_equipmentIntegrationWriteLockVault = views.Vault;", source);
            StringAssert.Contains("EnsureEquipmentViews(_equipmentIntegrationWriteLockVault, out views)", source);
            Assert.AreEqual(28, Regex.Matches(releaseMask, @"vault\.ReleaseWriteLock").Count);
            StringAssert.Contains("failedMask |=", releaseMask);
            StringAssert.Contains("failedMask |= 1u << 13", contentionTelemetry);
            StringAssert.Contains("failedMask |= 1u << 12", contentionTelemetry);
            StringAssert.Contains("_equipmentPendingReleaseMask |= failedMask", contentionTelemetry);
            StringAssert.Contains("if (!DrainEquipmentIntegrationLocksForLifecycle())", onDisable);
            StringAssert.Contains("TryRecordEquipmentWriteLockContention(EquipmentFaultWriteLockReleaseFailure)", onDisable);
            StringAssert.Contains("CompleteActiveEquipmentJob(forceComplete: true)", lifecycleDrain);
            StringAssert.Contains("ReleaseEquipmentIntegrationWriteLocks", lifecycleDrain);
            StringAssert.Contains("return TryFlushPendingEquipmentWriteLockReleases();", lifecycleDrain);
            StringAssert.Contains("if (!TryReleaseEquipmentVaultHandlesForLifecycle(_dataVault))", applyRebind);
            StringAssert.Contains("bool vaultHandlesReleased = TryReleaseEquipmentVaultHandlesForLifecycle(_dataVault);", disposeNative);
            StringAssert.Contains("if (vaultHandlesReleased)", disposeNative);
            StringAssert.Contains("TryFlushPendingEquipmentWriteLockReleases()", lifecycleRelease);
            StringAssert.Contains("ClearEquipmentVaultHandles();", lifecycleRelease);
            StringAssert.Contains("_equipmentFaultDumpPending = true;", lifecycleRelease);
            StringAssert.Contains("return false;", lifecycleRelease);
            StringAssert.Contains("return !HasEquipmentVaultHandles();", releaseHandles);
            StringAssert.Contains("released &= ReleaseEquipmentVaultHandle", releaseHandles);
            StringAssert.Contains("if (!vault.ReleaseBuffer(in handle))", releaseHandle);
            StringAssert.Contains("return false;", releaseHandle);
            StringAssert.Contains("handle = default;", releaseHandle);
            StringAssert.DoesNotContain("private void RebuildCompiledState", source);
            StringAssert.DoesNotContain("private void WriteUpgradeMatrixStaging", source);
            StringAssert.Contains("Mathf.Min(tool.CopyAuthoredModuleRules", registerTool);
            StringAssert.Contains("ToolUpgradeSystem.MaxModuleSlots", registerTool);
            StringAssert.Contains("if (!TryWriteUpgradeMatrixStaging", registerTool);
            AssertBefore(registerTool, "if (!TryWriteUpgradeMatrixStaging", "_toolOwners[slotIndex] = tool;");
            StringAssert.Contains("if (!RebuildCompiledState(slotIndex, owner, _registrationRules, slotCount))", installModule);
            StringAssert.Contains("if (!RebuildCompiledState(slotIndex, owner, _registrationRules, slotCount))", removeModule);
            StringAssert.DoesNotContain("WriteModuleRuleMirror(slotIndex, _registrationRules, slotCount);", installModule);
            StringAssert.DoesNotContain("WriteModuleRuleMirror(slotIndex, _registrationRules, slotCount);", removeModule);
            AssertBefore(rebuildState, "if (!TryWriteUpgradeMatrixStaging", "views.ToolStats[slotIndex] = compiledStats;");
            StringAssert.DoesNotContain("GetBatteryNormalized", rebuildState);
            AssertBefore(rebuildState, "ToolRuntimeStats previousStats = views.ToolStats[slotIndex];", "state.CurrentBattery *= math.max(0.1f, compiledStats.BatteryCapacity);");
            StringAssert.Contains("WriteModuleRuleMirror(slotIndex, moduleRules, slotCount);", rebuildState);
            AssertBefore(rebuildState, "views.ToolStats[slotIndex] = compiledStats;", "WriteModuleRuleMirror(slotIndex, moduleRules, slotCount);");
            AssertBefore(upgradeStaging, "if (ruleBase < 0", "views.UpgradeMasks[slotIndex]");
            StringAssert.Contains("return true;", upgradeStaging);
        }

        [Test]
        [Explicit("Agent 1416 equipment mock stress harness: creates GlobalDataVault and runs 1024 mock equipment frames. Run only in isolated Editor test pass.")]
        public void MockEquipmentStressHarness_MockGenerationSurvivesRepeatedLockCycles()
        {
            if (GlobalRegistry.DataVault != null)
                Assert.Ignore("Agent 1416 harness requires an empty DataVault slot; run in an isolated Editor test pass.");
            if (GlobalRegistry.ModularEquipment != null)
                Assert.Ignore("Agent 1416 harness requires an empty ModularEquipment slot; run in an isolated Editor test pass.");

            GlobalDataVault vault = GlobalDataVault.Create(256);
            GameObject host = new GameObject("ModularEquipmentEngine_1416_StressHarness");
            ModularEquipmentEngine engine = null;

            try
            {
                GlobalRegistry.RegisterDataVault(vault);
                engine = host.AddComponent<ModularEquipmentEngine>();
                engine.InitializeService();
                Assert.IsTrue(engine.IsInitialized);

                for (int i = 0; i < StressIterations; i++)
                {
                    engine.GenerateMockEquipmentState();
                    engine.Tick(1f / 60f);
                    engine.LateFrameTick();
                    Assert.IsTrue(engine.TryGetActiveEquipmentSlot(0, out ActiveEquipmentDTO state));
                    Assert.AreNotEqual(0u, state.ToolHashID);
                }
            }
            finally
            {
                if (engine != null)
                    GlobalRegistry.UnregisterModularEquipmentService(engine);
                UnityEngine.Object.DestroyImmediate(host);
                GlobalRegistry.UnregisterDataVault(vault);
                vault.Dispose();
            }
        }

        private static string ReadProjectFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }

        private static string ExtractMethod(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, signature);

            int brace = source.IndexOf((char)123, start);
            Assert.GreaterOrEqual(brace, 0, signature);

            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                char value = source[i];
                if (value == (char)123)
                    depth++;
                else if (value == (char)125)
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(start, i - start + 1);
                }
            }

            Assert.Fail(signature);
            return string.Empty;
        }

        private static void AssertBefore(string source, string first, string second)
        {
            int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
            Assert.GreaterOrEqual(firstIndex, 0, first);
            Assert.GreaterOrEqual(secondIndex, 0, second);
            Assert.Less(firstIndex, secondIndex);
        }
    }
}
