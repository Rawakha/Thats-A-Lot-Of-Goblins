Shader "UI/Gradient Drop Shadow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [Header(Gradient)]
        _GradientTop ("Top Colour", Color) = (1, 1, 1, 1)
        _GradientBottom ("Bottom Colour", Color) = (0.55, 0.55, 0.55, 1)
        _GradientDirection ("Direction (X, Y)", Vector) = (0, 1, 0, 0)
        _GradientOffset ("Offset", Range(-1, 1)) = 0
        _GradientStrength ("Strength", Range(0, 1)) = 1

        [Header(Drop Shadow)]
        _ShadowColor ("Colour", Color) = (0, 0, 0, 0.5)
        _ShadowOffset ("Offset (Screen Pixels)", Vector) = (4, 4, 0, 0)
        _ShadowSoftness ("Softness (Screen Pixels)", Range(0, 12)) = 3
        _ShadowPadding ("Padding (Screen Pixels)", Range(0, 64)) = 16

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _AlphaTex;

            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _EnableExternalAlpha;

            fixed4 _GradientTop;
            fixed4 _GradientBottom;
            float4 _GradientDirection;
            float _GradientOffset;
            float _GradientStrength;

            fixed4 _ShadowColor;
            float4 _ShadowOffset;
            float _ShadowSoftness;
            float _ShadowPadding;

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.worldPosition = input.vertex;

                // Enlarge a Simple Image's quad in screen space. Its UVs are
                // remapped in the fragment shader so the image does not stretch.
                float4 clipPosition = UnityObjectToClipPos(input.vertex);
                float2 cornerDirection = input.texcoord * 2.0 - 1.0;
                float2 pixelToClip = (2.0 * clipPosition.w) / _ScreenParams.xy;
                clipPosition.xy += cornerDirection * _ShadowPadding * pixelToClip;
                output.vertex = clipPosition;
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 SampleSprite(float2 uv)
            {
                fixed4 result = tex2D(_MainTex, uv);

                #if ETC1_EXTERNAL_ALPHA
                fixed externalAlpha = tex2D(_AlphaTex, uv).r;
                result.a = lerp(result.a, externalAlpha, _EnableExternalAlpha);
                #endif

                return result;
            }

            fixed IsInsideImage(float2 uv)
            {
                return step(0.0, uv.x)
                    * step(uv.x, 1.0)
                    * step(0.0, uv.y)
                    * step(uv.y, 1.0);
            }

            fixed SampleAlpha(float2 uv)
            {
                return saturate(SampleSprite(uv).a + _TextureSampleAdd.a)
                    * IsInsideImage(uv);
            }

            fixed BlurredShadowAlpha(float2 uv, float2 uvDx, float2 uvDy)
            {
                // Convert the inspector's screen-pixel values into UV offsets.
                float2 shadowUV = uv
                    - (uvDx * _ShadowOffset.x)
                    - (uvDy * _ShadowOffset.y);

                float2 blurX = uvDx * _ShadowSoftness;
                float2 blurY = uvDy * _ShadowSoftness;

                // Nine-tap Gaussian-style blur. Weights total 16.
                fixed alpha = SampleAlpha(shadowUV) * 4.0;
                alpha += SampleAlpha(shadowUV + blurX) * 2.0;
                alpha += SampleAlpha(shadowUV - blurX) * 2.0;
                alpha += SampleAlpha(shadowUV + blurY) * 2.0;
                alpha += SampleAlpha(shadowUV - blurY) * 2.0;
                alpha += SampleAlpha(shadowUV + blurX + blurY);
                alpha += SampleAlpha(shadowUV + blurX - blurY);
                alpha += SampleAlpha(shadowUV - blurX + blurY);
                alpha += SampleAlpha(shadowUV - blurX - blurY);
                return saturate(alpha * (1.0 / 16.0));
            }

            float2 ExpandedClipPosition(v2f input, float2 contentUV)
            {
                // worldPosition still interpolates across the original quad.
                // Extrapolate it into the new padding so RectMask2D clips the
                // expanded shadow at the correct canvas position.
                float2 rawUV = input.texcoord;
                float2 uvDx = ddx(rawUV);
                float2 uvDy = ddy(rawUV);
                float2 worldDx = ddx(input.worldPosition.xy);
                float2 worldDy = ddy(input.worldPosition.xy);

                float determinant = uvDx.x * uvDy.y - uvDy.x * uvDx.y;
                determinant = abs(determinant) < 0.0000001
                    ? (determinant < 0.0 ? -0.0000001 : 0.0000001)
                    : determinant;

                float2 uvDifference = contentUV - rawUV;
                float screenDifferenceX =
                    (uvDifference.x * uvDy.y - uvDy.x * uvDifference.y)
                    / determinant;
                float screenDifferenceY =
                    (uvDx.x * uvDifference.y - uvDifference.x * uvDx.y)
                    / determinant;

                return input.worldPosition.xy
                    + worldDx * screenDifferenceX
                    + worldDy * screenDifferenceY;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                // input.texcoord now spans the enlarged quad. Work out how much
                // of that range belongs to padding, then restore the original UV.
                float2 rawUV = input.texcoord;
                float2 uvChangePerPixel = float2(
                    length(float2(ddx(rawUV.x), ddy(rawUV.x))),
                    length(float2(ddx(rawUV.y), ddy(rawUV.y)))
                );

                float2 paddingUV = min(
                    uvChangePerPixel * _ShadowPadding,
                    float2(0.49, 0.49)
                );
                float2 contentUV =
                    (rawUV - paddingUV) / max(1.0 - paddingUV * 2.0, 0.0001);

                fixed insideImage = IsInsideImage(contentUV);
                fixed4 sprite =
                    (SampleSprite(contentUV) + _TextureSampleAdd)
                    * input.color
                    * insideImage;

                float2 direction = _GradientDirection.xy;
                float directionLength = max(length(direction), 0.0001);
                direction /= directionLength;

                // Scale the projection so every angle spans the full 0-1 range.
                float projectionExtent = max(abs(direction.x) + abs(direction.y), 0.0001);
                float gradientT = dot(contentUV - 0.5, direction) / projectionExtent + 0.5;
                gradientT = saturate(gradientT + _GradientOffset);

                fixed3 gradient = lerp(_GradientBottom.rgb, _GradientTop.rgb, gradientT);
                sprite.rgb *= lerp(fixed3(1, 1, 1), gradient, _GradientStrength);

                float2 uvDx = ddx(contentUV);
                float2 uvDy = ddy(contentUV);
                fixed shadowAlpha = BlurredShadowAlpha(contentUV, uvDx, uvDy);
                shadowAlpha *= input.color.a * _ShadowColor.a;

                // Composite the image over its shadow before blending with the UI.
                fixed outputAlpha = sprite.a + shadowAlpha * (1.0 - sprite.a);
                fixed3 premultipliedColor =
                    sprite.rgb * sprite.a
                    + _ShadowColor.rgb * shadowAlpha * (1.0 - sprite.a);

                fixed3 outputColor = premultipliedColor / max(outputAlpha, 0.0001);
                fixed4 result = fixed4(outputColor, outputAlpha);

                #ifdef UNITY_UI_CLIP_RECT
                float2 clippingPosition = ExpandedClipPosition(input, contentUV);
                result.a *= UnityGet2DClipping(clippingPosition, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif

                return result;
            }
            ENDCG
        }
    }
}
