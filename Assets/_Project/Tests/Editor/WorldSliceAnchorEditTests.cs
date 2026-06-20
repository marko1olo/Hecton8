#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using UnityEngine;
using Hecton8.World;
using System.Reflection;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class WorldSliceAnchorEditTests
    {
        [Test]
        public void ApplyForDistance_NegativeDistance_HandledAsAbsolute()
        {
            GameObject go = new GameObject("Anchor");
            WorldSliceAnchor anchor = go.AddComponent<WorldSliceAnchor>();

            // Set default distances that would usually put a point at -10 in "Near"
            // Default near is 20, mid is 40 via Awake->ClampSettings.

            // Force evaluate
            anchor.ApplyForDistance(-10f);
            Assert.AreEqual(WorldSliceAnchor.SliceState.Near, anchor.CurrentState, "Negative distance inside Near limit should map to Near.");

            anchor.ApplyForDistance(-30f);
            Assert.AreEqual(WorldSliceAnchor.SliceState.Mid, anchor.CurrentState, "Negative distance inside Mid limit should map to Mid.");

            anchor.ApplyForDistance(-100f);
            Assert.AreEqual(WorldSliceAnchor.SliceState.Far, anchor.CurrentState, "Negative large distance should map to Far.");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ApplyForDistance_ZeroDistance_HandledAsNear()
        {
            GameObject go = new GameObject("Anchor");
            WorldSliceAnchor anchor = go.AddComponent<WorldSliceAnchor>();

            anchor.ApplyForDistance(0f);
            Assert.AreEqual(WorldSliceAnchor.SliceState.Near, anchor.CurrentState, "Zero distance should securely map to Near.");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ApplyForDistance_ExtremelyLargeDistance_HandledAsFar()
        {
            GameObject go = new GameObject("Anchor");
            WorldSliceAnchor anchor = go.AddComponent<WorldSliceAnchor>();

            anchor.ApplyForDistance(1000000f);
            Assert.AreEqual(WorldSliceAnchor.SliceState.Far, anchor.CurrentState, "Very large distance should map to Far without float overflow issue.");

            Object.DestroyImmediate(go);
        }
    }
}
#endif
