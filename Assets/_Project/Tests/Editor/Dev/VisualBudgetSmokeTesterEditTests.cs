#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using UnityEngine;
using Hecton8.Dev;
using System.Reflection;

namespace Hecton8.Tests.Editor
{
    public class VisualBudgetSmokeTesterEditTests
    {
        private VisualBudgetSmokeTester tester;

        [SetUp]
        public void Setup()
        {
            var go = new GameObject("Tester");
            tester = go.AddComponent<VisualBudgetSmokeTester>();
        }

        [TearDown]
        public void TearDown()
        {
            if (tester != null)
            {
                Object.DestroyImmediate(tester.gameObject);
            }
        }

        [Test]
        public void RunSmokePass_WithoutSceneDependencies_ReturnsTrue()
        {
            // Execute the pass
            bool result = tester.RunSmokePass();

            // Should pass because all allocations are 0 in isolated state
            Assert.IsTrue(result, "RunSmokePass failed with baseline zero values");

            // Check debug fields via reflection
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var lastPass = (bool)typeof(VisualBudgetSmokeTester).GetField("_debugLastPass", flags).GetValue(tester);
            Assert.IsTrue(lastPass, "_debugLastPass should be true");
        }
    }
}
#endif
