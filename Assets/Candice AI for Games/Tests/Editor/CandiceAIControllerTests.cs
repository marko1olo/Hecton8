using NUnit.Framework;
using UnityEngine;
using CandiceAIforGames.AI;

namespace CandiceAIforGames.AI.Tests
{
    public class CandiceAIControllerTests
    {
        [Test]
        public void GetPickaxe_DoesNotThrow()
        {
            var go = new GameObject("CandiceAIControllerTest");
            var controller = go.AddComponent<CandiceAIController>();

            Assert.DoesNotThrow(() => controller.GetPickaxe());

            Object.DestroyImmediate(go);
        }
    }
}
