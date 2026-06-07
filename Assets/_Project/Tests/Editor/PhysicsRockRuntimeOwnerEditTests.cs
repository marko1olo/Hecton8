using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class PhysicsRockRuntimeOwnerEditTests
    {
        [Test]
        public void RaycastBatchHelper_RuntimeOwnerGateClearsStaleRegistryBeforeBootstrapAndLateFrameRouting()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "RaycastBatchHelper.cs"));
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsRaycastBatchRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "GameBootstrapper.PersistRuntimeService(this);");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterService();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterLateFrame();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterRaycastBatchRuntime(this);");
            StringAssert.Contains("RaycastBatchHelper registered = GlobalRegistry.RaycastBatch", gate);
            StringAssert.Contains("ReferenceEquals(registered, null)", gate);
            StringAssert.Contains("ReferenceEquals(registered, this)", gate);
            StringAssert.Contains("if (IsRaycastBatchRuntimeUsable(registered))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterRaycastBatchRuntime(registered);", gate);
            StringAssert.Contains("helper._registeredService", usable);
            StringAssert.Contains("helper.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
        }

        [Test]
        public void HectonRockManager_RuntimeOwnerGateClearsStaleMirrorAndRegistryBeforeGpuiRouting()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "HectonRockManager.cs"));
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string register = ExtractMethodBody(source, "private void TryRegisterToGlobalRegistry()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsRockManagerRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "_chunkData = new Dictionary");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "_prototypeLookup = new Dictionary");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "ApplyVendorGpuiManagerAdmission();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterToTickManager();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", start);
            AssertTextBefore(start, "if (TryAbortForUsableExistingRuntime())", "ApplyVendorGpuiManagerAdmission();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterRockManagerRuntime(this);");
            StringAssert.Contains("HectonRockManager active = s_activeRuntime", gate);
            StringAssert.Contains("HectonRockManager registered = GlobalRegistry.RockManager", gate);
            StringAssert.Contains("if (IsRockManagerRuntimeUsable(active))", gate);
            StringAssert.Contains("if (IsRockManagerRuntimeUsable(registered))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("s_activeRuntime = null", gate);
            StringAssert.Contains("s_activeRuntime = registered", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterRockManagerRuntime(active);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterRockManagerRuntime(registered);", gate);
            StringAssert.Contains("manager._serviceRegistered", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
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
