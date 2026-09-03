Shader "Custom/UI/EerieSway"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SwayAmount ("Sway Amount", Range(0, 0.05)) = 0.012
        _SwaySpeed ("Sway Speed", Range(0, 5)) = 0.6
        _SwayRegion ("Sway Region Falloff (higher = only top)", Range(0.5, 4)) = 2
        _ProtectMinX ("Building Column Left (UV X)", Range(0, 1)) = 0.47
        _ProtectMaxX ("Building Column Right (UV X)", Range(0, 1)) = 0.72
        _EdgeSoftness ("Column Edge Softness", Range(0.01, 0.2)) = 0.05
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

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
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _SwayAmount;
            float _SwaySpeed;
            float _SwayRegion;
            float _ProtectMinX;
            float _ProtectMaxX;
            float _EdgeSoftness;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float vertWeight = pow(saturate(IN.texcoord.y), _SwayRegion);

                // 건물이 있는 세로 기둥 구간([_ProtectMinX, _ProtectMaxX])은 위아래 상관없이 통째로 고정
                float leftRamp = smoothstep(_ProtectMinX - _EdgeSoftness, _ProtectMinX, IN.texcoord.x);
                float rightRamp = 1.0 - smoothstep(_ProtectMaxX, _ProtectMaxX + _EdgeSoftness, IN.texcoord.x);
                float insideColumn = leftRamp * rightRamp;

                float weight = vertWeight * (1.0 - insideColumn);
                float offset = sin(_Time.y * _SwaySpeed + IN.texcoord.y * 6.0) * _SwayAmount * weight;
                float2 uv = IN.texcoord + float2(offset, 0);
                fixed4 col = tex2D(_MainTex, uv) * IN.color;
                return col;
            }
            ENDCG
        }
    }
}
