Shader "Hecton8/Visor/HolographicEdge"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.05, 0.95, 1.0, 0.82)
        _ShellOffset("Shell Offset", Float) = 0.024
        _FlickerSpeed("Flicker Speed", Float) = 42
        _FlickerCutoff("Flicker Cutoff", Range(0, 1)) = 0.34
        _EdgePower("Edge Power", Float) = 2.6
        _ScanlineStrength("Scanline Strength", Float) = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "HolographicEdge"
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Front

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _ShellOffset;
                float _FlickerSpeed;
                float _FlickerCutoff;
                float _EdgePower;
                float _ScanlineStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                HECTON_CORE_LIT_DECLARE_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                HECTON_CORE_LIT_DECLARE_VERTEX_INPUT_INSTANCE_ID
                HECTON_CORE_LIT_DECLARE_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                HECTON_CORE_LIT_SETUP_INSTANCE_ID(input);
                HECTON_CORE_LIT_TRANSFER_INSTANCE_ID(input, output);
                HECTON_CORE_LIT_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 normalOS = HectonCoreLitSafeNormalize(input.normalOS);
                float3 positionOS = HectonCoreLitSanitizePositionOS(input.positionOS.xyz) + normalOS * _ShellOffset;
                output.positionWS = TransformObjectToWorld(positionOS);
                output.normalWS = TransformObjectToWorldNormal(normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                HECTON_CORE_LIT_SETUP_INSTANCE_ID(input);
                HECTON_CORE_LIT_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 absolutePosition = input.positionWS + _TotalUniverseOffset.xyz;
                float flickerGate = HectonCoreLitHologramFlickerGate(
                    input.positionCS,
                    absolutePosition,
                    _Time.y,
                    _FlickerSpeed,
                    _FlickerCutoff);
                clip(flickerGate);

                float3 normalWS = HectonCoreLitSafeNormalize(input.normalWS);
                float3 viewDirWS = HectonCoreLitSafeNormalize(GetCameraPositionWS() - input.positionWS);
                float edgeBase = 1.0 - saturate(abs(dot(normalWS, viewDirWS)));
                float edgeSq = edgeBase * edgeBase;
                float edge = lerp(edgeSq, edgeSq * edgeBase, saturate((_EdgePower - 2.0) * 0.5));
                float row = floor(input.positionCS.y * 0.5);
                float scanline = lerp(1.0, 0.45 + 0.55 * step(0.48, frac(row + _Time.y * 18.0)), saturate(_ScanlineStrength));
                float crawl = frac(sin(_Time.y * _FlickerSpeed) * 43758.5453123);
                half alpha = (half)saturate(_BaseColor.a * (0.18 + edge * 1.4) * scanline * lerp(0.62, 1.18, crawl));
                half3 color = _BaseColor.rgb * (half)(1.2 + edge * 2.8);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
