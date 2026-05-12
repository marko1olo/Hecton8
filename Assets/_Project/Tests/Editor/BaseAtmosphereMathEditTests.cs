using Hecton8.Atmosphere;
using Hecton8.Core;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

public sealed class BaseAtmosphereMathEditTests
{
    [Test]
    public void CompartmentState_IsExactly32Bytes()
    {
        Assert.That(UnsafeUtility.SizeOf<CompartmentState>(), Is.EqualTo(32));
    }

    [Test]
    public void MathLod_ResolvesHighAt5HzAndMx350At1Hz()
    {
        Assert.That(BaseAtmosphereMath.ResolveColdTickIntervalSeconds(HectonQualityTier.High), Is.EqualTo(0.2f));
        Assert.That(BaseAtmosphereMath.ResolveColdTickIntervalSeconds(HectonQualityTier.Mx350), Is.EqualTo(1f));
        Assert.That(BaseAtmosphereMath.ResolveSolveMode(HectonQualityTier.High), Is.EqualTo(BaseAtmosphereSolveMode.High5Hz));
        Assert.That(BaseAtmosphereMath.ResolveSolveMode(HectonQualityTier.Low), Is.EqualTo(BaseAtmosphereSolveMode.ActiveCompartment1Hz));
    }

    [Test]
    public void DaltonFake_SumsPartialPressuresOnly()
    {
        Assert.That(BaseAtmosphereMath.ResolveDaltonPressureFake(21f, 0.5f, 79f), Is.EqualTo(100.5f).Within(0.0001f));
    }

    [Test]
    public void SealFlags_UseBitwiseChecks()
    {
        ushort sealedFlags = BaseAtmosphereFlags.Sealed;
        ushort unsealedFlags = BaseAtmosphereFlags.Unsealed;

        Assert.That(BaseAtmosphereMath.IsSealed(sealedFlags), Is.True);
        Assert.That(BaseAtmosphereMath.IsUnsealed(sealedFlags), Is.False);
        Assert.That(BaseAtmosphereMath.IsUnsealed(unsealedFlags), Is.True);
    }

