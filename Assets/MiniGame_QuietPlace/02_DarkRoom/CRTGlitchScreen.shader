Shader "Custom/CRTGlitchScreen"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint (Green Phosphor)", Color) = (0, 1, 0.25, 1)
        _ScanlineIntensity ("Scanline Intensity", Range(0,1)) = 0.25
        _ScanlineCount ("Scanline Count", Float) = 240
        _CurvatureAmount ("Curvature", Range(0,0.3)) = 0.08
        _GlitchAmount ("Glitch Amount", Range(0,1)) = 0
        _NoiseIntensity ("Noise Intensity", Range(0,1)) = 0.05
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float _ScanlineIntensity;
            float _ScanlineCount;
            float _CurvatureAmount;
            float _GlitchAmount;
            float _NoiseIntensity;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float rand(float2 co)
            {
                return frac(sin(dot(co, float2(12.9898,78.233))) * 43758.5453);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv - 0.5;
                uv *= 1.0 + _CurvatureAmount * dot(uv, uv);
                uv += 0.5;

                // 글리치: 랜덤 라인마다 수평으로 UV를 밀어냄
                float glitchLine = floor(uv.y * 40);
                float glitchOffset = (rand(float2(glitchLine, _Time.y)) - 0.5) * _GlitchAmount * 0.1;
                uv.x += glitchOffset;

                fixed4 col = tex2D(_MainTex, uv) * _Color;

                float scanline = sin(uv.y * _ScanlineCount * 3.14159) * 0.5 + 0.5;
                col.rgb *= 1.0 - _ScanlineIntensity * scanline;

                float noise = (rand(uv + _Time.y) - 0.5) * _NoiseIntensity;
                col.rgb += noise;

                if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1)
                    col.rgb = 0;

                return col;
            }
            ENDCG
        }
    }
}
