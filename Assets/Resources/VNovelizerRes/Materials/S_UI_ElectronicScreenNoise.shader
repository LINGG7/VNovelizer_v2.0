Shader "VNovelizer/UI/ElectronicScreenNoise"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0.42, 0.9, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.12
        _PixelDensity ("Pixel Density", Range(40, 600)) = 260
        _FineNoise ("Fine Noise", Range(0, 1)) = 0.55
        _ScanlineAmount ("Scanline Amount", Range(0, 1)) = 0.34
        _ScanlineCount ("Scanline Count", Range(80, 720)) = 360
        _FlickerSpeed ("Flicker Speed", Range(0.5, 30)) = 8
        _FlickerAmount ("Flicker Amount", Range(0, 1)) = 0.18
        _GlitchAmount ("Glitch Amount", Range(0, 1)) = 0.16
        _CenterClear ("Center Clear", Range(0, 1)) = 0.45
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

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct VertexInput
            {
                float4 position : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct FragmentInput
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            sampler2D _MainTex;
            float _Opacity;
            float _PixelDensity;
            float _FineNoise;
            float _ScanlineAmount;
            float _ScanlineCount;
            float _FlickerSpeed;
            float _FlickerAmount;
            float _GlitchAmount;
            float _CenterClear;

            float randomFromPoint(float2 sampleCoord)
            {
                return frac(sin(dot(sampleCoord, float2(41.731, 289.137))) * 43758.5453);
            }

            float blockValue(float2 uv, float density, float frame)
            {
                float2 cell = floor(uv * density);
                float2 seed = cell + float2(frame * 5.37, frame * 11.19);
                return randomFromPoint(seed);
            }

            FragmentInput vert(VertexInput input)
            {
                FragmentInput output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            fixed4 frag(FragmentInput input) : SV_Target
            {
                float2 uv = input.uv;
                float frame = floor(_Time.y * _FlickerSpeed);

                float grain = blockValue(uv, _PixelDensity, frame);
                float fine = randomFromPoint(uv * float2(1531.7, 947.3) + _Time.y * 19.0);
                float grainShape = lerp(grain, grain * fine, _FineNoise);
                grainShape = smoothstep(0.44, 1.0, grainShape);

                float scanWave = sin((uv.y * _ScanlineCount + _Time.y * 2.0) * 6.2831853);
                float scanline = lerp(1.0, 0.58 + 0.42 * scanWave, _ScanlineAmount);

                float flickerSeed = randomFromPoint(float2(frame, frame * 0.271));
                float flicker = lerp(1.0, 0.72 + flickerSeed * 0.56, _FlickerAmount);

                float bandFrame = floor(_Time.y * max(1.0, _FlickerSpeed * 0.45));
                float rowSeed = randomFromPoint(float2(floor(uv.y * 96.0), bandFrame));
                float bandGate = step(1.0 - _GlitchAmount * 0.18, rowSeed);
                float bandCore = 1.0 - smoothstep(0.0, 0.018, abs(frac(uv.y * 12.0 + bandFrame * 0.071) - 0.5));
                float glitchBand = bandGate * bandCore * _GlitchAmount;

                float2 centered = uv * 2.0 - 1.0;
                float edgeWeight = smoothstep(0.16, 1.15, length(centered));
                float readableCenter = lerp(1.0, edgeWeight, _CenterClear);

                float intensity = saturate(grainShape * scanline * flicker + glitchBand);
                float textureAlpha = tex2D(_MainTex, uv).a;
                float alpha = saturate(_Opacity * intensity * readableCenter * textureAlpha);
                float3 color = _Color.rgb * lerp(0.68, 1.28, intensity + glitchBand);

                return fixed4(saturate(color), alpha * _Color.a) * input.color;
            }
            ENDCG
        }
    }
}
