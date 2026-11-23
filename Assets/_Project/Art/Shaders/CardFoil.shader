/**
 * CardFoil
 *
 * 손패 카드 이미지에 Foil 연출을 그리는 셰이더
 * Foil - 무지개빛이 카드 위를 천천히 흐르고 밝은 광택이 주기적으로 지나감
 * 스트레이트 이상 족보를 이룬 카드에만 CardView가 이 머티리얼을 씌움
 * 숫자·무늬는 카드의 자식 컴포넌트라 이 셰이더 영향 없이 그 위에 그려짐
 */
Shader "PokerDefense/CardFoil"
{
    Properties
    {
        // Image가 넣는 스프라이트 텍스처 (인스펙터에서 숨김)
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}

        // 무지개빛 세기 (0이면 원래 카드)
        _FoilStrength ("Foil Strength", Range(0, 1)) = 0.35

        // 광택 띠의 밝기
        _SheenStrength ("Sheen Strength", Range(0, 1)) = 0.5

        // 무지개빛이 흐르는 속도
        _Speed ("Speed", Range(0, 1)) = 0.15
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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half _FoilStrength;
                half _SheenStrength;
                half _Speed;
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
                // 1. 카드 바탕 색 × Image의 color (CanvasGroup 투명도 포함)
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;

                // 2. 화면 위치 - 카드마다 UV가 같아도 화면 자리가 달라 무늬가 이어져 보임
                // 프래그먼트 단계의 SV_POSITION은 픽셀 좌표라 화면 크기로 나눠 0~1로 맞춤
                float2 screen = input.positionCS.xy / _ScreenParams.xy;

                // 3. 무지개빛 - 대각선 위치와 시간으로 색상(hue)을 정함
                // cos를 R·G·B마다 1/3씩 어긋나게 돌려 빨강 -> 초록 -> 파랑이 차례로 나오게 함
                // 무지개 색을 곱하면 어두워지므로 1.4배로 밝기를 보충
                float hue = (screen.x + screen.y) * 1.5 + _Time.y * _Speed;
                half3 rainbow = 0.5 + 0.5 * cos(6.28318 * (hue + float3(0.0, 0.33, 0.67)));
                color.rgb = lerp(color.rgb, color.rgb * rainbow * 1.4, _FoilStrength);

                // 4. 광택 띠 - 화면 대각선을 따라 되풀이해 지나가는 밝은 줄 (여러 장을 차례로 훑음)
                // 카드 바탕은 9분할 스프라이트라 UV가 조각마다 끊겨 화면 위치를 씀
                // frac으로 0~1을 되풀이하고 0.5 근처만 밝게 남김
                float sheenPos = frac((screen.x + screen.y) * 0.8 - _Time.y * 0.35);
                half sheen = 1.0 - smoothstep(0.0, 0.04, abs(sheenPos - 0.5));
                color.rgb += sheen * _SheenStrength;

                return color;
            }
            ENDHLSL
        }
    }
}
