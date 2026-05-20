Shader "Hecton8/VFX/CarveDebrisIndirect"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.31, 0.29, 0.24, 1)
        _EdgeColor("Fresh Chip Edge", Color) = (0.66, 0.58, 0.45, 1)
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.18
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "UniversalMaterialType" = "Lit"
        }

        Cull Back
        ZWrite On

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"
            #include "Assets/_Project/Art/Shaders/Hecton_CustomLightProbeGrid.hlsl"

            StructuredBuffer<float4> _CarveDebrisRead;
            StructuredBuffer<float4> _CarveDebrisVelocityRead;
            StructuredBuffer<float4> _DebrisBuffer;
            StructuredBuffer<float4> _DebrisPhysicsBuffer;
            StructuredBuffer<uint> _CarveDebrisVisibleIndices;

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                half _Metallic;
                half _Smoothness;
                float4 _CarveDebrisMaterialParams;
                float4 _CarveDebrisMotionParams;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 viewDirWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                half life : TEXCOORD4;
                half edgeMask : TEXCOORD5;
                half impactMask : TEXCOORD6;
            };

            float Hash11(uint value)
            {
                value ^= value >> 16;
                value *= 2246822519u;
                value ^= value >> 13;
                value *= 3266489917u;
                value ^= value >> 16;
                return (value & 0x00ffffffu) * 0.000000059604644775390625;
            }

            float Triangle01(float value)
            {
                return abs(frac(value) * 2.0 - 1.0);
            }

            float TriangleSigned(float value)
            {
                return Triangle01(value) * 2.0 - 1.0;
            }

            float ResolveShardHeight(float3 positionWS)
            {
                float ridge = Triangle01(dot(positionWS, float3(7.19, 13.37, 5.73)));
                float crystal = Triangle01(dot(positionWS.yzx, float3(17.31, 3.91, 11.57)));
                float strata = Triangle01(dot(positionWS.zxy, float3(4.73, 23.11, 2.97)));
                return saturate(ridge * 0.46 + crystal * 0.36 + strata * 0.18);
            }

            half ResolveCrystalMask(float3 positionWS, half edgeMask, half impactMask)
            {
                float strataA = Triangle01(dot(positionWS, float3(3.17, 5.83, 7.41)));
                float strataB = Triangle01(dot(positionWS.zxy, float3(11.13, 2.91, 13.57)));
                float chipNoise = Triangle01(dot(positionWS.yzx, float3(19.31, 17.17, 6.73)));
                float crystalField = strataA * 0.42 + strataB * 0.34 + chipNoise * 0.18 + edgeMask * 0.28 + impactMask * 0.22;
                return (half)step(0.78, crystalField);
            }

            half3 PerturbHighTierNormal(float3 positionWS, half3 normalWS, half crystalMask)
            {
                float3 grain = float3(
                    Triangle01(dot(positionWS.yz, float2(17.17, 9.31))) - 0.5,
                    Triangle01(dot(positionWS.zx, float2(13.73, 15.97))) - 0.5,
                    Triangle01(dot(positionWS.xy, float2(19.91, 7.83))) - 0.5);
                float strength = lerp(0.12, 0.24, crystalMask);
                return (half3)HectonCoreLitSafeNormalize((float3)normalWS + grain * strength);
            }

            float3 ResolveReliefTangent(float3 normal, float3 viewDir)
            {
                float basisUpMask = 1.0 - step(0.92, abs(normal.y));
                float3 basisAxis = lerp(float3(1.0, 0.0, 0.0), float3(0.0, 1.0, 0.0), basisUpMask);
                float3 basisTangent = HectonCoreLitSafeNormalize(cross(basisAxis, normal));
                float3 viewTangent = viewDir - normal * dot(viewDir, normal);
                float viewTangentMask = step(0.0001, dot(viewTangent, viewTangent));
                return HectonCoreLitSafeNormalize(lerp(basisTangent, viewTangent, viewTangentMask));
            }

            half3 ResolveHighTierReliefNormal(float3 positionWS, half3 normalWS, half3 viewDirWS, half crystalMask, out half saltMask, out half occlusion)
            {
                float3 normal = HectonCoreLitSafeNormalize((float3)normalWS);
                float3 viewDir = HectonCoreLitSafeNormalize((float3)viewDirWS);
                float3 tangent = ResolveReliefTangent(normal, viewDir);
                float3 bitangent = HectonCoreLitSafeNormalize(cross(normal, tangent));
                float viewGrazing = 1.0 - saturate(dot(normal, viewDir));
                float parallaxScale = lerp(0.004, 0.021, viewGrazing);
                float relief = 0.0;

                [unroll]
                for (int tap = 0; tap < 16; tap++)
                {
                    float layer = ((float)tap + 0.5) * 0.0625;
                    float3 samplePosition = positionWS + tangent * (layer * parallaxScale) - normal * (layer * 0.006);
                    float height = ResolveShardHeight(samplePosition);
                    relief += step(layer, height);
                }

                relief *= 0.0625;
                float offset = 0.018;
                float center = ResolveShardHeight(positionWS);
                float dx = ResolveShardHeight(positionWS + tangent * offset) - center;
                float dy = ResolveShardHeight(positionWS + bitangent * offset) - center;
                float salt = saturate(relief * 0.85 + (float)crystalMask * 0.45 + viewGrazing * 0.2);
                saltMask = (half)step(0.64, salt);
                occlusion = (half)lerp(1.0, 0.72, saturate(relief + center * 0.35));
                float3 bumped = normal - tangent * dx * 2.8 - bitangent * dy * 2.8;
                return (half3)HectonCoreLitSafeNormalize(bumped);
            }

            half3 ResolveSaltSubsurfaceFake(float3 positionWS, half3 normalWS, half3 viewDirWS, half saltMask, half crystalMask)
            {
                half grazing = 1.0h - saturate(abs(dot(normalWS, viewDirWS)));
                half innerBand = (half)ResolveShardHeight(positionWS * 1.73 + (float3)normalWS * 0.19);
                half scatter = saturate((saltMask * 0.78h + crystalMask * 0.42h) * grazing * (0.38h + innerBand * 0.62h));
                scatter *= scatter;
                return half3(0.10h, 0.21h, 0.19h) * scatter;
            }

            void BuildDebrisBasis(uint particleIndex, float timeSeconds, float lowTierMask, out float3 rightWS, out float3 upWS, out float3 forwardWS, out float edgeJitter)
            {
                edgeJitter = Hash11(particleIndex ^ 0xC2B2AE35u);
                float3 rawForward = float3(
                    Hash11(particleIndex ^ 0x9E3779B9u) * 2.0 - 1.0,
                    Hash11(particleIndex ^ 0x85EBCA6Bu) * 0.7 - 0.35,
                    edgeJitter * 2.0 - 1.0);
                forwardWS = HectonCoreLitSafeNormalize(rawForward);
                float basisUpMask = 1.0 - step(0.92, abs(forwardWS.y));
                float3 basisUp = lerp(float3(1.0, 0.0, 0.0), float3(0.0, 1.0, 0.0), basisUpMask);
                rightWS = HectonCoreLitSafeNormalize(cross(basisUp, forwardWS));
                upWS = cross(forwardWS, rightWS);
                float angularSpeed = lerp(-8.0, 8.0, Hash11(particleIndex ^ 0x27D4EB2Du));
                float spinPhase = Hash11(particleIndex ^ 0x165667B1u) * 6.28318530718 + timeSeconds * angularSpeed;
                float spinS;
                float spinC;
                [branch]
                if (lowTierMask > 0.5)
                {
                    float lowPhase = spinPhase * 0.15915494309;
                    spinS = TriangleSigned(lowPhase);
                    spinC = TriangleSigned(lowPhase + 0.25);
                    float spinInvLength = rsqrt(max(spinS * spinS + spinC * spinC, 0.0001));
                    spinS *= spinInvLength;
                    spinC *= spinInvLength;
                }
                else
                {
                    sincos(spinPhase, spinS, spinC);
                }

                float3 spunRight = rightWS * spinC + upWS * spinS;
                upWS = upWS * spinC - rightWS * spinS;
                rightWS = spunRight;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                uint particleIndex = _CarveDebrisVisibleIndices[input.instanceID];
                float4 particle = _DebrisBuffer[particleIndex];
                half life = (half)saturate(particle.w);
                float randomScale = lerp(_CarveDebrisMaterialParams.x, _CarveDebrisMaterialParams.y, Hash11(particleIndex));
                float scale = max(0.001, randomScale * saturate(particle.w * 1.8));

                float3 rightWS;
                float3 upWS;
                float3 forwardWS;
                float edgeJitter;
                BuildDebrisBasis(particleIndex, _Time.y, _CarveDebrisMotionParams.z, rightWS, upWS, forwardWS, edgeJitter);

                float3 localPosition = input.positionOS.xyz * scale;
                float3 positionWS = particle.xyz +
                    rightWS * localPosition.x +
                    upWS * localPosition.y +
                    forwardWS * localPosition.z;
                float3 normalWS = HectonCoreLitSafeNormalize(
                    rightWS * input.normalOS.x +
                    upWS * input.normalOS.y +
                    forwardWS * input.normalOS.z);

                output.positionWS = HectonCoreLitSanitizePositionWS(positionWS);
                output.normalWS = (half3)normalWS;
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.viewDirWS = (half3)HectonCoreLitSafeNormalize(GetWorldSpaceViewDir(output.positionWS));
                output.fogFactor = (half)ComputeFogFactor(output.positionCS.z);
                output.life = life;
                output.edgeMask = (half)saturate(abs(input.normalOS.y) * 0.35 + edgeJitter * 0.25);
                output.impactMask = 0.0h;
                [branch]
                if (_CarveDebrisMaterialParams.w > 0.5)
                {
                    float3 velocityWS = _DebrisPhysicsBuffer[particleIndex].xyz;
                    float speedSq = dot(velocityWS, velocityWS);
                    output.impactMask = (half)(saturate(speedSq * 0.055) * saturate(life * 1.2));
                }

                return output;
            }

            half3 EvaluateDebrisLighting(float3 positionWS, float4 positionCS, half3 normalWS, half3 viewDirWS, half3 albedo)
            {
                half caveAmbientFactor = (half)HectonCoreLitEvaluateCaveAmbientFactor(positionWS, normalWS);
                half3 color = H8CustomLightProbeResolveAmbient(positionWS, normalWS, half3(0.015h, 0.025h, 0.035h)) * albedo * caveAmbientFactor;
                Light mainLight;
                half mainShadow = 1.0h;
                [branch]
                if (_CarveDebrisMaterialParams.w > 0.5)
                {
                    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                    mainLight = GetMainLight(shadowCoord);
                    mainShadow = HectonCoreLitResolveMx350ShadowDither((half)mainLight.shadowAttenuation, positionCS);
                }
                else
                {
                    mainLight = GetMainLight();
                }

                half3 lightDir = (half3)HectonCoreLitSafeNormalize(mainLight.direction);
                half nDotL = saturate(dot(normalWS, lightDir));
                half3 halfDir = (half3)HectonCoreLitSafeNormalize(lightDir + viewDirWS);
                half specularBase = saturate(dot(normalWS, halfDir));
                half specular = specularBase * specularBase;
                specular *= specular * lerp(0.035h, 0.18h, _Smoothness);
                color += (albedo * nDotL + specular) * mainLight.color * (mainLight.distanceAttenuation * mainShadow);
                color += HectonCoreLitEvaluateProjectedCausticsScattering(positionWS, normalWS) * albedo;
                return color;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half fadeOut = (half)(1.0 - saturate(input.life * 1.15));
                HectonCoreLitClipDitheredTransparencyFade(fadeOut, input.positionCS);
                half3 normalWS = (half3)HectonCoreLitSafeNormalize(input.normalWS);
                half3 viewDirWS = (half3)HectonCoreLitSafeNormalize(input.viewDirWS);
                half edgeMask = saturate(input.edgeMask + input.impactMask * 0.35h);
                half crystalMask = 0.0h;
                half saltMask = 0.0h;
                half reliefOcclusion = 1.0h;
                [branch]
                if (_CarveDebrisMaterialParams.w > 0.5)
                {
                    crystalMask = ResolveCrystalMask(input.positionWS, edgeMask, input.impactMask);
                    normalWS = PerturbHighTierNormal(input.positionWS, normalWS, crystalMask);
                    normalWS = ResolveHighTierReliefNormal(input.positionWS, normalWS, viewDirWS, crystalMask, saltMask, reliefOcclusion);
                    edgeMask = saturate(edgeMask + crystalMask * 0.45h);
                }

                half3 albedo = lerp(_BaseColor.rgb, _EdgeColor.rgb, edgeMask);
                half3 lit = EvaluateDebrisLighting(input.positionWS, input.positionCS, normalWS, viewDirWS, albedo);
                [branch]
                if (_CarveDebrisMaterialParams.w > 0.5)
                {
                    half rim = 1.0h - saturate(dot(normalWS, viewDirWS));
                    rim *= rim * rim;
                    lit += half3(0.22h, 0.34h, 0.39h) * crystalMask * saturate(rim + input.impactMask * 0.5h);
                    lit += ResolveSaltSubsurfaceFake(input.positionWS, normalWS, viewDirWS, saltMask, crystalMask);
                    lit = lerp(lit, lit * half3(1.14h, 1.22h, 1.18h), saltMask);
                    lit *= reliefOcclusion;
                }

                half3 finalColor = HectonCoreLitApplyNoirFog(lit, input.fogFactor, input.positionWS);
                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }

            ZWrite Off
            ZTest LEqual
            ColorMask RG

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"

            StructuredBuffer<float4> _DebrisBuffer;
            StructuredBuffer<float4> _DebrisPhysicsBuffer;
            StructuredBuffer<uint> _CarveDebrisVisibleIndices;

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                half _Metallic;
                half _Smoothness;
                float4 _CarveDebrisMaterialParams;
                float4 _CarveDebrisMotionParams;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 positionCSNoJitter : POSITION_CS_NO_JITTER;
                float4 previousPositionCSNoJitter : PREV_POSITION_CS_NO_JITTER;
                half coverage : TEXCOORD0;
            };

            float Hash11(uint value)
            {
                value ^= value >> 16;
                value *= 2246822519u;
                value ^= value >> 13;
                value *= 3266489917u;
                value ^= value >> 16;
                return (value & 0x00ffffffu) * 0.000000059604644775390625;
            }

            float Dither01(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float Triangle01(float value)
            {
                return abs(frac(value) * 2.0 - 1.0);
            }

            float TriangleSigned(float value)
            {
                return Triangle01(value) * 2.0 - 1.0;
            }

            float3 DebrisSafeNormalize(float3 value, float3 fallback)
            {
                float lengthSq = dot(value, value);
                float validMask = step(0.000001, lengthSq);
                return lerp(fallback, value * rsqrt(max(lengthSq, 0.000001)), validMask);
            }

            void BuildDebrisBasis(uint particleIndex, float timeSeconds, float lowTierMask, out float3 rightWS, out float3 upWS, out float3 forwardWS)
            {
                float edgeJitter = Hash11(particleIndex ^ 0xC2B2AE35u);
                float3 rawForward = float3(
                    Hash11(particleIndex ^ 0x9E3779B9u) * 2.0 - 1.0,
                    Hash11(particleIndex ^ 0x85EBCA6Bu) * 0.7 - 0.35,
                    edgeJitter * 2.0 - 1.0);
                forwardWS = DebrisSafeNormalize(rawForward, float3(0.0, 1.0, 0.0));
                float basisUpMask = 1.0 - step(0.92, abs(forwardWS.y));
                float3 basisUp = lerp(float3(1.0, 0.0, 0.0), float3(0.0, 1.0, 0.0), basisUpMask);
                rightWS = DebrisSafeNormalize(cross(basisUp, forwardWS), float3(1.0, 0.0, 0.0));
                upWS = cross(forwardWS, rightWS);
                float angularSpeed = lerp(-8.0, 8.0, Hash11(particleIndex ^ 0x27D4EB2Du));
                float spinPhase = Hash11(particleIndex ^ 0x165667B1u) * 6.28318530718 + timeSeconds * angularSpeed;
                float spinS;
                float spinC;
                [branch]
                if (lowTierMask > 0.5)
                {
                    float lowPhase = spinPhase * 0.15915494309;
                    spinS = TriangleSigned(lowPhase);
                    spinC = TriangleSigned(lowPhase + 0.25);
                    float spinInvLength = rsqrt(max(spinS * spinS + spinC * spinC, 0.0001));
                    spinS *= spinInvLength;
                    spinC *= spinInvLength;
                }
                else
                {
                    sincos(spinPhase, spinS, spinC);
                }

                float3 spunRight = rightWS * spinC + upWS * spinS;
                upWS = upWS * spinC - rightWS * spinS;
                rightWS = spunRight;
            }

            float3 TransformDebrisVertex(uint particleIndex, float3 localPositionOS, float3 particlePosition, float scale, float timeSeconds)
            {
                float3 rightWS;
                float3 upWS;
                float3 forwardWS;
                BuildDebrisBasis(particleIndex, timeSeconds, _CarveDebrisMotionParams.z, rightWS, upWS, forwardWS);
                float3 localPosition = localPositionOS * scale;
                return particlePosition +
                    rightWS * localPosition.x +
                    upWS * localPosition.y +
                    forwardWS * localPosition.z;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                uint particleIndex = _CarveDebrisVisibleIndices[input.instanceID];
                float4 particle = _DebrisBuffer[particleIndex];
                float3 velocityWS = _DebrisPhysicsBuffer[particleIndex].xyz;
                float dt = max(_CarveDebrisMotionParams.x, 0.0001);
                float life = saturate(particle.w);
                float randomScale = lerp(_CarveDebrisMaterialParams.x, _CarveDebrisMaterialParams.y, Hash11(particleIndex));
                float scale = max(0.001, randomScale * saturate(particle.w * 1.8));
                float currentTime = _Time.y;
                float previousTime = currentTime - dt;
                float3 currentWorldPosition = TransformDebrisVertex(particleIndex, input.positionOS.xyz, particle.xyz, scale, currentTime);
                float3 previousWorldPosition = TransformDebrisVertex(particleIndex, input.positionOS.xyz, particle.xyz - velocityWS * dt, scale, previousTime);

                output.positionCS = TransformWorldToHClip(currentWorldPosition);
                output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, float4(currentWorldPosition, 1.0));
                output.previousPositionCSNoJitter = mul(_PrevViewProjMatrix, float4(previousWorldPosition, 1.0));
                output.coverage = (half)life;
                ApplyMotionVectorZBias(output.positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half coverage = (half)step(Dither01(input.positionCS.xy), saturate(input.coverage));
                return half4(CalcNdcMotionVectorFromCsPositions(input.positionCSNoJitter, input.previousPositionCSNoJitter), 0.0h, coverage);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
