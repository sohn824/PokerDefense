using System.Collections.Generic;
using System.Globalization;
using PokerDefense.Game;
using PokerDefense.Poker;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /** 확률 상세 창. 계산 결과를 표시하며 교체 입력이나 덱을 변경하지 않는다. */
    public sealed class HandOddsScreen : MonoBehaviour
    {
        [SerializeField] RoundController round;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text heading;
        [SerializeField] TMP_Text description;
        [SerializeField] TMP_Text[] rows;
        [SerializeField] RectTransform[] bars;
        [SerializeField] ScrollRect scroll;
        [SerializeField] Button closeButton;
        [SerializeField] CanvasGroup[] inputGroups;
        bool[] previousInput;
        bool previousPaused;

        void Awake()
        {
            panel.SetActive(false);
            closeButton.onClick.AddListener(Close);
        }

        // 계산이 끝나 있을 때만 연다. 진행 중·실패 상태에서는 자세히 볼 결과가 없다
        public void Open(string context, bool assist)
        {
            if (panel.activeSelf || GameSession.IsPaused || GameSession.IsLoading
                || round.OddsState != HandOddsState.Ready || round.Odds == null)
            {
                return;
            }
            HandCategory current = round.PreviewHand().Category;
            List<HandOddsEntry> entries = new List<HandOddsEntry>(round.Odds);
            entries.Sort((left, right) => HandRarity.RankOf(right.Category).CompareTo(HandRarity.RankOf(left.Category)));
            heading.text = context;
            description.text = "현재: " + HandCategoryNames.Of(current) + "\n"
                + HandOddsText.Summary(entries, current) + "\n"
                + (assist ? "후보 중 고를 확률이 아닌, 한 장을 뽑을 때의 분포입니다."
                    : "높은 족보는 희귀도 기준이며, 전투 성능과는 다릅니다.");
            // 희귀도 순으로 한 줄씩 채우고, 남는 행은 숨긴다
            for (int i = 0; i < rows.Length; i++)
            {
                bool visible = i < entries.Count;
                rows[i].transform.parent.gameObject.SetActive(visible);
                if (visible == false)
                {
                    continue;
                }
                HandOddsEntry entry = entries[i];
                rows[i].text = HandCategoryNames.Of(entry.Category)
                    + (entry.Category == current ? " · 현재" : "") + "   " + HandOddsText.Percent(entry.Count, entry.Total)
                    + "\n<size=26><color=#9AA0B0>"
                    + entry.Count.ToString("N0", CultureInfo.InvariantCulture) + " / "
                    + entry.Total.ToString("N0", CultureInfo.InvariantCulture) + "가지</color></size>";
                rows[i].color = entry.Category == current ? UiStyle.Currency : UiStyle.TextPrimary;
                bars[i].anchorMax = new Vector2(entry.Probability, 1f);
            }
            scroll.content.sizeDelta = new Vector2(0f, entries.Count * 112f);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
            // 닫을 때 그대로 되돌릴 수 있게 입력·정지 상태를 먼저 기록해둔다
            previousPaused = GameSession.IsPaused;
            previousInput = new bool[inputGroups.Length];
            for (int i = 0; i < inputGroups.Length; i++)
            {
                previousInput[i] = inputGroups[i].interactable;
                inputGroups[i].interactable = false;
            }
            panel.SetActive(true);
            GameSession.SetPaused(true);
        }

        public void Close()
        {
            if (panel == null || panel.activeSelf == false || previousInput == null)
            {
                return;
            }
            panel.SetActive(false);
            for (int i = 0; i < inputGroups.Length; i++)
            {
                if (inputGroups[i] != null)
                {
                    inputGroups[i].interactable = previousInput[i];
                }
            }
            if (GameSession.IsLoading == false)
            {
                GameSession.SetPaused(previousPaused);
            }
        }

        void OnDisable()
        {
            Close();
        }
    }
}
