#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
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
    }
}
#endif
