using NUnit.Framework;

namespace Hecton8.SaveSystem.Tests
{
    /// <summary>
    /// Locks the required-owner census math that <see cref="SaveManager"/> runs immediately before it
    /// populates a save payload and immediately before it applies a loaded payload.
    ///
    /// Regression guarded: <c>GameBootstrapper.ExecuteSceneReadinessGatesAsync</c> calls
    /// <c>DisablePlayer()</c> - which <c>SetActive(false)</c>s the player - four scene-activation steps
    /// BEFORE "Step 4: Save/Load", and only calls <c>ActivatePlayer()</c> afterwards. Every player-owned
    /// <c>ISaveable</c> has therefore fired <c>OnDisable -> Unregister</c> by the time the load apply loop
    /// runs. The loop over the remaining registry visits no player owner, emits no log line at all when
    /// the registry is empty, and the load still reaches <c>RecordSuccessfulLoad</c>. Position, inventory,
    /// route state, opened/looted/scanned flags and hazard state were applied to nothing and the load
    /// reported success.
    ///
    /// These assertions prove the census produces a non-zero, decodable verdict for exactly that state,
    /// and that the deferred re-apply mask can never hand the payload twice to an owner that already
    /// consumed it.
    /// </summary>
    [TestFixture]
    public sealed class SaveOwnerCensusTests
    {
        [Test]
        public void RequiredMaskCoversTheFiveFirst20SaveLoadCategories()
        {
            Assert.AreEqual(5, SaveOwnerCensus.RequiredCategoryCount);
            Assert.AreEqual(0b11111u, SaveOwnerCensus.RequiredCategoryMask);
            Assert.AreEqual(5, SaveOwnerCensus.CountCategories(SaveOwnerCensus.RequiredCategoryMask));
        }

        [Test]
        public void EmptyRegistryIsACensusFailureAndReportsEveryCategoryMissing()
        {
            uint missing = SaveOwnerCensus.ResolveMissingCategories(0u);

            Assert.AreEqual(SaveOwnerCensus.RequiredCategoryMask, missing);
            Assert.AreEqual(5, SaveOwnerCensus.CountCategories(missing));
            Assert.IsFalse(SaveOwnerCensus.IsCensusSatisfied(0u, 0));
            Assert.AreEqual(0f, SaveOwnerCensus.ResolveCensusCoverage01(0u), 1e-6f);
        }

        [Test]
        public void PlayerDisabledDuringBootstrapLeavesFourOfFiveCategoriesUnowned()
        {
            // The world-side owners (WorldStateManager / HectonDiscoveryManager / ScanLogSystem) live on
            // scene roots that DisablePlayer() does not touch; every other required category is
            // player-owned and has unregistered.
            const uint presentAfterDisablePlayer = SaveOwnerCensus.CategoryWorldObjectFlags;

            uint missing = SaveOwnerCensus.ResolveMissingCategories(presentAfterDisablePlayer);

            Assert.AreEqual(
                SaveOwnerCensus.CategoryPlayerPosition |
                SaveOwnerCensus.CategoryInventory |
                SaveOwnerCensus.CategoryRouteState |
                SaveOwnerCensus.CategoryHazardState,
                missing);
            Assert.AreEqual(23u, missing);
            Assert.AreEqual(4, SaveOwnerCensus.CountCategories(missing));
            Assert.IsFalse(SaveOwnerCensus.IsCensusSatisfied(presentAfterDisablePlayer, 37));
            Assert.AreEqual(0.2f, SaveOwnerCensus.ResolveCensusCoverage01(presentAfterDisablePlayer), 1e-6f);
        }

        [Test]
        public void ANonEmptyRegistryWithNoRequiredOwnerStillFailsTheCensus()
        {
            // The registry has 40 live registrants and the apply loop runs 40 times, which is exactly why
            // the old code looked healthy: none of those 40 owns a contract category.
            Assert.IsFalse(SaveOwnerCensus.IsCensusSatisfied(0u, 40));
            Assert.AreEqual(SaveOwnerCensus.RequiredCategoryMask, SaveOwnerCensus.ResolveMissingCategories(0u));
        }

