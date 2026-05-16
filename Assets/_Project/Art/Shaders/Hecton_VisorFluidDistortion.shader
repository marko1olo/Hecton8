Shader "Hidden/Hecton8/VisorFluidDistortion"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "VisorFluid"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Assets/_Project/Art/Shaders/Post/Hecton_SnellRefractionCore.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _HectonVisorFluidIntensity;
                float _HectonVisorFluidWetness;
                float _HectonVisorFluidHullStress;
                float _HectonVisorFluidDistortionStrength;
                float _HectonVisorFluidSnellStrength;
                float _HectonVisorFluidDepthSoftness;
                float _HectonWaterDensitySignal;
                float _HectonVisorFluidHomeostasisFallback;
                float _HectonVisorFluidLowTier;
                float4 _HectonVisorFluidIorLut;
                float _HectonVisorFluidRunoffSpeed;
                float _HectonVisorFluidDropletScale;
                float _HectonVisorFluidLateralStreakStrength;
                float _HectonVisorFluidForwardStretchStrength;
                float _HectonVisorFluidEdgeStreakStrength;
                float _HectonVisorFluidEdgeFadeExponent;
                float _HectonVisorFluidSpeed;
                float4 _HectonVisorFluidLocalVelocity;
                float _HectonThermalDistortionMotionCull;
                float _HectonVisorFluidAmbientLight;
                float _HectonVisorFluidDustStrength;
                float _HectonVisorFluidAmbientDustResponse;
                float _HectonVisorFluidVisualOverkill;
            CBUFFER_END

            TEXTURE2D_X(_BlitTexture);
            float4 _BlitTexture_TexelSize;
            float _RainIntensity;
            float4 _GlobalWind;
            float4 _HectonScreenSpaceRainParams;
            float _HectonLightningFlash;

            struct Attributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float2 screenUV : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.screenUV = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(output.screenUV * 2.0 - 1.0, 0.0, 1.0);
            #if UNITY_UV_STARTS_AT_TOP
                output.screenUV.y = 1.0 - output.screenUV.y;
            #endif
                return output;
            }

            float2 ResolveXRStereoScreenUV(float2 screenUV)
            {
#if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                return UnityStereoTransformScreenSpaceTex(screenUV);
#else
                return screenUV;
#endif
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 34.45);
                return frac(p.x * p.y);
            }

            float ResolveInterleavedGradientNoise(float2 uv, float2 offset)
            {
                float2 pixel = floor(uv * _ScreenParams.xy + offset);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float FastPowerCurve01(float value, float exponent)
            {
                float v = saturate(value);
                float v2 = v * v;
                float v4 = v2 * v2;
                float v8 = v4 * v4;
                float low = lerp(v, v2, saturate(exponent - 1.0));
                float high = lerp(v2, v8, saturate((exponent - 2.0) * 0.16666667));
                return lerp(low, high, step(2.0, exponent));
            }

            float ComputeVisorEdgeMask(float2 uv)
            {
                float2 centered = uv * 2.0 - 1.0;
                float radial = saturate(dot(centered, centered));
                float rim = FastPowerCurve01(radial, max(0.1, _HectonVisorFluidEdgeFadeExponent));
                return saturate(0.28 + rim * 0.72);
            }

            float ComputeDropletMask(float2 uv, float2 flowDirection, float wetness, float hullStress)
            {
                float lateralStreak = _HectonVisorFluidLocalVelocity.x * _HectonVisorFluidLateralStreakStrength;
                float forwardStretch = abs(_HectonVisorFluidLocalVelocity.z) * _HectonVisorFluidForwardStretchStrength;
                float2 cellScale = float2(
                    max(2.0, _HectonVisorFluidDropletScale * (1.0 + wetness * 1.6 + forwardStretch * 0.45)),
                    max(4.0, _HectonVisorFluidDropletScale * (2.35 + wetness * 1.25 + hullStress * 0.9 + forwardStretch)));
                float2 scaledUV = uv * cellScale;
                float2 cellId = floor(scaledUV);
                float2 cellUV = frac(scaledUV) - 0.5;
                float seed = ResolveInterleavedGradientNoise(
                    (cellId + 0.5) / max(cellScale, float2(1.0, 1.0)),
                    float2(31.0, 17.0));
                float activeCell = step(0.34 - wetness * 0.12 - hullStress * 0.08, seed);

                float travel = frac(_Time.y * _HectonVisorFluidRunoffSpeed * (0.22 + seed * 0.48) + seed + scaledUV.x * 0.015);
                cellUV.y += (travel - 0.5) * (1.15 + wetness * 0.32 + hullStress * 0.24);
                cellUV.x += lateralStreak * 0.22 + (seed - 0.5) * 0.25;

                float radius = lerp(0.10, 0.24, seed);
                float2 dropletDelta = cellUV * float2(1.0, 1.45);
                float dropletRadiusSq = dot(dropletDelta, dropletDelta);
                float radiusSq = radius * radius;
                float droplet = (1.0 - smoothstep(radiusSq * 0.3844, radiusSq, dropletRadiusSq)) * activeCell;
                float streakWidth = lerp(0.016, 0.052, seed);
                float streak = (1.0 - smoothstep(streakWidth, streakWidth * 3.0, abs(cellUV.x)))
                    * smoothstep(0.48, -0.36, cellUV.y)
                    * activeCell;

                float hullFilm = 0.0;
                float condensationMask = 0.0;
                [branch]
                if (hullStress > 0.001)
                {
                    float filmNoise = saturate(ValueNoise(uv * float2(7.0, 13.0) + flowDirection * (_Time.y * 0.35)) - 0.52);
                    hullFilm = filmNoise * hullStress * (0.4 + abs(lateralStreak) * 0.4);
                    condensationMask = saturate(
                        ValueNoise(uv * float2(11.0, 19.0) - flowDirection * (_Time.y * 0.12) + hullStress * 2.0) -
                        (0.72 - hullStress * 0.18));
                }
                float topBias = smoothstep(0.08, 1.0, uv.y);
                return saturate((droplet * 0.86 + streak * 0.74 + hullFilm + condensationMask * hullStress * 0.55) * topBias);
            }

            float ComputeDustMask(float2 uv, float edgeMask, float ambientReveal)
            {
                if (ambientReveal <= 0.0001)
                    return 0.0;

                float ignNoise = ResolveInterleavedGradientNoise(uv, float2(0.0, 0.0));
                float specks = smoothstep(1.0 - ambientReveal * 0.62, 1.0 - ambientReveal * 0.18, ignNoise);
                float scratchNoise = Hash21(floor(uv * _ScreenParams.xy * 0.18) + float2(7.0, 19.0));
                float scratch = smoothstep(0.72, 0.97, scratchNoise) * ambientReveal;
                float centerProtection = smoothstep(0.0, 0.22, abs(uv.x - 0.5) + abs(uv.y - 0.5));
                return saturate((specks * (0.32 + edgeMask * 0.68) + scratch * 0.35) * centerProtection);
            }

            float ComputeSaltCrystalMask(float2 uv, float wetness, float inverseDirtRefraction, float depthRefractionMask, float lowTierMode)
            {
                float overkill = saturate(_HectonVisorFluidVisualOverkill);
                float crystalDrive = saturate(overkill * wetness * inverseDirtRefraction * depthRefractionMask * (1.0 - lowTierMode));
                if (crystalDrive <= 0.0001)
                    return 0.0;

                float2 crystalGrid = uv * lerp(92.0, 176.0, overkill);
                float2 cell = floor(crystalGrid);
                float2 crystalLocal = frac(crystalGrid) - 0.5;
                float seed = Hash21(cell + floor(_Time.y * 0.03125));
                float2 axis = float2(seed - 0.5, Hash21(cell + 19.17) - 0.5);
                axis *= rcp(max(0.001, abs(axis.x) + abs(axis.y)));
                float ridge = 1.0 - smoothstep(0.011, 0.052, abs(dot(crystalLocal, axis)));
                float branch = 1.0 - smoothstep(0.012, 0.064, abs(dot(crystalLocal, axis.yx * float2(-1.0, 1.0))));
                float growth = saturate(frac(_Time.y * 0.047 + seed) * 1.7 - 0.34);
                float active = step(0.88 - overkill * 0.08, seed);
                return saturate((ridge * 0.78 + ridge * branch * 0.42) * active * growth * crystalDrive);
            }

            float ComputeSuspendedSiltMask(float2 uv, float wetness, float rainIntensity, float inverseDirtRefraction, float depthRefractionMask, float lowTierMode)
            {
                float overkill = saturate(_HectonVisorFluidVisualOverkill);
                float activity = max(wetness, rainIntensity * 0.45);
                float siltDrive = saturate(overkill * activity * inverseDirtRefraction * depthRefractionMask * (1.0 - lowTierMode));
                if (siltDrive <= 0.0001)
                    return 0.0;

                float2 flow = float2(
                    _HectonVisorFluidLocalVelocity.x * 0.038 + 0.017,
                    -0.023 - abs(_HectonVisorFluidLocalVelocity.z) * 0.016);
                float slowSwirl = ValueNoise(uv * float2(8.0, 13.0) + flow * (_Time.y * 0.37));
                float2 siltUV = uv * lerp(float2(46.0, 88.0), float2(84.0, 148.0), overkill);
                siltUV += float2(slowSwirl * 0.21, -slowSwirl * 0.13) + flow * _Time.y;
                float filament = 1.0 - smoothstep(0.11, 0.41, abs(frac(siltUV.y + slowSwirl * 0.31) - 0.5));
                float speckSeed = Hash21(floor(uv * _ScreenParams.xy * lerp(0.07, 0.145, overkill)) + floor(_Time.y * 3.0));
                float speck = step(0.965 - overkill * 0.035, speckSeed);
                return saturate((filament * 0.32 + speck * 0.86) * siltDrive);
            }

            float2 ComputeRefractionOffset(float2 uv, float mask, float wetness, float hullStress)
            {
                float2 flowDirection = float2(
                    _HectonVisorFluidLocalVelocity.x * _HectonVisorFluidLateralStreakStrength * 0.6,
                    -1.0 - abs(_HectonVisorFluidLocalVelocity.z) * _HectonVisorFluidForwardStretchStrength * 0.4);
                float2 noiseUV = uv * float2(10.0, 16.0) + flowDirection * (_Time.y * _HectonVisorFluidRunoffSpeed * 0.5);
                float noiseX = ValueNoise(noiseUV + float2(0.0, 13.1)) - 0.5;
                float noiseY = ValueNoise(noiseUV + float2(17.3, 4.7)) - 0.5;
                float downwardPull = saturate(abs(_HectonVisorFluidLocalVelocity.y) * 0.15 + wetness * 0.35 + hullStress * 0.2);
                float2 centered = uv * 2.0 - 1.0;
                float2 centeredAbs = abs(centered);
                float centeredApprox = max(0.0001, max(centeredAbs.x, centeredAbs.y) + min(centeredAbs.x, centeredAbs.y) * 0.375);
                float2 edgeDirection = centered / centeredApprox;
                float edgePush = _HectonVisorFluidSpeed * _HectonVisorFluidEdgeStreakStrength * (0.25 + hullStress * 0.75);
                float2 offset = float2(noiseX + flowDirection.x * 0.18, noiseY - downwardPull * 0.2);
                offset += edgeDirection * edgePush * (0.25 + saturate(centeredApprox) * 0.75);
                return offset * (_HectonVisorFluidDistortionStrength * mask);
            }

            struct RainOverlayResult
            {
                float mask;
                float2 normalOffset;
            };

            float2 ComputeScrollingRainNormal(float2 uv, float2 windDir, float windSpeed, float rainIntensity)
            {
                float2 normalUV = uv * float2(28.0, 58.0);
                normalUV.x += uv.y * (1.4 + windDir.x * 2.6) + windDir.x * _Time.y * 1.7;
                normalUV.y -= _Time.y * (7.5 + windSpeed * 7.0);
                float height = ValueNoise(normalUV);
                float heightX = ValueNoise(normalUV + float2(0.071, 0.0)) - height;
                float heightY = ValueNoise(normalUV + float2(0.0, 0.071)) - height;
                return float2(heightX + windDir.x * 0.05, heightY - 0.08) * (rainIntensity * 0.0065);
            }

            RainOverlayResult ComputeScreenSpaceRain(float2 uv, float rainIntensity)
            {
                RainOverlayResult result;
                result.mask = 0.0;
                result.normalOffset = float2(0.0, 0.0);
                float densityScale = max(0.25, _HectonScreenSpaceRainParams.y);
                float areaScale = max(0.1, _HectonScreenSpaceRainParams.z);
                float exposure = saturate(_HectonScreenSpaceRainParams.w);
                if (rainIntensity <= 0.0001 || exposure <= 0.0001)
                    return result;

                float windSpeed = saturate(_GlobalWind.w * 0.08);
                float2 windXZ = _GlobalWind.xz;
                float windLenSq = max(dot(windXZ, windXZ), 0.0001);
                float2 windDir = windXZ * rsqrt(windLenSq);
                float slant = 0.16 + windDir.x * (0.22 + windSpeed * 0.28);
                float fallSpeed = 18.0 + windSpeed * 12.0;
                float2 scale = float2(96.0 * areaScale, 44.0);
                float2 rainUV = uv * scale;
                rainUV.x += uv.y * slant * 28.0 + windDir.x * _Time.y * 4.0;
                rainUV.y -= _Time.y * fallSpeed;

                float2 cell = floor(rainUV);
                float seed = Hash21(cell);
                float lane = abs(frac(rainUV.x + seed * 0.37) - 0.5);
                float drop = frac(rainUV.y + seed);
                float streak = smoothstep(0.5, 0.0, lane * (10.0 + rainIntensity * densityScale * 24.0));
                streak *= smoothstep(0.98, 0.64, drop) * smoothstep(0.02, 0.18, drop);
                streak *= step(1.0 - saturate(rainIntensity * densityScale * 0.85), seed);

                float mistNoise = saturate(ValueNoise(uv * float2(18.0, 32.0) + float2(_Time.y * windDir.x, -_Time.y * 1.7)) - 0.54);
                result.mask = saturate((streak + mistNoise * rainIntensity * 0.28) * rainIntensity * exposure);
                [branch]
                if (result.mask > 0.0001)
                    result.normalOffset = ComputeScrollingRainNormal(uv, windDir, windSpeed, rainIntensity) * exposure;

                return result;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = ResolveXRStereoScreenUV(input.screenUV);
                float wetness = saturate(_HectonVisorFluidWetness);
                float hullStress = saturate(_HectonVisorFluidHullStress);
                float intensity = saturate(_HectonVisorFluidIntensity);
                float glitchAmount = saturate((hullStress - 0.52) * 2.08);
                float edgeMask = 0.0;
                float edgeMaskResolved = 0.0;
                float dustMask = 0.0;
                float dustReveal = saturate(_HectonVisorFluidAmbientLight * _HectonVisorFluidDustStrength * _HectonVisorFluidAmbientDustResponse);
                float thermalMotionCull = saturate(_HectonThermalDistortionMotionCull);
                float combinedMask = 0.0;
                float2 refractedUV = screenUV;
                float fluidActivity = saturate(max(wetness, hullStress) * intensity * (1.0 - thermalMotionCull));
                float rainIntensity = saturate(_RainIntensity);
                float lightningFlash = saturate(_HectonLightningFlash);
                float rawSceneDepth = SampleSceneDepth(screenUV);
#if UNITY_REVERSED_Z
                float sceneDepthValid = step(0.0001, rawSceneDepth);
#else
                float sceneDepthValid = step(rawSceneDepth, 0.9999);
#endif
                float linearSceneDepth = LinearEyeDepth(rawSceneDepth, _ZBufferParams);
                float depthRefractionMask = sceneDepthValid * smoothstep(0.12, max(0.13, _HectonVisorFluidDepthSoftness + 0.12), linearSceneDepth);

                [branch]
                if (fluidActivity <= 0.001 &&
                    dustReveal <= 0.0001 &&
                    glitchAmount <= 0.001 &&
                    rainIntensity <= 0.001 &&
                    lightningFlash <= 0.0001)
                {
                    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, screenUV);
                }

                [branch]
                if (fluidActivity > 0.001)
                {
                    edgeMask = ComputeVisorEdgeMask(screenUV);
                    edgeMaskResolved = 1.0;
                    float2 flowDirection = float2(
                        _HectonVisorFluidLocalVelocity.x * _HectonVisorFluidLateralStreakStrength,
                        -1.0 - abs(_HectonVisorFluidLocalVelocity.z) * _HectonVisorFluidForwardStretchStrength);
                    float dropletMask = ComputeDropletMask(screenUV, flowDirection, wetness, hullStress);
                    combinedMask = saturate(dropletMask * edgeMask * intensity * (1.0 - thermalMotionCull));
                }

                [branch]
                if (dustReveal > 0.0001)
                {
                    if (edgeMaskResolved < 0.5)
                        edgeMask = ComputeVisorEdgeMask(screenUV);
                    dustMask = ComputeDustMask(screenUV, edgeMask, dustReveal);
                }

                float2 refractionOffset = float2(0.0, 0.0);
                float inverseDirtRefraction = HectonInverseDirtMask(dustMask + combinedMask * 0.15);
                float lowTierMode = max(step(0.5, _HectonVisorFluidLowTier), step(0.5, _HectonVisorFluidHomeostasisFallback));
                [branch]
                if (combinedMask > 0.0001 && lowTierMode < 0.5)
                {
                    float2 baseOffset = ComputeRefractionOffset(screenUV, combinedMask, wetness, hullStress);
                    float2 offsetAbs = abs(baseOffset);
                    float offsetMagnitude = max(offsetAbs.x, offsetAbs.y) + min(offsetAbs.x, offsetAbs.y) * 0.375;
                    float2 offsetNormal = baseOffset * rcp(max(0.0001, offsetMagnitude));
                    float snellStrength = _HectonVisorFluidSnellStrength * (0.65 + wetness * 0.25 + hullStress * 0.25);
                    refractionOffset = baseOffset + HectonSnellUvOffset(
                        offsetNormal,
                        0.72,
                        _HectonWaterDensitySignal,
                        _HectonVisorFluidIorLut,
                        snellStrength,
                        depthRefractionMask,
                        inverseDirtRefraction);
                    refractionOffset = HectonClampUvOffset(refractionOffset, 0.1);
                    refractedUV = saturate(screenUV + refractionOffset * depthRefractionMask * inverseDirtRefraction);
                }

                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, screenUV);
                [branch]
                if (combinedMask > 0.0001 && lowTierMode < 0.5)
                {
                    half3 refractedColor = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, refractedUV).rgb;
                    color.rgb = lerp(color.rgb, refractedColor, (half)saturate(combinedMask * 0.82));
                }
                [branch]
                if ((combinedMask > 0.0001 || hullStress > 0.001) && lowTierMode > 0.5)
                {
                    float chromaDrive = saturate((combinedMask + hullStress * 0.35) * depthRefractionMask * inverseDirtRefraction);
                    float2 chromaOffset = HectonClampUvOffset(float2(0.0012 + hullStress * 0.0022, 0.0) * chromaDrive, 0.004);
                    half red = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, saturate(screenUV + chromaOffset)).r;
                    half blue = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, saturate(screenUV - chromaOffset)).b;
                    color.r = lerp(color.r, red, (half)chromaDrive);
                    color.b = lerp(color.b, blue, (half)chromaDrive);
                }
                [branch]
                if (glitchAmount > 0.001)
                {
                    float2 chromaOffset = float2(
                        (ValueNoise(screenUV * float2(91.0, 47.0) + _Time.y * 3.2) - 0.5) * 0.0035 * glitchAmount,
                        (ValueNoise(screenUV * float2(53.0, 29.0) - _Time.y * 2.4) - 0.5) * 0.0018 * glitchAmount);
                    half red = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(refractedUV + chromaOffset)).r;
                    half blue = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(refractedUV - chromaOffset)).b;
                    color.r = red;
                    color.b = blue;

                    float staticNoise = saturate(ValueNoise(screenUV * _ScreenParams.xy * 0.08 + _Time.y * 18.0) - 0.68) * glitchAmount;
                    color.rgb += staticNoise * half3(0.055, 0.08, 0.1);
                }
                [branch]
                if (combinedMask > 0.0001)
                {
                    half sheen = (half)saturate(combinedMask * (0.08 + wetness * 0.06 + hullStress * 0.05));
                    color.rgb = max(color.rgb, color.rgb + sheen * half3(0.018, 0.025, 0.03));
                }
                float crystalMask = ComputeSaltCrystalMask(screenUV, wetness, inverseDirtRefraction, depthRefractionMask, lowTierMode);
                [branch]
                if (crystalMask > 0.0001)
                {
                    half3 crystalTint = half3(0.13h, 0.17h, 0.18h);
                    color.rgb += crystalTint * (half)crystalMask;
                }
                float siltMask = ComputeSuspendedSiltMask(screenUV, wetness, rainIntensity, inverseDirtRefraction, depthRefractionMask, lowTierMode);
                [branch]
                if (siltMask > 0.0001)
                {
                    half3 siltTint = half3(0.055h, 0.078h, 0.066h);
                    color.rgb += siltTint * (half)siltMask;
                }
                [branch]
                if (dustMask > 0.0001)
                {
                    half3 dustTint = lerp(half3(0.018, 0.022, 0.018), half3(0.11, 0.13, 0.10), saturate(_HectonVisorFluidAmbientLight));
                    color.rgb = lerp(color.rgb, max(color.rgb - dustTint * 0.55h, half3(0.0h, 0.0h, 0.0h)), (half)(dustMask * 0.55));
                    color.rgb += dustTint * (half)(dustMask * 0.18);
                }

                float rainMask = 0.0;
                [branch]
                if (rainIntensity > 0.001)
                {
                    RainOverlayResult rainOverlay = ComputeScreenSpaceRain(screenUV, rainIntensity);
                    rainMask = rainOverlay.mask;
                    color.rgb = lerp(color.rgb, color.rgb * (1.0h - (half)(rainIntensity * 0.08)), (half)rainIntensity);
                    [branch]
                    if (rainMask > 0.0001)
                    {
                        half3 rainRefracted = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(screenUV + rainOverlay.normalOffset)).rgb;
                        half3 rainTint = half3(0.48h, 0.58h, 0.68h);
                        color.rgb = lerp(color.rgb, rainRefracted, (half)(rainMask * 0.36));
                        color.rgb += rainTint * (half)(rainMask * 0.22);
                    }
                }
                [branch]
                if (lightningFlash > 0.0001)
                {
                    float2 lightningCenter = screenUV * 2.0 - 1.0;
                    float whiteVignette = smoothstep(0.18, 1.15, dot(lightningCenter, lightningCenter));
                    color.rgb += (half)lightningFlash * half3(1.0h, 1.0h, 1.0h) * (half)(0.10 + whiteVignette * 0.72);
                }

                float stormVoltage = saturate(rainIntensity * 0.72 + lightningFlash);
                [branch]
                if (stormVoltage > 0.0001)
                {
                    float bandSeed = Hash21(floor(screenUV * float2(11.0, 19.0)));
                    float voltageBand = abs(frac(screenUV.y * 22.0 - _Time.y * 3.1 + bandSeed) - 0.5);
                    float voltagePulse = smoothstep(0.035, 0.0, voltageBand) * stormVoltage;
                    color.rgb += half3(0.025h, 0.045h, 0.065h) * (half)voltagePulse;
                }
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
