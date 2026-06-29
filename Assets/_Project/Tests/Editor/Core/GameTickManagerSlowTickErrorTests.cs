#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Tests.Core
{
    [TestFixture]
    public class GameTickManagerSlowTickErrorTests
    {
        private class ThrowingSlowTickable : ISlowTickable
        {
            public void SlowTick()
            {
                throw new System.Exception("Simulated exception during SlowTick");
            }
        }

        [Test]
        public void ExecuteSlowTick_WithThrowingItem_EnsuresEndIterationIsCalled()
        {
            // Set up GameTickManager
            var go = new GameObject();
            var manager = go.AddComponent<GameTickManager>();
            var ensureInitMethod = typeof(GameTickManager).GetMethod("EnsureInitialized", BindingFlags.NonPublic | BindingFlags.Instance);
            if (ensureInitMethod != null)
                ensureInitMethod.Invoke(manager, null);

            var throwingItem = new ThrowingSlowTickable();
            manager.Register(throwingItem);

            // Trigger SlowTick using reflection since ExecuteSlowTick is private
            var executeSlowTickMethod = typeof(GameTickManager).GetMethod("ExecuteSlowTick", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(executeSlowTickMethod, "Could not find ExecuteSlowTick method");

            // Execute it - expecting TargetInvocationException due to reflection unwrapping
            Assert.Throws<TargetInvocationException>(() =>
            {
                executeSlowTickMethod.Invoke(manager, null);
            });

            // Verify _slowTickables state - isIterating should be false
            var slowTickablesField = typeof(GameTickManager).GetField("_slowTickables", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(slowTickablesField, "Could not find _slowTickables field");
            var slowTickablesList = slowTickablesField.GetValue(manager);

            var isIteratingField = slowTickablesList.GetType().GetField("_isIterating", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(isIteratingField, "Could not find _isIterating field in TickList");
            bool isIterating = (bool)isIteratingField.GetValue(slowTickablesList);

            Assert.That(isIterating, Is.False, "_isIterating should be false after an exception in ExecuteSlowTick, because finally block should execute EndIteration().");

            Object.DestroyImmediate(go);
        }
    }
}
#endif
