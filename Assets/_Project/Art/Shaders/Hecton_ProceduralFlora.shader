Shader "Hecton8/URP/ProceduralFlora"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.1, 0.5, 0.2, 1)
        _TipColor("Tip Color", Color) = (0.4, 0.8, 0.3, 1)
        _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        
        _SwaySpeed("Sway Speed", Float) = 1.0
        _SwayAmount("Sway Amount", Float) = 0.2
        
        _HeightScale("Height Scale", Float) = 10.0
        _ColorHeightFalloff("Color Height Falloff", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  heightFrac : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TipColor;
                half4 _EmissionColor;
                float _SwaySpeed;
                float _SwayAmount;
                float _HeightScale;
                float _ColorHeightFalloff;
            CBUFFER_END

            float3 mod289(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
            float2 mod289(float2 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
            float3 permute(float3 x) { return mod289(((x*34.0)+1.0)*x); }
            float snoise(float2 v)
            {
                const float4 C = float4(0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439);
                float2 i  = floor(v + dot(v, C.yy));
                float2 x0 = v -   i + dot(i, C.xx);
                float2 i1;
                i1 = (x0.x > x0.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);
                float4 x12 = x0.xyxy + C.xxzz;
                x12.xy -= i1;
                i = mod289(i);
                float3 p = permute( permute( i.y + float3(0.0, i1.y, 1.0 )) + i.x + float3(0.0, i1.x, 1.0 ));
                float3 m = max(0.5 - float3(dot(x0,x0), dot(x12.xy,x12.xy), dot(x12.zw,x12.zw)), 0.0);
                m = m*m;
                m = m*m;
                float3 x = 2.0 * frac(p * C.www) - 1.0;
                float3 h = abs(x) - 0.5;
                float3 ox = floor(x + 0.5);
                float3 a0 = x - ox;
                m *= 1.79284291400159 - 0.85373472095314 * ( a0*a0 + h*h );
                float3 g;
                g.x  = a0.x  * x0.x  + h.x  * x0.y;
                g.yz = a0.yz * x12.xz + h.yz * x12.yw;
                return 130.0 * dot(m, g);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                
                float heightFrac = saturate((IN.positionOS.y + _HeightScale*0.5) / max(_HeightScale, 0.001));
                
                float time = _Time.y * _SwaySpeed;
                float noise = snoise(posWS.xz * 0.5 + time);
                float noiseZ = snoise(posWS.xz * 0.5 + time + 13.37);
                
                float3 swayOffset = float3(noise, 0, noiseZ) * _SwayAmount * heightFrac * heightFrac;
                posWS += swayOffset;

                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.positionWS = posWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.heightFrac = heightFrac;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float colorFactor = saturate(pow(IN.heightFrac, _ColorHeightFalloff));
                half3 albedo = lerp(_BaseColor.rgb, _TipColor.rgb, colorFactor);
                half3 emission = _EmissionColor.rgb * colorFactor;

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalize(IN.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.alpha = 1.0;
                surfaceData.emission = emission;
                surfaceData.metallic = 0.0;
                surfaceData.smoothness = 0.3;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, ComputeFogFactor(IN.positionCS.z));
                return color;
            }
            ENDHLSL
        }
        
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float _SwaySpeed;
                float _SwayAmount;
                float _HeightScale;
            CBUFFER_END
            
            float3 mod289(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
            float2 mod289(float2 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
            float3 permute(float3 x) { return mod289(((x*34.0)+1.0)*x); }
            float snoise(float2 v)
            {
                const float4 C = float4(0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439);
                float2 i  = floor(v + dot(v, C.yy));
                float2 x0 = v -   i + dot(i, C.xx);
                float2 i1;
                i1 = (x0.x > x0.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);
                float4 x12 = x0.xyxy + C.xxzz;
                x12.xy -= i1;
                i = mod289(i);
                float3 p = permute( permute( i.y + float3(0.0, i1.y, 1.0 )) + i.x + float3(0.0, i1.x, 1.0 ));
                float3 m = max(0.5 - float3(dot(x0,x0), dot(x12.xy,x12.xy), dot(x12.zw,x12.zw)), 0.0);
                m = m*m;
                m = m*m;
                float3 x = 2.0 * frac(p * C.www) - 1.0;
                float3 h = abs(x) - 0.5;
                float3 ox = floor(x + 0.5);
                float3 a0 = x - ox;
                m *= 1.79284291400159 - 0.85373472095314 * ( a0*a0 + h*h );
                float3 g;
                g.x  = a0.x  * x0.x  + h.x  * x0.y;
                g.yz = a0.yz * x12.xz + h.yz * x12.yw;
                return 130.0 * dot(m, g);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nrmWS = TransformObjectToWorldNormal(IN.normalOS);
                
                float heightFrac = saturate((IN.positionOS.y + _HeightScale*0.5) / max(_HeightScale, 0.001));
                float time = _Time.y * _SwaySpeed;
                float noise = snoise(posWS.xz * 0.5 + time);
                float noiseZ = snoise(posWS.xz * 0.5 + time + 13.37);
                float3 swayOffset = float3(noise, 0, noiseZ) * _SwayAmount * heightFrac * heightFrac;
                posWS += swayOffset;

                float3 lightDirectionWS = _LightDirection;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    lightDirectionWS = normalize(_LightPosition - posWS);
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, nrmWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
