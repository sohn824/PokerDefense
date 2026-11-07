using UnityEngine;

namespace PokerDefense.UI
{
    /**
     * SafeAreaFitter
     *
     * 오브젝트의 RectTransform을 Screen.safeArea에 맞추는 클래스
     * 스마트폰 노치·둥근 모서리·제스처 바에 UI가 가려지지 않게 함
     * 화면을 꽉 채우는 오브젝트에 붙이고 그 아래에 실제 UI를 둠
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
            // 회전, 해상도 변화에 맞춰 다시 계산함
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
