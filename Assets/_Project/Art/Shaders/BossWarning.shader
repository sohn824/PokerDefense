/**
 * BossWarning
 *
 * 보스 등장 때 화면 전체에 Warning 연출을 그리는 셰이더 (전체 화면 UI Image용)
 * Warning 연출 정의 - 화면 가장자리가 보라색으로 물들고 보스가 나오는 곳에서 충격파 고리가 퍼짐
 * _Progress를 0 -> 1로 올리면 한 번 재생됨 (0과 1이면 아무것도 안 보임)
 */
Shader "PokerDefense/BossWarning"
{
    Properties
    {
        // Image가 넣는 스프라이트 텍스처 (인스펙터에서 숨김, 무늬는 UV로만 그려 쓰지 않음)
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}

        // 재생 진행도·고리 중심(화면 왼쪽 아래 0, 오른쪽 위 1)·화면비(가로 / 세로)
        _Progress ("Progress", Range(0, 1)) = 0
        _Center ("Center", Vector) = (0.5, 0.5, 0, 0)
        _Aspect ("Aspect", Float) = 0.5625

        // 가장자리 색(알파가 세기)·고리 색(알파가 세기)
        _EdgeColor ("Edge Color", Color) = (0.28, 0.04, 0.45, 0.55)
        _RingColor ("Ring Color", Color) = (0.75, 0.4, 1, 0.45)

        // 고리 두께 (화면 높이가 1)
        _RingWidth ("Ring Width", Range(0.01, 0.2)) = 0.02
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
                float _Progress;
                float4 _Center;
                float _Aspect;
                half4 _EdgeColor;
                half4 _RingColor;
                float _RingWidth;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            // 중심에서 radius만큼 퍼진 고리 (고리 위 1, 바깥은 짧게 안쪽은 길게 흐려짐)
            float Ring(float r, float radius)
            {
                float d = r - radius;
                float outer = 1.0 - smoothstep(0.0, _RingWidth * 0.4, d);
                float inner = smoothstep(-_RingWidth * 1.6, 0.0, d);
                return outer * inner;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 1. 가장자리 물들임 - 화면 가운데에서 멀수록 진해짐
                // 빠르게 나타났다가 후반에 사라짐
                float2 fromMiddle = input.uv - 0.5;
                float edge = smoothstep(0.25, 0.72, length(fromMiddle));
                float fade = smoothstep(0.0, 0.15, _Progress) * (1.0 - smoothstep(0.6, 1.0, _Progress));
                float edgeAlpha = edge * fade * _EdgeColor.a;

                // 2. 고리 중심에서의 거리 - 가로에 화면비를 곱해야 타원이 아닌 원이 됨
                float2 fromCenter = input.uv - _Center.xy;
                fromCenter.x *= _Aspect;
                float r = length(fromCenter);

                // 3. 충격파 - 처음에 빠르고 갈수록 느려지게 퍼지는 고리 두 개 (두 번째는 조금 늦게)
                float first = saturate(_Progress / 0.75);
                float second = saturate((_Progress - 0.18) / 0.75);
                float ringFirst = Ring(r, (1.0 - (1.0 - first) * (1.0 - first)) * 1.1) * (1.0 - first);
                float ringSecond = Ring(r, (1.0 - (1.0 - second) * (1.0 - second)) * 1.1) * (1.0 - second) * step(0.0001, second) * 0.6;
                float ringAlpha = saturate(ringFirst * step(0.0001, first) + ringSecond) * _RingColor.a;

                // 4. 합치기 - 물들인 색 위에 고리를 덮음
                float alpha = 1.0 - (1.0 - edgeAlpha) * (1.0 - ringAlpha);
                float3 rgb = (_RingColor.rgb * ringAlpha + _EdgeColor.rgb * edgeAlpha * (1.0 - ringAlpha)) / max(alpha, 0.0001);
                return half4(rgb, alpha) * input.color;
            }
            ENDHLSL
        }
    }
}
