using NUnit.Framework;
using Hecton8.Core;
using Hecton8.Interaction;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Pins the mask contract the player interaction probe depends on.
    ///
    /// Regression guarded: <c>PlayerInteraction.ResolveInteractableLayerMask</c> resolved
    /// Everything (-1) to the Interactable route default but returned Nothing (0) unchanged.
    /// Nothing is the serialized default of the <c>interactableMask</c> field, so an
    /// unconfigured Player prefab handed 0 to <c>InteractableRegistry.TryResolveSpatialTarget</c>,
    /// whose <c>LayerIncluded</c> filter is <c>(layerMask &amp; (1 &lt;&lt; layer)) != 0</c>.
    /// Every registered collider was rejected, no target was ever resolved, no hover fired,
    /// and the entire aim -&gt; query -&gt; activation chain was dead with only an
    /// editor/DEVELOPMENT_BUILD warning. <c>PhysicalInteractionHandler.RefreshPanelButtonLayerMask</c>
    /// had the identical hole against <c>PhysicalHandReceiverRegistry.QuerySphere</c>.
    /// </summary>
    [TestFixture]
    public class InteractionProbeLayerMaskResolverTests
    {
        // Shipped route defaults at the two call sites.
        private static readonly int PlayerInteractionRouteDefault = HectonLayerMasks.InteractableLayerMask;

        private static readonly int PanelButtonRouteDefault =
            HectonLayerMasks.UILayerMask | HectonLayerMasks.InteractableLayerMask;

        /// <summary>
        /// Exact filter expression used by InteractableRegistry.LayerIncluded and by
        /// PhysicalHandReceiverRegistry.QuerySphere. Replicated so the test proves the
        /// resolved mask is actually consumable by the hop below it.
        /// </summary>
        private static bool LayerIncluded(int layer, int layerMask)
        {
            if (layer < 0 || layer >= 32)
                return false;

            if (layerMask == HectonLayerMasks.EverythingLayerMaskValue)
                return true;

            return (layerMask & (1 << layer)) != 0;
        }

        [Test]
        public void OldExpression_LeftNothingIntact_AndNothingRejectsEveryLayer()
        {
            // The pre-fix expression, reproduced verbatim.
            int serializedMask = 0;
            int oldResult = HectonLayerMasks.IsEverythingLayerMask(serializedMask)
                ? PlayerInteractionRouteDefault
                : serializedMask;

            Assert.AreEqual(0, oldResult, "The old expression only rewrote Everything, never Nothing.");

            for (int layer = 0; layer < 32; layer++)
            {
                Assert.IsFalse(
                    LayerIncluded(layer, oldResult),
                    "A mask of 0 rejects every layer, which is why the probe resolved no target on any layer.");
            }
        }

        [Test]
        public void Nothing_ResolvesToRouteDefault_AndThatDefaultAcceptsTheInteractableLayer()
        {
            int resolved = InteractionProbeLayerMask.Resolve(0, PlayerInteractionRouteDefault);

            Assert.AreEqual(PlayerInteractionRouteDefault, resolved);
            Assert.IsTrue(
                LayerIncluded(HectonLayerMasks.Interactable, resolved),
                "The recovered mask must actually pass the registry filter for the Interactable layer.");
        }

        [Test]
        public void Everything_StillResolvesToRouteDefault()
        {
            int resolved = InteractionProbeLayerMask.Resolve(
                HectonLayerMasks.EverythingLayerMaskValue,
                PlayerInteractionRouteDefault);

            Assert.AreEqual(PlayerInteractionRouteDefault, resolved,
                "Everything is forbidden authored data and must not reach a physics-style filter.");
        }

        [Test]
        public void ConfiguredMask_IsPassedThroughUnchanged()
        {
            int authored = HectonLayerMasks.InteractableLayerMask | HectonLayerMasks.DroppedItemLayerMask;

            Assert.AreEqual(authored, InteractionProbeLayerMask.Resolve(authored, PlayerInteractionRouteDefault),
                "A designer-authored mask is authority and must survive the resolver untouched.");
            Assert.IsTrue(LayerIncluded(HectonLayerMasks.DroppedItem, authored));
        }

        [Test]
        public void MaskSelectingOnlyUnassignedLayers_ResolvesToRouteDefault()
        {
            // TagManager assigns layers 0..22 in this project; bit 30 can never match a collider.
            const int unassignedOnly = 1 << 30;

            Assert.AreEqual(0, unassignedOnly & HectonLayerMasks.AllDefinedProjectLayersMask,
                "Bit 30 must be outside the authored project layer set for this case to be meaningful.");
            Assert.AreEqual(
                PlayerInteractionRouteDefault,
                InteractionProbeLayerMask.Resolve(unassignedOnly, PlayerInteractionRouteDefault),
                "A positive mask that matches nothing is the same dead query as Nothing.");
        }

        [Test]
        public void PanelButtonRouteDefault_IsRecoveredAndCoversBothAuthoredLayers()
        {
            int resolved = InteractionProbeLayerMask.Resolve(0, PanelButtonRouteDefault);

            Assert.AreEqual(PanelButtonRouteDefault, resolved);
            Assert.IsTrue(LayerIncluded(HectonLayerMasks.UI, resolved));
            Assert.IsTrue(LayerIncluded(HectonLayerMasks.Interactable, resolved));
        }

        [Test]
        public void UnusableRouteDefault_FallsBackToTheMinimumProbeMask()
        {
            Assert.AreEqual(
                InteractionProbeLayerMask.MinimumProbeLayerMask,
                InteractionProbeLayerMask.Resolve(0, 0));
            Assert.AreEqual(
                InteractionProbeLayerMask.MinimumProbeLayerMask,
                InteractionProbeLayerMask.Resolve(0, HectonLayerMasks.EverythingLayerMaskValue));
            Assert.AreEqual(HectonLayerMasks.InteractableLayerMask, InteractionProbeLayerMask.MinimumProbeLayerMask);
        }

        [Test]
        public void ResolveNeverReturnsAMaskThatMatchesNothing()
        {
            int[] hostileInputs =
            {
                0,
                -1,
                int.MinValue,
                1 << 23,
                1 << 30,
                unchecked((int)0xFF800000u)
            };

            foreach (int serializedMask in hostileInputs)
            {
                int resolved = InteractionProbeLayerMask.Resolve(serializedMask, PlayerInteractionRouteDefault);

                Assert.IsTrue(
                    InteractionProbeLayerMask.IsUsableProbeMask(resolved),
                    "Resolve must always hand the probe a mask that can match at least one authored layer.");
                Assert.AreNotEqual(HectonLayerMasks.EverythingLayerMaskValue, resolved,
                    "Resolve must never launder a bad input into Everything.");
            }
        }
    }
}
