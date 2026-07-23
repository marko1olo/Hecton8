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
        [Test]
        public void CopyActiveAnchorsTo_NullDestination_ReturnsZero()
        {
            Assert.AreEqual(0, WorldSliceAnchor.CopyActiveAnchorsTo(null), "Should return 0 for null destination array.");
        }

        [Test]
        public void CopyActiveAnchorsTo_EmptyDestination_ReturnsZero()
        {
            WorldSliceAnchor[] empty = new WorldSliceAnchor[0];
            Assert.AreEqual(0, WorldSliceAnchor.CopyActiveAnchorsTo(empty), "Should return 0 for empty destination array.");
        }

        [Test]
        public void CopyActiveAnchorsTo_ValidDestination_CopiesAnchorsAndClearsRest()
        {
            // Reset static state for clean test isolation
            MethodInfo resetMethod = typeof(WorldSliceAnchor).GetMethod("ResetStaticState", BindingFlags.NonPublic | BindingFlags.Static);
            resetMethod?.Invoke(null, null);

            GameObject go1 = new GameObject("Anchor1");
            WorldSliceAnchor anchor1 = go1.AddComponent<WorldSliceAnchor>();

            GameObject go2 = new GameObject("Anchor2");
            WorldSliceAnchor anchor2 = go2.AddComponent<WorldSliceAnchor>();

            WorldSliceAnchor[] dest = new WorldSliceAnchor[5];

            // Fill array with garbage to ensure the rest of the array is cleared
            GameObject dummyGo = new GameObject("Dummy");
            WorldSliceAnchor dummy = dummyGo.AddComponent<WorldSliceAnchor>();
            for (int i = 0; i < dest.Length; i++)
            {
                dest[i] = dummy;
            }

            int count = WorldSliceAnchor.CopyActiveAnchorsTo(dest);

            // AddComponent automatically registers the anchor since OnEnable is called.
            // 3 total: go1, go2, and dummyGo
            Assert.AreEqual(3, count, "Should have copied exactly 3 anchors.");

            // The first 3 should be valid anchors
            Assert.IsNotNull(dest[0], "Copied anchor should not be null.");
            Assert.IsNotNull(dest[1], "Copied anchor should not be null.");
            Assert.IsNotNull(dest[2], "Copied anchor should not be null.");

            // The rest of the array should be cleared to null
            Assert.IsNull(dest[3], "Remaining array elements should be cleared to null.");
            Assert.IsNull(dest[4], "Remaining array elements should be cleared to null.");

            // Cleanup
            Object.DestroyImmediate(go1);
            Object.DestroyImmediate(go2);
            Object.DestroyImmediate(dummyGo);
        }
        [Test]
        public void CopyActiveAnchorsTo_DestinationSmallerThanActiveCount_CopiesUpToCapacity()
        {
            // Reset static state for clean test isolation
            MethodInfo resetMethod = typeof(WorldSliceAnchor).GetMethod("ResetStaticState", BindingFlags.NonPublic | BindingFlags.Static);
            resetMethod?.Invoke(null, null);

            GameObject go1 = new GameObject("Anchor1");
            WorldSliceAnchor anchor1 = go1.AddComponent<WorldSliceAnchor>();

            GameObject go2 = new GameObject("Anchor2");
            WorldSliceAnchor anchor2 = go2.AddComponent<WorldSliceAnchor>();

            GameObject go3 = new GameObject("Anchor3");
            WorldSliceAnchor anchor3 = go3.AddComponent<WorldSliceAnchor>();

            WorldSliceAnchor[] dest = new WorldSliceAnchor[2];

            int count = WorldSliceAnchor.CopyActiveAnchorsTo(dest);

            // AddComponent automatically registers the anchor since OnEnable is called.
            // But we only have capacity for 2.
            Assert.AreEqual(2, count, "Should have only copied 2 anchors due to destination capacity.");

            Assert.IsNotNull(dest[0], "Copied anchor should not be null.");
            Assert.IsNotNull(dest[1], "Copied anchor should not be null.");

            // Cleanup
            Object.DestroyImmediate(go1);
            Object.DestroyImmediate(go2);
            Object.DestroyImmediate(go3);
        }
        [Test]
        public void CopyActiveAnchorsTo_WithNullAnchorInList_SkipsAndContinues()
        {
            // Reset static state for clean test isolation
            MethodInfo resetMethod = typeof(WorldSliceAnchor).GetMethod("ResetStaticState", BindingFlags.NonPublic | BindingFlags.Static);
            resetMethod?.Invoke(null, null);

            GameObject go1 = new GameObject("Anchor1");
            WorldSliceAnchor anchor1 = go1.AddComponent<WorldSliceAnchor>();

            GameObject go2 = new GameObject("Anchor2");
            WorldSliceAnchor anchor2 = go2.AddComponent<WorldSliceAnchor>();

            GameObject go3 = new GameObject("Anchor3");
            WorldSliceAnchor anchor3 = go3.AddComponent<WorldSliceAnchor>();

            // Force a null into the active anchors array to test the 'if (anchor == null) continue;' branch.
            // This simulates an edge case of an empty/null input internally.
            FieldInfo activeAnchorsField = typeof(WorldSliceAnchor).GetField("_ActiveAnchors", BindingFlags.NonPublic | BindingFlags.Static);
            WorldSliceAnchor[] activeAnchors = (WorldSliceAnchor[])activeAnchorsField.GetValue(null);
            activeAnchors[1] = null;

            WorldSliceAnchor[] dest = new WorldSliceAnchor[5];
            int count = WorldSliceAnchor.CopyActiveAnchorsTo(dest);

            Assert.AreEqual(2, count, "Should have skipped the null anchor and copied the other 2.");
            Assert.AreEqual(anchor1, dest[0], "First valid anchor should be copied.");
            Assert.AreEqual(anchor3, dest[1], "Second valid anchor should be copied.");

            // Cleanup
            Object.DestroyImmediate(go1);
            Object.DestroyImmediate(go2);
            Object.DestroyImmediate(go3);
        }


    }
}
#endif
