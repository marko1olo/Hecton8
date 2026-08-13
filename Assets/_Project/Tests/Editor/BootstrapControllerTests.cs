using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Tests.Editor
{
    public class BootstrapControllerTests
    {
        [Test]
        public void BootstrapController_CanBeInstantiated()
        {
            GameObject go = new GameObject("BootstrapControllerTest");
            var controller = go.AddComponent<BootstrapController>();

            Assert.IsNotNull(controller);

            Object.DestroyImmediate(go);
        }
    }
}
