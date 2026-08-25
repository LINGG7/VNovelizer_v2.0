Shader "VNovelizer/MainMenuBurn/FlameRibbon"
{
    Properties
    {
        _MainTex ("Continuous Flame Strip", 2D) = "black" {}
        _CoreColor ("Core Color", Color) = (1,1,0.8,1)
        _EdgeColor ("Edge Color", Color) = (1,0.08,0.002,1)
        _Intensity ("Intensity", Range(0,3)) = 1
        _GlobalFade ("Global Fade", Range(0,1)) = 1
        _SequenceTime ("Sequence Time", Float) = 0
        _LayerPhase ("Layer Phase", Float) = 0
        _TileCount ("Tile Count", Range(1,8)) = 4
        _Distortion ("Flow Distortion", Range(0,0.2)) = 0.065
        _TongueActivity ("Tongue Activity", Range(0.5,1.5)) = 1
        _Cutoff ("Black Cutoff", Range(0,0.2)) = 0.012
        _Softness ("Edge Softness", Range(0.001,0.4)) = 0.11
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Overlay+110"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
        }

        Pass
        {
            Name "ContinuousFlameRibbon"
            Blend SrcAlpha One
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
                float _SequenceTime;
                float _LayerPhase;
                float _TileCount;
                float _Distortion;
                half _TongueActivity;
                half _Cutoff;
                half _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 tongueData : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 tongueData : TEXCOORD1;
            };

            float MirroredRepeat(float value)
            {
                float f = frac(value * 0.5);
                return 1.0 - abs(f * 2.0 - 1.0);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.tongueData = input.tongueData;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float v = saturate(input.uv.y);
                float u = input.uv.x * _TileCount;
                float upperMotion = v * v;
                float longitudinalWave = sin(u * 2.7 + v * 8.5 - _SequenceTime * 5.2 + _LayerPhase);
                float curlA = (sin(v * 10.5 + _SequenceTime * 4.3 + _LayerPhase) * 0.72 + longitudinalWave * 0.48)
                    * _Distortion * upperMotion * _TongueActivity;
                float curlB = (sin(v * 15.5 - _SequenceTime * 3.4 + _LayerPhase * 1.7) * 0.58
                    + sin(u * 4.1 - _SequenceTime * 6.1) * 0.34)
                    * _Distortion * upperMotion * _TongueActivity;

                float2 uvA = float2(MirroredRepeat(u + curlA + _SequenceTime * 0.035),
                                    lerp(0.09, 0.93, v));
                float periodicOffset = sin(input.uv.x * 6.2831853) * 0.17;
                float2 uvB = float2(MirroredRepeat(u + periodicOffset - curlB - _SequenceTime * 0.026 + _LayerPhase * 0.13),
                                    lerp(0.075, 0.9, saturate(v * 1.015)));
                half3 sampleA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvA).rgb;
                half3 sampleB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvB).rgb;
                half lumaA = dot(sampleA, half3(0.299h, 0.587h, 0.114h));
                half lumaB = dot(sampleB, half3(0.299h, 0.587h, 0.114h));
                half upperBlend = smoothstep(0.24h, 0.72h, v);
                half3 filledAuthored = max(sampleA, sampleB * 0.68h);
                half filledLuminance = max(lumaA, lumaB * 0.68h);
                half3 splitAuthored = sampleA * 0.76h + sampleB * 0.18h;
                half splitLuminance = lumaA * (0.72h + lumaB * 0.28h);
                half3 authored = lerp(filledAuthored, splitAuthored, upperBlend);
                half luminance = lerp(filledLuminance, splitLuminance, upperBlend);

                half rootGlow = (1.0h - smoothstep(0.0h, 0.065h, v)) *
                    (0.78h + 0.22h * sin(input.uv.x * 91.0h + _SequenceTime * 7.0h + _LayerPhase));
                half breakup = sin(u * 5.7h - _SequenceTime * 7.1h + _LayerPhase)
                    + sin(u * 11.3h + _SequenceTime * 4.6h) * 0.48h;
                half animatedCutoff = _Cutoff + upperBlend * saturate(0.045h - breakup * 0.018h);
                half mask = smoothstep(animatedCutoff, animatedCutoff + max(_Softness * lerp(1.0h, 0.68h, upperBlend), 0.001h), luminance);
                mask = max(mask, rootGlow);
                half tonguePulse = saturate(input.tongueData.x);
                half proceduralNoise = saturate(0.64h
                    + sin(u * 7.3h - _SequenceTime * 8.7h + v * 5.1h) * 0.22h
                    + sin(u * 15.7h + _SequenceTime * 5.4h - v * 9.2h) * 0.14h);
                half tongueColumn = smoothstep(0.14h, 0.68h, tonguePulse * proceduralNoise);
                half verticalWindow = smoothstep(0.1h, 0.3h, v) * (1.0h - smoothstep(0.89h, 0.98h, v));
                half proceduralTongue = tongueColumn * verticalWindow * lerp(0.24h, 0.56h, tonguePulse);
                mask = max(mask, proceduralTongue);
                half heat = saturate(luminance * 1.75h + rootGlow * 0.65h + proceduralTongue * 0.58h);
                half3 gradient = lerp(_EdgeColor.rgb, _CoreColor.rgb, smoothstep(0.08h, 0.78h, heat));
                half3 authoredShape = authored * lerp(_EdgeColor.rgb * 1.6h, _CoreColor.rgb, heat);
                half flicker = lerp(1.0h, 0.82h + 0.18h * sin(u * 3.9h - _SequenceTime * 8.2h + v * 7.0h), upperBlend);
                half authoredWeight = 0.72h * (1.0h - saturate(proceduralTongue * 0.86h));
                half3 color = lerp(gradient, authoredShape, authoredWeight) * _Intensity * flicker;
                half tipFade = 1.0h - smoothstep(0.96h, 1.0h, v);
                half alpha = mask * tipFade * _GlobalFade * lerp(_EdgeColor.a, _CoreColor.a, heat);
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
