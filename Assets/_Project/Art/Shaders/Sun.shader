Shader "Hecton/Celestial/Sun"
{
    Properties
    {
        [HDR] _SunColor ("Disc Color", Color) = (1.0, 0.82, 0.45, 1.0)
        [HDR] _CoreColor ("Core Color", Color) = (1.12, 1.04, 0.9, 1.0)
        [HDR] _CoronaColor ("Corona Color", Color) = (0.92, 0.58, 0.2, 1.0)
        _GlowIntensity ("Disc Intensity", Float) = 12.0
        _CoreIntensity ("Core Intensity", Float) = 7.5
        _CoronaIntensity ("Corona Intensity", Float) = 5.0
        _DiscSoftness ("Disc Softness", Range(0.01, 0.6)) = 0.28
        _CoronaPower ("Corona Power", Range(0.5, 6.0)) = 1.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "SunUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back
            Fog { Mode Off }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON
            #pragma skip_variants POINT POINT_COOKIE SHADOWS_CUBE
            #pragma skip_variants _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SunColor;
                half4 _CoreColor;
                half4 _CoronaColor;
                half _GlowIntensity;
                half _CoreIntensity;
                half _CoronaIntensity;
                half _DiscSoftness;
                half _CoronaPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);

                return output;
            }

            half3 LimbDarkening(half mu)
            {
                static const half3 a0 = half3(0.3h, 0.25h, 0.15h);
                static const half3 a1 = half3(0.93h, 0.87h, 0.73h);
                static const half3 a2 = half3(-0.23h, -0.12h, 0.0h);

                half mu2 = mu * mu;
                half3 darkening = a0 + a1 * mu + a2 * mu2;
                return saturate(darkening);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 N = normalize(input.normalWS);
                half3 V = normalize(input.viewDirWS);
                half mu = saturate(dot(N, V));

                half3 limbFactor = LimbDarkening(mu);
                half edgeFade = smoothstep(0.0h, _DiscSoftness, mu);
                half centerBlend = smoothstep(0.22h, 1.0h, mu);

                half3 discColor = lerp(_SunColor.rgb, _CoreColor.rgb, centerBlend);
                discColor *= lerp(half3(1.0h, 1.0h, 1.0h), limbFactor, 0.68h);

                half discIntensity = lerp(0.26h, 1.0h, pow(mu, 0.32h)) * _GlowIntensity;
                half3 finalColor = discColor * discIntensity * edgeFade;

                half3 coreGlow = _CoreColor.rgb * pow(mu, 5.2h) * _CoreIntensity * edgeFade;
                finalColor += coreGlow;

                half coronaFactor = pow(saturate(1.0h - mu), _CoronaPower);
                half coronaVisibility = smoothstep(0.0h, saturate(_DiscSoftness * 0.6h + 0.08h), 1.0h - mu);
                half3 coronaColor = _CoronaColor.rgb * coronaFactor * _CoronaIntensity * coronaVisibility;
                finalColor += coronaColor;

                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite Off
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON
            #pragma skip_variants POINT POINT_COOKIE SHADOWS_CUBE
            #pragma skip_variants _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
