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
        _HUD_Color ("HUD Tint", Color) = (0.2, 1.0, 0.3, 1.0)
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

        [Header(Refraction Distortion)]
        _DistortionStrength ("Edge Distortion", Range(0, 0.1)) = 0.02
        _DistortionFalloff ("Distortion Edge Falloff", Range(0.5, 5)) = 2.0

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

                float  _DistortionStrength;
                float  _DistortionFalloff;

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
            TEXTURE2D(_CameraOpaqueTexture); SAMPLER(sampler_CameraOpaqueTexture);

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
                float scratchMask = length(scratchNormalTS.xy);

                float3x3 TBN = float3x3(
                    normalize(IN.tangentWS),
                    normalize(IN.bitangentWS),
                    normalize(IN.normalWS)
                );
                float3 normalWS = normalize(mul(scratchNormalTS, TBN));

                float2 fpUV = TRANSFORM_TEX(IN.uv, _FingerprintTex);
                float fingerprint = SAMPLE_TEXTURE2D(_FingerprintTex, sampler_FingerprintTex, fpUV).r * _FingerprintStrength;
                float smudgeOpacity = fingerprint * 0.4;

                float3 viewDir = normalize(IN.viewDirWS);
                float NdotV = saturate(dot(normalWS, viewDir));
                float fresnel = pow(1.0 - NdotV, _FresnelPower) * _FresnelIntensity;
                float3 fresnelColor = _FresnelColor.rgb * fresnel;

                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float edgeDist = EdgeMask(IN.uv, _DistortionFalloff);
                float2 distortionOffset = scratchNormalTS.xy * _DistortionStrength;
                distortionOffset += edgeDist * normalWS.xy * _DistortionStrength * 0.5;

                float runoffMask = 0.0;
                if (_WaterRunoffStrength > 0.001)
                {
                    float runoffTime = _Time.y * _WaterRunoffSpeed;
                    runoffMask = ComputeWaterRunoffMask(IN.uv, runoffTime);
                    runoffMask = saturate(runoffMask * _WaterRunoffStrength * (1.0 + fingerprint * 0.5));
                    distortionOffset += scratchNormalTS.xy * runoffMask * _WaterRunoffDistortion;
                    distortionOffset.y -= runoffMask * _WaterRunoffDistortion * 0.5;
                }

                float2 refractedUV = screenUV + distortionOffset;
                float3 sceneColor = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, refractedUV).rgb;

                float hudEdgeFade;
                float2 hudUV = ComputeCurvedHudUV(IN.uv, IN.positionOS, hudEdgeFade);
                hudUV = TRANSFORM_TEX(hudUV, _HUD_RenderTexture);
                float2 hudDistortedUV = hudUV + distortionOffset * 0.3;

                float4 hudSample = SAMPLE_TEXTURE2D(_HUD_RenderTexture, sampler_HUD_RenderTexture, hudDistortedUV);
                float2 insideRT = step(0.0, hudDistortedUV) * step(hudDistortedUV, 1.0);
                float rtMask = insideRT.x * insideRT.y;
                float hudAlpha = hudSample.a * hudEdgeFade * rtMask;
                float3 hudColor = hudSample.rgb * _HUD_Color.rgb * _HUD_Intensity;

                float scratchBleed = scratchMask * _HUD_ScratchBleed * hudAlpha;
                float3 hudScratchGlow = hudColor * scratchBleed * 0.5;
                float3 hudFingerprintGlow = hudColor * fingerprint * hudAlpha * 0.3;

                float3 reflectDir = reflect(-viewDir, normalWS);
                float3 envRefl = GlossyEnvironmentReflection(reflectDir, _Smoothness, _EnvReflStrength);

                Light mainLight = GetMainLight();
                float3 specular = mainLight.color * pow(
                    saturate(dot(reflect(-mainLight.direction, normalWS), viewDir)),
                    128.0 * _Smoothness) * 0.3;

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

                float finalAlpha = _GlassAlpha
                    + hudAlpha * 0.9
                    + fresnel * 0.4
                    + smudgeOpacity
                    + scratchBleed * 0.2
                    + runoffMask * 0.18;
                finalAlpha = saturate(finalAlpha);

                finalColor = MixFog(finalColor, IN.fogCoord);
                return float4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
