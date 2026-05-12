Shader "Hidden/Hecton8/VisorUberPost"
{
    Properties
    {
        _HectonVisorCrackTex ("Packed Crack Normal Alpha", 2D) = "black" {}
        _HectonLensDirtTex ("Lens Dirt", 2D) = "white" {}
        _HectonBlueNoiseTex ("Blue Noise", 2D) = "gray" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "VisorUberPost"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _HectonUberHealthFraction;
                float _HectonUberLocalTemperature;
                float _HectonUberAmbientPressure;
                float _HectonUberPlayerStress01;
                float _HectonUberHypoxia01;
                float _HectonUberBleeding01;
                float _HectonUberWetLens01;
                float _HectonUberHullStress01;
                float _HectonUberAupShiftFrame;
                float _HectonUberLowTier;
                float4 _HectonUberStrengths0;
                float4 _HectonUberStrengths1;
                float4 _HectonUberWaveParams;
                float4 _HectonUberTextureFlags;
            CBUFFER_END

            TEXTURE2D_X(_BlitTexture);
            float4 _BlitTexture_TexelSize;
            TEXTURE2D(_HectonVisorCrackTex);
            SAMPLER(sampler_HectonVisorCrackTex);
            TEXTURE2D(_HectonLensDirtTex);
            SAMPLER(sampler_HectonLensDirtTex);
            TEXTURE2D(_HectonBlueNoiseTex);
            SAMPLER(sampler_HectonBlueNoiseTex);

            struct Attributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float2 screenUV : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.screenUV = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(output.screenUV * 2.0 - 1.0, 0.0, 1.0);
            #if UNITY_UV_STARTS_AT_TOP
                output.screenUV.y = 1.0 - output.screenUV.y;
            #endif
                return output;
            }

            float2 ResolveXRStereoScreenUV(float2 screenUV)
            {
            #if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                return UnityStereoTransformScreenSpaceTex(screenUV);
            #else
                return screenUV;
            #endif
            }

            float InterleavedGradientNoise(float2 uv, float frameSalt)
            {
                float2 pixel = floor(uv * _ScreenParams.xy);
                pixel += float2(frameSalt, frameSalt);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float FastEdge01(float2 uv)
            {
                float2 centered = uv - 0.5;
                return saturate(dot(centered, centered) * 4.0);
            }

            float2 BarrelWarp(float2 uv, float pressure01, float strength)
            {
                float2 centered = uv * 2.0 - 1.0;
                float radiusSq = dot(centered, centered);
                float barrel = pressure01 * strength;
                centered *= 1.0 + radiusSq * barrel;
                return saturate(centered * 0.5 + 0.5);
            }

            float2 HeatHazeOffset(float2 uv, float heat01, float lowTier)
            {
                float enabled = 1.0 - step(0.5, lowTier);
                float freq = max(1.0, _HectonUberWaveParams.x);
                float speed = _HectonUberWaveParams.y;
                float amplitude = _HectonUberWaveParams.z * heat01 * enabled;
                float2 wave;
                wave.x = sin(uv.y * freq + _Time.y * speed);
                wave.y = sin(uv.x * freq * 0.73 - _Time.y * speed * 0.71);
                return wave * amplitude;
            }

            half3 ApplySingleSampleChroma(half3 color, float edge01, float damageDrive, float strength)
            {
                float drive = saturate(edge01 * damageDrive * strength);
                half heat = (half)drive;
                half3 shifted;
                shifted.r = color.r + heat * (0.035h + color.r * 0.045h);
                shifted.g = color.g * (1.0h - heat * 0.025h);
                shifted.b = color.b * (1.0h - heat * 0.075h) + heat * 0.018h;
                return shifted;
            }

            void ResolveProceduralCracks(float2 uv, float damage01, out float crackReveal, out float2 crackNormal)
            {
                float2 centered = uv * 2.0 - 1.0;
                float radial = saturate(dot(centered, centered));
                float2 cell = floor(uv * 11.0);
                float seed = Hash21(cell);
                float primary = abs(centered.x * 0.72 + centered.y * 0.31 + (seed - 0.5) * 0.13);
                float branch = abs(centered.x * -0.27 + centered.y * 0.86 + sin((centered.x + seed) * 9.0) * 0.025);
                float primaryVein = 1.0 - smoothstep(0.008, 0.035, primary);
                float branchVein = 1.0 - smoothstep(0.004, 0.019, branch);
                float vein = saturate(max(primaryVein, branchVein * 0.62) * smoothstep(0.08, 0.96, radial));
                float threshold = lerp(1.15, 0.18 + seed * 0.54, vein);
                crackReveal = step(threshold, damage01) * vein;
                float2 gradient = float2(primaryVein - branchVein, branchVein - primaryVein * 0.38);
                float2 normalSeed = gradient + centered * (0.15 + seed * 0.1);
                crackNormal = normalSeed * rsqrt(max(dot(normalSeed, normalSeed), 0.0001));
            }

            float ResolveDitherNoise(float2 uv, float shiftSalt)
            {
                float2 salt2 = float2(shiftSalt, shiftSalt);
                float proceduralNoise = frac(InterleavedGradientNoise(uv, shiftSalt) + Hash21(floor(uv * _ScreenParams.xy * 0.25) + salt2) * 0.5);
                [branch]
                if (_HectonUberTextureFlags.z > 0.5)
                {
                    float textureNoise = SAMPLE_TEXTURE2D(_HectonBlueNoiseTex, sampler_HectonBlueNoiseTex, uv * _ScreenParams.xy * 0.00390625 + salt2).r;
                    return frac(textureNoise + proceduralNoise * 0.5);
                }

                return proceduralNoise;
            }

            half3 ResolveLensDirt(float2 uv, float edge01)
            {
                [branch]
                if (_HectonUberTextureFlags.y > 0.5)
                    return SAMPLE_TEXTURE2D(_HectonLensDirtTex, sampler_HectonLensDirtTex, uv).rgb;

                float2 cell = floor(uv * 18.0);
                float grain = Hash21(cell);
                float streak = 1.0 - smoothstep(0.08, 0.34, abs(frac(uv.x * 7.0 + uv.y * 1.7 + grain) - 0.5));
                float grime = saturate(edge01 * 0.55 + streak * 0.22 + grain * 0.16);
                return half3(
                    (half)(1.0 - grime * 0.34),
                    (half)(1.0 - grime * 0.24),
                    (half)(1.0 - grime * 0.18));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = ResolveXRStereoScreenUV(input.screenUV);
                float health01 = saturate(_HectonUberHealthFraction);
                float damage01 = saturate(1.0 - health01);
                float edge01 = FastEdge01(uv);
                float pressure01 = saturate((_HectonUberAmbientPressure - 1.0) * _HectonUberStrengths1.x);
                float heat01 = saturate(abs(_HectonUberLocalTemperature) * _HectonUberStrengths1.y);
                float stress01 = saturate(_HectonUberPlayerStress01);
                float hypoxia01 = saturate(_HectonUberHypoxia01);

                float crackReveal;
                float2 crackNormal;
                [branch]
                if (_HectonUberTextureFlags.x > 0.5)
                {
                    float4 crackSample = SAMPLE_TEXTURE2D(_HectonVisorCrackTex, sampler_HectonVisorCrackTex, uv);
                    crackReveal = step(crackSample.a, damage01);
                    crackNormal = crackSample.rg * 2.0 - 1.0;
                }
                else
                {
                    ResolveProceduralCracks(uv, damage01, crackReveal, crackNormal);
                }

                float crackMask = crackReveal * saturate(_HectonUberStrengths0.w);

                float2 warpedUV = BarrelWarp(uv, pressure01, _HectonUberStrengths0.z);
                warpedUV += HeatHazeOffset(uv, heat01, _HectonUberLowTier);
                warpedUV += crackNormal * (crackMask * _HectonUberStrengths1.z);
                warpedUV = saturate(warpedUV);

                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, warpedUV);

                float damageDrive = saturate(max(damage01, max(_HectonUberHullStress01, stress01)) + crackMask * 0.35);
                color.rgb = ApplySingleSampleChroma(color.rgb, edge01, damageDrive, _HectonUberStrengths0.x);

                float shiftSalt = frac(_HectonUberAupShiftFrame * 0.6180339887);
                float blueNoise = ResolveDitherNoise(uv, shiftSalt);
                half3 dirt = ResolveLensDirt(uv, edge01);
                float dirtDrive = saturate(_HectonUberStrengths1.w * (0.18 + edge01 * 0.82 + _HectonUberWetLens01 * 0.35));
                float dirtMask = step(blueNoise, dirtDrive);
                color.rgb *= lerp(half3(1.0h, 1.0h, 1.0h), dirt, (half)dirtMask);

                half crackDarken = (half)(crackMask * (0.22 + edge01 * 0.18));
                color.rgb *= 1.0h - crackDarken;
                color.rgb += (half3(0.16h, 0.22h, 0.24h) * (half)(crackMask * 0.035));

                half luma = dot(color.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half3 hypoxiaLuma = half3(luma, luma, luma);
                color.rgb = lerp(color.rgb, hypoxiaLuma * half3(0.78h, 0.91h, 1.05h), (half)(hypoxia01 * _HectonUberStrengths0.y));

                float vignette = saturate(edge01 * stress01 * _HectonUberStrengths1.w + edge01 * damageDrive * _HectonUberWaveParams.w);
                color.rgb *= 1.0h - (half)vignette;

                float bleeding = saturate(_HectonUberBleeding01);
                half bloodEdge = (half)(bleeding * edge01 * _HectonUberStrengths1.w);
                color.rgb = lerp(color.rgb, half3(0.48h, 0.015h, 0.012h), bloodEdge);
                color.rgb = max(color.rgb, half3(0.0015h, 0.0022h, 0.0030h));
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
