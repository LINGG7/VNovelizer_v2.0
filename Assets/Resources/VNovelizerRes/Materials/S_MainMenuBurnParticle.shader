Shader "VNovelizer/MainMenuBurn/Particle"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "black" {}
        _CoreColor ("Core Color", Color) = (1, 1, 0.8, 1)
        _EdgeColor ("Edge Color", Color) = (1, 0.08, 0.002, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1
        _GlobalFade ("Global Fade", Range(0, 1)) = 1
        _UvRect ("Texture Crop", Vector) = (0, 0, 1, 1)
        _FlipV ("Flip Texture Vertically", Range(0, 1)) = 0
        _TextureColorWeight ("Authored Color Weight", Range(0, 1)) = 0.8
        _ProceduralShape ("Procedural Shape (0 Texture, 1 Spark, 2 Falling Flame)", Range(0, 2)) = 0
        _Cutoff ("Black Cutoff", Range(0, 0.5)) = 0.02
        _Softness ("Edge Softness", Range(0.001, 0.5)) = 0.12
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Overlay+100"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
        }

        Pass
        {
            Name "MainMenuBurnParticle"
            Blend [_SrcBlend] [_DstBlend]
            Cull Off
            Lighting Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _CoreColor;
                half4 _EdgeColor;
                half _Intensity;
                half _GlobalFade;
                float4 _UvRect;
                half _FlipV;
                half _TextureColorWeight;
                half _ProceduralShape;
                half _Cutoff;
                half _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 sampleUv = _UvRect.xy + input.uv * _UvRect.zw;
                sampleUv.y = lerp(sampleUv.y, _UvRect.y + (1.0 - input.uv.y) * _UvRect.w, _FlipV);
                half3 sampleColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUv).rgb;
                half luminance = dot(sampleColor, half3(0.299h, 0.587h, 0.114h));

                float2 centered = input.uv * 2.0 - 1.0;
                half sparkShape = saturate(1.0h - dot(centered, centered));
                sparkShape = smoothstep(0.05h, 0.82h, sparkShape);
                half fallingFlameMode = step(1.5h, _ProceduralShape);
                half sparkMode = saturate(_ProceduralShape) * (1.0h - fallingFlameMode);
                luminance = lerp(luminance, sparkShape, sparkMode);
                sampleColor = lerp(sampleColor,
                    half3(sparkShape, sparkShape * 0.48h, sparkShape * 0.05h), sparkMode);

                float flameY = saturate(input.uv.y);
                float flameTime = _Time.y;
                float sway = sin(flameY * 10.8 - flameTime * 9.6) * flameY * 0.095
                    + sin(flameY * 22.4 + flameTime * 6.7 + 1.3) * flameY * 0.038;
                float flameX = (input.uv.x - 0.5) - sway;
                float flameWidth = lerp(0.43, 0.055, smoothstep(0.04, 0.96, flameY));
                half sideBody = 1.0h - smoothstep(flameWidth, flameWidth + 0.075, abs(flameX));
                half baseFade = smoothstep(0.0h, 0.055h, flameY);
                half tipHeight = 0.91h + sin(flameTime * 7.8h) * 0.035h;
                half tipFade = 1.0h - smoothstep(tipHeight - 0.12h, tipHeight, flameY);
                half tongueBreakup = saturate(0.76h
                    + sin(flameY * 17.0h - flameTime * 11.2h + flameX * 8.0h) * 0.17h
                    + sin(flameY * 29.0h + flameTime * 7.1h - flameX * 13.0h) * 0.11h);
                half flameShape = smoothstep(0.08h, 0.84h, sideBody * tongueBreakup) * baseFade * tipFade;
                half innerWidth = 1.0h - smoothstep(flameWidth * 0.18h, flameWidth * 0.62h + 0.001h, abs(flameX));
                half verticalHeat = 1.0h - smoothstep(0.46h, 0.9h, flameY);
                half flameCore = flameShape * innerWidth * verticalHeat;
                half flameLuminance = max(flameShape * 0.58h, flameCore);
                half3 flameAuthored = lerp(half3(1.0h, 0.055h, 0.001h),
                    half3(1.0h, 1.0h, 0.82h), saturate(flameCore * 1.25h + verticalHeat * 0.18h));
                luminance = lerp(luminance, flameLuminance, fallingFlameMode);
                sampleColor = lerp(sampleColor, flameAuthored * flameShape, fallingFlameMode);

                half mask = smoothstep(_Cutoff, _Cutoff + max(_Softness, 0.001h), luminance);
                half depthShape = smoothstep(0.06h, 0.82h, luminance);
                half3 tintColor = lerp(_EdgeColor.rgb, _CoreColor.rgb, depthShape);
                half3 authoredColor = sampleColor * 1.28h;
                half3 volumeColor = lerp(tintColor, authoredColor, _TextureColorWeight);
                volumeColor *= input.color.rgb * _Intensity;
                half alpha = mask * input.color.a * lerp(_EdgeColor.a, _CoreColor.a, depthShape) * _GlobalFade;
                return half4(volumeColor, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
