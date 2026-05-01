Shader "Custom/MapDistrictFocus"
{
    Properties
    {
        [MainTexture] _MainTex ("Map Texture", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _DistrictMaskTex ("District Mask Texture", 2D) = "white" {}
        _PreviousDistrictColor ("Previous District Mask Color", Color) = (1, 0, 0, 1)
        _HoveredDistrictColor ("Hovered District Mask Color", Color) = (1, 0, 0, 1)
        _DistrictBlend ("District Blend", Range(0, 1)) = 1
        _FocusStrength ("Focus Strength", Range(0, 1)) = 0
        _OutsideBrightness ("Outside Brightness", Range(0, 1)) = 0.68
        _OutsideSaturation ("Outside Saturation", Range(0, 1)) = 0.35
        _HoverBrightness ("Hover Brightness", Range(0.5, 1.5)) = 1.08
        _MaskTolerance ("Mask Tolerance", Range(0, 0.25)) = 0.025
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.25)) = 0.035
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "DistrictFocus"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_DistrictMaskTex);
            SAMPLER(sampler_DistrictMaskTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _PreviousDistrictColor;
                half4 _HoveredDistrictColor;
                half _DistrictBlend;
                half _FocusStrength;
                half _OutsideBrightness;
                half _OutsideSaturation;
                half _HoverBrightness;
                half _MaskTolerance;
                half _EdgeSoftness;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            half3 ApplySaturation(half3 color, half saturation)
            {
                half luminance = dot(color, half3(0.2126, 0.7152, 0.0722));
                return lerp(half3(luminance, luminance, luminance), color, saturation);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 mapColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;

                half3 maskColor = SAMPLE_TEXTURE2D(_DistrictMaskTex, sampler_DistrictMaskTex, input.uv).rgb;
                half previousColorDistance = distance(maskColor, _PreviousDistrictColor.rgb);
                half previousMask = 1.0 - smoothstep(
                    _MaskTolerance,
                    _MaskTolerance + max(_EdgeSoftness, 0.001),
                    previousColorDistance);

                half hoveredColorDistance = distance(maskColor, _HoveredDistrictColor.rgb);
                half hoveredMask = 1.0 - smoothstep(
                    _MaskTolerance,
                    _MaskTolerance + max(_EdgeSoftness, 0.001),
                    hoveredColorDistance);

                half selectedMask = lerp(previousMask, hoveredMask, saturate(_DistrictBlend));

                half3 outsideColor = ApplySaturation(mapColor.rgb, _OutsideSaturation) * _OutsideBrightness;
                half3 hoveredColor = saturate(mapColor.rgb * _HoverBrightness);
                half3 focusedColor = lerp(outsideColor, hoveredColor, selectedMask);

                mapColor.rgb = lerp(mapColor.rgb, focusedColor, saturate(_FocusStrength));
                return mapColor;
            }
            ENDHLSL
        }
    }
}