    [Test]
    public void PressureGauge_UsesPrecomputedMaxPressureReciprocal()
    {
        CompartmentState state = BaseAtmosphereMath.CreateDefaultCompartment(200f, 0);
        state.TotalPressureKPa = 100f;
        state.OxygenKPa = 42f;

        Assert.That(state.InvMaxPressureKPa, Is.EqualTo(0.005f).Within(0.000001f));
        Assert.That(BaseAtmosphereMath.ResolvePressureGauge01(state), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(BaseAtmosphereMath.ResolveOxygenWholePercent(state), Is.EqualTo(21));
    }

    [Test]
    public void PlayerOxygenConsumption_ClampsStressToAtLeastOne()
    {
        Assert.That(BaseAtmosphereMath.ResolvePlayerOxygenConsumption(0.25f, 0.2f), Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(BaseAtmosphereMath.ResolvePlayerOxygenConsumption(0.25f, 3f), Is.EqualTo(0.75f).Within(0.0001f));
    }

    [Test]
    public void CrushDepthDamage_UsesSquaredTimesRsqrtApproximation()
    {
        Assert.That(BaseAtmosphereMath.ResolveCrushDepthDamage(4f), Is.EqualTo(8f).Within(0.0001f));
    }

    [Test]
    public void PhysiologyHazard_AppliesImmediateBendsDamage()
    {
        AtmospherePhysiologyHazard hazard = BaseAtmosphereMath.ResolvePhysiologyHazard(
            0f,
            101f,
            10.1f,
            50f,
            0f,
            123u,
            false,
            0,
            0f,
            1f);

        Assert.That(hazard.HealthDamage, Is.EqualTo(BaseAtmosphereMath.BendsHealthDamage).Within(0.0001f));
        Assert.That(BaseAtmosphereMath.HasFlag(hazard.Flags, BaseAtmosphereFlags.BendsDamageRequested), Is.True);
        Assert.That(BaseAtmosphereMath.HasFlag(hazard.Flags, BaseAtmosphereFlags.VisualBlurRequested), Is.True);
    }

    [Test]
    public void Narcosis_UsesDeterministicTriangleOffsetAndHelioxBypass()
    {
        float first = BaseAtmosphereMath.ResolveNarcosisTriangleOffset(260f, 1.25f, 0xA5A5u, false, false);
        float second = BaseAtmosphereMath.ResolveNarcosisTriangleOffset(260f, 1.25f, 0xA5A5u, false, false);

        Assert.That(first, Is.EqualTo(second).Within(0.000001f));
        Assert.That(math.abs(first), Is.GreaterThan(0.0001f));
        Assert.That(BaseAtmosphereMath.ResolveNarcosisTriangleOffset(260f, 1.25f, 0xA5A5u, true, false), Is.EqualTo(0f));
        Assert.That(BaseAtmosphereMath.ResolveNarcosisTriangleOffset(260f, 1.25f, 0xA5A5u, false, true), Is.EqualTo(0f));
    }

    [Test]
    public void ColdTick_LowTierUpdatesOnlyActiveCompartment()
    {
        NativeArray<CompartmentState> input = new NativeArray<CompartmentState>(2, Allocator.Temp);
        NativeArray<CompartmentState> output = new NativeArray<CompartmentState>(2, Allocator.Temp);
        NativeArray<byte> carbonDioxideBytes = new NativeArray<byte>(2, Allocator.Temp);
        try
        {
            CompartmentState state = BaseAtmosphereMath.CreateDefaultCompartment(100f, BaseAtmosphereFlags.ScrubberPowered);
            state.OxygenBaseConsumptionKPaPerSecond = 1f;
            state.CarbonDioxideGenerationKPaPerSecond = 0.25f;
            input[0] = state;
            input[1] = state;

            BaseAtmosphereColdTickJob job = new BaseAtmosphereColdTickJob
            {
                Input = input,
                Output = output,
                CarbonDioxideByteLane = carbonDioxideBytes,
                CompartmentCount = 2,
                ActiveCompartmentIndex = 1,
                DeltaTime = 1f,
                PlayerStressMultiplier = 2f,
                LogisticsPowerWatts = 10f,
                ScrubberKPaPerSecond = 0.1f,
                SolveMode = (byte)BaseAtmosphereSolveMode.ActiveCompartment1Hz,
                ScrubberBytePerColdTick = 1,
                ScalabilityHigh = 0
            };

            job.Execute();

            Assert.That(output[0].OxygenKPa, Is.EqualTo(input[0].OxygenKPa).Within(0.0001f));
            Assert.That(output[1].OxygenKPa, Is.EqualTo(input[1].OxygenKPa - 2f).Within(0.0001f));
            Assert.That(output[1].TotalPressureKPa, Is.EqualTo(output[1].OxygenKPa + output[1].CarbonDioxideKPa + output[1].NitrogenKPa).Within(0.0001f));
        }
        finally
        {
            input.Dispose();
            output.Dispose();
            carbonDioxideBytes.Dispose();
        }
    }

    [Test]
    public void Scrubber_ReducesScalarCo2AndByteLaneWhenPowered()
    {
        NativeArray<CompartmentState> input = new NativeArray<CompartmentState>(1, Allocator.Temp);
        NativeArray<CompartmentState> output = new NativeArray<CompartmentState>(1, Allocator.Temp);
        NativeArray<byte> carbonDioxideBytes = new NativeArray<byte>(1, Allocator.Temp);
        try
        {
            input[0] = new CompartmentState
            {
                OxygenKPa = 21f,
                CarbonDioxideKPa = 10f,
                NitrogenKPa = 69f,
                TotalPressureKPa = 100f,
                InvMaxPressureKPa = 0.01f,
                Flags = BaseAtmosphereFlags.Sealed | BaseAtmosphereFlags.ScrubberPowered
            };

            BaseAtmosphereColdTickJob job = new BaseAtmosphereColdTickJob
            {
                Input = input,
                Output = output,
                CarbonDioxideByteLane = carbonDioxideBytes,
                CompartmentCount = 1,
                ActiveCompartmentIndex = 0,
                DeltaTime = 1f,
                PlayerStressMultiplier = 1f,
                LogisticsPowerWatts = 10f,
                ScrubberKPaPerSecond = 2f,
                SolveMode = (byte)BaseAtmosphereSolveMode.ActiveCompartment1Hz,
                ScrubberBytePerColdTick = 3,
                ScalabilityHigh = 0
            };

            job.Execute();

            byte expectedByte = BaseAtmosphereMath.ReduceCarbonDioxideByte(
                BaseAtmosphereMath.EncodeCarbonDioxideByte(8f, 98f),
                3);

            Assert.That(output[0].CarbonDioxideKPa, Is.EqualTo(8f).Within(0.0001f));
            Assert.That(carbonDioxideBytes[0], Is.EqualTo(expectedByte));
        }
        finally
        {
            input.Dispose();
            output.Dispose();
            carbonDioxideBytes.Dispose();
        }
    }

    [Test]
    public unsafe void OxygenTankSwap_BlitCopiesItemIds()
    {
        NativeArray<ushort> inventory = new NativeArray<ushort>(4, Allocator.Temp);
        NativeArray<ushort> suitSlots = new NativeArray<ushort>(4, Allocator.Temp);
        try
        {
            inventory[1] = 401;
            inventory[2] = 402;

            bool copied = BaseAtmosphereEngine.TryBlitOxygenTankItemIds(inventory, 1, suitSlots, 0, 2);

            Assert.That(copied, Is.True);
            Assert.That(suitSlots[0], Is.EqualTo(401));
            Assert.That(suitSlots[1], Is.EqualTo(402));
        }
        finally
        {
            inventory.Dispose();
            suitSlots.Dispose();
        }
    }

    [Test]
    public void AtmosphericFog_FlagsOnlyOnHighHumidityHighScalability()
    {
        CompartmentState state = BaseAtmosphereMath.CreateDefaultCompartment(100f, 0);
        state.HumidityPercent = 91;

        CompartmentState high = BaseAtmosphereMath.StepCompartment(state, 1f, 1f, 0f, 0f, 0f, 10f, 0f, true);
        CompartmentState low = BaseAtmosphereMath.StepCompartment(state, 1f, 1f, 0f, 0f, 0f, 10f, 0f, false);

        Assert.That(BaseAtmosphereMath.HasFlag(high.Flags, BaseAtmosphereFlags.RenderFogRequested), Is.True);
        Assert.That(BaseAtmosphereMath.HasFlag(low.Flags, BaseAtmosphereFlags.RenderFogRequested), Is.False);
    }

    [Test]
    public void SuitRupture_DrainsOxygenAndRequestsBubbleVfx()
    {
        CompartmentState state = BaseAtmosphereMath.CreateDefaultCompartment(100f, 0);
        state.OxygenKPa = 20f;
        state.TotalPressureKPa = BaseAtmosphereMath.ResolveDaltonPressureFake(
            state.OxygenKPa,
            state.CarbonDioxideKPa,
            state.NitrogenKPa);

        CompartmentState ruptured = BaseAtmosphereMath.StepCompartment(state, 1f, 1f, 0f, 0f, 20f, 10f, 0.1f, false);

        Assert.That(ruptured.OxygenKPa, Is.LessThan(20f));
        Assert.That(BaseAtmosphereMath.HasFlag(ruptured.Flags, BaseAtmosphereFlags.BubbleVfxRequested), Is.True);
    }

    [Test]
    public void SmokeFake_UsesSaturatingToxicityByte()
    {
        Assert.That(BaseAtmosphereMath.SaturatingAddByte(120, 10), Is.EqualTo(130));
        Assert.That(BaseAtmosphereMath.SaturatingAddByte(250, 10), Is.EqualTo(255));
    }

    [Test]
    public void Hypercapnia_HalvesStaminaRecovery()
    {
        Assert.That(BaseAtmosphereMath.ResolveStaminaRecoveryMultiplierForCarbonDioxide(0.051f), Is.EqualTo(0.5f));
        Assert.That(BaseAtmosphereMath.ResolveStaminaRecoveryMultiplierForCarbonDioxide(0.01f), Is.EqualTo(1f));
    }
}
