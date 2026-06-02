Shader "Hecton/Prologue/Capsule Reentry Plasma Fake"
{
    Properties
    {
        [HDR] _PlasmaColor ("Plasma Color", Color) = (1.0, 0.46, 0.12, 1.0)
        [HDR] _CoreColor ("Core Color", Color) = (1.0, 0.92, 0.58, 1.0)
        _NoiseScale ("Noise Scale", Float) = 18.0
        _Alpha ("Alpha", Range(0, 1)) = 0.82
        _PlasmaIntensity ("Plasma Intensity", Range(0, 1)) = 0
        _AblationAmount ("Ablation Amount", Range(0, 1)) = 0
        _GlassCrackIntensity ("Glass Crack Intensity", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "CapsulePlasmaFake"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _PlasmaColor;
                half4 _CoreColor;
                half _NoiseScale;
                half _Alpha;
                half _PlasmaIntensity;
                half _AblationAmount;
                half _GlassCrackIntensity;
            CBUFFER_END

            float _H8OrbitalReentryHeat;
            float _H8OrbitalLeadingEdgeDot;
            float _H8OrbitalUniverseSpeed;
            float _H8OrbitalMathLod;
            float4 _HectonReentryAblationState;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float plasmaIntensity = saturate(max(_H8OrbitalReentryHeat, max(_PlasmaIntensity, _HectonReentryAblationState.x)));
                float ablationAmount = saturate(max(_AblationAmount, _HectonReentryAblationState.y));
                float shellLift = plasmaIntensity * 0.08 + ablationAmount * 0.035;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz + input.normalOS * shellLift);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                output.positionCS = vertexInput.positionCS;
                output.normalWS = normalInput.normalWS;
                output.positionWS = vertexInput.positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half heat = saturate(max(_H8OrbitalReentryHeat, max(_PlasmaIntensity, _HectonReentryAblationState.x)));
                half ablation = saturate(max(_AblationAmount, _HectonReentryAblationState.y));
                half glassStress = saturate(max(_GlassCrackIntensity, _HectonReentryAblationState.z));
                float mathLod = isfinite(_H8OrbitalMathLod) ? _H8OrbitalMathLod : 1.0;
                half mathLod01 = (half)saturate(mathLod * 0.33333334);
                half detailWeight = smoothstep(0.16h, 0.82h, mathLod01);
                half overkillWeight = smoothstep(0.82h, 1.0h, mathLod01);
                half edge = saturate(_H8OrbitalLeadingEdgeDot);
                half detailedFlicker = saturate(sin((input.positionWS.x + input.positionWS.y * 0.37h + _Time.y * 24.0h) * _NoiseScale) * 0.5h + 0.5h);
                half flicker = lerp(0.72h, detailedFlicker, detailWeight);
                half overkill = lerp(1.0h, 1.25h, overkillWeight);
                half alpha = saturate((heat + ablation * 0.18h + glassStress * 0.08h) * lerp(0.22h, 1.0h, edge) * lerp(0.55h, 1.0h, flicker) * _Alpha);
                half3 color = lerp(_PlasmaColor.rgb, _CoreColor.rgb, flicker * heat) * (1.0h + heat * 3.0h * overkill);
                half scorchMask = saturate(ablation * (1.0h - edge * 0.35h));
                half3 scorchColor = color * half3(0.45h, 0.30h, 0.18h);
                color = lerp(color, scorchColor, scorchMask);
                color += _CoreColor.rgb * (glassStress * 0.35h);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
