Shader "Hecton8/URP/ProceduralFlora"
{
    Properties
    {
        _BaseMap("Albedo Map", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _MaskMap("Mask Map (R=Met, G=AO, B=Emis, A=Smth)", 2D) = "white" {}
        
        _BaseColor("Base Color Tint", Color) = (1, 1, 1, 1)
        _TipColor("Tip Color Tint", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        
        _SwaySpeed("Sway Speed", Float) = 1.0
        _SwayAmount("Sway Amount", Float) = 0.2
        
        _HeightScale("Height Scale", Float) = 10.0
        _ColorHeightFalloff("Color Height Falloff", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "IgnoreProjector"="True" }

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
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  heightFrac : TEXCOORD4;
                float2 uv         : TEXCOORD5;
                float3 tangentWS  : TEXCOORD6;
                float3 bitangentWS: TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);

            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
                UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(half4, _TipColor)
                UNITY_DEFINE_INSTANCED_PROP(half4, _EmissionColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwaySpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwayAmount)
                UNITY_DEFINE_INSTANCED_PROP(float, _HeightScale)
                UNITY_DEFINE_INSTANCED_PROP(float, _ColorHeightFalloff)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

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
                
                float heightScale = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _HeightScale);
                float heightFrac = saturate((IN.positionOS.y + heightScale*0.5) / max(heightScale, 0.001));
                
                float time = _Time.y * UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwaySpeed);
                float noise = snoise(posWS.xz * 0.5 + time);
                float noiseZ = snoise(posWS.xz * 0.5 + time + 13.37);
                
                float swayMask = IN.uv.y * IN.uv.y;
                float3 swayOffset = float3(noise, 0, noiseZ) * UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwayAmount) * swayMask;
                posWS += swayOffset;

                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.positionWS = posWS;
                
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.tangentWS = TransformObjectToWorldDir(IN.tangentOS.xyz);
                float tangentSign = IN.tangentOS.w * unity_WorldTransformParams.w;
                OUT.bitangentWS = cross(OUT.normalWS, OUT.tangentWS) * tangentSign;
                
                float4 baseMapST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
                OUT.uv = IN.uv * baseMapST.xy + baseMapST.zw;
                OUT.heightFrac = heightFrac;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 albedoTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                
                #if defined(_ALPHATEST_ON)
                clip(albedoTex.a - 0.5);
                #endif

                half4 normalTex = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv);
                half4 maskTex = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, IN.uv);

                float colorFalloff = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ColorHeightFalloff);
                float colorFactor = saturate(pow(IN.heightFrac, colorFalloff));
                half3 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor).rgb;
                half3 tipColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _TipColor).rgb;
                half3 tint = lerp(baseColor, tipColor, colorFactor);
                half3 albedo = albedoTex.rgb * tint;
                
                half metallic = maskTex.r;
                half ao = maskTex.g;
                half emissionMask = maskTex.b;
                half smoothness = maskTex.a * 0.8;

                half3 emColorLocal = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionColor).rgb;
                half3 emission = emColorLocal * emissionMask * colorFactor;

                half3 normalTS = UnpackNormal(normalTex);
                half3x3 tangentSpaceTransform = half3x3(IN.tangentWS.xyz, IN.bitangentWS.xyz, IN.normalWS.xyz);
                half3 normalWS = normalize(mul(normalTS, tangentSpaceTransform));

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.positionCS = IN.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);
                inputData.tangentToWorld = tangentSpaceTransform;

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.alpha = albedoTex.a;
                surfaceData.metallic = metallic;
                surfaceData.smoothness = smoothness;
                surfaceData.occlusion = ao;
                
                Light mainLight = GetMainLight(inputData.shadowCoord);
                half sss = saturate(dot(inputData.viewDirectionWS, -mainLight.direction)) * 0.3 * albedoTex.a;
                surfaceData.emission = emission + (albedo * sss * mainLight.color);

                half4 c = UniversalFragmentPBR(inputData, surfaceData);
                
                half forcedEmissionMask = max(emissionMask, 0.5);
                half3 emColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionColor).rgb;
                half3 em = max(emColor, half3(0.0, 0.5, 0.5)) * forcedEmissionMask * 20.0;
                c.rgb += em;

                c.rgb = MixFog(c.rgb, ComputeFogFactor(IN.positionCS.z));
                
                return c;
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
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

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
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
                UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(half4, _TipColor)
                UNITY_DEFINE_INSTANCED_PROP(half4, _EmissionColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwaySpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _SwayAmount)
                UNITY_DEFINE_INSTANCED_PROP(float, _HeightScale)
                UNITY_DEFINE_INSTANCED_PROP(float, _ColorHeightFalloff)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)
            
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

            float3 _LightDirection;
            float3 _LightPosition;

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nrmWS = TransformObjectToWorldNormal(IN.normalOS);
                
                float time = _Time.y * UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwaySpeed);
                float noise = snoise(posWS.xz * 0.5 + time);
                float noiseZ = snoise(posWS.xz * 0.5 + time + 13.37);
                float swayMask = IN.uv.y * IN.uv.y; 
                float3 swayOffset = float3(noise, 0, noiseZ) * UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SwayAmount) * swayMask;
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
                
                float4 baseMapST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
                OUT.uv = IN.uv * baseMapST.xy + baseMapST.zw;
                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                
                #if defined(_ALPHATEST_ON)
                half4 albedoTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                clip(albedoTex.a - 0.5);
                #endif
                
                return 0;
            }
            ENDHLSL
        }
    }
}
