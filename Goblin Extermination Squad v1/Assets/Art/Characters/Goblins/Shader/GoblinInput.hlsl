#ifndef GOBLIN_INPUT_INCLUDED
#define GOBLIN_INPUT_INCLUDED

// Shared by every pass of Goblins/Goblin: material properties, the per-instance colour, and the goblin's tint.
// v1's Goblin.shadergraph, hand-written: same properties and look, less work per pixel.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _Texture_ST;
    half4 _Color;
    half4 _EmissionColor;
    half _EmissionValue;
    half _UseVertex;
    half _GradientCenter;
    half _GradientSize;
    half _GradientDarkness;
    half _LightingInfluence;
CBUFFER_END

TEXTURE2D(_Texture);
SAMPLER(sampler_Texture);

// Per goblin, set by Horde_RenderGroup: rgb = tint (linear), a = hit flash 0-1. Black (or unset) uses the material's
// Color and Emission Value, so the material also works on a plain MeshRenderer.
UNITY_INSTANCING_BUFFER_START(GoblinInstanceProperties)
    UNITY_DEFINE_INSTANCED_PROP(float4, _GoblinColorFlash)
UNITY_INSTANCING_BUFFER_END(GoblinInstanceProperties)

// The goblin's colour at a vertex, before the texture: its tint, times the vertex colour (if Use Vertex), darkened
// toward the bottom of the mesh by the gradient. a = flash. Per vertex, not per pixel: it only varies with height.
half4 GoblinTint(float3 positionOS, half4 vertexColour)
{
    half4 colourFlash = half4(_Color.rgb, _EmissionValue);

#if UNITY_ANY_INSTANCING_ENABLED
    float4 instance = UNITY_ACCESS_INSTANCED_PROP(GoblinInstanceProperties, _GoblinColorFlash);

    if (dot(instance.rgb, float3(1.0, 1.0, 1.0)) > 0.0)
        colourFlash = half4(instance);
#endif

    half3 tint = colourFlash.rgb * lerp(half3(1.0, 1.0, 1.0), vertexColour.rgb, _UseVertex);

    // Height gradient: 0 at Center - Size/2, 1 at Center + Size/2 (object space), smoothed, darkening the bottom
    half halfSize = max(_GradientSize * 0.5, 1e-4);
    half height = saturate((positionOS.y - (_GradientCenter - halfSize)) / (2.0 * halfSize));
    half shade = 1.0 - _GradientDarkness * (1.0 - smoothstep(0.0, 1.0, height));

    return half4(tint * shade, colourFlash.a);
}

// Final colour before lighting: texture times tint, flashed toward the emission colour
half3 GoblinColour(float2 uv, half4 tint)
{
    half3 albedo = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, uv).rgb * tint.rgb;
    return lerp(albedo, _EmissionColor.rgb, tint.a);
}

#endif
