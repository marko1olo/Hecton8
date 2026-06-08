using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class PhysicsHotSwapLifecycleEditTests
    {
        [Test]
        public void PhysicsRuntimeOwnersUseTryHotSwapLaneForRegisterAndTeardown()
        {
            AssertTryHotSwapLifecycle(
                ReadProjectFile("Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs"),
                "private void TryRegisterHotSwap()",
                "private void TryUnregisterHotSwap()",
                "_registeredHotSwap");
            AssertTryHotSwapLifecycle(
                ReadProjectFile("Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsRuntime.cs"),
                "private void TryRegister()",
                "private void TryUnregister()",
                "_registeredHotSwap");
            AssertTryHotSwapLifecycle(
                ReadProjectFile("Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementRuntime.cs"),
                "private void TryRegister()",
                "private void TryUnregister()",
                "_registeredHotSwap");
            AssertTryHotSwapLifecycle(
                ReadProjectFile("Assets/_Project/Scripts/Physics/Buoyancy/AnalyticalGerstnerWaveRuntime.cs"),
                "private void TryRegister()",
                "private void TryUnregister()",
                "_registeredHotSwap");
            AssertTryHotSwapLifecycle(
                ReadProjectFile("Assets/_Project/Scripts/Physics/Vehicles/Automation/DockingAutopilotService.cs"),
                "private void TryRegisterHotSwapListener()",
                "private void TryUnregisterHotSwapListener()",
                "_hotSwapRegistered");
        }

        private static void AssertTryHotSwapLifecycle(
            string source,
            string registerSignature,
            string unregisterSignature,
            string registrationField)
        {
            string register = ExtractMethodBody(source, registerSignature);
            string unregister = ExtractMethodBody(source, unregisterSignature);

            StringAssert.Contains(
                registrationField + " = GlobalRegistry.TryRegisterHotSwapListener(this);",
                register);
            StringAssert.Contains("GlobalRegistry.TryUnregisterHotSwapListener(this);", unregister);
            StringAssert.Contains(registrationField + " = false;", unregister);
            StringAssert.DoesNotContain("GlobalRegistry.RegisterHotSwapListener(this);", register);
            StringAssert.DoesNotContain("GlobalRegistry.UnregisterHotSwapListener(this);", unregister);
            StringAssert.DoesNotContain("GlobalRegistry.IsHotSwapListenerRegistered(this)", source);
        }

        private static string ReadProjectFile(string relativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(projectRoot, relativePath));
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "Missing method: " + signature);

            int brace = source.IndexOf('{', start);
            Assert.GreaterOrEqual(brace, 0, "Missing body: " + signature);

            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(start, i - start + 1);
                }
            }

            Assert.Fail("Unclosed method body: " + signature);
            return string.Empty;
        }
    }
}
