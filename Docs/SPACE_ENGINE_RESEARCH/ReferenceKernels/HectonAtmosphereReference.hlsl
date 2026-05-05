#ifndef HECTON_ATMOSPHERE_REFERENCE_INCLUDED
#define HECTON_ATMOSPHERE_REFERENCE_INCLUDED

// Status: REFERENCE
// Source: clean-room translation from SpaceEngine 0.9.9 exposed atmosphere
// parameters, shader manifest names, and cache symbols. Not SpaceEngine source.

#define H8_PI 3.14159265359

struct H8AtmoParams
{
    float planetRadiusKm;
    float atmosphereRadiusKm;
    float rayleighHeightKm;
    float mieHeightKm;
    float mieG;
    float3 betaRayleigh;
    float3 betaMieSca;
    float3 betaMieExt;
};

float H8PhaseRayleigh(float mu)
{
    return (3.0 / (16.0 * H8_PI)) * (1.0 + mu * mu);
}

float H8PhaseMieHG(float mu, float g)
{
    float g2 = g * g;
    float denom = max(1.0 + g2 - 2.0 * g * mu, 1e-3);
    return (1.0 - g2) / (4.0 * H8_PI * pow(denom, 1.5));
}

float H8AtmosphereDensity(float radiusKm, float planetRadiusKm, float scaleHeightKm)
{
    float h = max(0.0, radiusKm - planetRadiusKm);
    return exp(-h / max(scaleHeightKm, 1e-3));
}

float H8RaySphereExit(float3 ro, float3 rd, float radiusKm)
{
    float b = dot(ro, rd);
    float c = dot(ro, ro) - radiusKm * radiusKm;
    float d = b * b - c;
    return d > 0.0 ? -b + sqrt(d) : 0.0;
}

half3 H8SingleScatterLow(
    float3 eyeKm,
    float3 viewDir,
    float3 lightDir,
    H8AtmoParams p,
    int sampleCount)
{
    sampleCount = clamp(sampleCount, 2, 8);

    float tMax = H8RaySphereExit(eyeKm, viewDir, p.atmosphereRadiusKm);
    float dt = tMax / sampleCount;

    float optR = 0.0;
    float optM = 0.0;
    float3 sumR = 0.0;
    float3 sumM = 0.0;

    [loop]
    for (int i = 0; i < sampleCount; i++)
    {
        float t = (i + 0.5) * dt;
        float3 pos = eyeKm + viewDir * t;
        float r = length(pos);

        float rhoR = H8AtmosphereDensity(r, p.planetRadiusKm, p.rayleighHeightKm);
        float rhoM = H8AtmosphereDensity(r, p.planetRadiusKm, p.mieHeightKm);

        optR += rhoR * dt;
        optM += rhoM * dt;

        float tSun = H8RaySphereExit(pos, lightDir, p.atmosphereRadiusKm);
        float sunStep = tSun * 0.25;
        float sunOptR = 0.0;
        float sunOptM = 0.0;

        [unroll]
        for (int j = 0; j < 4; j++)
        {
            float3 sp = pos + lightDir * ((j + 0.5) * sunStep);
            float sr = length(sp);
            sunOptR += H8AtmosphereDensity(sr, p.planetRadiusKm, p.rayleighHeightKm) * sunStep;
            sunOptM += H8AtmosphereDensity(sr, p.planetRadiusKm, p.mieHeightKm) * sunStep;
        }

        float3 tau = p.betaRayleigh * (optR + sunOptR) + p.betaMieExt * (optM + sunOptM);
        float3 tr = exp(-tau);

        sumR += tr * rhoR * dt;
        sumM += tr * rhoM * dt;
    }

    float mu = dot(viewDir, lightDir);
    float3 rgb =
        sumR * p.betaRayleigh * H8PhaseRayleigh(mu) +
        sumM * p.betaMieSca * H8PhaseMieHG(mu, p.mieG);

    return (half3)max(rgb, 0.0);
}

#endif
