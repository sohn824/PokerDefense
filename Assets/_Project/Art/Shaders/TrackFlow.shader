/**
 * TrackFlow
 *
 * 적이 도는 트랙 위에 Flow 연출을 그리는 셰이더 (LineRenderer용)
 * Flow 연출 정의 - 화살촉(>) 무늬가 선을 따라 적이 걷는 방향으로 흐름
 * 선의 색·투명도는 CombatScreen.cs가 LineRenderer의 color로 넣음
 */
Shader "PokerDefense/TrackFlow"
{
    Properties
    {
        // 한 바퀴에 들어가는 화살촉 수 (정수여야 이음새가 안 보임)
        [IntRange] _Count ("Count", Range(4, 60)) = 24

        // 1초에 흐르는 화살촉 수
        _Speed ("Speed", Range(0, 4)) = 0.8

        // 화살촉 꺾임·굵기 (화살촉 한 칸 길이가 1)
        _Slant ("Slant", Range(0, 2)) = 0.5
        _Thickness ("Thickness", Range(0.05, 0.5)) = 0.2
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
            // 2D 렌더러가 그리는 패스
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                // x = 선을 따라 0 -> 1, y = 선 폭을 가로질러 0 -> 1
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Count;
                float _Speed;
                float _Slant;
                float _Thickness;
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
                // 1. 선 가운데에서 떨어진 거리 - 가운데 0, 양쪽 끝 0.5
                float side = abs(input.uv.y - 0.5);

                // 2. 무늬 위치 - 시간만큼 빼서 무늬를 적이 걷는 방향으로 흘림
                // 선 가장자리일수록 값을 더해 무늬를 뒤로 밀면 가운데가 앞선 화살촉 모양이 됨
                float x = input.uv.x * _Count - _Time.y * _Speed + side * _Slant;

                // 3. 화살촉 - frac으로 칸마다 되풀이하고 칸 가운데를 _Thickness만큼만 남김
                // fwidth - 옆 픽셀과의 값 차이 (그만큼 경계를 흐려 계단 현상을 줄임)
                float blur = fwidth(x);
                float dist = abs(frac(x) - 0.5);
                half arrow = 1.0 - smoothstep(_Thickness * 0.5 - blur, _Thickness * 0.5 + blur, dist);

                // 4. 가장자리 흐림 - 선 폭의 양쪽 끝으로 갈수록 투명해짐
                half fade = 1.0 - smoothstep(0.3, 0.5, side);

                half4 color = input.color;
                color.a *= arrow * fade;
                return color;
            }
            ENDHLSL
        }
    }
}
