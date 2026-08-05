using NUnit.Framework;
using UnityEngine;
using ScifiOffice;
using System.Reflection;

namespace ScifiOffice.Tests
{
    [TestFixture]
    public class DemoFirstPersonControllerTests
    {
        private GameObject _go;
        private DemoFirstPersonController _controller;
        private CapsuleCollider _collider;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject();
            _collider = _go.AddComponent<CapsuleCollider>();
            _controller = _go.AddComponent<DemoFirstPersonController>();

            _controller.playerBody = new GameObject().transform;
            _controller.canvas = new GameObject();

            // Use reflection to set the private capsule collider field
            var colField = typeof(DemoFirstPersonController).GetField("col", BindingFlags.NonPublic | BindingFlags.Instance);
            colField.SetValue(_controller, _collider);
        }

        [TearDown]
        public void Teardown()
        {
            if (_controller.playerBody != null)
                Object.DestroyImmediate(_controller.playerBody.gameObject);
            if (_controller.canvas != null)
                Object.DestroyImmediate(_controller.canvas);
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void MobileWalk_DirectionOne_SetsHorizontalMovementToOne()
        {
            _controller.MobileWalk(1);

            var hMovementField = typeof(DemoFirstPersonController).GetField("horizontalMovement", BindingFlags.NonPublic | BindingFlags.Instance);
            float hMovement = (float)hMovementField.GetValue(_controller);

            Assert.That(hMovement, Is.EqualTo(1f));
        }

        [Test]
        public void MobileWalk_DirectionMinusOne_SetsHorizontalMovementToMinusOne()
        {
            _controller.MobileWalk(-1);

            var hMovementField = typeof(DemoFirstPersonController).GetField("horizontalMovement", BindingFlags.NonPublic | BindingFlags.Instance);
            float hMovement = (float)hMovementField.GetValue(_controller);

            Assert.That(hMovement, Is.EqualTo(-1f));
        }

        [Test]
        public void MobileWalk_DirectionThree_ResetsMovements()
        {
            // Set initial values
            var hMovementField = typeof(DemoFirstPersonController).GetField("horizontalMovement", BindingFlags.NonPublic | BindingFlags.Instance);
            var vMovementField = typeof(DemoFirstPersonController).GetField("verticalMovement", BindingFlags.NonPublic | BindingFlags.Instance);
            hMovementField.SetValue(_controller, 5f);
            vMovementField.SetValue(_controller, 5f);

            _controller.MobileWalk(3);

            float hMovement = (float)hMovementField.GetValue(_controller);
            float vMovement = (float)vMovementField.GetValue(_controller);

            Assert.That(hMovement, Is.EqualTo(0f));
            Assert.That(vMovement, Is.EqualTo(0f));
        }

        [Test]
        public void MobileWalk_DirectionTwo_SetsVerticalMovementToOne()
        {
            _controller.MobileWalk(2);

            var vMovementField = typeof(DemoFirstPersonController).GetField("verticalMovement", BindingFlags.NonPublic | BindingFlags.Instance);
            float vMovement = (float)vMovementField.GetValue(_controller);

            Assert.That(vMovement, Is.EqualTo(1f));
        }

        [Test]
        public void MobileWalk_DirectionZero_SetsVerticalMovementToMinusOne()
        {
            _controller.MobileWalk(0);

            var vMovementField = typeof(DemoFirstPersonController).GetField("verticalMovement", BindingFlags.NonPublic | BindingFlags.Instance);
            float vMovement = (float)vMovementField.GetValue(_controller);

            Assert.That(vMovement, Is.EqualTo(-1f));
        }

        [Test]
        public void MobileCrouch_TogglesCrouchStateAndColliderHeight()
        {
            // Initial state (assuming not crouching)
            var isCrouchingField = typeof(DemoFirstPersonController).GetField("isCrouching", BindingFlags.NonPublic | BindingFlags.Instance);
            isCrouchingField.SetValue(_controller, false);
            _collider.height = 2f;

            // First toggle (to crouching)
            _controller.MobileCrouch();

            bool isCrouching = (bool)isCrouchingField.GetValue(_controller);
            Assert.That(isCrouching, Is.True);
            Assert.That(_collider.height, Is.EqualTo(0.5f));

            // Second toggle (to standing)
            _controller.MobileCrouch();

            isCrouching = (bool)isCrouchingField.GetValue(_controller);
            Assert.That(isCrouching, Is.False);
            Assert.That(_collider.height, Is.EqualTo(2f));
        }
    }
}
