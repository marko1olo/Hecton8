Shader "Hecton8/UI/DiegeticPanelUnlit"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "black" {}
        _MainTex ("Main Tex", 2D) = "black" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _PanelPowerLevel ("Panel Power", Range(0, 1)) = 1
        _DepthFadeRange ("Depth Fade Range", Range(0.001, 1)) = 0.08
        _OcclusionActive ("Occlusion Active", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _BaseMap_ST;
                float _PanelPowerLevel;
                float _DepthFadeRange;
                float _OcclusionActive;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float Bayer4x4(float2 pixelCoord)
            {
                float2 cell = floor(frac(pixelCoord * 0.25) * 4.0);

                if (cell.y < 0.5)
                {
                    if (cell.x < 0.5) return 0.0 / 16.0;
                    if (cell.x < 1.5) return 8.0 / 16.0;
                    if (cell.x < 2.5) return 2.0 / 16.0;
                    return 10.0 / 16.0;
                }

                if (cell.y < 1.5)
                {
                    if (cell.x < 0.5) return 12.0 / 16.0;
                    if (cell.x < 1.5) return 4.0 / 16.0;
                    if (cell.x < 2.5) return 14.0 / 16.0;
                    return 6.0 / 16.0;
                }

                if (cell.y < 2.5)
                {
                    if (cell.x < 0.5) return 3.0 / 16.0;
                    if (cell.x < 1.5) return 11.0 / 16.0;
                    if (cell.x < 2.5) return 1.0 / 16.0;
                    return 9.0 / 16.0;
                }

                if (cell.x < 0.5) return 15.0 / 16.0;
                if (cell.x < 1.5) return 7.0 / 16.0;
                if (cell.x < 2.5) return 13.0 / 16.0;
                return 5.0 / 16.0;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 mainSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 screenSample = max(baseSample, mainSample);
                float rgbAlpha = saturate(max(max(screenSample.r, screenSample.g), screenSample.b) * 2.0);
                float powerLevel = saturate(_PanelPowerLevel);
                float3 emissive = screenSample.rgb * _Color.rgb * lerp(0.45, 1.0, powerLevel);
                float alpha = max(screenSample.a, rgbAlpha) * _Color.a;

                if (_OcclusionActive > 0.5 && alpha > 0.001)
                {
                    float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;
                    float fragRawDepth = saturate(input.positionCS.z / input.positionCS.w);
                    float sceneRawDepth = SampleSceneDepth(screenUV);
#if UNITY_REVERSED_Z
                    float sceneDepthValid = step(0.0001, sceneRawDepth);
#else
                    float sceneDepthValid = step(sceneRawDepth, 0.9999);
#endif
                    float linearSceneDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                    float linearFragDepth = LinearEyeDepth(fragRawDepth, _ZBufferParams);
                    float occluded = sceneDepthValid * step(linearSceneDepth + _DepthFadeRange, linearFragDepth);

                    if (occluded > 0.5)
                    {
                        float2 screenPixel = floor(input.positionCS.xy);
                        float bayer = Bayer4x4(screenPixel);
                        clip((alpha * 0.25) - bayer);
                    }
                }

                return half4(emissive, alpha);
            }
            ENDHLSL
        }
    }
}
