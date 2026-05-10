// ============================================================================
// HECTON-8 -- Hecton_AegirHazeOverlay.shader
// Foreground atmospheric haze veil for the gas giant.
// Purpose: keep the gas giant huge and readable while softening it through
// atmosphere, without laying low-quality cloud shapes over the planet itself.
// ============================================================================

Shader "HECTON/Sky/Hecton_AegirHazeOverlay"
{
    Properties
    {
        [HDR] _SkyColorZenith ("Zenith Color", Color) = (0.05, 0.08, 0.25, 1)
        [HDR] _SkyColorHorizon ("Horizon Color", Color) = (0.4, 0.35, 0.5, 1)
        [HDR] _SkyColorNadir ("Nadir Color", Color) = (0.02, 0.03, 0.08, 1)
        [HDR] _HazeColor ("Haze Color", Color) = (0.5, 0.45, 0.55, 1)
        _HazeIntensity ("Haze Intensity", Range(0, 3)) = 1.5
        _HazeFalloff ("Haze Falloff", Range(0.5, 8)) = 3.0
        _HazeSunTintStrength ("Haze Sun Tint", Range(0, 2)) = 0.8
        _OverlayAlpha ("Overlay Alpha", Range(0, 1)) = 0.42
        _DiscBaseVeil ("Disc Base Veil", Range(0, 1)) = 0.18
        _DiscEdgeVeil ("Disc Edge Veil", Range(0, 2)) = 0.55
        _DiscEdgePower ("Disc Edge Power", Range(0.5, 8)) = 2.5
        _OverlayDiscInnerDot ("Overlay Disc Inner Dot", Range(-1, 1)) = 0.995
        _OverlayDiscOuterDot ("Overlay Disc Outer Dot", Range(-1, 1)) = 0.985
        _GameTime ("Game Time (unused sync)", Float) = 0.0
        _NightBlend ("Night Blend (unused sync)", Range(0, 1)) = 0.0
        _StarIntensity ("Star Intensity (unused sync)", Range(0, 10)) = 0.0
        _SunElevation ("Sun Elevation (unused sync)", Range(-1, 1)) = 0.0
        _EclipseOcclusion ("Eclipse Occlusion (unused sync)", Range(0, 1)) = 0.0
        _DitherScale ("Dither Scale", Range(1, 8)) = 4.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "UniversalMaterialType" = "Unlit"
            "ForceNoShadowCasting" = "True"
            "IgnoreProjector" = "True"
            "PreviewType" = "Skybox"
        }

        LOD 50

        Pass
        {
            Name "AegirHazeOverlay"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex OverlayVert
            #pragma fragment OverlayFrag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON
            #pragma skip_variants POINT POINT_COOKIE SHADOWS_CUBE
            #pragma skip_variants _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SkyColorZenith;
                half4 _SkyColorHorizon;
                half4 _SkyColorNadir;
                half4 _HazeColor;
                half _HazeIntensity;
                half _HazeFalloff;
                half _HazeSunTintStrength;
                half _OverlayAlpha;
                half _DiscBaseVeil;
                half _DiscEdgeVeil;
                half _DiscEdgePower;
                half _OverlayDiscInnerDot;
                half _OverlayDiscOuterDot;
                float _GameTime;
                float _NightBlend;
                float _StarIntensity;
                float _SunElevation;
                float _EclipseOcclusion;
                half _DitherScale;
            CBUFFER_END

            float4 _SunDirection;
            float4 _AegirDirection;

            static const float3 FALLBACK_SUN_DIR = float3(0.57735, 0.57735, 0.57735);
            static const float3 FALLBACK_AEGIR_DIR = float3(0.0, 0.93633, -0.35112);
            static const float DIR_THRESHOLD = 0.001;
            static const float HORIZON_CLAMP = 0.08;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS    : SV_POSITION;
                float3 viewDirWS     : TEXCOORD0;
                half   horizonFactor : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 SafeNormalizeDir(float3 v, float3 fallback)
            {
                float lenSq = dot(v, v);
                return (lenSq < DIR_THRESHOLD * DIR_THRESHOLD) ? fallback : v * rsqrt(lenSq);
            }

            float ResolveInterleavedGradientNoise(float2 positionCS)
            {
                float2 pixel = floor(positionCS);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            half FastMaskPower(half value, half power)
            {
                half v2 = value * value;
                half v4 = v2 * v2;
                half low = lerp(value, v2, saturate(power - 1.0h));
                half high = lerp(v2, v4, saturate((power - 2.0h) * 0.5h));
                return power < 2.0h ? low : high;
            }

            float ResolveIgnThreshold(float4 positionCS)
            {
                float2 pixel = floor(positionCS.xy / max(_DitherScale, 1.0h));
                return ResolveInterleavedGradientNoise(pixel);
            }

            Varyings OverlayVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;

                float3 viewDirWS = pos.positionWS - _WorldSpaceCameraPos;
                output.viewDirWS = viewDirWS;

                float3 normalizedView = SafeNormalizeDir(viewDirWS, float3(0.0, 1.0, 0.0));
                output.horizonFactor = normalizedView.y;
                return output;
            }

            half4 OverlayFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 V = SafeNormalizeDir(input.viewDirWS, float3(0.0, 1.0, 0.0));
                float3 sunDir = SafeNormalizeDir(_SunDirection.xyz, FALLBACK_SUN_DIR);
                float3 aegirDir = SafeNormalizeDir(_AegirDirection.xyz, FALLBACK_AEGIR_DIR);

                half horizonFactor = input.horizonFactor;
                half zenithMask = saturate(horizonFactor);
                half nadirMask = saturate(-horizonFactor);
                half horizonSkyMask = 1.0h - zenithMask - nadirMask;

                half3 skyGradient = _SkyColorZenith.rgb * zenithMask
                                  + _SkyColorHorizon.rgb * horizonSkyMask
                                  + _SkyColorNadir.rgb * nadirMask;

                half hazeRaw = 1.0h - abs(horizonFactor);
                half horizonVeil = saturate(FastMaskPower(hazeRaw, _HazeFalloff) * _HazeIntensity);

                half sunViewDot = saturate(dot(V, -sunDir));
                half3 hazeSunTint = lerp(half3(1.0h, 1.0h, 1.0h), half3(1.0h, 0.7h, 0.3h), sunViewDot * _HazeSunTintStrength);

                half aegirDot = saturate(dot(V, aegirDir));
                half discMask = smoothstep(_OverlayDiscOuterDot, _OverlayDiscInnerDot, aegirDot);
                half discRange = max(0.0001h, _OverlayDiscInnerDot - _OverlayDiscOuterDot);
                half discRadial = saturate((aegirDot - _OverlayDiscOuterDot) / discRange);
                half edgeVeil = FastMaskPower(1.0h - discRadial, _DiscEdgePower);

                half baseVeil = _DiscBaseVeil
                              + horizonVeil * 0.65h
                              + _NightBlend * 0.10h
                              + _EclipseOcclusion * 0.15h;

                half alpha = saturate(discMask * _OverlayAlpha * (baseVeil + edgeVeil * _DiscEdgeVeil));
                half3 atmosphericColor = lerp(_HazeColor.rgb, skyGradient, 0.55h + horizonVeil * 0.25h);
                atmosphericColor = lerp(atmosphericColor, _SkyColorHorizon.rgb, horizonVeil * 0.45h + _NightBlend * 0.15h);
                clip(alpha - ResolveIgnThreshold(input.positionCS));

                return half4(atmosphericColor * hazeSunTint, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
