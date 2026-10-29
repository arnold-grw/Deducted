// Full-screen 1-bit pass: pixelates the screen to a low resolution and
// reduces every pixel to pure black or white using a brightness threshold,
// with optional ordered (Bayer) dithering for in-between greys.
// Black and white can be swapped for any two colours (Dark / Light Colour).
// Used by a Full Screen Pass Renderer Feature on the URP renderer.
Shader "Deducted/OneBit"
{
    Properties
    {
        _Threshold ("Threshold", Range(0, 1)) = 0.5
        _PixelHeight ("Vertical Resolution (pixels)", Float) = 120
        _Exposure ("Exposure", Range(0, 4)) = 1
        _Contrast ("Contrast", Range(0, 4)) = 1
        _BlackPoint ("Black Point (darker = pure black)", Range(0, 1)) = 0.05
        _WhitePoint ("White Point (brighter = pure white)", Range(0, 1)) = 0.95
        [Toggle] _Invert ("Invert", Float) = 0
        [Toggle] _Pixelate ("Pixelate", Float) = 1

        [Header(Colours)]
        _DarkColor ("Dark Colour (replaces black)", Color) = (0, 0, 0, 1)
        _LightColor ("Light Colour (replaces white)", Color) = (1, 1, 1, 1)

        [Header(Dithering)]
        _DitherStrength ("Dither Strength", Range(0, 1)) = 1
        [Enum(Bayer 8x8, 8, Bayer 4x4, 4, Bayer 2x2, 2)] _DitherSize ("Dither Pattern", Float) = 4
        [IntRange] _DitherScale ("Dither Scale (pixels per dot)", Range(1, 4)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "OneBit"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Threshold;
            float _PixelHeight;
            float _Exposure;
            float _Contrast;
            float _BlackPoint;
            float _WhitePoint;
            float _Invert;
            float _Pixelate;
            float _DitherStrength;
            float _DitherSize;
            float _DitherScale;
            half4 _DarkColor;
            half4 _LightColor;

            // Recursive Bayer matrix, returns a threshold in [0, 1) for an integer pixel coordinate.
            float Bayer2(float2 p)
            {
                p = floor(p);
                return frac(p.x * 0.5 + p.y * p.y * 0.75);
            }

            float Bayer4(float2 p) { return Bayer2(p * 0.5) * 0.25 + Bayer2(p); }
            float Bayer8(float2 p) { return Bayer4(p * 0.5) * 0.25 + Bayer2(p); }

            // Bayer value centred in its step, so it lies strictly inside (0, 1).
            float BayerValue(float2 pixel)
            {
                float cells = _DitherSize * _DitherSize;
                float b = _DitherSize > 6.0 ? Bayer8(pixel) : (_DitherSize > 3.0 ? Bayer4(pixel) : Bayer2(pixel));
                return b + 0.5 / cells;
            }

            // Bends brightness so that _Threshold lands on 0.5 while 0 stays 0 and 1 stays 1.
            // That way pure black never gets white dots and pure white never gets black ones.
            float ApplyThreshold(float luma)
            {
                float t = clamp(_Threshold, 0.001, 0.999);
                return luma <= t ? 0.5 * luma / t : 0.5 + 0.5 * (luma - t) / (1.0 - t);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                // Low-res grid: height is fixed, width follows the aspect ratio.
                float2 lowRes = float2(round(_PixelHeight * _ScreenParams.x / _ScreenParams.y), _PixelHeight);
                bool pixelate = _Pixelate > 0.5;
                float2 gridRes = pixelate ? lowRes : _ScreenParams.xy;
                float2 pixel = floor(uv * gridRes);
                if (pixelate)
                    uv = (pixel + 0.5) / lowRes;

                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).rgb;

                // Perceptual brightness so the threshold slider feels linear to the eye.
                half luma = Luminance(color) * _Exposure;
                luma = pow(saturate(luma), 1.0 / 2.2);
                luma = saturate((luma - 0.5) * _Contrast + 0.5);
                // Clip near-black / near-white (e.g. ambient light) to solid colour.
                luma = saturate((luma - _BlackPoint) / max(_WhitePoint - _BlackPoint, 0.001));
                luma = ApplyThreshold(luma);

                // The dither pattern is locked to the low-res pixel grid so it stays crisp.
                // Strength 0 compares against a flat 0.5 (hard cut), 1 uses the full pattern.
                float bayer = BayerValue(floor(pixel / max(_DitherScale, 1.0)));
                float cutoff = lerp(0.5, bayer, _DitherStrength);

                half bit = luma > cutoff ? 1.0 : 0.0;
                bit = lerp(bit, 1.0 - bit, step(0.5, _Invert));

                // Still only two colours: the dark and the light one picked on the material.
                return half4(lerp(_DarkColor.rgb, _LightColor.rgb, bit), 1.0);
            }
            ENDHLSL
        }
    }
}
