using System.Collections.Generic;
using System.Globalization;
using PokerDefense.Game;
using PokerDefense.Poker;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * HandOddsScreen
     *
     * 족보별 교체 확률을 희귀도 순으로 보여 주는 상세 창
     * 결과를 보여 주기만 하고 손패·덱은 바꾸지 않음
     * 열린 동안 게임을 일시정지하고 입력을 막음, 닫으면 열기 전 상태로 되돌림
     */
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
        // 창이 열린 동안 막는 입력 묶음과, 열기 전 상태(닫을 때 되돌림)
        [SerializeField] CanvasGroup[] inputGroups;
        bool[] previousInput;

        void Awake()
        {
            panel.SetActive(false);
            closeButton.onClick.AddListener(Close);
        }

        // 확률 계산이 끝났을 때만 엶 (계산 중이거나 실패했으면 보여 줄 결과가 없음)
        // context - 제목 문구, assist - 선택 교체 창에서 열었는지 (설명 문구가 달라짐)
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
            // 희귀도 순으로 한 줄씩 채우고, 남는 행은 숨김
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
            // 닫을 때 그대로 되돌릴 수 있게 입력 상태를 먼저 기록해 둠
            previousInput = new bool[inputGroups.Length];
            for (int i = 0; i < inputGroups.Length; i++)
            {
                previousInput[i] = inputGroups[i].interactable;
                inputGroups[i].interactable = false;
            }
            panel.SetActive(true);
            GameSession.SetPaused(true);
        }

        // 창을 닫고 입력을 열기 전 상태로 되돌린 뒤 일시정지를 풂 (씬 로딩 중이면 일시정지는 건드리지 않음)
        // 이미 일시정지 중이면 Open이 열리지 않으므로, 열기 전은 항상 일시정지가 아닌 상태임
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
                GameSession.SetPaused(false);
            }
        }

        void OnDisable()
        {
            Close();
        }
    }
}
