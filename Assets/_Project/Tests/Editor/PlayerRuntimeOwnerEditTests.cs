using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class PlayerRuntimeOwnerEditTests
    {
        [Test]
        public void PlayerExpressionManager_RuntimeOwnerGateClearsStaleMirrorAndRegistryBeforeProfileAndSaveRouting()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Gameplay", "PlayerExpressionManager.cs"));
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsPlayerExpressionRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "AutoResolveReferences();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "CachePlayerRuntimeContext(GlobalRegistry.Player);");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterSaveOwner();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterPlayerExpressionRuntime(this);");
            StringAssert.Contains("PlayerExpressionManager active = s_activeRuntimeInstance", gate);
            StringAssert.Contains("PlayerExpressionManager registered = GlobalRegistry.PlayerExpression", gate);
            StringAssert.Contains("if (IsPlayerExpressionRuntimeUsable(active))", gate);
            StringAssert.Contains("if (IsPlayerExpressionRuntimeUsable(registered))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("s_activeRuntimeInstance = null", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterPlayerExpressionRuntime(active);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterPlayerExpressionRuntime(registered);", gate);
            StringAssert.Contains("manager._serviceRegistered", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
            StringAssert.DoesNotContain("s_activeRuntimeInstance ?? GlobalRegistry.PlayerExpression", source);
        }

        [Test]
        public void PlayerActionController_RuntimeOwnerGateClearsStaleRegistryBeforeInputAudioRouting()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Gameplay", "PlayerActionController.cs"));
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsPlayerActionRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "_cachedTransform = transform;");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "ConsumableItem.BindSurvivalSystemCold(_survivalSystem);");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterHotSwap();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterPlayerActionRuntime(this);");
            StringAssert.Contains("PlayerActionController registered = GlobalRegistry.PlayerActions", gate);
            StringAssert.Contains("ReferenceEquals(registered, null)", gate);
            StringAssert.Contains("ReferenceEquals(registered, this)", gate);
            StringAssert.Contains("if (IsPlayerActionRuntimeUsable(registered))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterPlayerActionRuntime(registered);", gate);
            StringAssert.Contains("controller._serviceRegistered", usable);
            StringAssert.Contains("controller.isActiveAndEnabled", usable);
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
