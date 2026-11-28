/**
 * CardBurn
 *
 * 교체한 카드 위에 Burn 연출을 그리는 셰이더 (카드를 덮는 UI Image용)
 * Burn 연출 정의 - 카드를 덮은 밝은 막이 불규칙하게 타들어 가며 새 카드가 드러남
 * _Progress는 CardView.cs가 0 -> 1로 올림 (0이면 카드를 다 덮고, 1이면 다 타서 안 보임)
 */
Shader "PokerDefense/CardBurn"
{
    Properties
    {
        // Image가 넣는 스프라이트 텍스처 (인스펙터에서 숨김, 카드 모양의 알파만 씀)
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}

        // 진행도·타는 경계 색·경계 두께
        _Progress ("Progress", Range(0, 1)) = 0
        _EdgeColor ("Edge Color", Color) = (1, 0.55, 0.15, 1)
        _EdgeWidth ("Edge Width", Range(0, 0.3)) = 0.07

        // 노이즈 무늬 크기 (클수록 잘게 부서짐, UI 좌표 1픽셀 기준)
        _NoiseScale ("Noise Scale", Float) = 0.035
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        // UI 기본 셰이더와 같은 반투명 섞기
        // 앞뒷면 구분·깊이 기록은 끄고, 깊이 비교는 Canvas가 정한 값(unity_GUIZTestMode)을 따름
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]

        // LightMode 태그 없음 - Overlay 캔버스는 2D 렌더러 패스가 아니라 UI가 직접 그림
        Pass
        {
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
                // 노이즈 좌표 - 카드 바탕은 9분할 스프라이트라 UV 대신 UI 좌표를 씀
                float2 noisePos : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _Progress;
                half4 _EdgeColor;
                half _EdgeWidth;
                float _NoiseScale;
            CBUFFER_END

            // 좌표마다 고정된 0~1 난수 (EnemySprite와 같은 식)
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
                output.color = input.color;
                output.uv = input.uv;
                output.noisePos = input.positionOS.xy * _NoiseScale;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 1. 덮개 색 × 카드 모양 - 색은 Image의 color, 모양은 카드 바탕 스프라이트의 알파
                half4 color = input.color;
                color.a *= SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;

                // 2. 노이즈 - 큰 무늬 70% + 잔무늬 30% (EnemySprite Dissolve와 같은 섞기)
                float noise = ValueNoise(input.noisePos) * 0.7 + ValueNoise(input.noisePos * 2.7) * 0.3;

                // 3. Burn - 노이즈가 기준선 이상인 곳만 남기고, 기준선 바로 위 _EdgeWidth만큼은 경계 색으로 칠함
                // 기준선은 _Progress 0 -> 1에 따라 -_EdgeWidth -> 1로 오름 (0에서 경계 색이 안 보이게)
                float threshold = lerp(-_EdgeWidth, 1.0, _Progress);
                half visible = step(threshold, noise);
                half edge = visible * (1.0 - step(threshold + _EdgeWidth, noise));

                color.rgb = lerp(color.rgb, _EdgeColor.rgb, edge);
                color.a *= visible;
                return color;
            }
            ENDHLSL
        }
    }
}
