using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class RuntimePlayerAupResolverGuardEditTests
    {
        [Test]
        public void RuntimePlayerAupResolvers_FailClosedForNonFiniteMovementAups()
        {
            AssertPlayerAupAssignmentFailsClosed(
                "_Project/Scripts/AcousticZoneController.cs",
                "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = movement.CurrentAup;");
            AssertPlayerAupAssignmentFailsClosed(
                "_Project/Scripts/Fabricator.cs",
                "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = _playerMovement.CurrentAup;");
            AssertPlayerAupAssignmentFailsClosed(
                "_Project/Scripts/Fabricator.cs",
                "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = cachedMovement.CurrentAup;");
            AssertPlayerAupAssignmentFailsClosed(
                "_Project/Scripts/WorldInterestDirector.cs",
                "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = _playerMovement.CurrentAup;");
            AssertPlayerAupAssignmentFailsClosed(
                "_Project/Scripts/Gameplay/PlayerNoiseEmitter.cs",
                "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = _playerMovement.CurrentAup;");
            AssertPlayerAupAssignmentFailsClosed(
                "_Project/Scripts/Gameplay/PlayerNoiseEmitter.cs",
                "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = movementState.PredictedAup;");
            AssertPlayerAupAssignmentFailsClosed(
                "_Project/Scripts/Quest/MissionMarkerSystem.cs",
                "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = _playerMovement.CurrentAup;");
            AssertPlayerAupAssignmentFailsClosed(
                "_Project/Scripts/Progression/PlayerAchievementRegistry.cs",
                "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = _playerMovement.CurrentAup;");
            AssertPlayerAupAssignmentFailsClosed(
                "_Project/Scripts/Progression/NarrativeProgressionBridge.cs",
                "private static bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = playerContext.PlayerMovement.CurrentAup;");
            AssertPlayerAupAssignmentFailsClosed(
                "_Project/Scripts/Visor/SpectrumSystem.cs",
                "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = _playerMovement.CurrentAup;");
            AssertPlayerAupAssignmentFailsClosed(
                "_Project/Scripts/Visor/SpectrumSystem.cs",
                "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = movementState.PredictedAup;");
            AssertPlayerAupAssignmentFailsClosed(
                "_Project/Scripts/Visor/SpectrumSystem.cs",
                "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = movement.CurrentAup;");
        }

        [Test]
        public void RuntimePlayerAupResolvers_FailClosedInWorldAndPhysicsSystems()
        {
            AssertAupAssignmentFailsClosed(
                "_Project/Scripts/GlobalPhysicsStateManager.cs",
                "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = movementState.PredictedAup;",
                "return IsFinite(in playerAup);");
            AssertPlayerAupAssignmentFailsClosed(
                "_Project/Scripts/Fauna/FaunaBrain.cs",
                "private bool TryResolvePlayerPredictedAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = runtimeContext.MovementState.PredictedAup;");
            AssertPlayerAupAssignmentFailsClosed(
                "_Project/Scripts/World/WreckMaterialRegistry.cs",
                "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = playerMovement.CurrentAup;");
            AssertPlayerAupAssignmentFailsClosed(
                "_Project/Scripts/World/WreckMaterialRegistry.cs",
                "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = movementState.PredictedAup;");
            AssertAupAssignmentFailsClosed(
                "_Project/Scripts/World/WorldSpatialHashGrid.cs",
                "private static bool TryResolveActivePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = playerMovement.CurrentAup;",
                "return IsFiniteAup(in playerAup);");
            AssertAupAssignmentFailsClosed(
                "_Project/Scripts/World/WorldSpatialHashGrid.cs",
                "private static bool TryResolveActivePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = movementState.PredictedAup;",
                "return IsFiniteAup(in playerAup);");
            AssertAupAssignmentFailsClosed(
                "_Project/Scripts/HectonVoxelEngine.cs",
                "private static bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = playerMovement.CurrentAup;",
                "return AbsoluteUniversePosition.IsFinite(in playerAup);");
            AssertAupAssignmentFailsClosed(
                "_Project/Scripts/Gameplay/LifePodTactilePrologueController.cs",
                "private bool TryResolveObserverAup(out AbsoluteUniversePosition observerAup)",
                "observerAup = _cachedObserverMovement.PredictedAup;",
                "return observerAup.IsFinite();");
            AssertAupAssignmentFailsClosed(
                "_Project/Scripts/SubmarineAtmosphereSystem.cs",
                "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = playerContext.PlayerMovement.CurrentAup;",
                "return AbsoluteUniversePosition.IsFinite(in playerAup);");
            AssertPlayerAupAssignmentFailsClosed(
                "_Project/Scripts/World/EcosystemDirector.cs",
                "private static bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = movementState.PredictedAup;");
            AssertPlayerAupAssignmentFailsClosed(
                "_Project/Scripts/World/EcosystemDirector.cs",
                "private static bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = playerMovement.CurrentAup;");
            AssertPlayerAupAssignmentFailsClosed(
                "_Project/Scripts/World/HectonMapMagicVegetationBridge.cs",
                "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = movementState.PredictedAup;");
            AssertPlayerAupAssignmentFailsClosed(
                "_Project/Scripts/World/HectonMapMagicVegetationBridge.cs",
                "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)",
                "playerAup = movement.PredictedAup;");
        }

        private static void AssertPlayerAupAssignmentFailsClosed(
            string projectRelativePath,
            string methodSignature,
            string assignment)
        {
            AssertAupAssignmentFailsClosed(
                projectRelativePath,
                methodSignature,
                assignment,
                "return playerAup.IsFinite();");
        }

        private static void AssertAupAssignmentFailsClosed(
            string projectRelativePath,
            string methodSignature,
            string assignment,
            string expectedReturn)
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, projectRelativePath));
            string methodBody = ExtractMethodBody(source, methodSignature);
            int assignmentIndex = methodBody.IndexOf(assignment, StringComparison.Ordinal);
            Assert.GreaterOrEqual(assignmentIndex, 0, projectRelativePath + " missing assignment " + assignment);

            int finiteReturnIndex = methodBody.IndexOf(expectedReturn, assignmentIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(finiteReturnIndex, 0, projectRelativePath + " must fail closed after " + assignment);

            string beforeFiniteReturn = methodBody.Substring(assignmentIndex, finiteReturnIndex - assignmentIndex);
            StringAssert.DoesNotContain("return true;", beforeFiniteReturn, projectRelativePath + " must not return success before finite guard");
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "Missing method: " + signature);
            int brace = source.IndexOf('{', start);
            Assert.GreaterOrEqual(brace, 0, "Missing method body: " + signature);

            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(brace, i - brace + 1);
                }
            }

            Assert.Fail("Unclosed method body: " + signature);
            return string.Empty;
        }
    }
}
