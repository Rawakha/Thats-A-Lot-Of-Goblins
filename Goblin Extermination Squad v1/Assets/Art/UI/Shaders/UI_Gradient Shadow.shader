// UI shader for TextMeshPro text and UI Images: a centre gradient (lighter through the middle, darker toward the top
// and bottom edges, like the reference HUD) and an optional drop shadow, both drawn in the one pass.
//
// Source picks how the texture is read:
//   Image: a sprite's colour and alpha (UI Image, Raw Image).
//   Text Bitmap: a TextMeshPro font atlas in a Raster render mode (alpha only).
//   Text SDF: a TextMeshPro font atlas in an SDF render mode; the edge is kept crisp at any size.
// Text: make a material whose name starts with the font asset's name (TextMeshPro lists presets by name, e.g.
// "m6x11 SDF HUD"), put it on this shader, drag the font's atlas texture (inside the font asset) into Texture, set
// Source, and pick it as the text's Material Preset. The gradient runs across each character (TMP's per-character UVs); set the text's Extra Settings
// > Vertical Mapping to Line to run it across the whole line instead. Images: make a material on this shader and set
// it on the Image. The gradient runs across the image's UVs.
//
// Drop shadow: a copy of the shape offset by Shadow Offset (texture pixels: atlas pixels for text, sprite pixels for
// images), drawn under it. It can only show inside the quad, so:
//   text: Padding widens each character's quad (TextMeshPro reads it); keep it at least the shadow offset and no
//     more than the font atlas's own padding (7 here) or neighbouring letters bleed in;
//   images: the sprite needs transparent space round its shape for the shadow to fall into.
// Supports UI masks (stencil and RectMask2D) like the default UI shader.
Shader "Goblins/UI Gradient Shadow"
{
    Properties
    {
        // Shown (not PerRendererData) so a text material can be given its font atlas; Images set it themselves
        _MainTex ("Texture (font atlas for text)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [KeywordEnum(Image, Text Bitmap, Text SDF)] _Source ("Source", Float) = 0

        [Header(Centre Gradient)]
        _GradientStrength ("Strength", Range(0, 1)) = 1
        _CentreTint ("Centre Tint", Color) = (1, 1, 1, 1)
        _EdgeTint ("Edge Tint", Color) = (0.6, 0.6, 0.65, 1)
        _GradientCentre ("Centre Height", Range(0, 1)) = 0.5
        _GradientFalloff ("Falloff", Range(0.1, 8)) = 1.5

        [Header(Drop Shadow)]
        [Toggle(_SHADOW_ON)] _Shadow ("Drop Shadow", Float) = 0
        _ShadowColor ("Shadow Color", Color) = (0, 0, 0, 0.75)
        _ShadowOffset ("Shadow Offset (texture pixels)", Vector) = (3, -3, 0, 0)
        _ShadowSoftness ("Shadow Softness (SDF text)", Range(0, 0.5)) = 0
        _Padding ("Padding (text, atlas pixels)", Float) = 4

        // Standard UI properties: masks, colour mask, clipping
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma shader_feature_local _SOURCE_IMAGE _SOURCE_TEXT_BITMAP _SOURCE_TEXT_SDF
            #pragma shader_feature_local _ _SHADOW_ON

            struct Attributes
            {
                float4 vertex : POSITION;
                float4 colour : COLOR;
                float4 uv0 : TEXCOORD0;     // texture (TMP packs more into zw; only xy is used)
                float2 uv1 : TEXCOORD1;     // TMP's per-character 0-1 mapping
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                fixed4 colour : COLOR;
                float2 uv : TEXCOORD0;
                float gradient : TEXCOORD1;     // 0 bottom, 1 top, for the centre gradient
                float4 world : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            half _GradientStrength;
            fixed4 _CentreTint;
            fixed4 _EdgeTint;
            half _GradientCentre;
            half _GradientFalloff;

            fixed4 _ShadowColor;
            float4 _ShadowOffset;
            half _ShadowSoftness;

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.world = input.vertex;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv0.xy, _MainTex);
                output.colour = input.colour * _Color;

            #if defined(_SOURCE_IMAGE)
                output.gradient = input.uv0.y;
            #else
                output.gradient = input.uv1.y;
            #endif

                return output;
            }

            // How much of the shape covers this point of the texture, 0-1
            half Coverage(float2 uv, half softness)
            {
                half a = tex2D(_MainTex, uv).a;

            #if defined(_SOURCE_TEXT_SDF)
                // The distance field's edge is at 0.5; fwidth keeps it one screen pixel wide at any size
                half width = max(fwidth(a) * 0.5, 0.0001);
                return smoothstep(0.5 - width - softness, 0.5 + width, a);
            #else
                return a;
            #endif
            }

            fixed4 Fragment(Varyings input) : SV_Target
            {
                // The shape itself
                half coverage = Coverage(input.uv, 0);
                fixed3 colour = input.colour.rgb;

            #if defined(_SOURCE_IMAGE)
                fixed4 sprite = tex2D(_MainTex, input.uv) + _TextureSampleAdd;
                colour *= sprite.rgb;
                coverage = sprite.a;
            #endif

                // Centre gradient: 1 at the centre height, 0 at the top and bottom, shaped by the falloff
                half fromCentre = input.gradient < _GradientCentre
                    ? 1 - input.gradient / max(_GradientCentre, 0.001)
                    : (input.gradient - _GradientCentre) / max(1 - _GradientCentre, 0.001);
                half centre = pow(saturate(1 - fromCentre), _GradientFalloff);
                fixed3 tint = lerp(_EdgeTint.rgb, _CentreTint.rgb, centre);
                colour *= lerp(fixed3(1, 1, 1), tint, _GradientStrength);

                half alpha = coverage * input.colour.a;
                fixed4 result = fixed4(colour * alpha, alpha);     // premultiplied

            #if defined(_SHADOW_ON)
                // The same shape, moved by the offset, under the face
                half shadow = Coverage(input.uv - _ShadowOffset.xy * _MainTex_TexelSize.xy, _ShadowSoftness);
                half shadowAlpha = shadow * _ShadowColor.a * input.colour.a * (1 - alpha);
                result.rgb += _ShadowColor.rgb * shadowAlpha;
                result.a += shadowAlpha;
            #endif

            #if defined(UNITY_UI_CLIP_RECT)
                result *= UnityGet2DClipping(input.world.xy, _ClipRect);
            #endif

            #if defined(UNITY_UI_ALPHACLIP)
                clip(result.a - 0.001);
            #endif

                return result;
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}
