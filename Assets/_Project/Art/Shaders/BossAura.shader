/**
 * BossAura
 *
 * 보스 발밑에 Aura 연출을 그리는 셰이더 (정사각형 스프라이트에 붙여 사용)
 * Aura 연출 정의 - 마법진 모양의 눈금과 방사형 선이 서로 반대로 천천히 돎
 * 색·투명도는 CombatScreen.cs가 SpriteRenderer의 color로 넣음
 */
Shader "PokerDefense/BossAura"
{
    Properties
    {
        // SpriteRenderer가 넣는 스프라이트 텍스처 (인스펙터에서 숨김, 무늬는 UV로만 그려 쓰지 않음)
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}

        // 바깥 눈금 수 (정수여야 이음새가 안 보임)
        [IntRange] _Ticks ("Ticks", Range(4, 48)) = 24

        // 눈금·방사형 선이 1초에 도는 바퀴 수
        _Speed ("Speed", Range(0, 1)) = 0.08

        // 가운데 빛 세기
        _Glow ("Glow", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        // 기본 스프라이트와 같은 반투명 섞기
        // 2D 프로젝트이므로 앞뒷면 구분·깊이 기록은 끔
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            // 2D 렌더러가 스프라이트를 그리는 패스
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Ticks;
                float _Speed;
                half _Glow;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                // unity_SpriteColor - SRP Batcher로 그릴 때 SpriteRenderer의 color가 들어오는 곳
                // 그 외에는 정점 색으로 들어오므로 둘 다 곱함
                output.color = input.color * unity_SpriteColor;
                output.uv = input.uv;
                return output;
            }

            // 반지름 center에 그리는 고리 (고리 위 1, halfWidth만큼 멀어지면 0)
            half Ring(float r, float center, float halfWidth)
            {
                return 1.0 - smoothstep(halfWidth * 0.5, halfWidth, abs(r - center));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 1. 극좌표 - UV를 거리와 각도로 바꿈
                // r - 가운데에서의 거리 (가운데 0, 가장자리 1)
                // turn - 각도 (-0.5 ~ 0.5, 한 바퀴가 1)
                // atan2(y, x) - 점의 각도 (-π ~ π)
                float2 p = input.uv * 2.0 - 1.0;
                float r = length(p);
                float turn = atan2(p.y, p.x) / 6.28318;

                // 2. 고리 - 바깥 한 줄, 안쪽 한 줄
                half shape = max(Ring(r, 0.9, 0.05), Ring(r, 0.62, 0.04));

                // 3. 눈금 - 각도에 시간을 더해 돌리고 frac으로 _Ticks번 되풀이함 (두 고리 사이에만 남김)
                float tick = abs(frac((turn + _Time.y * _Speed) * _Ticks) - 0.5);
                half between = step(0.66, r) * step(r, 0.84);
                shape = max(shape, (1.0 - smoothstep(0.1, 0.18, tick)) * between);

                // 4. 방사형 선 - 각도에서 시간을 빼 눈금과 반대로 돌리고 6번 되풀이함 (안쪽 고리 안에만 남김)
                float ray = abs(frac((turn - _Time.y * _Speed) * 6.0) - 0.5);
                half inside = step(r, 0.58) * smoothstep(0.1, 0.5, r);
                shape = max(shape, (1.0 - smoothstep(0.02, 0.12, ray)) * inside);

                // 5. 가운데 빛 - 가운데에서 가장 밝고 가장자리로 갈수록 사라짐
                half glow = saturate(1.0 - r) * _Glow;

                half4 color = input.color;
                color.a *= saturate(shape + glow);
                return color;
            }
            ENDHLSL
        }
    }
}
