#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Hecton8.World;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class WorldProceduralScatterDirectorEditTests
    {
        [Test]
        public void WorldProceduralScatterDirector_CanBeInstantiated()
        {
            GameObject go = new GameObject("TestDirector");
            var director = go.AddComponent<WorldProceduralScatterDirector>();
            Assert.IsNotNull(director, "WorldProceduralScatterDirector should be added successfully.");
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
#endif
