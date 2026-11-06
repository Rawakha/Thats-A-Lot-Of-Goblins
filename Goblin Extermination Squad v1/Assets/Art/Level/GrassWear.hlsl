#ifndef GOBLINS_GRASS_WEAR_INCLUDED
#define GOBLINS_GRASS_WEAR_INCLUDED
// Uniforms live in each material's UnityPerMaterial buffer. Texture/mapping are supplied per authored ground.
TEXTURE2D(_WearMask);
SAMPLER(sampler_WearMask);
float GrassWearAt(float3 world)
{
    if (_WearEnabled < 0.5) return 0;
    float3 local = mul(_WearWorldToLocal,float4(world,1)).xyz;
    float2 uv = (local.xz-_WearBounds.xy)*_WearBounds.zw;
    if (any(uv < 0) || any(uv > 1)) return 0;
    return SAMPLE_TEXTURE2D_LOD(_WearMask,sampler_WearMask,uv,0).r;
}
void ClipGrassWear(float3 world)
{
    float worn = GrassWearAt(world);
    if (_WearLayer > 0.5) clip(_WearEnabled > 0.5 ? worn-_WearThreshold : -1);
    else if (_WearEnabled > 0.5) clip(_WearThreshold-worn-0.00001);
}
#endif
