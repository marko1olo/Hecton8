// File: Shaders/SuitVisor.shader
Shader "NASAPunk/SuitVisor"
{
    Properties
    {
        [Header(Glass)]
        _BaseColor ("Glass Tint", Color) = (0.05, 0.08, 0.06, 0.15)
        _GlassAlpha ("Glass Base Alpha", Range(0, 1)) = 0.12
        _IOR ("Index of Refraction", Range(1.0, 1.5)) = 1.05

        [Header(HUD)]
        _HUD_RenderTexture ("HUD Render Texture", 2D) = "black" {}
        _HUD_Intensity ("HUD Brightness", Range(0, 5)) = 3.0
        _HUD_Color ("HUD Tint", Color) = (0.82, 0.96, 1.0, 0.14)
        _HUD_ScratchBleed ("HUD Scratch Light Bleed", Range(0, 2)) = 0.8
        _HUD_CurveStrength ("HUD Curvature", Range(0, 1)) = 0.45
        _HUD_Scale ("HUD Scale", Range(0.4, 1.2)) = 0.68
        _HUD_EdgeFade ("HUD Edge Fade", Range(0.01, 0.5)) = 0.25
        _HUD_Offset ("HUD Offset", Vector) = (0, 0, 0, 0)

        [Header(Imperfections)]
        _ScratchNormalMap ("Scratch Normal Map", 2D) = "bump" {}
        _ScratchNormalStrength ("Scratch Normal Strength", Range(0, 2)) = 0.6
        _FingerprintTex ("Fingerprint Smudge (R=mask)", 2D) = "black" {}
        _FingerprintStrength ("Fingerprint Strength", Range(0, 1)) = 0.3

        [Header(Water Runoff)]
        _WaterRunoffStrength ("Water Runoff Strength", Range(0, 1)) = 0
        _WaterRunoffSpeed ("Water Runoff Speed", Range(0.5, 4)) = 1.35
        _WaterRunoffDistortion ("Water Runoff Distortion", Range(0, 0.05)) = 0.012
        _WaterDropletDensity ("Water Droplet Density", Range(0, 2)) = 1
        _WaterDropletScale ("Water Droplet Scale", Range(0.5, 12)) = 5
        _WaterRunoffNormalTex ("Water Runoff Normal", 2D) = "bump" {}
        _WaterRunoffNormalStrength ("Water Runoff Normal Strength", Range(0, 2)) = 0.85
        _WaterDropletMaskTex ("Water Droplet Mask", 2D) = "black" {}
        _WaterDropletMaskInfluence ("Water Droplet Mask Influence", Range(0, 1)) = 1

        [Header(Condensation)]
        _CondensationStrength ("Condensation Strength", Range(0, 1)) = 0
        _CondensationDistortion ("Condensation Distortion", Range(0, 0.05)) = 0.008
        _CondensationEdgeExponent ("Condensation Edge Exponent", Range(0.5, 6)) = 2.35
        _CondensationDriftSpeed ("Condensation Drift Speed", Range(0, 2)) = 0.18

        [Header(Frost)]
        _ScreenFrostStrength ("Screen Frost Strength", Range(0, 1)) = 0

        [Header(Refraction Distortion)]
        _DistortionStrength ("Edge Distortion", Range(0, 0.1)) = 0.02
        _DistortionFalloff ("Distortion Edge Falloff", Range(0.5, 5)) = 2.0
        _LensEdgeRefraction ("Lens Edge Refraction", Range(0, 0.08)) = 0.028
        _ChromaticAberration ("Structural Chromatic Aberration", Range(0, 0.02)) = 0
        _StaticNoise ("Structural Static Noise", Range(0, 1)) = 0

        [Header(Fresnel)]
        _FresnelColor ("Fresnel Rim Color", Color) = (0.4, 0.6, 0.8, 1.0)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3.0
        _FresnelIntensity ("Fresnel Intensity", Range(0, 2)) = 0.6

        [Header(Environment Reflection)]
        _EnvReflStrength ("Environment Reflection", Range(0, 1)) = 0.15
        _Smoothness ("Smoothness", Range(0, 1)) = 0.95
        _Metallic ("Metallic", Range(0, 1)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Front

        Pass
        {
            Name "VisorForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex VisorVert
            #pragma fragment VisorFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _GlassAlpha;
                float  _IOR;

                float4 _HUD_RenderTexture_ST;
                float  _HUD_Intensity;
                float4 _HUD_Color;
                float  _HUD_ScratchBleed;
                float  _HUD_CurveStrength;
                float  _HUD_Scale;
                float  _HUD_EdgeFade;
                float4 _HUD_Offset;

                float4 _ScratchNormalMap_ST;
                float  _ScratchNormalStrength;
                float4 _FingerprintTex_ST;
                float  _FingerprintStrength;

                float  _WaterRunoffStrength;
                float  _WaterRunoffSpeed;
                float  _WaterRunoffDistortion;
                float  _WaterDropletDensity;
                float  _WaterDropletScale;
                float4 _WaterRunoffNormalTex_ST;
                float  _WaterRunoffNormalStrength;
                float4 _WaterDropletMaskTex_ST;
                float  _WaterDropletMaskInfluence;

                float  _CondensationStrength;
                float  _CondensationDistortion;
                float  _CondensationEdgeExponent;
                float  _CondensationDriftSpeed;
                float  _ScreenFrostStrength;

                float  _DistortionStrength;
                float  _DistortionFalloff;
                float  _LensEdgeRefraction;
                float  _ChromaticAberration;
                float  _StaticNoise;

                float4 _FresnelColor;
                float  _FresnelPower;
                float  _FresnelIntensity;

                float  _EnvReflStrength;
                float  _Smoothness;
                float  _Metallic;
            CBUFFER_END

            TEXTURE2D(_HUD_RenderTexture); SAMPLER(sampler_HUD_RenderTexture);
            TEXTURE2D(_ScratchNormalMap); SAMPLER(sampler_ScratchNormalMap);
            TEXTURE2D(_FingerprintTex); SAMPLER(sampler_FingerprintTex);
            TEXTURE2D(_WaterRunoffNormalTex); SAMPLER(sampler_WaterRunoffNormalTex);
            TEXTURE2D(_WaterDropletMaskTex); SAMPLER(sampler_WaterDropletMaskTex);
            TEXTURE2D(_CameraOpaqueTexture); SAMPLER(sampler_CameraOpaqueTexture);
            float4 _SonarRevealOriginWS;
            float4 _SonarRevealWaveParams;
            float _SonarWaveFront;
            float4 _SonarGridParams0;
            float4 _SonarGridHardColor;
            float4 _SonarGridOrganicColor;
            float4 _SonarGridAbyssalColor;
            float4 _SonarRevealContacts[24];
            float4 _SonarRevealContactMeta[24];
            float _SonarRevealExpireTime;
            int _SonarRevealContactCount;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 tangentWS   : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float4 screenPos   : TEXCOORD5;
                float  fogCoord    : TEXCOORD6;
                float3 viewDirWS   : TEXCOORD7;
                float3 positionOS  : TEXCOORD8;
            };

            float3 UnpackScaledNormal(float4 packedNormal, float scale)
            {
                float3 n;
                n.xy = (packedNormal.rg * 2.0 - 1.0) * scale;
                n.z = sqrt(max(0.0, 1.0 - dot(n.xy, n.xy)));
                return n;
            }

            float EdgeMask(float2 uv, float falloff)
            {
                float2 centered = uv * 2.0 - 1.0;
                return pow(saturate(length(centered)), falloff);
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float SampleRgbMask(float3 rgb)
            {
                return saturate(dot(rgb, float3(0.2126, 0.7152, 0.0722)));
            }

            float ComputeProceduralScratchMask(float2 uv)
            {
                float2 scratchUV = uv * float2(96.0, 52.0);
                float2 coarseCell = floor(scratchUV * float2(0.22, 0.12));

                float gateA = step(0.72, Hash21(coarseCell + 0.17));
                float gateB = step(0.78, Hash21(coarseCell * 1.23 + 0.61));

                float lineA = abs(frac(scratchUV.x + scratchUV.y * 0.18) - 0.5);
                float lineB = abs(frac(scratchUV.x * 0.74 - scratchUV.y * 0.22 + 0.31) - 0.5);

                float scratchA = (1.0 - smoothstep(0.010, 0.032, lineA)) * gateA;
                float scratchB = (1.0 - smoothstep(0.012, 0.038, lineB)) * gateB;

                float edgeWear = smoothstep(0.10, 0.82, EdgeMask(uv, 1.4));
                float topBias = smoothstep(0.18, 0.96, uv.y);
                return saturate((scratchA * 0.72 + scratchB * 0.58) * edgeWear * topBias * 0.32);
            }

            float ComputeProceduralSmudgeMask(float2 uv)
            {
                float2 smudgeUV = uv * float2(5.6, 7.4);
                float2 cellId = floor(smudgeUV);
                float2 cell = frac(smudgeUV) - 0.5;

                float seedA = Hash21(cellId + 0.37);
                float seedB = Hash21(cellId * 1.29 + 0.91);

                float smearA = step(0.64, seedA)
                    * (1.0 - smoothstep(0.10, 0.38, length(cell * float2(1.7, 0.7))));
                float smearB = step(0.76, seedB)
                    * (1.0 - smoothstep(0.08, 0.34, length((cell + float2(seedA - 0.5, seedB - 0.5) * 0.22) * float2(0.9, 1.5))));

                float edgeBias = smoothstep(0.08, 0.74, EdgeMask(uv, 1.1));
                float topBias = smoothstep(0.08, 0.86, uv.y);
                return saturate((smearA * 0.66 + smearB * 0.52) * edgeBias * topBias * 0.28);
            }

            float ComputeProceduralFrostMask(float2 uv, float edgeDist, float timeValue)
            {
                float frostEdge = pow(saturate(edgeDist), 1.42);
                float2 baseUv = uv * float2(11.5, 17.0) + float2(timeValue * 0.004, timeValue * -0.006);
                float2 sampleUv = TRANSFORM_TEX(baseUv, _FingerprintTex);
                float4 packedNoise = SAMPLE_TEXTURE2D(_FingerprintTex, sampler_FingerprintTex, sampleUv);
                float crystalSeed = packedNoise.r;
                float grainSeed = packedNoise.g;
                float shardBands = step(0.68, frac(baseUv.x * 6.4 + baseUv.y * 2.7 + grainSeed * 1.9));
                float shardRibs = step(0.74, frac(baseUv.x * 2.1 - baseUv.y * 5.3 + crystalSeed * 2.7));
                float lobe = 1.0 - smoothstep(0.18, 0.46, length((frac(baseUv) - 0.5) * float2(1.0, 1.7)));
                float crystalMask = saturate(shardBands * 0.55 + shardRibs * 0.45 + lobe * 0.3 + crystalSeed * 0.35 - 0.62);
                float topBias = smoothstep(0.06, 0.94, uv.y);
                return saturate(crystalMask * frostEdge * topBias);
            }

            float ComputeWaterRunoffMask(float2 uv, float time)
            {
                float2 scaledUV = uv * float2(
                    max(1.0, _WaterDropletScale),
                    max(1.0, _WaterDropletScale * 1.75));
                float2 cellId = floor(scaledUV);
                float2 cellUV = frac(scaledUV) - 0.5;
                float seed = Hash21(cellId);
                float activeCell = step(0.42, seed) * saturate(_WaterDropletDensity);

                float travel = frac(time * (0.18 + seed * 0.47) + seed);
                cellUV.y += (travel - 0.5) * 1.15;
                cellUV.x += (seed - 0.5) * 0.28;

                float radius = lerp(0.14, 0.28, seed);
                float droplet = (1.0 - smoothstep(radius * 0.65, radius, length(cellUV))) * activeCell;
                float streakWidth = lerp(0.02, 0.05, seed);
                float streak = (1.0 - smoothstep(streakWidth, streakWidth * 3.0, abs(cellUV.x)))
                    * smoothstep(0.45, -0.35, cellUV.y)
                    * activeCell;
                float topBias = smoothstep(0.15, 1.0, uv.y);
                return saturate((droplet * 0.85 + streak * 0.75) * topBias);
            }

            float2 ComputeCurvedHudUV(float2 meshUV, float3 positionOS, out float edgeFade)
            {
                float2 visorCoord = positionOS.xy * (2.0 * _HUD_Scale);
                float visorRadius = length(visorCoord);
                float visorRadiusClamped = saturate(visorRadius);
                float r2 = visorRadiusClamped * visorRadiusClamped;
                float r4 = r2 * r2;
                float curveAmount = 1.0 + r2 * _HUD_CurveStrength + r4 * _HUD_CurveStrength * 0.5;
                float2 curvedCoord = visorCoord * curveAmount;
                float2 curvedUV = curvedCoord * 0.5 + 0.5 + _HUD_Offset.xy;

                float2 fromCenter = curvedUV - 0.5;
                float ellipseR = length(fromCenter * float2(1.0, 0.85));
                float fadeStart = max(0.01, 1.0 - _HUD_EdgeFade);
                edgeFade = 1.0 - smoothstep(fadeStart * 0.7, fadeStart, ellipseR);
                return curvedUV;
            }

            float3 SampleSceneWorldPosition(float2 screenUV, out float rawDepth, out float validMask)
            {
                rawDepth = SampleSceneDepth(screenUV);
#if UNITY_REVERSED_Z
                validMask = step(0.0001, rawDepth);
#else
                validMask = step(rawDepth, 0.9999);
#endif
                return ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
            }

            float ComputeSonarContourMask(float2 screenUV, float rawDepth)
            {
                float2 texel = 1.0 / _ScaledScreenParams.xy;
                float depthDx = SampleSceneDepth(screenUV + float2(texel.x, 0.0));
                float depthDy = SampleSceneDepth(screenUV + float2(0.0, texel.y));
                float depthGradient = abs(depthDx - rawDepth) + abs(depthDy - rawDepth);
                return saturate(depthGradient * max(1.0, _SonarGridParams0.w) * 180.0);
            }

            float ComputeSonarGridMask(float3 sceneWorldPos)
            {
                float lineScale = max(0.1, _SonarGridParams0.y);
                float lineWidth = max(0.001, _SonarGridParams0.z);
                float2 cell = abs(frac(sceneWorldPos.xz * lineScale) - 0.5);
                return 1.0 - smoothstep(lineWidth, lineWidth * 2.5, min(cell.x, cell.y));
            }

            Varyings VisorVert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrmInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = nrmInputs.normalWS;
                OUT.tangentWS = nrmInputs.tangentWS;
                OUT.bitangentWS = nrmInputs.bitangentWS;
                OUT.uv = IN.uv;
                OUT.screenPos = ComputeScreenPos(posInputs.positionCS);
                OUT.fogCoord = ComputeFogFactor(posInputs.positionCS.z);
                OUT.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            float4 VisorFrag(Varyings IN) : SV_Target
            {
                float2 scratchUV = TRANSFORM_TEX(IN.uv, _ScratchNormalMap);
                float4 scratchPacked = SAMPLE_TEXTURE2D(_ScratchNormalMap, sampler_ScratchNormalMap, scratchUV);
                float3 scratchNormalTS = UnpackScaledNormal(scratchPacked, _ScratchNormalStrength);
                float scratchTextureMask = length(scratchNormalTS.xy);
                float proceduralScratchMask = ComputeProceduralScratchMask(IN.uv);
                float2 proceduralScratchXY = clamp(
                    float2(ddx(proceduralScratchMask), ddy(proceduralScratchMask)) * (_ScratchNormalStrength * 12.0),
                    -0.22,
                    0.22);
                float proceduralScratchBlend = saturate(1.0 - scratchTextureMask * 3.0);
                scratchNormalTS.xy = clamp(
                    scratchNormalTS.xy + proceduralScratchXY * proceduralScratchBlend,
                    -0.48,
                    0.48);
                scratchNormalTS.z = sqrt(max(0.0, 1.0 - dot(scratchNormalTS.xy, scratchNormalTS.xy)));
                float scratchMask = saturate(max(scratchTextureMask, proceduralScratchMask));

                float3x3 TBN = float3x3(
                    normalize(IN.tangentWS),
                    normalize(IN.bitangentWS),
                    normalize(IN.normalWS)
                );
                float3 normalWS = normalize(mul(scratchNormalTS, TBN));

                float2 fpUV = TRANSFORM_TEX(IN.uv, _FingerprintTex);
                float fingerprintSample = SAMPLE_TEXTURE2D(_FingerprintTex, sampler_FingerprintTex, fpUV).r;
                float proceduralSmudgeMask = ComputeProceduralSmudgeMask(IN.uv);
                float fingerprint = max(fingerprintSample, proceduralSmudgeMask) * _FingerprintStrength;
                float smudgeOpacity = fingerprint * 0.4;

                float3 viewDir = normalize(IN.viewDirWS);
                float NdotV = saturate(dot(normalWS, viewDir));
                float fresnel = pow(1.0 - NdotV, _FresnelPower) * _FresnelIntensity;
                float3 fresnelColor = _FresnelColor.rgb * fresnel;

                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float edgeDist = EdgeMask(IN.uv, _DistortionFalloff);
                float2 distortionOffset = scratchNormalTS.xy * _DistortionStrength;
                distortionOffset += edgeDist * normalWS.xy * _DistortionStrength * 0.5;
                float2 radialScreenOffset = screenUV - 0.5;
                float radialMagnitude = saturate(length(radialScreenOffset) * 1.75);
                distortionOffset += radialScreenOffset * (radialMagnitude * radialMagnitude) * _LensEdgeRefraction;

                float runoffMask = 0.0;
                if (_WaterRunoffStrength > 0.001)
                {
                    float runoffTime = _Time.y * _WaterRunoffSpeed;
                    float proceduralRunoffMask = ComputeWaterRunoffMask(IN.uv, runoffTime);
                    float2 dropletMaskUV = TRANSFORM_TEX(IN.uv + float2(0.0, runoffTime * -0.035), _WaterDropletMaskTex);
                    float authoredDropletMask = SampleRgbMask(
                        SAMPLE_TEXTURE2D(_WaterDropletMaskTex, sampler_WaterDropletMaskTex, dropletMaskUV).rgb);
                    runoffMask = lerp(
                        proceduralRunoffMask,
                        max(proceduralRunoffMask, authoredDropletMask),
                        saturate(_WaterDropletMaskInfluence));
                    runoffMask = saturate(runoffMask * _WaterRunoffStrength * (1.0 + fingerprint * 0.5));

                    float2 runoffNormalUV = TRANSFORM_TEX(IN.uv + float2(0.0, runoffTime * -0.08), _WaterRunoffNormalTex);
                    float4 runoffNormalPacked = SAMPLE_TEXTURE2D(_WaterRunoffNormalTex, sampler_WaterRunoffNormalTex, runoffNormalUV);
                    float3 runoffNormalTS = UnpackScaledNormal(runoffNormalPacked, _WaterRunoffNormalStrength);
                    float2 runoffDistortion = (scratchNormalTS.xy * 0.35 + runoffNormalTS.xy) * _WaterRunoffDistortion;
                    distortionOffset += runoffDistortion * runoffMask;
                    distortionOffset.y -= runoffMask * _WaterRunoffDistortion * (0.35 + abs(runoffNormalTS.y) * 0.25);
                }

                float condensationStrength = saturate(_CondensationStrength);
                float condensationMask = 0.0;
                if (condensationStrength > 0.001)
                {
                    float condensationTime = _Time.y * _CondensationDriftSpeed;
                    float2 condensationUV = fpUV + float2(condensationTime * 0.021, condensationTime * -0.047);
                    float condensationTextureMask = SAMPLE_TEXTURE2D(_FingerprintTex, sampler_FingerprintTex, condensationUV).r;
                    float condensationProceduralMask = ComputeProceduralSmudgeMask(
                        IN.uv + float2(condensationTime * 0.012, condensationTime * -0.018));
                    float condensationEdge = pow(saturate(edgeDist), max(0.5, _CondensationEdgeExponent));
                    condensationMask = saturate(
                        max(condensationTextureMask, condensationProceduralMask * 1.2)
                        * condensationEdge
                        * condensationStrength);

                    float2 condensationDistortion = (scratchNormalTS.xy * 0.4 + normalWS.xy * 0.25) * _CondensationDistortion;
                    distortionOffset += condensationDistortion * condensationMask;
                    distortionOffset.y -= condensationMask * _CondensationDistortion * 0.28;
                }

                float frostStrength = saturate(_ScreenFrostStrength);
                float frostMask = 0.0;
                if (frostStrength > 0.001)
                {
                    float authoredFrost = ComputeProceduralFrostMask(IN.uv, edgeDist, _Time.y);
                    frostMask = saturate(authoredFrost * frostStrength);
                    distortionOffset += (scratchNormalTS.xy * 0.2 + radialScreenOffset * 0.06) * frostMask * 0.006;
                }

                float2 refractedUV = screenUV + distortionOffset;
                float staticNoise = (Hash21(floor(screenUV * _ScaledScreenParams.xy * 0.35 + _Time.y * 32.0)) - 0.5) * 2.0;
                float2 chromaOffset = radialScreenOffset * _ChromaticAberration;
                float3 sceneColor = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, refractedUV).rgb;
                if (_ChromaticAberration > 0.0001)
                {
                    sceneColor.r = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, refractedUV + chromaOffset).r;
                    sceneColor.b = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, refractedUV - chromaOffset).b;
                }

                float sonarSceneDepth;
                float sonarDepthValid;
                float3 sonarSceneWorldPos = SampleSceneWorldPosition(screenUV, sonarSceneDepth, sonarDepthValid);
                float sonarOverlayMask = 0.0;
                float3 sonarOverlayColor = 0.0;

                float sonarGridIntensity = saturate(_SonarGridParams0.x);
                float sonarWaveSpeed = max(0.01, _SonarRevealWaveParams.y);
                float sonarFadeDuration = max(0.05, _SonarRevealWaveParams.z);
                float sonarContactLifetimeMask = step(
                    _Time.y,
                    _SonarRevealWaveParams.x + (_SonarRevealOriginWS.w / sonarWaveSpeed) + sonarFadeDuration);
                if (sonarGridIntensity > 0.0001 && sonarDepthValid > 0.5 && sonarContactLifetimeMask > 0.5)
                {
                    float distanceToOrigin = distance(sonarSceneWorldPos, _SonarRevealOriginWS.xyz);
                    float timeSinceArrival = _Time.y - (_SonarRevealWaveParams.x + distanceToOrigin / sonarWaveSpeed);
                    float arrivalMask = step(0.0, timeSinceArrival);
                    float terrainFade = arrivalMask * saturate(1.0 - (timeSinceArrival / sonarFadeDuration));
                    float waveRadius = max(0.0, _SonarWaveFront);
                    float waveBandWidth = lerp(6.0, 2.0, saturate(_SonarRevealWaveParams.w));
                    float waveFront = 1.0 - smoothstep(waveBandWidth, waveBandWidth * 2.0, abs(distanceToOrigin - waveRadius));
                    float contourMask = ComputeSonarContourMask(screenUV, sonarSceneDepth);
                    float gridMask = ComputeSonarGridMask(sonarSceneWorldPos);
                    float activeTerrainMask = step(_Time.y, _SonarRevealExpireTime);
                    float terrainGrid = gridMask * max(contourMask, 0.14) * max(terrainFade, waveFront * 0.85) * activeTerrainMask;

                    float hardAccum = terrainGrid * 0.55;
                    float organicAccum = terrainGrid * 0.18;
                    float abyssalAccum = 0.0;

                    [unroll(24)]
                    for (int contactIndex = 0; contactIndex < 24; contactIndex++)
                    {
                        float active = step((float)contactIndex + 0.5, (float)_SonarRevealContactCount);
                        float contactArrivalTime = _SonarRevealWaveParams.x + _SonarRevealContacts[contactIndex].w;
                        float contactTimeSinceArrival = _Time.y - contactArrivalTime;
                        float contactFade = active * step(0.0, contactTimeSinceArrival) * saturate(1.0 - (contactTimeSinceArrival / sonarFadeDuration));
                        float contactRadius = max(0.25, _SonarRevealContactMeta[contactIndex].z);
                        float contactDistance = distance(sonarSceneWorldPos, _SonarRevealContacts[contactIndex].xyz);
                        float contactMask = (1.0 - smoothstep(contactRadius * 0.55, contactRadius, contactDistance)) * contactFade;
                        float abyssalMask = step(0.5, _SonarRevealContactMeta[contactIndex].w);
                        hardAccum += contactMask * _SonarRevealContactMeta[contactIndex].x * (1.0 - abyssalMask);
                        organicAccum += contactMask * _SonarRevealContactMeta[contactIndex].y * (1.0 - abyssalMask);
                        abyssalAccum += contactMask * abyssalMask * 1.15;
                    }

                    float hardStrength = saturate(hardAccum);
                    float organicStrength = saturate(organicAccum);
                    float abyssalStrength = saturate(abyssalAccum);
                    sonarOverlayColor =
                        (_SonarGridHardColor.rgb * hardStrength) +
                        (_SonarGridOrganicColor.rgb * organicStrength) +
                        (_SonarGridAbyssalColor.rgb * abyssalStrength);
                    sonarOverlayMask = sonarGridIntensity * saturate(max(max(hardStrength, organicStrength), abyssalStrength) + waveFront * contourMask * 0.4);
                }

                float hudEdgeFade;
                float2 hudUV = ComputeCurvedHudUV(IN.uv, IN.positionOS, hudEdgeFade);
                hudUV = TRANSFORM_TEX(hudUV, _HUD_RenderTexture);
                float2 hudDistortedUV = hudUV + distortionOffset * 0.3;

                float4 hudSample = SAMPLE_TEXTURE2D(_HUD_RenderTexture, sampler_HUD_RenderTexture, hudDistortedUV);
                float2 insideRT = step(0.0, hudDistortedUV) * step(hudDistortedUV, 1.0);
                float rtMask = insideRT.x * insideRT.y;
                float hudAlpha = hudSample.a * hudEdgeFade * rtMask;
                float hudTintStrength = saturate(_HUD_Color.a);
                float3 hudColor = lerp(hudSample.rgb, hudSample.rgb * _HUD_Color.rgb, hudTintStrength) * _HUD_Intensity;

                float wetImperfectionBoost = 1.0 + runoffMask * 0.9;
                float boostedFingerprint = saturate(fingerprint * wetImperfectionBoost);
                float scratchBleed = scratchMask * _HUD_ScratchBleed * hudAlpha * wetImperfectionBoost;
                float3 hudScratchGlow = hudColor * scratchBleed * 0.5;
                float3 hudFingerprintGlow = hudColor * boostedFingerprint * hudAlpha * 0.3;

                float3 reflectDir = reflect(-viewDir, normalWS);
                float3 envRefl = GlossyEnvironmentReflection(reflectDir, _Smoothness, _EnvReflStrength);

                Light mainLight = GetMainLight();
                float3 specular = mainLight.color * pow(
                    saturate(dot(reflect(-mainLight.direction, normalWS), viewDir)),
                    128.0 * _Smoothness) * 0.3;
                float wetHazeMask = saturate(runoffMask * (0.45 + proceduralSmudgeMask * 0.55) + scratchMask * runoffMask * 0.35);
                float condensationHazeMask = saturate(condensationMask * (0.52 + proceduralSmudgeMask * 0.3 + scratchMask * 0.18));
                float3 runoffSheen = (fresnelColor * 0.55 + specular * 0.25 + mainLight.color * 0.04) * runoffMask;

                float3 finalColor = 0.0;
                finalColor += sceneColor * (1.0 - _BaseColor.a);
                finalColor += _BaseColor.rgb * _GlassAlpha;
                finalColor = lerp(finalColor, finalColor * 0.85 + 0.02, smudgeOpacity);
                finalColor += envRefl;
                finalColor += specular;
                finalColor += fresnelColor;
                finalColor += hudColor * hudAlpha;
                finalColor += hudScratchGlow;
                finalColor += hudFingerprintGlow;
                finalColor = lerp(finalColor, sceneColor * 0.86 + 0.04 + fresnelColor * 0.2, runoffMask * 0.35);
                finalColor = lerp(finalColor, finalColor * 0.78 + sceneColor * 0.18 + fresnelColor * 0.12, wetHazeMask * 0.22);
                finalColor = lerp(
                    finalColor,
                    finalColor * 0.74 + sceneColor * 0.14 + fresnelColor * 0.18 + float3(0.055, 0.07, 0.075),
                    condensationHazeMask * 0.42);
                finalColor = lerp(
                    finalColor,
                    finalColor * 0.55 + float3(0.72, 0.78, 0.82) * 0.38 + sceneColor * 0.08,
                    frostMask * 0.65);
                finalColor += runoffSheen;
                finalColor += sonarOverlayColor * sonarOverlayMask;
                finalColor += staticNoise * (_StaticNoise * 0.045);

                float finalAlpha = _GlassAlpha
                    + hudAlpha * 0.9
                    + fresnel * 0.4
                    + smudgeOpacity
                    + scratchBleed * 0.2
                    + runoffMask * 0.18
                    + wetHazeMask * 0.08
                    + condensationHazeMask * 0.16
                    + frostMask * 0.22
                    + sonarOverlayMask * 0.08;
                finalAlpha = saturate(finalAlpha);

                finalColor = MixFog(finalColor, IN.fogCoord);
                return float4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
