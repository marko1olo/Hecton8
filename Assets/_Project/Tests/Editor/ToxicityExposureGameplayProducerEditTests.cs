using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class ToxicityExposureGameplayProducerEditTests
    {
        [Test]
        public void GameplayToxicityExposureProducers_FailClosedOnNonFiniteSeverity()
        {
            string root = Directory.GetCurrentDirectory();
            string hazardZone = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs"));
            string environmentalHazard = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/Gameplay/EnvironmentalHazard.cs"));
            string trauma = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/Gameplay/TraumaDispatcher.cs"));
            string flora = File.ReadAllText(Path.Combine(root, "Assets/_Project/Scripts/World/FloraInteractionManager.cs"));

            string hazardPublish = ExtractMethodBody(hazardZone, "private void PublishToxicityExposureSignal(");
            string hazardPlayerAup = ExtractMethodBody(hazardZone, "private static bool TryResolvePlayerPredictedAup(");
            string hazardResolvePlayerContext = ExtractMethodBody(hazardZone, "private void ResolvePlayerContext()");
            string hazardRefreshPlayerContext = ExtractMethodBody(hazardZone, "private void RefreshPlayerContextSnapshot()");
            string hazardResolveSignalEntity = ExtractMethodBody(hazardZone, "private uint ResolvePlayerToxicitySignalEntityId(");
            StringAssert.Contains("private const uint PlayerToxicityFallbackEntityHash = ToxicityExposureSignal.PlayerEntityFallbackHash;", hazardZone);
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", hazardZone);
            StringAssert.DoesNotContain("if (targetId == 0)", hazardPublish);
            StringAssert.Contains("uint signalEntityId = ResolvePlayerToxicitySignalEntityId();", hazardPublish);
            StringAssert.Contains("float exposure01 = FiniteSaturate01(currentIntensity, 0f);", hazardPublish);
            StringAssert.Contains("float safeDamageMagnitude = FiniteNonNegativeOrZero(damageMagnitude);", hazardPublish);
            StringAssert.Contains("float toxemiaDelta = math.saturate(exposure01 * math.max(0.1f, safeDamageMagnitude) * ToxicityExposureToxemiaScale);", hazardPublish);
            StringAssert.Contains("bool hasSourceAup = TryResolvePlayerPredictedAup(out AbsoluteUniversePosition playerAup) ||", hazardPublish);
            StringAssert.Contains("if (hasSourceAup)", hazardPublish);
            StringAssert.Contains("signal.AUP = playerAup.ToAbsoluteDouble3();", hazardPublish);
            StringAssert.Contains("signal.EntityId = signalEntityId;", hazardPublish);
            StringAssert.Contains("signal.Flags = ToxicityExposureSignal.FlagHasSourceAup;", hazardPublish);
            Assert.That(hazardPublish, Does.Not.Contain("(_playerTransform == null || !TryResolveAupFromRuntimeOrigin(_playerTransform.position, out playerAup)))"));
            AssertSourceOrder(hazardPublish, "uint signalEntityId = ResolvePlayerToxicitySignalEntityId();", "signal.EntityId = signalEntityId;");
            AssertSourceOrder(hazardPublish, "bool hasSourceAup = TryResolvePlayerPredictedAup(out AbsoluteUniversePosition playerAup)", "ToxicityExposureSignal signal = default;");
            AssertSourceOrder(hazardPublish, "signal.AUP = playerAup.ToAbsoluteDouble3();", "signal.Flags = ToxicityExposureSignal.FlagHasSourceAup;");
            AssertSourceOrder(hazardPublish, "if (hasSourceAup)", "SignalBus<ToxicityExposureSignal>.TryPushTracked");
            AssertSourceOrder(hazardPublish, "float exposure01 = FiniteSaturate01(currentIntensity, 0f);", "SignalBus<ToxicityExposureSignal>.TryPushTracked");
            StringAssert.Contains("IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;", hazardPlayerAup);
            StringAssert.Contains("runtimeContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)", hazardPlayerAup);
            StringAssert.Contains("(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", hazardPlayerAup);
            StringAssert.Contains("IsFiniteAup(in snapshot.Aup)", hazardPlayerAup);
            StringAssert.Contains("playerAup = snapshot.Aup;", hazardPlayerAup);
            StringAssert.Contains("runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", hazardPlayerAup);
            StringAssert.Contains("!IsFiniteAup(in movementState.PredictedAup)", hazardPlayerAup);
            StringAssert.Contains("playerAup = movementState.PredictedAup;", hazardPlayerAup);
            AssertSourceOrder(hazardPlayerAup, "runtimeContext.TryGetPlayerPoseSnapshot", "runtimeContext.TryGetMovementRuntimeState");
            Assert.That(hazardPlayerAup, Does.Not.Contain("PlayerRuntimeContextService.TryGetActiveRuntimeContext"));
            Assert.That(hazardPlayerAup, Does.Not.Contain("runtimeContext.MovementState"));
            StringAssert.Contains("IPlayerRuntimeContext activeRuntimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;", hazardResolvePlayerContext);
            StringAssert.Contains("bool hasActiveRuntimeContext = activeRuntimeContext != null;", hazardResolvePlayerContext);
            StringAssert.Contains("if (IsPlayerRuntimeContextBound(activeRuntimeContext))", hazardResolvePlayerContext);
            StringAssert.Contains("else if (hasActiveRuntimeContext)", hazardResolvePlayerContext);
            StringAssert.Contains("ClearPlayerRuntimeBindings();", hazardResolvePlayerContext);
            StringAssert.Contains("IPlayerRuntimeContext activeRuntimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;", hazardRefreshPlayerContext);
            StringAssert.Contains("bool hasActiveRuntimeContext = activeRuntimeContext != null;", hazardRefreshPlayerContext);
            StringAssert.Contains("else if (hasActiveRuntimeContext)", hazardRefreshPlayerContext);
            StringAssert.Contains("ClearPlayerRuntimeBindings();", hazardRefreshPlayerContext);
            Assert.That(hazardResolveSignalEntity, Does.Not.Contain("return unchecked((uint)combatTargetId);"));
            StringAssert.Contains("IPlayerRuntimeContext playerContext = _playerRuntimeContext;", hazardResolveSignalEntity);
            StringAssert.Contains("playerObject = playerContext.PlayerObject;", hazardResolveSignalEntity);
            StringAssert.Contains("playerObject = _playerTransform.gameObject;", hazardResolveSignalEntity);
            StringAssert.Contains("playerObject = BootstrapState.CurrentPlayerObject;", hazardResolveSignalEntity);
            StringAssert.Contains("EntityId.ToULong(playerObject.GetEntityId())", hazardResolveSignalEntity);
            StringAssert.Contains("return entityHash != 0u ? entityHash : PlayerToxicityFallbackEntityHash;", hazardResolveSignalEntity);

            StringAssert.Contains("private static float SafeSaturate01(float value)", environmentalHazard);
            StringAssert.Contains("private static float SafeNonNegative(float value)", environmentalHazard);
            string environmentalApplyDamage = ExtractMethodBody(environmentalHazard, "private void ApplyDamage(");
            string environmentalPublish = ExtractMethodBody(environmentalHazard, "private void ApplyToxicityExposure(");
            string environmentalResolveSignalEntity = ExtractMethodBody(environmentalHazard, "private uint ResolveToxicitySignalEntityId(");
            StringAssert.Contains("private const uint PlayerToxicityFallbackEntityHash = ToxicityExposureSignal.PlayerEntityFallbackHash;", environmentalHazard);
            AssertSourceOrder(environmentalApplyDamage, "HectonPlayerHealth playerHealth = ResolvePlayerHealth();", "if (IsToxicHazardType())");
            AssertSourceOrder(environmentalApplyDamage, "if (IsToxicHazardType())", "if (playerHealth == null)");
            StringAssert.Contains("GameObject playerObject = playerHealth != null ? playerHealth.gameObject : null;", environmentalPublish);
            StringAssert.Contains("Transform playerTransform = playerHealth != null ? playerHealth.transform : _playerTransform;", environmentalPublish);
            StringAssert.Contains("int targetId = playerObject != null ? CombatDamageRuntime.ResolveTargetId(playerObject) : 0;", environmentalPublish);
            StringAssert.Contains("uint signalEntityId = ResolveToxicitySignalEntityId(playerObject);", environmentalPublish);
            StringAssert.DoesNotContain("if (targetId == 0 ||", environmentalPublish);
            StringAssert.Contains("float exposure01 = SafeSaturate01(_currentIntensity);", environmentalPublish);
            StringAssert.Contains("float safeDamageMagnitude = SafeNonNegative(damageMagnitude);", environmentalPublish);
            StringAssert.Contains("float severity01 = math.saturate(math.max(exposure01, safeDamageMagnitude * 0.05f));", environmentalPublish);
            StringAssert.Contains("float toxemiaDelta = math.saturate(exposure01 * math.max(0.1f, safeDamageMagnitude) * ToxicityExposureToxemiaScale);", environmentalPublish);
            StringAssert.Contains("bool hasSourceAup = TryResolveAupFromRuntimeOrigin(playerTransform.position, out AbsoluteUniversePosition playerAup);", environmentalPublish);
            StringAssert.Contains("if (hasSourceAup)", environmentalPublish);
            StringAssert.Contains("signal.AUP = playerAup.ToAbsoluteDouble3();", environmentalPublish);
            StringAssert.Contains("signal.EntityId = signalEntityId;", environmentalPublish);
            StringAssert.Contains("signal.Flags = ToxicityExposureSignal.FlagHasSourceAup;", environmentalPublish);
            Assert.That(environmentalPublish, Does.Not.Contain("if (!TryResolveAupFromRuntimeOrigin(playerTransform.position, out AbsoluteUniversePosition playerAup))"));
            AssertSourceOrder(environmentalPublish, "uint signalEntityId = ResolveToxicitySignalEntityId(playerObject);", "signal.EntityId = signalEntityId;");
            AssertSourceOrder(environmentalPublish, "bool hasSourceAup = TryResolveAupFromRuntimeOrigin(playerTransform.position, out AbsoluteUniversePosition playerAup);", "ToxicityExposureSignal signal = default;");
            AssertSourceOrder(environmentalPublish, "signal.AUP = playerAup.ToAbsoluteDouble3();", "signal.Flags = ToxicityExposureSignal.FlagHasSourceAup;");
            AssertSourceOrder(environmentalPublish, "if (hasSourceAup)", "SignalBus<ToxicityExposureSignal>.TryPushTracked");
            AssertSourceOrder(environmentalPublish, "float exposure01 = SafeSaturate01(_currentIntensity);", "SignalBus<ToxicityExposureSignal>.TryPushTracked");
            Assert.That(environmentalResolveSignalEntity, Does.Not.Contain("return unchecked((uint)combatTargetId);"));
            StringAssert.Contains("IPlayerRuntimeContext playerContext = _playerRuntime;", environmentalResolveSignalEntity);
            StringAssert.Contains("resolvedPlayerObject = BootstrapState.CurrentPlayerObject;", environmentalResolveSignalEntity);
            StringAssert.Contains("return entityHash != 0u ? entityHash : PlayerToxicityFallbackEntityHash;", environmentalResolveSignalEntity);

            StringAssert.Contains("private static float SafeSaturate01(float value)", trauma);
            string traumaPublish = ExtractMethodBody(trauma, "private void PublishParasiteSporePoisonStatus(");
            string traumaResolveSignalEntity = ExtractMethodBody(trauma, "private uint ResolvePlayerToxicitySignalEntityId(");
            StringAssert.Contains("private const uint PlayerToxicityFallbackEntityHash = ToxicityExposureSignal.PlayerEntityFallbackHash;", trauma);
            StringAssert.DoesNotContain("if (targetId == 0)", traumaPublish);
            StringAssert.Contains("uint signalEntityId = ResolvePlayerToxicitySignalEntityId();", traumaPublish);
            StringAssert.Contains("if (targetId != 0 && CombatDamageRuntime.IsTargetRegistered(targetId))", traumaPublish);
            StringAssert.Contains("float severity01 = SafeSaturate01(hazardIntensity);", traumaPublish);
            StringAssert.Contains("signal.ToxemiaDelta = math.saturate(severity01 * intervals * ParasiteSporeToxemiaScale);", traumaPublish);
            StringAssert.Contains("bool hasSourceAup = _playerMovement != null && _playerMovement.CurrentAup.IsFinite();", traumaPublish);
            StringAssert.Contains("if (hasSourceAup)", traumaPublish);
            StringAssert.Contains("signal.AUP = _playerMovement.CurrentAup.ToAbsoluteDouble3();", traumaPublish);
            StringAssert.Contains("signal.EntityId = signalEntityId;", traumaPublish);
            StringAssert.Contains("signal.Flags = ToxicityExposureSignal.FlagHasSourceAup;", traumaPublish);
            Assert.That(traumaPublish, Does.Not.Contain("if (_playerMovement == null || !_playerMovement.CurrentAup.IsFinite())"));
            AssertSourceOrder(traumaPublish, "uint signalEntityId = ResolvePlayerToxicitySignalEntityId();", "signal.EntityId = signalEntityId;");
            AssertSourceOrder(traumaPublish, "bool hasSourceAup = _playerMovement != null && _playerMovement.CurrentAup.IsFinite();", "ToxicityExposureSignal signal = default;");
            AssertSourceOrder(traumaPublish, "signal.AUP = _playerMovement.CurrentAup.ToAbsoluteDouble3();", "signal.Flags = ToxicityExposureSignal.FlagHasSourceAup;");
            AssertSourceOrder(traumaPublish, "if (hasSourceAup)", "SignalBus<ToxicityExposureSignal>.TryPushTracked");
            AssertSourceOrder(traumaPublish, "float severity01 = SafeSaturate01(hazardIntensity);", "CombatDamageRuntime.TryQueueStatusEffect(");
            AssertSourceOrder(traumaPublish, "float severity01 = SafeSaturate01(hazardIntensity);", "SignalBus<ToxicityExposureSignal>.TryPushTracked");
            Assert.That(traumaResolveSignalEntity, Does.Not.Contain("return unchecked((uint)combatTargetId);"));
            StringAssert.Contains("playerObject = _playerMovement.gameObject;", traumaResolveSignalEntity);
            StringAssert.Contains("return entityHash != 0u ? entityHash : PlayerToxicityFallbackEntityHash;", traumaResolveSignalEntity);

            string floraApply = ExtractMethodBody(flora, "private void TryApplyToxicSporePoisonStatus(");
            string floraResolveSignalEntity = ExtractMethodBody(flora, "private uint ResolveToxicSporeSignalEntityId(");
            string floraPublish = ExtractMethodBody(flora, "private void PublishToxicSporeToxicityExposure(");
            StringAssert.Contains("private const uint PlayerToxicityFallbackEntityHash = ToxicityExposureSignal.PlayerEntityFallbackHash;", flora);
            StringAssert.Contains("Transform playerTransform = _playerTransform;", floraApply);
            StringAssert.Contains("playerTransform = playerContext.PlayerTransform;", floraApply);
            StringAssert.Contains("if (playerTransform == null)", floraApply);
            StringAssert.Contains("int targetId = playerHealth != null ? CombatDamageRuntime.ResolveTargetId(playerHealth.gameObject) : 0;", floraApply);
            StringAssert.Contains("uint signalEntityId = ResolveToxicSporeSignalEntityId(playerHealth);", floraApply);
            StringAssert.Contains("PublishToxicSporeToxicityExposure(signalEntityId, playerPositionWS, exposure01);", floraApply);
            AssertSourceOrder(floraApply, "PublishToxicSporeToxicityExposure(signalEntityId, playerPositionWS, exposure01);", "if (playerHealth == null || targetId == 0 || !CombatDamageRuntime.IsTargetRegistered(targetId))");
            Assert.That(floraResolveSignalEntity, Does.Not.Contain("return unchecked((uint)combatTargetId);"));
            StringAssert.Contains("IPlayerRuntimeContext playerContext = _playerRuntimeContext;", floraResolveSignalEntity);
            StringAssert.Contains("playerObject = BootstrapState.CurrentPlayerObject;", floraResolveSignalEntity);
            StringAssert.Contains("return entityHash != 0u ? entityHash : PlayerToxicityFallbackEntityHash;", floraResolveSignalEntity);
            StringAssert.Contains("float exposure = float.IsFinite(exposure01) ? math.saturate(exposure01) : 0f;", floraPublish);
            StringAssert.Contains("if (signalEntityId == 0u || exposure <= 0.0001f)", floraPublish);
            StringAssert.Contains("signal.ToxemiaDelta = math.saturate(exposure * ToxicSporeToxemiaDeltaScale);", floraPublish);
            StringAssert.Contains("bool hasSourceAup = TryResolveToxicSporePlayerAup(playerPositionWS, out AbsoluteUniversePosition playerAup);", floraPublish);
            StringAssert.Contains("if (hasSourceAup)", floraPublish);
            StringAssert.Contains("signal.AUP = playerAup.ToAbsoluteDouble3();", floraPublish);
            StringAssert.Contains("signal.EntityId = signalEntityId;", floraPublish);
            StringAssert.Contains("signal.Flags = ToxicityExposureSignal.FlagHasSourceAup;", floraPublish);
            Assert.That(floraPublish, Does.Not.Contain("if (!TryResolveToxicSporePlayerAup(playerPositionWS, out AbsoluteUniversePosition playerAup))"));
            AssertSourceOrder(floraPublish, "bool hasSourceAup = TryResolveToxicSporePlayerAup(playerPositionWS, out AbsoluteUniversePosition playerAup);", "ToxicityExposureSignal signal = default;");
            AssertSourceOrder(floraPublish, "signal.AUP = playerAup.ToAbsoluteDouble3();", "signal.Flags = ToxicityExposureSignal.FlagHasSourceAup;");
            AssertSourceOrder(floraPublish, "if (hasSourceAup)", "SignalBus<ToxicityExposureSignal>.TryPushTracked");
            AssertSourceOrder(floraPublish, "float exposure = float.IsFinite(exposure01) ? math.saturate(exposure01) : 0f;", "SignalBus<ToxicityExposureSignal>.TryPushTracked");
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, "Missing method signature: " + signature);
            int open = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(open, 0, "Missing method open brace: " + signature);

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(open, i - open + 1);
                }
            }

            Assert.Fail("Missing method close brace: " + signature);
            return string.Empty;
        }

        private static void AssertSourceOrder(string source, string before, string after)
        {
            int beforeIndex = source.IndexOf(before, StringComparison.Ordinal);
            int afterIndex = source.IndexOf(after, StringComparison.Ordinal);

            Assert.GreaterOrEqual(beforeIndex, 0, "Missing source token: " + before);
            Assert.GreaterOrEqual(afterIndex, 0, "Missing source token: " + after);
            Assert.Less(beforeIndex, afterIndex);
        }
    }
}
