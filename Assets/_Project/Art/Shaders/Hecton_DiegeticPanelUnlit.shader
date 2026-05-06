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
        _InventoryScanlineStrength ("Inventory Scanline Strength", Range(0, 1)) = 0.22
        _InventoryScanlineDensity ("Inventory Scanline Density", Range(16, 320)) = 140
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
                float _InventoryScanlineStrength;
                float _InventoryScanlineDensity;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _HectonPdaInventoryParallax;
            float4 _HectonUiAnalogJitter;

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
                float inventoryMask = saturate(_HectonPdaInventoryParallax.z);
                float2 panelUv = input.uv + (input.uv - 0.5) * _HectonPdaInventoryParallax.xy;
                float screenCenteredX = (input.positionCS.x / max(1.0, _ScaledScreenParams.x)) - 0.5;
                panelUv.x += screenCenteredX * _HectonPdaInventoryParallax.x * inventoryMask * 0.08;

                float analogStrength = saturate(_HectonUiAnalogJitter.x) * inventoryMask;
                float2 analogCell = floor(input.uv * float2(113.0, 47.0));
                float analogNoise = frac(sin(dot(analogCell, float2(12.9898, 78.233))) * 43758.5453);
                float analogWave = sin(_Time.y * 100.0 + input.uv.y * 613.0 + analogNoise * 6.28318);
                panelUv += float2(
                    analogWave * (analogNoise - 0.5) * analogStrength * 0.006,
                    analogWave * analogStrength * 0.0015);
                panelUv = saturate(panelUv);
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, panelUv);
                half4 mainSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, panelUv);
                half4 screenSample = max(baseSample, mainSample);
                float rgbAlpha = saturate(max(max(screenSample.r, screenSample.g), screenSample.b) * 2.0);
                float powerLevel = saturate(_PanelPowerLevel);
                float3 emissive = screenSample.rgb * _Color.rgb * lerp(0.45, 1.0, powerLevel);
                float alpha = max(screenSample.a, rgbAlpha) * _Color.a;
                float scanCoord = frac(input.uv.y * max(1.0, _InventoryScanlineDensity) + _Time.y * 0.85);
                float scanline = lerp(1.0, lerp(0.72, 1.08, step(0.5, scanCoord)), inventoryMask * _InventoryScanlineStrength);
                emissive *= scanline;
                alpha *= lerp(1.0, 0.92 + scanline * 0.08, inventoryMask);
                float2 edgeDistance = min(input.uv, 1.0 - input.uv);
                float edgePulseMask = (1.0 - smoothstep(0.012, 0.075, min(edgeDistance.x, edgeDistance.y))) * inventoryMask * powerLevel;
                float edgePulse = 0.74 + 0.26 * sin(_Time.y * 4.7 + input.uv.x * 13.0 - input.uv.y * 9.0);
                emissive += _Color.rgb * edgePulseMask * edgePulse * 0.085;
                alpha = saturate(alpha + edgePulseMask * edgePulse * 0.035);

                if (powerLevel < 0.1)
                {
                    float bayer = Bayer4x4(floor(input.positionCS.xy));
                    float luminance = dot(screenSample.rgb, float3(0.2126, 0.7152, 0.0722));
                    float phosphorBit = step(0.12 + bayer * 0.55, luminance * 2.2);
                    emissive = float3(0.018, 0.84, 0.20) * phosphorBit * lerp(0.70, 1.0, bayer);
                    alpha = max(alpha * 0.32, phosphorBit * max(screenSample.a, rgbAlpha) * _Color.a);
                }

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
                        float ditherGate = step(bayer, 0.3125);
                        float weakProjection = lerp(0.08, 0.46, ditherGate);
                        emissive *= weakProjection;
                        alpha *= lerp(0.035, 0.18, ditherGate);
                    }
                }

                return half4(emissive, alpha);
            }
            ENDHLSL
        }
    }
}
