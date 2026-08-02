#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Dev;


namespace Hecton8.Tests.Editor
{
    public class ToolRuntimeSmokeTesterEditTests
    {
        private class MockThrowingPlayerTool : PlayerTool
        {
            public bool throwInPrimary;
            public bool throwInSecondary;

            public override void UsePrimary(float deltaTime)
            {
                if (throwInPrimary) throw new InvalidOperationException("Mock Primary Exception");
            }

            public override void UseSecondary(float deltaTime)
            {
                if (throwInSecondary) throw new InvalidOperationException("Mock Secondary Exception");
            }

            // Abstract members we need to implement
            public override string GetToolInternalId() => "MockTool";
            public override string GetToolDisplayName() => "MockTool";
            public override void OnHotSwapRefSync(Hecton8.Core.GlobalRegistry.HotSwapRefEvent evt) {}
            public override void OnHotSwap(Hecton8.Core.GlobalRegistry.HotSwapEvent evt) {}
            public override void OnSpawn() {}
            public override void OnDespawn() {}
        }

        private ToolRuntimeSmokeTester smokeTester;
        private MethodInfo runToolInvocationMethod;

        [SetUp]
        public void Setup()
        {
            var go = new GameObject("Tester");
            smokeTester = go.AddComponent<ToolRuntimeSmokeTester>();

            runToolInvocationMethod = typeof(ToolRuntimeSmokeTester).GetMethod("RunToolInvocation", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(runToolInvocationMethod, "RunToolInvocation method not found");
        }

        [TearDown]
        public void TearDown()
        {
            if (smokeTester != null)
            {
                UnityEngine.Object.DestroyImmediate(smokeTester.gameObject);
            }
        }

        [Test]
        public void RunToolInvocation_WhenPrimaryThrows_ReturnsFalse()
        {
            var mockToolGo = new GameObject("MockTool");
            var mockTool = mockToolGo.AddComponent<MockThrowingPlayerTool>();
            mockTool.throwInPrimary = true;

            var result = (bool)runToolInvocationMethod.Invoke(smokeTester, new object[] { "TestTool", mockTool });

            Assert.IsFalse(result);

            UnityEngine.Object.DestroyImmediate(mockToolGo);
        }

        [Test]
        public void RunToolInvocation_WhenSecondaryThrows_ReturnsFalse()
        {
            var mockToolGo = new GameObject("MockTool");
            var mockTool = mockToolGo.AddComponent<MockThrowingPlayerTool>();
            mockTool.throwInSecondary = true;

            var result = (bool)runToolInvocationMethod.Invoke(smokeTester, new object[] { "TestTool", mockTool });

            Assert.IsFalse(result);

            UnityEngine.Object.DestroyImmediate(mockToolGo);
        }

        [Test]
        public void RunToolInvocation_WhenNoExceptions_ReturnsTrue()
        {
            var mockToolGo = new GameObject("MockTool");
            var mockTool = mockToolGo.AddComponent<MockThrowingPlayerTool>();

            var result = (bool)runToolInvocationMethod.Invoke(smokeTester, new object[] { "TestTool", mockTool });

            Assert.IsTrue(result);

            UnityEngine.Object.DestroyImmediate(mockToolGo);
        }

        [Test]
        public void RunToolInvocation_WhenLiveToolIsNull_ReturnsFalse()
        {
            var result = (bool)runToolInvocationMethod.Invoke(smokeTester, new object[] { "TestTool", null });

            Assert.IsFalse(result);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void RunToolInvocation_WhenToolNameIsNullOrEmpty_HandlesGracefully(string toolName)
        {
            var mockToolGo = new GameObject("MockTool");
            var mockTool = mockToolGo.AddComponent<MockThrowingPlayerTool>();

            var result = (bool)runToolInvocationMethod.Invoke(smokeTester, new object[] { toolName, mockTool });

            Assert.IsTrue(result);

            UnityEngine.Object.DestroyImmediate(mockToolGo);
        }

        [Test]
        public void RunSmokePassAsync_WhenToolManagerOrInventoryMissing_SetsDebugLastIssue()
        {
            // TryRunImmediately starts the async method, which synchronously bails out if dependencies are missing.
            smokeTester.TryRunImmediately();

            Assert.AreEqual("Missing PlayerToolManager or PlayerInventory.", smokeTester.DebugLastIssue);
        }

        [Test]
        public void TestSingleToolAsync_WhenSetupThrows_ReturnsFalse()
        {
            // WaitForHolsterAsync runs before the setup try/catch and requires a live toolManager.
            // Null ToolData makes ResolvePersistentHashId return 0 → InvalidOperationException inside setup catch.
            var toolManagerGo = new GameObject("MockPlayerToolManager");
            var liveToolManager = toolManagerGo.AddComponent<PlayerToolManager>();
            var toolManagerField = typeof(ToolRuntimeSmokeTester).GetField(
                "toolManager",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(toolManagerField, "toolManager field not found");
            toolManagerField.SetValue(smokeTester, liveToolManager);

            var mockToolGo = new GameObject("MockTool");
            var mockTool = mockToolGo.AddComponent<MockThrowingPlayerTool>();
            // Intentionally leave PlayerTool._toolData null so setup throws on hash 0.

            var testMethod = typeof(ToolRuntimeSmokeTester).GetMethod(
                "TestSingleToolAsync",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(testMethod, "TestSingleToolAsync method not found");

            object awaitable = testMethod.Invoke(
                smokeTester,
                new object[] { mockToolGo, mockTool, CancellationToken.None });
            Assert.IsNotNull(awaitable, "TestSingleToolAsync returned null awaitable");

            MethodInfo getAwaiterMethod = awaitable.GetType().GetMethod("GetAwaiter");
            Assert.IsNotNull(getAwaiterMethod, "GetAwaiter not found on awaitable");
            object awaiter = getAwaiterMethod.Invoke(awaitable, null);
            Assert.IsNotNull(awaiter, "GetAwaiter returned null");

            MethodInfo getResultMethod = awaiter.GetType().GetMethod("GetResult");
            Assert.IsNotNull(getResultMethod, "GetResult not found on awaiter");
            object resultObj = getResultMethod.Invoke(awaiter, null);
            Assert.IsNotNull(resultObj, "GetResult returned null");
            bool result = (bool)resultObj;

            Assert.IsFalse(result, "Setup exception path must return false");
            Assert.AreEqual("Setup exception for MockTool", smokeTester.DebugLastIssue);
            Assert.IsFalse(smokeTester.DebugLastPass);
            Assert.AreEqual(1, smokeTester.DebugFailCount);

            UnityEngine.Object.DestroyImmediate(mockToolGo);
            UnityEngine.Object.DestroyImmediate(toolManagerGo);
        }


    }
}
#endif

