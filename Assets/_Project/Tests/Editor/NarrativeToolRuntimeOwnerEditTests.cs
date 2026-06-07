using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class NarrativeToolRuntimeOwnerEditTests
    {
        [Test]
        public void CorporateOrderSystem_RuntimeOwnerGateClearsStaleRegistryBeforeSaveAndTickRouting()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Narrative", "CorporateOrderSystem.cs"));
            string awake = ExtractMethodBody(source, "private void Awake()");
            string register = ExtractMethodBody(source, "private bool TryRegisterRuntime()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsCorporateOrderRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterCorporateOrderRuntime(this);");
            StringAssert.Contains("CorporateOrderSystem registered = GlobalRegistry.CorporateOrders", gate);
            StringAssert.Contains("ReferenceEquals(registered, null)", gate);
            StringAssert.Contains("ReferenceEquals(registered, this)", gate);
            StringAssert.Contains("if (IsCorporateOrderRuntimeUsable(registered))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterCorporateOrderRuntime(registered);", gate);
            StringAssert.Contains("system._runtimeRegistered", usable);
            StringAssert.Contains("system.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
        }

        [Test]
        public void ToolDurabilitySystem_RuntimeOwnerGateClearsStaleRegistryBeforeNativeStateAndSaveRouting()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Tools", "ToolDurabilitySystem.cs"));
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsToolDurabilityRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "CacheRegistryDependenciesCold();");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "EnsureNativeStateCold();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "EnsureNativeStateCold();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterSaveService();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", start);
            AssertTextBefore(start, "if (TryAbortForUsableExistingRuntime())", "EnsureNativeStateCold();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterToolDurabilityRuntime(this);");
            StringAssert.Contains("ToolDurabilitySystem registered = GlobalRegistry.ToolDurability", gate);
            StringAssert.Contains("ReferenceEquals(registered, null)", gate);
            StringAssert.Contains("ReferenceEquals(registered, this)", gate);
            StringAssert.Contains("if (IsToolDurabilityRuntimeUsable(registered))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterToolDurabilityRuntime(registered);", gate);
            StringAssert.Contains("system._serviceRegistered", usable);
            StringAssert.Contains("system.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
        }

        [Test]
        public void LoreHashRebakeWritesSourceThroughAtomicTempPromotion()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Narrative", "LoreDatabaseManager.cs"));
            string rebake = ExtractMethodBody(source, "private static void RebakeLoreHashes()");
            string write = ExtractMethodBody(source, "private static void WriteAllLinesAtomic(");
            string promote = ExtractMethodBody(source, "private static void PromoteTempFileAtomic(");

            StringAssert.Contains("WriteAllLinesAtomic(fullSourcePath, lines, new UTF8Encoding(false));", rebake);
            StringAssert.Contains("string tempPath = path + \".tmp\";", write);
            StringAssert.Contains("TryDeleteFileNoThrow(tempPath);", write);
            StringAssert.Contains("FileMode.CreateNew", write);
            StringAssert.Contains("FileOptions.WriteThrough | FileOptions.SequentialScan", write);
            StringAssert.Contains("writer.Flush();", write);
            StringAssert.Contains("stream.Flush(true);", write);
            StringAssert.Contains("PromoteTempFileAtomic(tempPath, path);", write);
            StringAssert.Contains("File.Replace(tempPath, destinationPath, null, true);", promote);
            StringAssert.Contains("File.Move(tempPath, destinationPath);", promote);
            StringAssert.DoesNotContain("File.WriteAllLines(fullSourcePath", source);
            AssertTextBefore(write, "stream.Flush(true);", "PromoteTempFileAtomic(tempPath, path);");
        }

        private static void AssertTextBefore(string body, string expectedEarlier, string expectedLater)
        {
            int earlierIndex = body.IndexOf(expectedEarlier, StringComparison.Ordinal);
            int laterIndex = body.IndexOf(expectedLater, StringComparison.Ordinal);
            Assert.GreaterOrEqual(earlierIndex, 0, "Missing earlier text: " + expectedEarlier);
            Assert.GreaterOrEqual(laterIndex, 0, "Missing later text: " + expectedLater);
            Assert.Less(earlierIndex, laterIndex, expectedEarlier + " should appear before " + expectedLater);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, "Missing method signature: " + signature);

            int bodyStart = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(bodyStart, 0, "Missing method body: " + signature);

            int depth = 0;
            for (int i = bodyStart; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c != '}')
                    continue;

                depth--;
                if (depth == 0)
                    return source.Substring(bodyStart, i - bodyStart + 1);
            }

            Assert.Fail("Unclosed method body: " + signature);
            return string.Empty;
        }
    }
}
