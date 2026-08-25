Shader "VNovelizer/UI/TapePlayback"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Cold Tint", Color) = (0.16, 0.30, 0.30, 1)
        _TintAlpha ("Tint Alpha", Range(0, 0.3)) = 0.12
        _VignetteStrength ("Vignette Strength", Range(0, 0.4)) = 0.18
        _ScanlineStrength ("Scanline Strength", Range(0, 0.2)) = 0.05
        _ScanlineCount ("Scanline Count", Range(80, 720)) = 360
        _NoiseStrength ("Noise Strength", Range(0, 0.2)) = 0.05
        _NoiseDensity ("Noise Density", Range(80, 600)) = 320
        _NoiseRefreshRate ("Noise Refresh Rate", Range(1, 30)) = 12
        _BandStrength ("Tracking Band Strength", Range(0, 0.3)) = 0.12
        _BandSpeed ("Tracking Band Speed", Range(0.1, 5)) = 1.15
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

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _TintAlpha;
            float _VignetteStrength;
            float _ScanlineStrength;
            float _ScanlineCount;
            float _NoiseStrength;
            float _NoiseDensity;
            float _NoiseRefreshRate;
            float _BandStrength;
            float _BandSpeed;

            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float trackingBand(float2 uv, float slot, float time)
            {
                float epoch = floor(time * 0.42 + slot * 5.71);
                float center = hash(float2(epoch + slot * 17.3, slot * 9.1));
                float width = lerp(0.008, 0.024, hash(float2(epoch * 1.7, slot + 3.2)));
                float softBand = 1.0 - smoothstep(width, width * 2.6, abs(uv.y - center));

                float streakCell = floor((uv.x + time * (0.22 + slot * 0.07)) * 96.0);
                float streak = lerp(0.35, 1.0, hash(float2(streakCell, epoch + slot)));
                float activity = lerp(0.42, 1.0, hash(float2(epoch + 8.4, slot * 13.7)));
                return softBand * streak * activity;
            }

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord;
                output.color = input.color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y * _BandSpeed;

                float2 centered = uv * 2.0 - 1.0;
                float vignette = smoothstep(0.58, 1.22, length(centered));

                float scanWave = 0.5 + 0.5 * sin((uv.y * _ScanlineCount + _Time.y * 1.5) * 6.2831853);
                float scanline = scanWave * _ScanlineStrength;

                float noiseFrame = floor(_Time.y * _NoiseRefreshRate);
                float2 noiseCell = floor(uv * _NoiseDensity);
                float grain = hash(noiseCell + noiseFrame * float2(11.3, 7.9));
                float grainShape = abs(grain - 0.5) * 2.0;
                float noiseAlpha = grainShape * _NoiseStrength;

                float bands = trackingBand(uv, 0.0, time);
                bands += trackingBand(uv, 1.0, time);
                bands += trackingBand(uv, 2.0, time);
                bands = saturate(bands);

                float tintAlpha = _TintAlpha;
                float vignetteAlpha = vignette * _VignetteStrength;
                float bandAlpha = bands * _BandStrength;
                float totalAlpha = saturate(tintAlpha + vignetteAlpha + scanline + noiseAlpha + bandAlpha);

                float3 tintColor = _Color.rgb;
                float3 darkColor = float3(0.015, 0.035, 0.035);
                float3 grainColor = lerp(float3(0.03, 0.06, 0.06), float3(0.48, 0.58, 0.56), grain);
                float3 bandColor = lerp(float3(0.02, 0.075, 0.075), float3(0.34, 0.50, 0.47), hash(float2(floor(time * 2.0), floor(uv.y * 72.0))));

                float3 weightedColor = tintColor * tintAlpha;
                weightedColor += darkColor * (vignetteAlpha + scanline);
                weightedColor += grainColor * noiseAlpha;
                weightedColor += bandColor * bandAlpha;
                float3 finalColor = weightedColor / max(totalAlpha, 0.0001);

                fixed sourceAlpha = tex2D(_MainTex, uv).a;
                return fixed4(saturate(finalColor), totalAlpha * sourceAlpha) * input.color;
            }
            ENDCG
        }
    }
}
