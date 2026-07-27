Shader "Hidden/Hecton8/DeferredCaustics"
{
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

        HLSLINCLUDE
        #pragma target 3.5
        // This fullscreen composite stays LIGHT_COOKIE independent. Optional 1719-baked atlas input is a
        // precompressed offline visual fake; null atlas keeps the procedural fallback path.
        #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS
        #pragma skip_variants POINT POINT_COOKIE _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(HectonAbyssalCaustics)
            float4 ProjectionVectorAndScale;   // xyz=sun-to-surface direction, w=noise scale
            float4 NoiseAnimationSpeed;        // xy=wrapped AUP xz, z=flow phase, w=chromatic dispersion
            float4 IntensityAndDepthFalloff;   // x=intensity, y=inv depth range, z=max depth, w=SDF shadow strength
            float4 QualityAndColor;            // x=GlobalQualityWeight, yzw=linear RGB tint
        CBUFFER_END

        TEXTURE2D_X(_HectonDeferredCausticsSource);
        TEXTURE2D_X_FLOAT(_HectonDeferredCausticsDepth);
        TEXTURE2D(_HectonBakedCausticAtlas);
        SAMPLER(sampler_HectonBakedCausticAtlas);
        TEXTURE2D(_HectonBakedCausticWaterlineMask);
        SAMPLER(sampler_HectonBakedCausticWaterlineMask);
        float4 _HectonBakedCausticAtlasParams;     // x=atlas weight, y=columns, z=rows, w=frame count
        float4 _HectonBakedCausticAtlasTexelParams; // xy=local texel size inside one atlas cell
        float4 _HectonBakedCausticWaterlineParams; // x=mask weight, y=min world Y, z=inv world Y range
        TEXTURE3D(_HectonCaveVoxelSdfTex);
        SAMPLER(sampler_HectonCaveVoxelSdfTex);
        float _HectonCaveVoxelActive;
        float4x4 _HectonCaveVoxelWorldToLocal;
        float4 _HectonCaveVoxelHalfExtents;
        float4 _HectonCaveVoxelInvDoubleHalfExtents;

        struct Attributes
        {
            uint vertexID : SV_VertexID;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 screenUV : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.screenUV = float2((input.vertexID << 1) & 2, input.vertexID & 2);
            output.positionCS = float4(output.screenUV * 2.0 - 1.0, 0.0, 1.0);
        #if UNITY_UV_STARTS_AT_TOP
            output.screenUV.y = 1.0 - output.screenUV.y;
        #endif
            return output;
        }

        float ResolveDepthValid(float rawDepth)
        {
        #if UNITY_REVERSED_Z
            return step(0.0001, rawDepth);
        #else
            return step(rawDepth, 0.9999);
        #endif
        }

        float SampleCausticsDepth(float2 screenUV)
        {
            return SAMPLE_TEXTURE2D_X(_HectonDeferredCausticsDepth, sampler_PointClamp, screenUV).r;
        }

        float SafeFinite(float value, float fallbackValue)
        {
            return isfinite(value) ? value : fallbackValue;
        }

        float3 SafeNormalize3(float3 value, float3 fallbackValue)
        {
            float lenSq = dot(value, value);
            float valid = (isfinite(lenSq) && lenSq > 0.000001) ? 1.0 : 0.0;
            float3 normalized = value * rsqrt(max(lenSq, 0.000001));
            return lerp(fallbackValue, normalized, valid);
        }

        float2 Hash22(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.xx + p3.yz) * p3.zy);
        }

        float VoronoiDistanceSq(float2 uv)
        {
            float2 cell = floor(uv);
            float2 local = frac(uv);
            float best = 8.0;
            [unroll]
            for (int y = -1; y <= 1; y++)
            {
                [unroll]
                for (int x = -1; x <= 1; x++)
                {
                    float2 neighbor = float2((float)x, (float)y);
                    float2 jitter = Hash22(cell + neighbor);
                    float2 delta = neighbor + jitter - local;
                    best = min(best, dot(delta, delta));
                }
            }

            return best;
        }

        float CausticLineLayer(float2 uv)
        {
            float distanceSqToCell = VoronoiDistanceSq(uv);
            float lineMask = saturate(1.0 - distanceSqToCell * 1.85);
            lineMask *= lineMask;
            return lineMask * lineMask;
        }

        float2 ResolveBakedAtlasUv(float2 localUv, float frameIndex, float columns, float rows)
        {
            float safeColumns = max(columns, 1.0);
            float safeRows = max(rows, 1.0);
            float cellY = floor(frameIndex / safeColumns);
            float cellX = frameIndex - cellY * safeColumns;
            float2 cellSize = rcp(float2(safeColumns, safeRows));
            float2 inset = min(max(_HectonBakedCausticAtlasTexelParams.xy, 0.0) * 1.5, 0.25);
            float2 safeLocalUv = lerp(inset, 1.0 - inset, frac(localUv));
            return (float2(cellX, cellY) + safeLocalUv) * cellSize;
        }

        half3 SampleBakedCausticAtlas(float2 uv, float flowPhase, float quality)
        {
            float columns = max(floor(_HectonBakedCausticAtlasParams.y + 0.5), 1.0);
            float rows = max(floor(_HectonBakedCausticAtlasParams.z + 0.5), 1.0);
            float frameLimit = max(columns * rows, 1.0);
            float frameCount = clamp(floor(_HectonBakedCausticAtlasParams.w + 0.5), 1.0, frameLimit);
            float frameCursor = frac(flowPhase * 0.125) * frameCount;
            float frame0 = floor(frameCursor);
            float frame1 = frame0 + 1.0;
            frame1 = frame1 - floor(frame1 / frameCount) * frameCount;
            float frameBlend = frac(frameCursor) * smoothstep(0.24, 0.76, saturate(quality));
            half3 caustic0 = SAMPLE_TEXTURE2D(_HectonBakedCausticAtlas, sampler_HectonBakedCausticAtlas, ResolveBakedAtlasUv(uv, frame0, columns, rows)).rgb;
            [branch]
            if (frameBlend > 0.0001)
            {
                half3 caustic1 = SAMPLE_TEXTURE2D(_HectonBakedCausticAtlas, sampler_HectonBakedCausticAtlas, ResolveBakedAtlasUv(uv, frame1, columns, rows)).rgb;
                return lerp(caustic0, caustic1, (half)frameBlend);
            }

            return caustic0;
        }

        float ResolveBakedWaterlineMask(float3 worldPos)
        {
            float maskWeight = saturate(_HectonBakedCausticWaterlineParams.x);
            [branch]
            if (maskWeight <= 0.0001)
                return 1.0;

            float maskV = saturate((worldPos.y - _HectonBakedCausticWaterlineParams.y) * max(_HectonBakedCausticWaterlineParams.z, 0.0001));
            float mask = SAMPLE_TEXTURE2D(_HectonBakedCausticWaterlineMask, sampler_HectonBakedCausticWaterlineMask, float2(0.5, maskV)).r;
            return lerp(1.0, mask, maskWeight);
        }

        float SampleCaveVoxelSignedDistance(float3 positionWS)
        {
            if (_HectonCaveVoxelActive <= 0.5)
                return _HectonCaveVoxelHalfExtents.w;

            float3 invDoubleHalfExtents = _HectonCaveVoxelInvDoubleHalfExtents.xyz;
            if (any(invDoubleHalfExtents <= 0.0))
                return _HectonCaveVoxelHalfExtents.w;

            float3 localPosition = mul(_HectonCaveVoxelWorldToLocal, float4(positionWS, 1.0)).xyz;
            float3 sampleUv = localPosition * invDoubleHalfExtents + 0.5;
            if (any(sampleUv < 0.0) || any(sampleUv > 1.0))
                return _HectonCaveVoxelHalfExtents.w;

            float encoded = SAMPLE_TEXTURE3D_LOD(_HectonCaveVoxelSdfTex, sampler_HectonCaveVoxelSdfTex, sampleUv, 0).r;
            return lerp(-_HectonCaveVoxelHalfExtents.w, _HectonCaveVoxelHalfExtents.w, encoded);
        }

        float ResolveSdfCavernOcclusion(float3 worldPos, float3 sunToSurface, float quality, float strength)
        {
            if (_HectonCaveVoxelActive <= 0.5 || strength <= 0.0001)
                return 1.0;

            float firstSdf = SampleCaveVoxelSignedDistance(worldPos + (-sunToSurface) * 0.35);
            float shadow = smoothstep(-0.08, 0.85, firstSdf);
            float3 towardSun = -sunToSurface;
            float stepBase = lerp(1.2, 3.8, saturate(quality));
            float sdfSampleBudget = saturate((quality - 0.30) * 1.4285715) * 4.0;
            [unroll]
            for (int i = 0; i < 4; i++)
            {
                float stepWeight = saturate(sdfSampleBudget - (float)i);
                [branch]
                if (stepWeight > 0.0001)
                {
                    float3 samplePos = worldPos + towardSun * (stepBase * ((float)i + 1.0));
                    float sdf = SampleCaveVoxelSignedDistance(samplePos);
                    float rayOpen = smoothstep(0.05, 1.65 + (float)i * 0.45, sdf);
                    shadow = min(shadow, lerp(1.0, rayOpen, stepWeight));
                }
            }

            return lerp(1.0, saturate(shadow), saturate(strength));
        }

        float2 ProjectCausticUv(float3 worldPos, float3 sunToSurface, float scale)
        {
            float projectedDepth = dot(worldPos, sunToSurface);
            float2 wrappedOffset = NoiseAnimationSpeed.xy;
            float flowPhase = NoiseAnimationSpeed.z;
            float2 projected = worldPos.xz - sunToSurface.xz * projectedDepth;
            float2 flow = float2(flowPhase * 0.073, flowPhase * -0.041);
            return projected * max(scale, 0.0001) + wrappedOffset * max(scale, 0.0001) + flow;
        }

        // Depth deltas at or above this are treated as "no usable neighbour" (sky, or off-screen clamp).
        #define H8_CAUSTIC_NORMAL_TAP_REJECT 1.0e9

        // A fullscreen composite has no vertex normal, so the incidence term is rebuilt from the depth
        // buffer. Four point taps rather than ddx/ddy on purpose: this runs after the early-out branches
        // below, and implicit derivatives are undefined once lanes diverge. Per axis the nearer neighbour
        // wins, so a silhouette cannot bend the normal across a depth cliff.
        float3 ResolveGeometricNormalWS(float2 screenUV, float3 centerWorldPos, float centerRawDepth)
        {
            float2 texel = rcp(max(_ScreenParams.xy, float2(1.0, 1.0)));
            float centerEyeDepth = LinearEyeDepth(centerRawDepth, _ZBufferParams);

            float2 uvRight = screenUV + float2(texel.x, 0.0);
            float2 uvLeft = screenUV - float2(texel.x, 0.0);
            float2 uvUp = screenUV + float2(0.0, texel.y);
            float2 uvDown = screenUV - float2(0.0, texel.y);

            float rawRight = SampleCausticsDepth(uvRight);
            float rawLeft = SampleCausticsDepth(uvLeft);
            float rawUp = SampleCausticsDepth(uvUp);
            float rawDown = SampleCausticsDepth(uvDown);

            float deltaRight = ResolveDepthValid(rawRight) > 0.5
                ? abs(LinearEyeDepth(rawRight, _ZBufferParams) - centerEyeDepth)
                : H8_CAUSTIC_NORMAL_TAP_REJECT;
            float deltaLeft = ResolveDepthValid(rawLeft) > 0.5
                ? abs(LinearEyeDepth(rawLeft, _ZBufferParams) - centerEyeDepth)
                : H8_CAUSTIC_NORMAL_TAP_REJECT;
            float deltaUp = ResolveDepthValid(rawUp) > 0.5
                ? abs(LinearEyeDepth(rawUp, _ZBufferParams) - centerEyeDepth)
                : H8_CAUSTIC_NORMAL_TAP_REJECT;
            float deltaDown = ResolveDepthValid(rawDown) > 0.5
                ? abs(LinearEyeDepth(rawDown, _ZBufferParams) - centerEyeDepth)
                : H8_CAUSTIC_NORMAL_TAP_REJECT;

            // Both taps dead on either axis: report level ground, which leaves caustic energy as authored.
            [branch]
            if (min(deltaRight, deltaLeft) >= H8_CAUSTIC_NORMAL_TAP_REJECT ||
                min(deltaUp, deltaDown) >= H8_CAUSTIC_NORMAL_TAP_REJECT)
            {
                return float3(0.0, 1.0, 0.0);
            }

            // Select the winning neighbour first so only one world-space reconstruction runs per axis.
            bool useRight = deltaRight <= deltaLeft;
            bool useUp = deltaUp <= deltaDown;
            float2 uvX = useRight ? uvRight : uvLeft;
            float2 uvY = useUp ? uvUp : uvDown;
            float rawX = useRight ? rawRight : rawLeft;
            float rawY = useUp ? rawUp : rawDown;
            float3 alongX = (ComputeWorldSpacePosition(uvX, rawX, UNITY_MATRIX_I_VP) - centerWorldPos) *
                (useRight ? 1.0 : -1.0);
            float3 alongY = (ComputeWorldSpacePosition(uvY, rawY, UNITY_MATRIX_I_VP) - centerWorldPos) *
                (useUp ? 1.0 : -1.0);

            // Cross-product winding flips with UNITY_UV_STARTS_AT_TOP and with which neighbour won, so the
            // result is oriented against the view vector instead of trusting the sign. Every pixel that
            // survived the depth test faces the camera.
            float3 geometric = cross(alongY, alongX);
            float3 towardCamera = GetCameraPositionWS() - centerWorldPos;
            geometric *= (dot(geometric, towardCamera) < 0.0) ? -1.0 : 1.0;
            return SafeNormalize3(geometric, float3(0.0, 1.0, 0.0));
        }

        // Caustics are refracted light landing ON a surface, so the energy carries the incidence cosine of
        // the projection. Without it the sun-aligned planar projection in ProjectCausticUv paints
        // full-strength ribbons down vertical cliffs, and because the projected UV barely moves along a
        // vertical face those ribbons smear into vertical streaks. This is the math of the forward path's
        // HectonCoreLitEvaluateDirectionalCausticsWeightFromUnitNormal, which no shader had wired up.
        //
        // The up-mask from that same header is deliberately NOT multiplied in here: with an overhead sun it
        // measures the same quantity as the cosine, so combining them squares the falloff and over-darkens
        // moderate slopes. Foreshortening of a planar projection is the cosine, nothing more.
        float ResolveCausticSlopeFade(float3 normalWS, float3 sunToSurface, float quality)
        {
            float incidence = saturate(dot(normalWS, -sunToSurface));
            // Physical and linear above the terminator band; the smoothstep only removes the derivative cut
            // at the clamp, so grazing faces fade out C1-continuously instead of stopping dead.
            float shaped = incidence * smoothstep(0.0, 0.18, incidence);
            // Turbid water still delivers multiply-scattered light to near-vertical rock, and a hard zero
            // would draw a visible seam at the cliff edge. Mirrors the floored up-mask shape that
            // Hecton_AbyssalVoxelRock.shader uses. Truer terminator as quality rises.
            float floorEnergy = lerp(0.12, 0.04, saturate(quality));
            return lerp(floorEnergy, 1.0, shaped);
        }

        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 screenUV = UnityStereoTransformScreenSpaceTex(input.screenUV);
            half4 sourceColor = SAMPLE_TEXTURE2D_X(_HectonDeferredCausticsSource, sampler_LinearClamp, screenUV);
            float rawDepth = SampleCausticsDepth(screenUV);
            [branch]
            if (ResolveDepthValid(rawDepth) <= 0.5)
                return sourceColor;

            float linearEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
            float maxDepthMeters = max(SafeFinite(IntensityAndDepthFalloff.z, 0.0), 0.0);
            [branch]
            if (linearEyeDepth > maxDepthMeters || maxDepthMeters <= 0.001)
                return sourceColor;

            float quality = saturate(SafeFinite(QualityAndColor.x, 0.0));
            float intensity = max(SafeFinite(IntensityAndDepthFalloff.x, 0.0), 0.0);
            [branch]
            if (intensity <= 0.0001 || quality <= 0.0001)
                return sourceColor;

            float3 worldPos = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
            float3 sunToSurface = SafeNormalize3(ProjectionVectorAndScale.xyz, float3(0.0, -1.0, 0.0));
            float baseScale = max(SafeFinite(ProjectionVectorAndScale.w, 0.05), 0.001);
            float2 uv0 = ProjectCausticUv(worldPos, sunToSurface, baseScale);

            float atlasWeight = saturate(_HectonBakedCausticAtlasParams.x);
            float proceduralWeight = 1.0 - atlasWeight;
            half3 causticRgb = half3(0.0, 0.0, 0.0);
            [branch]
            if (atlasWeight > 0.0001)
            {
                causticRgb += SampleBakedCausticAtlas(uv0, NoiseAnimationSpeed.z, quality) * (half)atlasWeight;
            }

            [branch]
            if (proceduralWeight > 0.0001)
            {
                float layer0 = CausticLineLayer(uv0);
                float secondWeight = smoothstep(0.34, 0.82, quality);
                float chromaWeight = smoothstep(0.62, 1.0, quality);
                float caustic = layer0;
                [branch]
                if (secondWeight > 0.0001)
                {
                    float2 uv1 = uv0 * (1.731 + quality * 0.19) + float2(17.31, -9.42) + NoiseAnimationSpeed.z * float2(-0.019, 0.027);
                    caustic += CausticLineLayer(uv1) * secondWeight * 0.62;
                }

                float chromaticDispersion = saturate(NoiseAnimationSpeed.w) * chromaWeight;
                float causticR = caustic;
                float causticB = caustic;
                [branch]
                if (chromaticDispersion > 0.0001)
                {
                    causticR = lerp(caustic, CausticLineLayer(uv0 + sunToSurface.xz * (0.035 + chromaticDispersion * 0.055)), chromaticDispersion);
                    causticB = lerp(caustic, CausticLineLayer(uv0 - sunToSurface.xz * (0.029 + chromaticDispersion * 0.051)), chromaticDispersion);
                }

                causticRgb += half3(causticR, caustic, causticB) * (half)proceduralWeight;
            }

            float depthFade = saturate(1.0 - linearEyeDepth * max(IntensityAndDepthFalloff.y, 0.00001));
            depthFade *= depthFade;
            float sdfOcclusion = ResolveSdfCavernOcclusion(worldPos, sunToSurface, quality, IntensityAndDepthFalloff.w);
            float waterlineMask = ResolveBakedWaterlineMask(worldPos);
            float3 geometricNormalWS = ResolveGeometricNormalWS(screenUV, worldPos, rawDepth);
            float slopeFade = ResolveCausticSlopeFade(geometricNormalWS, sunToSurface, quality);
            half3 causticTint = half3(QualityAndColor.y, QualityAndColor.z, QualityAndColor.w);
            causticRgb *= causticTint;
            half energy = (half)(intensity * depthFade * sdfOcclusion * waterlineMask * slopeFade);
            sourceColor.rgb = sourceColor.rgb + sourceColor.rgb * causticRgb * energy;
            return sourceColor;
        }
        ENDHLSL

        Pass
        {
            Name "DeferredCaustics"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }

    FallBack Off
}
