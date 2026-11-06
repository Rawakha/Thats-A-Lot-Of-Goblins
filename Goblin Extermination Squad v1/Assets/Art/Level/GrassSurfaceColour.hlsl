#ifndef GOBLINS_GRASS_SURFACE_COLOUR_INCLUDED
#define GOBLINS_GRASS_SURFACE_COLOUR_INCLUDED

// Shared by the shelf material and future instanced grass. Use the same base tint, seed and settings.
// Sampling the blade root keeps each clump aligned with the colour of the surface beneath it.
float GrassColourHash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}
float GrassColourNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(GrassColourHash(cell), GrassColourHash(cell + float2(1, 0)), f.x),
        lerp(GrassColourHash(cell + float2(0, 1)), GrassColourHash(cell + float2(1, 1)), f.x), f.y);
}
half3 GrassSurfaceColour(float3 positionWS, half3 baseColour, float patchSize,
    float strength, float detail, float seed)
{
    float2 p = positionWS.xz / max(patchSize, 0.1);
    float2 offset = float2(seed * 3.17, seed * 7.31);
    float broad = GrassColourNoise(p + offset) * 2.0 - 1.0;
    float fine = GrassColourNoise(positionWS.xz * 1.7 + offset) * 2.0 - 1.0;
    return baseColour * (1.0 + broad * strength + fine * detail);
}
#endif
