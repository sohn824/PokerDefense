using UnityEngine;

namespace PokerDefense.UI
{
    /**
     * UiStyle
     *
     * 모든 화면이 함께 쓰는 색 · 글자 크기 · 간격 값
     * 씬 오브젝트와 코드로 만드는 위젯이 같은 값을 참조하도록 한곳에 모은다
     */
    public static class UiStyle
    {
        // 패널 - 저채도 짙은 바탕
        public static readonly Color PanelBg = new Color(0.070f, 0.078f, 0.104f, 1f);      // 화면을 덮는 패널 (상점 · 결과)
        public static readonly Color Plate   = new Color(0.100f, 0.110f, 0.145f, 0.94f);   // 경기장 위에 얹는 정보판 · 배지
        public static readonly Color Scrim   = new Color(0f, 0f, 0f, 0.72f);               // 모달 뒤에 까는 어두운 막

        // 버튼 - 주 행동은 채운 강조색, 보조는 낮은 대비의 어두운 바탕, 위험은 따뜻한 붉은색
        public static readonly Color ButtonPrimary   = new Color(0.180f, 0.620f, 0.357f, 1f);
        public static readonly Color ButtonSecondary = new Color(0.141f, 0.149f, 0.184f, 0.96f);
        public static readonly Color ButtonDanger    = new Color(0.698f, 0.275f, 0.196f, 1f);

        // 비활성 버튼 바탕 - 역할색 없이 확실히 죽은 어두운 중립색. 사유는 라벨 문구가 전달한다
        public static readonly Color ButtonDisabled = new Color(0.20f, 0.21f, 0.25f, 0.92f);

        // 본문 - 밝은 중립색 세 단계
        public static readonly Color TextPrimary = new Color(0.925f, 0.929f, 0.949f, 1f);
        public static readonly Color TextBody    = new Color(0.843f, 0.851f, 0.878f, 1f);
        public static readonly Color TextMuted   = new Color(0.604f, 0.627f, 0.690f, 1f);

        // 재화 금색 - 선택색과 구별해 재화 표시에만 쓴다
        public static readonly Color Currency = new Color(1f, 0.843f, 0.420f, 1f);

        // 글자 크기 네 단계 - 제목 / 행동(버튼) / 본문 / 보조 (Canvas 1080x1920 기준 px)
        public const float TitleSize   = 72f;
        public const float ActionSize  = 40f;
        public const float BodySize    = 34f;
        public const float CaptionSize = 30f;

        // 간격 공통 배수
        public const float Unit = 8f;
    }
}
