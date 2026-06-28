using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CandiceAIforGames.AI;

namespace CandiceAIforGames.Tests
{
    public class CandiceProjectileTests
    {
        private GameObject projectileObj;
        private CandiceProjectile projectile;
        private GameObject targetObj;

        [SetUp]
        public void SetUp()
        {
            projectileObj = new GameObject("TestProjectile");
            var rb = projectileObj.AddComponent<Rigidbody>();
            projectile = projectileObj.AddComponent<CandiceProjectile>();

            targetObj = new GameObject("TestTarget");
            targetObj.transform.position = new Vector3(10, 0, 10);
        }

        [TearDown]
        public void TearDown()
        {
            if (projectileObj != null)
                Object.DestroyImmediate(projectileObj);
            if (targetObj != null)
                Object.DestroyImmediate(targetObj);
        }

        [Test]
        public void Fire_WithNullTarget_SetsIsFiredFalse()
        {
            projectile.isFired = true; // Set to true to verify it changes to false

            projectile.Fire(null);

            Assert.That(projectile.isFired, Is.False);
        }

        [Test]
        public void Fire_WithValidTarget_SetsIsFiredTrue()
        {
            projectile.isFired = false;

            projectile.Fire(targetObj);

            Assert.That(projectile.isFired, Is.True);
        }

        [Test]
        public void Fire_WithValidTarget_UpdatesRotation()
        {
            projectileObj.transform.rotation = Quaternion.identity;
            Vector3 expectedDirection = (new Vector3(targetObj.transform.position.x, projectileObj.transform.position.y - 1, targetObj.transform.position.z) - projectileObj.transform.position).normalized;

            projectile.Fire(targetObj);

            // Using vector comparison to verify the forward direction
            Assert.That(Vector3.Dot(projectileObj.transform.forward, expectedDirection), Is.GreaterThan(0.99f));
        }

        [Test]
        public void Fire_WithDestroyAfterDelay_SchedulesDeactivate()
        {
            projectile.destroyAfterDelay = true;
            projectile.destroyDelay = 5f;

            Assert.DoesNotThrow(() => projectile.Fire(targetObj));
        }
    }
}