        [Test]
        public void FullOwnerSetSatisfiesTheCensus()
        {
            Assert.AreEqual(0u, SaveOwnerCensus.ResolveMissingCategories(SaveOwnerCensus.RequiredCategoryMask));
            Assert.IsTrue(SaveOwnerCensus.IsCensusSatisfied(SaveOwnerCensus.RequiredCategoryMask, 1));
            Assert.AreEqual(1f, SaveOwnerCensus.ResolveCensusCoverage01(SaveOwnerCensus.RequiredCategoryMask), 1e-6f);
        }

        [Test]
        public void ACompleteCategorySetWithAnEmptyRegistryStillFails()
        {
            // Guards the ordering of the two conditions: a stale category mask must not outvote a
            // registry that currently holds nothing.
            Assert.IsFalse(SaveOwnerCensus.IsCensusSatisfied(SaveOwnerCensus.RequiredCategoryMask, 0));
        }

        [Test]
        public void CategoryBitsOutsideTheContractAreIgnoredNotFaultedOn()
        {
            const uint futureSixthCategory = 1u << 9;

            Assert.AreEqual(
                0u,
                SaveOwnerCensus.ResolveMissingCategories(SaveOwnerCensus.RequiredCategoryMask | futureSixthCategory));
            Assert.AreEqual(0, SaveOwnerCensus.CountCategories(futureSixthCategory));
            Assert.AreEqual(0u, SaveOwnerCensus.ResolveSatisfiedCategories(futureSixthCategory, futureSixthCategory));
        }

        [Test]
        public void DeferredHydrationOnlyReAppliesOwnersWhoseCategoryIsStillOutstanding()
        {
            const uint outstanding =
                SaveOwnerCensus.CategoryPlayerPosition |
                SaveOwnerCensus.CategoryInventory |
                SaveOwnerCensus.CategoryRouteState |
                SaveOwnerCensus.CategoryHazardState;

            // The world-flag owner already consumed the payload in the load apply loop: re-applying it
            // would overwrite gameplay that has run since the load.
            Assert.AreEqual(
                0u,
                SaveOwnerCensus.ResolveSatisfiedCategories(outstanding, SaveOwnerCensus.CategoryWorldObjectFlags));

            // The survival owner re-registers when ActivatePlayer() runs and closes exactly one category.
            uint satisfied = SaveOwnerCensus.ResolveSatisfiedCategories(
                outstanding,
                SaveOwnerCensus.CategoryPlayerPosition);
            Assert.AreEqual(SaveOwnerCensus.CategoryPlayerPosition, satisfied);

            uint afterSurvival = SaveOwnerCensus.ClearSatisfiedCategories(outstanding, satisfied);
            Assert.AreEqual(22u, afterSurvival);
            Assert.AreEqual(3, SaveOwnerCensus.CountCategories(afterSurvival));

            // Registering the same owner a second time closes nothing further and cannot loop.
            Assert.AreEqual(
                0u,
                SaveOwnerCensus.ResolveSatisfiedCategories(afterSurvival, SaveOwnerCensus.CategoryPlayerPosition));
            Assert.AreEqual(
                afterSurvival,
                SaveOwnerCensus.ClearSatisfiedCategories(afterSurvival, SaveOwnerCensus.CategoryPlayerPosition));
        }

        [Test]
        public void DrainingEveryOutstandingCategoryEmptiesTheDeferredMask()
        {
            uint outstanding = SaveOwnerCensus.RequiredCategoryMask;
            int applied = 0;

            for (int i = 0; i < SaveOwnerCensus.RequiredCategoryCount; i++)
            {
                uint ownerCategory = SaveOwnerCensus.ResolveCategoryAtIndex(i);
                uint satisfied = SaveOwnerCensus.ResolveSatisfiedCategories(outstanding, ownerCategory);
                if (satisfied == 0u)
                    continue;

                outstanding = SaveOwnerCensus.ClearSatisfiedCategories(outstanding, satisfied);
                applied++;
            }

            Assert.AreEqual(5, applied);
            Assert.AreEqual(0u, outstanding);
        }

