using UnityEngine;

namespace PokerDefense.UI
{
    /**
     * SafeAreaFitter
     *
     * 모바일 화면 대응
     * 기기마다 화면 위쪽 카메라 홈 · 둥근 모서리 · 아래쪽 제스처 바의 위치가 달라서
     * OS가 "여기 안에 그리면 안 가린다"고 알려주는 Screen.safeArea 안으로
     * 이 SafeAreaFitter가 붙어 있는 오브젝트의 RectTransform을 맞츰
     * 화면을 꽉 채우는 컨테이너에 붙이고 그 아래에 실제 UI를 둠
     * 파임이 없는 환경(에디터 · PC)에선 safeArea가 화면 전체와 같아 아무 일도 안 함
     */
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        RectTransform rt;
        Rect lastArea;
        Vector2Int lastScreen;

        void Awake()
        {
            rt = GetComponent<RectTransform>();
            Apply();
        }

        void Update()
        {
            // 회전 · 해상도 변화에 맞춰 다시 계산한다
            if (Screen.safeArea != lastArea || Screen.width != lastScreen.x || Screen.height != lastScreen.y)
            {
                Apply();
            }
        }

        void Apply()
        {
            lastArea = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);

            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Vector2 min = lastArea.position;
            Vector2 max = lastArea.position + lastArea.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
