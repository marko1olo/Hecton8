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
            string acquireBuffer = ExtractMethod(source, "private static bool TryAcquireEquipmentWriteBuffer");
            string lateFrame = ExtractMethod(source, "public void LateFrameTick");
            string complete = ExtractMethod(source, "private unsafe void CompleteActiveEquipmentJob");
            string releaseMask = ExtractMethod(source, "private uint ReleaseEquipmentWriteLockMask");

            Assert.AreEqual(28, Regex.Matches(acquireViews, @"TryAcquireEquipmentWriteBuffer\(").Count);
            StringAssert.DoesNotContain("CountAcquiredWriteLock", source);
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
    }
}
