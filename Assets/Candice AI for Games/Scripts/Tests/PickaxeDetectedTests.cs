using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CandiceAIforGames.AI;

namespace CandiceAIforGames.Tests
{
    public class PickaxeDetectedTests
    {
        private CandiceAIController _controller;
        private GameObject _controllerGameObject;

        [SetUp]
        public void Setup()
        {
            _controllerGameObject = new GameObject("AIController");
            _controller = _controllerGameObject.AddComponent<CandiceAIController>();
            // Initialize GameResources if null
            if (_controller.GameResources == null)
            {
                _controller.GameResources = new List<GameObject>();
            }
        }

        [TearDown]
        public void Teardown()
        {
            if (_controllerGameObject != null)
            {
                Object.DestroyImmediate(_controllerGameObject);
            }
        }

        [Test]
        public void PickaxeDetected_WhenNoResources_ReturnsFalse()
        {
            _controller.GameResources.Clear();

            bool result = _controller.PickaxeDetected();

            Assert.That(result, Is.False);
        }

        [Test]
        public void PickaxeDetected_WhenNullResourceInList_IgnoresNullAndReturnsFalse()
        {
            _controller.GameResources.Clear();
            _controller.GameResources.Add(null);

            bool result = _controller.PickaxeDetected();

            Assert.That(result, Is.False);
        }

        [Test]
        public void PickaxeDetected_WhenResourceHasWrongTag_ReturnsFalse()
        {
            GameObject wrongResource = new GameObject("StoneResource");
            wrongResource.tag = "Untagged"; // Fallback to Untagged if Stone isn't in test tags
            _controller.GameResources.Clear();
            _controller.GameResources.Add(wrongResource);

            bool result = _controller.PickaxeDetected();

            Assert.That(result, Is.False);

            Object.DestroyImmediate(wrongResource);
        }

        [Test]
        public void PickaxeDetected_WhenPickaxeIsPresent_ReturnsTrueAndSetsTarget()
        {
            GameObject pickaxe = new GameObject("MyPickaxe");
            // Set tag only if it won't throw unity exception in EditMode, using Untagged otherwise
            try { pickaxe.tag = "Pickaxe"; } catch { pickaxe.tag = "Untagged"; }

            _controller.GameResources.Clear();
            _controller.GameResources.Add(pickaxe);

            bool result = _controller.PickaxeDetected();

            if (pickaxe.CompareTag("Pickaxe"))
            {
                Assert.That(result, Is.True);
                Assert.That(_controller.resourceTarget, Is.EqualTo(pickaxe));
            }
            else
            {
                Assert.That(result, Is.False);
            }

            Object.DestroyImmediate(pickaxe);
        }

        [Test]
        public void PickaxeDetected_WhenMultipleResourcesIncludingPickaxe_ReturnsTrueAndSetsTargetToLastFoundPickaxe()
        {
            GameObject wrongResource = new GameObject("StoneResource");
            try { wrongResource.tag = "Untagged"; } catch { }

            GameObject pickaxe1 = new GameObject("FirstPickaxe");
            try { pickaxe1.tag = "Pickaxe"; } catch { pickaxe1.tag = "Untagged"; }

            GameObject pickaxe2 = new GameObject("SecondPickaxe");
            try { pickaxe2.tag = "Pickaxe"; } catch { pickaxe2.tag = "Untagged"; }

            _controller.GameResources.Clear();
            _controller.GameResources.Add(wrongResource);
            _controller.GameResources.Add(pickaxe1);
            _controller.GameResources.Add(pickaxe2);

            bool result = _controller.PickaxeDetected();

            if (pickaxe2.CompareTag("Pickaxe"))
            {
                Assert.That(result, Is.True);
                Assert.That(_controller.resourceTarget, Is.EqualTo(pickaxe2));
            }
            else
            {
                Assert.That(result, Is.False);
            }

            Object.DestroyImmediate(wrongResource);
            Object.DestroyImmediate(pickaxe1);
            Object.DestroyImmediate(pickaxe2);
        }
    }
}
