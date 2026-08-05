using NUnit.Framework;
using UnityEngine;
using Hecton8.World;

namespace Hecton8.Tests.Editor
{
#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
    public sealed class WorldProceduralScatterDirectorEditTests
    {
        [Test]
        public void WorldProceduralScatterDirector_CanBeInstantiated()
        {
            var go = new GameObject("TestScatterDirector");
            var director = go.AddComponent<WorldProceduralScatterDirector>();
            Assert.IsNotNull(director);
            Object.DestroyImmediate(go);
        }
    }
#endif
}
