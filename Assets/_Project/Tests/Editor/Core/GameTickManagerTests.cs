#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Tests.Core
{
    public class GameTickManagerTests
    {
        private class MockTickable : ITickable
        {
            public int TickCount { get; private set; }
            public float LastDeltaTime { get; private set; }

            public void Tick(float deltaTime)
            {
                TickCount++;
                LastDeltaTime = deltaTime;
            }
        }

        [Test]
        public void GameTickManager_Tick_ExecutesLoopAndCallsTickOnRegisteredItems()
        {
            // Arrange
            GameObject go = new GameObject("TickManager");
            GameTickManager tickManager = go.AddComponent<GameTickManager>();

            // Allow Awake/OnEnable logic to run manually using Reflection since we are in edit mode
            var ensureInitMethod = typeof(GameTickManager).GetMethod("EnsureInitialized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (ensureInitMethod != null)
            {
                ensureInitMethod.Invoke(tickManager, null);
            }

            MockTickable mockTickable = new MockTickable();

            // Register our mock into the tick loop buffer
            tickManager.Register(mockTickable);

            // Act
            float expectedDeltaTime = 0.05f;
            tickManager.Tick(expectedDeltaTime);

            // Assert
            Assert.AreEqual(1, mockTickable.TickCount, "Tick should have been called exactly once on the registered mock ITickable.");
            Assert.AreEqual(expectedDeltaTime, mockTickable.LastDeltaTime, "DeltaTime should match the expected value passed to GameTickManager.Tick.");

            // Cleanup
            Object.DestroyImmediate(go);
        }
    }
}
#endif
