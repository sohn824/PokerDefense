using PokerDefense.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    // 배속 토글. 누를 때마다 1x/2x/3x를 순환하고, 실제 timeScale 반영은 GameSession이 맡는다
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
