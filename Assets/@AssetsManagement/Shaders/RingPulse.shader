Shader "Custom/RingPulse"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _HitPoint ("Hit Point (world space)", Vector) = (0,0,0,0)
        _Progress ("Progress (current radius, world units)", Float) = 0
        _RingWidth ("Ring Width (world units)", Range(0.01, 2)) = 0.2
        _MaxRadius ("Max Radius (world units)", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float4 _HitPoint;
            float _Progress;
            float _RingWidth;
            float _MaxRadius;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(worldPos);
                OUT.worldPos = worldPos;
                return OUT;
            }

            // 이 면이 어떤 오브젝트에 붙어있든(바닥/벽 어디든) 실제 월드 좌표 거리로 판정하기 때문에,
            // 같은 _HitPoint/_Progress를 공유하는 여러 면(바닥+주변 벽)에 동시에 적용하면
            // 착지 지점에서 사방으로(벽까지) 이어져 퍼지는 것처럼 보인다.
            half4 frag(Varyings IN) : SV_Target
            {
                float dist = distance(IN.worldPos, _HitPoint.xyz);
                float band = smoothstep(_Progress - _RingWidth, _Progress, dist)
                            - smoothstep(_Progress, _Progress + _RingWidth, dist);
                float fade = 1.0 - saturate(_Progress / _MaxRadius);

                half4 col = _Color;
                col.a *= band * fade;
                return col;
            }
            ENDHLSL
        }
    }
}
