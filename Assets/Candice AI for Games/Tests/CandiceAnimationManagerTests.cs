using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CandiceAIforGames.AI;

namespace CandiceAIforGames.Tests
{
    public class CandiceAnimationManagerTests
    {
        private GameObject _agentGo;
        private CandiceAnimationManager _manager;

        [SetUp]
        public void Setup()
        {
            _agentGo = new GameObject("TestAgent");
            _manager = _agentGo.AddComponent<CandiceAnimationManager>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_agentGo != null)
            {
                UnityEngine.Object.DestroyImmediate(_agentGo);
            }
        }

        [Test]
        public void InputManager2Call_AlwaysReturnsFalse()
        {
            // Act
            bool result = _manager.InputManager2Call("AnyInput");

            // Assert
            Assert.That(result, Is.False, "InputManager2Call should currently return false as it is a stub for upcoming input system support.");
        }

        [UnityTest]
        public IEnumerator IveHitSomething_WithProjectileTag_SetsHitAndAttackDamage()
        {
            var colGo = new GameObject("ProjectileGo");
            bool tagSet = true;
            try { colGo.tag = "Projectile"; } catch (UnityException) { tagSet = false; }
            if (!tagSet)
            {
                Assert.Ignore("Tag 'Projectile' missing from TagManager. Skipping test.");
            }

            var projectile = colGo.AddComponent<CandiceProjectile>();
            projectile.attackDamage = 35.5f;

            var collider = colGo.AddComponent<BoxCollider>();
            var rb = colGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            var agentCollider = _agentGo.AddComponent<BoxCollider>();
            var agentRb = _agentGo.AddComponent<Rigidbody>();
            agentRb.useGravity = false;

            var collisionHelper = _agentGo.AddComponent<CollisionHelper>();
            collisionHelper.manager = _manager;

            // Place objects very close
            colGo.transform.position = new Vector3(1f, 0, 0);
            agentRb.velocity = new Vector3(10f, 0, 0);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(0.1f);

            Assert.That(collisionHelper.collisionOccurred, Is.True, "Collision did not occur");
            Assert.That(_manager.hit, Is.True);
            Assert.That(_manager.atkDamage, Is.EqualTo(35.5f));

            UnityEngine.Object.DestroyImmediate(colGo);
        }

        [UnityTest]
        public IEnumerator IveHitSomething_WithPlayerTag_SetsHit()
        {
            var colGo = new GameObject("PlayerGo");
            bool tagSet = true;
            try { colGo.tag = "Player"; } catch (UnityException) { tagSet = false; }
            if (!tagSet)
            {
                Assert.Ignore("Tag 'Player' missing from TagManager. Skipping test.");
            }

            var collider = colGo.AddComponent<BoxCollider>();
            var rb = colGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            var agentCollider = _agentGo.AddComponent<BoxCollider>();
            var agentRb = _agentGo.AddComponent<Rigidbody>();
            agentRb.useGravity = false;

            var collisionHelper = _agentGo.AddComponent<CollisionHelper>();
            collisionHelper.manager = _manager;

            // Place objects very close
            colGo.transform.position = new Vector3(1f, 0, 0);
            agentRb.velocity = new Vector3(10f, 0, 0);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(0.1f);

            Assert.That(collisionHelper.collisionOccurred, Is.True, "Collision did not occur");
            Assert.That(_manager.hit, Is.True);

            UnityEngine.Object.DestroyImmediate(colGo);
        }

        [UnityTest]
        public IEnumerator IveHitSomething_WithUntagged_DoesNotSetHit()
        {
            var colGo = new GameObject("UntaggedGo");
            try { colGo.tag = "Untagged"; } catch (UnityException) { }

            var collider = colGo.AddComponent<BoxCollider>();
            var rb = colGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            var agentCollider = _agentGo.AddComponent<BoxCollider>();
            var agentRb = _agentGo.AddComponent<Rigidbody>();
            agentRb.useGravity = false;

            var collisionHelper = _agentGo.AddComponent<CollisionHelper>();
            collisionHelper.manager = _manager;

            // Place objects very close
            colGo.transform.position = new Vector3(1f, 0, 0);
            agentRb.velocity = new Vector3(10f, 0, 0);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(0.1f);

            Assert.That(collisionHelper.collisionOccurred, Is.True, "Collision did not occur");
            Assert.That(_manager.hit, Is.False);

            UnityEngine.Object.DestroyImmediate(colGo);
        }

        public class CollisionHelper : MonoBehaviour
        {
            public CandiceAnimationManager manager;
            public bool collisionOccurred;

            void OnCollisionEnter(Collision collision)
            {
                collisionOccurred = true;
                manager.IveHitSomething(collision);
            }
        }
    }
}
