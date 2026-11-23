/**
 * UnitSprite
 *
 * 유닛 아트에 머지·승급 Sweep 연출을 그리는 셰이더
 * Sweep 연출 정의 - 비스듬한 빛 띠가 왼쪽 아래에서 오른쪽 위로 유닛을 한 번 지나감
 * _SweepProgress는 UnitSlotView.cs가 0 -> 1로 올림 (0이면 효과 없음)
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
            CBUFFER_END

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
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;

                // 2. 대각선 위치 - 왼쪽 아래 0 -> 오른쪽 위 1
                // 유닛 스프라이트는 한 장씩 따로 있어 UV 0~1이 그림 전체를 덮음
                float diagonal = (input.uv.x + input.uv.y) * 0.5;

                // 3. 띠 중심 - _SweepProgress 0 -> 1에 따라 -_SweepWidth -> 1 + _SweepWidth로 이동
                // 띠 두께만큼 바깥에서 시작하고 끝나야 처음과 끝에 띠가 유닛에 걸치지 않음
                float center = lerp(-_SweepWidth, 1.0 + _SweepWidth, _SweepProgress);

                // 4. 띠 세기 - 중심에서 1, 두께만큼 멀어지면 0
                // smoothstep - 0과 1 사이를 부드럽게 이어 띠 가장자리를 흐리게 함
                half band = 1.0 - smoothstep(0.0, _SweepWidth, abs(diagonal - center));

                // 5. 빛 더하기 - 띠 세기만큼 _SweepColor를 더해 밝힘
                // 알파는 그대로라 유닛 바깥의 투명한 곳은 빛나지 않음
                color.rgb += _SweepColor.rgb * _SweepColor.a * band;
                return color;
            }
            ENDHLSL
        }
    }
}