        [Test]
        public void CategoryIndexOrderIsTheContractOrderAndBoundsAreTotal()
        {
            Assert.AreEqual(SaveOwnerCensus.CategoryPlayerPosition, SaveOwnerCensus.ResolveCategoryAtIndex(0));
            Assert.AreEqual(SaveOwnerCensus.CategoryInventory, SaveOwnerCensus.ResolveCategoryAtIndex(1));
            Assert.AreEqual(SaveOwnerCensus.CategoryRouteState, SaveOwnerCensus.ResolveCategoryAtIndex(2));
            Assert.AreEqual(SaveOwnerCensus.CategoryWorldObjectFlags, SaveOwnerCensus.ResolveCategoryAtIndex(3));
            Assert.AreEqual(SaveOwnerCensus.CategoryHazardState, SaveOwnerCensus.ResolveCategoryAtIndex(4));
            Assert.AreEqual(0u, SaveOwnerCensus.ResolveCategoryAtIndex(-1));
            Assert.AreEqual(0u, SaveOwnerCensus.ResolveCategoryAtIndex(SaveOwnerCensus.RequiredCategoryCount));
        }

        [Test]
        public void CensusContextHashIsDeterministicNonZeroAndSeparatesDistinctVerdicts()
        {
            const uint seed = 0x534F434Cu;
            const uint slot = 0x0000AB01u;

            uint first = SaveOwnerCensus.ComputeCensusContextHash(seed, slot, 23u, 37);
            uint second = SaveOwnerCensus.ComputeCensusContextHash(seed, slot, 23u, 37);
            uint otherMask = SaveOwnerCensus.ComputeCensusContextHash(seed, slot, 31u, 37);
            uint otherOwners = SaveOwnerCensus.ComputeCensusContextHash(seed, slot, 23u, 0);

            Assert.AreEqual(first, second);
            Assert.AreNotEqual(0u, first);
            Assert.AreNotEqual(first, otherMask);
            Assert.AreNotEqual(first, otherOwners);

            // A negative owner count is clamped rather than wrapped to a huge uint.
            Assert.AreEqual(otherOwners, SaveOwnerCensus.ComputeCensusContextHash(seed, slot, 23u, -4));
        }

        [Test]
        public void DeferredWindowUsesAnAbsoluteClockNotATickCount()
        {
            Assert.IsFalse(SaveOwnerCensus.IsDeferredHydrationExpired(10.0d, 40.0d));
            Assert.IsTrue(SaveOwnerCensus.IsDeferredHydrationExpired(40.0d, 40.0d));
            Assert.IsTrue(SaveOwnerCensus.IsDeferredHydrationExpired(41.5d, 40.0d));

            // A broken clock releases the retained payload instead of pinning it for the session.
            Assert.IsTrue(SaveOwnerCensus.IsDeferredHydrationExpired(double.NaN, 40.0d));
            Assert.IsTrue(SaveOwnerCensus.IsDeferredHydrationExpired(10.0d, double.NaN));
        }

        [Test]
        public void EveryCategoryHasAStableDistinctLabel()
        {
            Assert.AreEqual("position", SaveOwnerCensus.DescribeCategory(SaveOwnerCensus.CategoryPlayerPosition));
            Assert.AreEqual("inventory", SaveOwnerCensus.DescribeCategory(SaveOwnerCensus.CategoryInventory));
            Assert.AreEqual("route-state", SaveOwnerCensus.DescribeCategory(SaveOwnerCensus.CategoryRouteState));
            Assert.AreEqual(
                "opened-looted-scanned-flags",
                SaveOwnerCensus.DescribeCategory(SaveOwnerCensus.CategoryWorldObjectFlags));
            Assert.AreEqual("hazard-state", SaveOwnerCensus.DescribeCategory(SaveOwnerCensus.CategoryHazardState));
            Assert.AreEqual("unknown-category", SaveOwnerCensus.DescribeCategory(1u << 9));
        }
    }
}
