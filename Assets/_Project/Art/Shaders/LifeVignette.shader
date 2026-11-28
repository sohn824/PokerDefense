/**
 * LifeVignette
 *
 * 라이프가 위험할 때 화면 가장자리에 Vignette 연출을 그리는 셰이더 (전체 화면 UI Image용)
 * Vignette 연출 정의 - 화면 가장자리가 붉게 물든 채 천천히 밝아졌다 어두워짐
 * _Intensity는 CombatScreen.cs가 라이프가 낮을수록 크게 넣음 (0이면 효과 없음)
 */
Shader "PokerDefense/LifeVignette"
{
    Properties
    {
        // Image가 넣는 스프라이트 텍스처 (인스펙터에서 숨김, 무늬는 UV로만 그려 쓰지 않음)
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}

        // 세기·색(알파가 최대 세기)·밝아졌다 어두워지는 속도
        _Intensity ("Intensity", Range(0, 1)) = 0
        _Color ("Color", Color) = (0.6, 0.02, 0.05, 0.6)
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 3
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
            };

            CBUFFER_START(UnityPerMaterial)
                half _Intensity;
                half4 _Color;
                float _PulseSpeed;
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
                // 1. 가장자리 - 화면 가운데에서 멀수록 진해짐 (BossWarning과 같은 계산)
                float edge = smoothstep(0.3, 0.75, length(input.uv - 0.5));

                // 2. 밝기 변화 - 세기를 0.65~1 사이에서 _PulseSpeed 빠르기로 오르내리게 함
                half pulse = 0.65 + 0.35 * sin(_Time.y * _PulseSpeed);

                half4 color = _Color * input.color;
                color.a *= edge * pulse * _Intensity;
                return color;
            }
            ENDHLSL
        }
    }
}
