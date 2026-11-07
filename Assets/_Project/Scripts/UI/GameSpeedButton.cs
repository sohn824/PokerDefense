using PokerDefense.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * GameSpeedButton
     *
     * 누를 때마다 배속을 1x -> 2x -> 3x 순서로 바꾸는 배속 버튼
     * 일시정지 중에는 눌러도 바뀌지 않음
     */
    public sealed class GameSpeedButton : MonoBehaviour
    {
        static readonly float[] Steps = { 1f, 2f, 3f };

        [SerializeField] Button button;
        [SerializeField] TMP_Text label;

        int index;

        void Awake()
        {
            button.onClick.AddListener(Cycle);
            Refresh();
        }

        void Cycle()
        {
            if (GameSession.IsPaused)
            {
                return;
            }

            index = (index + 1) % Steps.Length;
            GameSession.SetSpeed(Steps[index]);
            Refresh();
        }

        void Refresh()
        {
            label.text = $"{Steps[index]:0.#}x";
        }
    }
}
