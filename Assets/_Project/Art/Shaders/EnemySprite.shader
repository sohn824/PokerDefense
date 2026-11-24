/**
 * EnemySprite
 *
 * 적 몸통 스프라이트에 Flash·Dissolve 연출을 그리는 셰이더
 * Flash 연출 정의 - 맞았을 때 원래 색을 _FlashColor로 덮음
 * Dissolve 연출 정의 - 죽었을 때 불규칙한 구멍이 퍼지며 타들어 가듯 사라짐
 * 두 값(_FlashAmount·_Dissolve)은 CombatScreen.cs가 적마다 0~1로 넣음 (0이면 효과 없음)
 */
Shader "PokerDefense/EnemySprite"
{
    Properties
    {
        // SpriteRenderer가 넣는 스프라이트 텍스처 (인스펙터에서 숨김)
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}

        // Flash 색·세기
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _FlashAmount ("Flash Amount", Range(0, 1)) = 0

        // Dissolve 진행도·타는 경계 색·경계 두께
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _EdgeColor ("Edge Color", Color) = (1, 0.55, 0.15, 1)
        _EdgeWidth ("Edge Width", Range(0, 0.3)) = 0.1

        // 노이즈 무늬 크기 (클수록 잘게 부서짐)
        _NoiseScale ("Noise Scale", Float) = 6
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
                // 노이즈 좌표 - UV 대신 메시 좌표를 씀
                // 아틀라스에 묶이거나 크기를 키워도 무늬 비율이 같음
                float2 noisePos : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _FlashColor;
                half _FlashAmount;
                half _Dissolve;
                half4 _EdgeColor;
                half _EdgeWidth;
                float _NoiseScale;
            CBUFFER_END

            // 좌표마다 고정된 0~1 난수
            // 같은 좌표는 늘 같은 값이라 프레임마다 깜빡이지 않음
            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // 격자 꼭짓점의 난수를 부드럽게 이은 노이즈 (0~1)
            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                // smoothstep 곡선 - 격자 경계가 각져 보이지 않게 함
                float2 w = f * f * (3.0 - 2.0 * f);

                float a = Hash(cell);
                float b = Hash(cell + float2(1, 0));
                float c = Hash(cell + float2(0, 1));
                float d = Hash(cell + float2(1, 1));
                return lerp(lerp(a, b, w.x), lerp(c, d, w.x), w.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                // unity_SpriteColor - SRP Batcher로 그릴 때 SpriteRenderer의 color가 들어오는 곳
                // 그 외에는 정점 색으로 들어오므로 둘 다 곱함
                output.color = input.color * unity_SpriteColor;
                output.uv = input.uv;
                output.noisePos = input.positionOS.xy * _NoiseScale;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 1. 스프라이트 색 × SpriteRenderer의 color
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;

                // 2. Flash
                color.rgb = lerp(color.rgb, _FlashColor.rgb, _FlashAmount);

                // 3. 노이즈 - 큰 무늬 70% + 잔무늬 30%
                float noise = ValueNoise(input.noisePos) * 0.7 + ValueNoise(input.noisePos * 2.7) * 0.3;

                // 4. Dissolve - 노이즈 값을 기준선과 비교해 지우거나 경계 색으로 칠함
                // step(a, b) - b가 a 이상이면 1, 아니면 0

                // 기준선은 _Dissolve 0 -> 1에 따라 -_EdgeWidth -> 1로 오름
                // 0이 아닌 -_EdgeWidth에서 시작해야 살아 있는 적(_Dissolve 0)에 경계 색이 안 보임
                float threshold = lerp(-_EdgeWidth, 1.0, _Dissolve);

                // 노이즈가 기준선 이상이면 남김 (1), 낮으면 지움 (0)
                half visible = step(threshold, noise);

                // 남은 픽셀 중 기준선 + _EdgeWidth보다 낮으면 경계 (1)
                half edge = visible * (1.0 - step(threshold + _EdgeWidth, noise));

                color.rgb = lerp(color.rgb, _EdgeColor.rgb, edge);
                color.a *= visible;
                return color;
            }
            ENDHLSL
        }
    }
}
