using NUnit.Framework;
using UnityEngine;
using CandiceAIforGames.AI;
using CandiceAIforGames.AI.Pathfinding;
using System.Reflection;

namespace VendorCompatibility.Tests.Editor
{
    public class CandiceAIGizmosTest
    {
        private GameObject _controllerGo;
        private CandiceAIController _controller;
        private GameObject _candiceGo;
        private CandiceAIManager _candice;

        [SetUp]
        public void SetUp()
        {
            _controllerGo = new GameObject("Controller");
            _controller = _controllerGo.AddComponent<CandiceAIController>();

            _candiceGo = new GameObject("Candice");
            _candice = _candiceGo.AddComponent<CandiceAIManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_controllerGo != null)
                Object.DestroyImmediate(_controllerGo);
            if (_candiceGo != null)
                Object.DestroyImmediate(_candiceGo);
        }

        private void SetCandice(CandiceAIController controller, CandiceAIManager candiceManager)
        {
            var field = typeof(CandiceAIController).GetField("candice", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(controller, candiceManager);
            }
        }

        private void SetPath(CandiceAIController controller, Path path)
        {
            var pathField = typeof(CandiceAIController).GetField("_path", BindingFlags.NonPublic | BindingFlags.Instance);
            if (pathField != null)
            {
                pathField.SetValue(controller, path);
            }
        }

        [Test]
        public void TestOnDrawGizmos_CandiceNull_DoesNotThrow()
        {
            // By default candice is null, should early exit and not reach Gizmos API.
            Assert.DoesNotThrow(() => _controller.OnDrawGizmos());
        }

        [Test]
        public void TestOnDrawGizmos_DrawFlagsFalse_DoesNotThrow()
        {
            SetCandice(_controller, _candice);
            _candice.DrawAllAgentPaths = false;
            _controller.DrawAgentPath = false;

            var path = new Path(new Vector3[] { Vector3.zero, Vector3.one }, Vector3.zero, 1.0f);
            SetPath(_controller, path);

            // Flags are false, should early exit and not reach Gizmos API.
            Assert.DoesNotThrow(() => _controller.OnDrawGizmos());
        }

        [Test]
        public void TestOnDrawGizmos_DrawFlagsTrue_NoPath_DoesNotThrow()
        {
            SetCandice(_controller, _candice);
            _candice.DrawAllAgentPaths = true;
            _controller.DrawAgentPath = true;

            // Path is null by default. Logic hits flag check, but skips Gizmos API.
            Assert.DoesNotThrow(() => _controller.OnDrawGizmos());
        }

        [Test]
        public void TestOnDrawGizmos_DrawAllAgentPathsTrue_WithPath_ThrowsUnityException()
        {
            SetCandice(_controller, _candice);
            _candice.DrawAllAgentPaths = true;
            _controller.DrawAgentPath = false;

            var path = new Path(new Vector3[] { Vector3.zero, Vector3.one }, Vector3.zero, 1.0f);
            SetPath(_controller, path);

            // Reaches Gizmos API calls. Unity throws UnityException when Gizmos are drawn outside of proper callback context.
            Assert.Throws<UnityException>(() => _controller.OnDrawGizmos());
        }

        [Test]
        public void TestOnDrawGizmos_DrawAgentPathTrue_WithPath_ThrowsUnityException()
        {
            SetCandice(_controller, _candice);
            _candice.DrawAllAgentPaths = false;
            _controller.DrawAgentPath = true;

            var path = new Path(new Vector3[] { Vector3.zero, Vector3.one }, Vector3.zero, 1.0f);
            SetPath(_controller, path);

            // Reaches Gizmos API calls.
            Assert.Throws<UnityException>(() => _controller.OnDrawGizmos());
        }
    }
}
