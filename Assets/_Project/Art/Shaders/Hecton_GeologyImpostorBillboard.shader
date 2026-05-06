Shader "Hecton8/Environment/Hecton_GeologyImpostorBillboard"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo Atlas", 2D) = "white" {}
        [NoScaleOffset] _NormalMap ("Normal Atlas", 2D) = "bump" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.65
        _AlphaClipThreshold ("Alpha Clip Threshold", Range(0, 1)) = 0.45
        _AmbientFloor ("Ambient Floor", Range(0, 1)) = 0.18
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
        }

        Pass
        {
            Name "ForwardUnlitAtlas"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _NormalStrength;
                half _AlphaClipThreshold;
                half _AmbientFloor;
                half _LightWrap;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                half4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 tangentWS : TEXCOORD2;
                half3 bitangentWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = positionInputs.positionCS;
                output.uv = input.uv;
                output.normalWS = normalize(normalInputs.normalWS);
                output.tangentWS = normalize(normalInputs.tangentWS);
                output.bitangentWS = normalize(normalInputs.bitangentWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedoSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                clip(albedoSample.a - _AlphaClipThreshold);

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv), _NormalStrength);
                half3 normalWS = normalize(
                    input.tangentWS * normalTS.x +
                    input.bitangentWS * normalTS.y +
                    input.normalWS * normalTS.z);

                Light mainLight = GetMainLight();
                half wrappedNdotL = saturate((dot(normalWS, mainLight.direction) + _LightWrap) / max(0.0001h, 1.0h + _LightWrap));
                half3 ambient = SampleSH(normalWS) + half3(_AmbientFloor, _AmbientFloor, _AmbientFloor);
                half3 color = albedoSample.rgb * (ambient + mainLight.color * wrappedNdotL * mainLight.distanceAttenuation);
                color = MixFog(color, input.fogFactor);
                return half4(color, albedoSample.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
