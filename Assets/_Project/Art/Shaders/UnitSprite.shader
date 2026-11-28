/**
 * UnitSprite
 *
 * 유닛 아트에 머지·승급 Sweep, 최고 성급 Rim, 배치·교체 Appear 연출을 그리는 셰이더
 * Sweep 연출 정의 - 비스듬한 빛 띠가 왼쪽 아래에서 오른쪽 위로 유닛을 한 번 지나감
 * Rim 연출 정의 - 그림 가장자리 안쪽이 금빛으로 천천히 밝아졌다 어두워짐
 * Appear 연출 정의 - 발끝부터 울퉁불퉁한 금빛 경계가 올라오며 유닛이 나타남
 * _SweepProgress는 UnitSlotView.cs가 0 -> 1로 올림 (0이면 효과 없음)
 * _RimAmount는 UnitSlotView.cs가 최고 성급이면 1, 아니면 0으로 넣음
 * _AppearProgress는 UnitSlotView.cs가 0 -> 1로 올림 (다른 값과 반대로 1이면 효과 없음, 0이면 안 보임)
 */
Shader "PokerDefense/UnitSprite"
{
    Properties
    {
        // SpriteRenderer가 넣는 스프라이트 텍스처 (인스펙터에서 숨김)
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}

        // 빛 띠 색·진행도·두께
        _SweepColor ("Sweep Color", Color) = (1, 0.92, 0.6, 1)
        _SweepProgress ("Sweep Progress", Range(0, 1)) = 0
        _SweepWidth ("Sweep Width", Range(0.02, 0.5)) = 0.15

        // Rim 색·세기·두께 (두께는 텍스처 픽셀 수)
        _RimColor ("Rim Color", Color) = (1, 0.8, 0.3, 1)
        _RimAmount ("Rim Amount", Range(0, 1)) = 0
        _RimWidth ("Rim Width", Range(1, 20)) = 6

        // Appear 진행도·경계 두께 (경계 색은 _SweepColor를 같이 씀)
        _AppearProgress ("Appear Progress", Range(0, 1)) = 1
        _AppearEdge ("Appear Edge", Range(0.01, 0.3)) = 0.05
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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _SweepColor;
                half _SweepProgress;
                half _SweepWidth;
                half4 _RimColor;
                half _RimAmount;
                half _RimWidth;
                half _AppearProgress;
                half _AppearEdge;
            CBUFFER_END

            // 텍스처 한 픽셀의 UV 크기 (x = 1/가로, y = 1/세로)
            // 이름이 텍스처이름_TexelSize면 Unity가 값을 자동으로 넣음
            // 위치가 CBUFFER 밖이라 SRP Batcher와 호환되지 않음
            float4 _MainTex_TexelSize;

            // 가장자리를 찾을 때 둘러보는 이웃 8방향 (상하좌우 + 대각선)
            // 대각선은 0.707(√2/2)을 곱해 상하좌우와 같은 거리로 맞춤
            static const float2 RimDirections[8] =
            {
                float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1),
                float2(0.707, 0.707), float2(-0.707, 0.707), float2(0.707, -0.707), float2(-0.707, -0.707)
            };

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
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 1. 스프라이트 색 × SpriteRenderer의 color
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 color = texel * input.color;

                // 2. 가장자리 찾기 - _RimWidth 픽셀 떨어진 이웃 8곳 중 가장 투명한 곳의 알파를 구함
                // 이웃 중 하나라도 투명하면 minAlpha가 0에 가까워져 가장자리로 판단
                // 그림이 텍스처 끝에서 잘린 곳(발밑 등)은 바깥 UV가 끝 픽셀로 고정돼 가장자리가 아님
                float2 rimOffset = _MainTex_TexelSize.xy * _RimWidth;
                half minAlpha = 1.0;

                for (int i = 0; i < 8; i++)
                {
                    minAlpha = min(minAlpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + RimDirections[i] * rimOffset).a);
                }

                // 3. Rim - 가장자리만큼 _RimColor로 덮고 세기는 0.75~1 사이에서 천천히 밝아졌다 어두워짐
                // texel.a를 곱해 그림 바깥의 투명한 곳은 칠하지 않음
                half pulse = 0.75 + 0.25 * sin(_Time.y * 3.0);
                half rim = texel.a * (1.0 - minAlpha) * _RimAmount * pulse;
                color.rgb = lerp(color.rgb, _RimColor.rgb, rim);

                // 4. 대각선 위치 - 왼쪽 아래 0 -> 오른쪽 위 1
                // 유닛 스프라이트는 한 장씩 따로 있어 UV 0~1이 그림 전체를 덮음
                float diagonal = (input.uv.x + input.uv.y) * 0.5;

                // 5. 띠 중심 - _SweepProgress 0 -> 1에 따라 -_SweepWidth -> 1 + _SweepWidth로 이동
                // 띠 두께만큼 바깥에서 시작하고 끝나야 처음과 끝에 띠가 유닛에 걸치지 않음
                float center = lerp(-_SweepWidth, 1.0 + _SweepWidth, _SweepProgress);

                // 6. 띠 세기 - 중심에서 1, 두께만큼 멀어지면 0
                // smoothstep - 0과 1 사이를 부드럽게 이어 띠 가장자리를 흐리게 함
                half band = 1.0 - smoothstep(0.0, _SweepWidth, abs(diagonal - center));

                // 7. 빛 더하기 - 띠 세기만큼 _SweepColor를 더해 밝힘
                // 알파는 그대로라 유닛 바깥의 투명한 곳은 빛나지 않음
                color.rgb += _SweepColor.rgb * _SweepColor.a * band;

                // 8. Appear 높이 - 발끝 0 -> 머리 1(75%)에 노이즈(25%)를 섞어 경계를 울퉁불퉁하게 함
                // UV × 9 - 그림 한 장에 노이즈 무늬가 가로·세로 9칸쯤 들어감
                float height = input.uv.y * 0.75 + ValueNoise(input.uv * 9.0) * 0.25;

                // 9. Appear - 높이가 기준선 이하인 픽셀만 남기고, 기준선 바로 아래 _AppearEdge만큼은 _SweepColor로 칠함
                // 기준선은 _AppearProgress 0 -> 1에 따라 -_AppearEdge -> 1 + _AppearEdge로 오름
                // 경계 두께만큼 바깥에서 시작하고 끝나야 0에서 아무것도 안 보이고 1에서 경계 색이 남지 않음
                float appearLine = lerp(-_AppearEdge, 1.0 + _AppearEdge, _AppearProgress);
                half shown = step(height, appearLine);
                half edge = shown * (1.0 - step(height, appearLine - _AppearEdge));
                color.rgb = lerp(color.rgb, _SweepColor.rgb, edge);
                color.a *= shown;
                return color;
            }
            ENDHLSL
        }
    }
}
