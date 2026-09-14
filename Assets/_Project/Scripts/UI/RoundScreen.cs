using System.Collections;
using System.Collections.Generic;
using PokerDefense.Game;
using PokerDefense.Poker;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * RoundScreen
     *
     * 드로우 -> 교체 -> 족보 표시 화면. RoundController를 구독만 하고 입력은 메서드로 넘김
     * 손패 크기가 상수 5라 카드 칸을 씬에 고정해 두고 생성하지 않음
     *
     */
    public sealed class RoundScreen : MonoBehaviour
    {
        [SerializeField] RoundController controller;
        [SerializeField] TMP_Text goalLabel;
        [SerializeField] GameObject goalPanel;
        [SerializeField] HandUnitTable unitTable;
        [SerializeField] CardVisualSet cardVisuals;
        [SerializeField] CardView[] cardViews;
        [SerializeField] TMP_Text categoryLabel;
        [SerializeField] TMP_Text statusLabel;
        [SerializeField] Button exchangeButton;
        [SerializeField] TMP_Text exchangeLabel;
        [SerializeField] Button confirmButton;
        [SerializeField] TMP_Text confirmLabel;

        [Tooltip("상위 족보 확정 시 번쩍이는 전체 화면 이펙트")]
        [SerializeField] Image celebrateFlash;
        [Tooltip("상위 족보 확정 시 뜨는 축포 이펙트")]
        [SerializeField] TMP_Text celebrateStamp;

        // 상위 족보로 인정하는 최소 희귀도
        static readonly int CelebrateRarityThreshold = HandRarity.RankOf(HandCategory.Straight);

        const float CelebratePopSeconds = 0.18f;
        const float CelebratePopScale = 1.15f;
        const float CelebrateSettleSeconds = 0.35f;
        const float CelebrateHoldSeconds = 0.55f;
        const float CelebrateFadeSeconds = 0.35f;
        const float CelebrateFlashPeakAlpha = 0.4f;

        // PlayCelebration 코루틴이 돌고 있으면
        // 중간에 멈추고 새로 시작해야 하므로
        // 코루틴 참조 핸들을 들고 있음
        Coroutine celebrateRoutine;

        RoundPhase phase;

        void Awake()
        {

            for (int i = 0; i < cardViews.Length; i++)
            {
                cardViews[i].Bind(cardVisuals);
                cardViews[i].Clicked += OnCardClicked;
            }

            exchangeButton.onClick.AddListener(OnExchange);
            confirmButton.onClick.AddListener(controller.ConfirmHand);

            controller.PhaseChanged += ShowPhase;
            controller.HandChanged += ShowHand;
            controller.Evaluated += ShowResult;
        }

        // 페이즈 전환 반영 - Exchange로 돌아오면 이전 라운드 축포 이펙트를 정리
        void ShowPhase(RoundPhase newPhase)
        {
            phase = newPhase;

            if (phase == RoundPhase.Exchange)
            {
                categoryLabel.text = string.Empty;

                if (celebrateRoutine != null)
                {
                    StopCoroutine(celebrateRoutine);
                    celebrateRoutine = null;
                }

                celebrateStamp.gameObject.SetActive(false);

                // 새 손패가 깔릴 때 딜 사운드를 한 번 재생
                AudioManager.Instance?.Play(AudioManager.Sfx.CardDeal);
            }

            Refresh();
        }

        // 손패 5장 뷰 갱신 - 상태는 곧바로 부르는 Refresh가 다시 계산한다
        void ShowHand(IReadOnlyList<Card> hand)
        {
            for (int i = 0; i < cardViews.Length; i++)
            {
                cardViews[i].Show(hand[i]);
                cardViews[i].SetState(CardView.CardState.Normal);
            }

            Refresh();
        }

        // 족보 확정 결과 표시 - 상위 족보면 축포 코루틴 시작
        void ShowResult(HandResult result)
        {
            categoryLabel.text = HandCategoryNames.Of(result.Category);

            // 확정 족보 등급에 따라 팡파레를 3단계로 나눠 재생
            int rarity = HandRarity.RankOf(result.Category);

            if (rarity >= HandRarity.RankOf(HandCategory.FourOfAKind))
            {
                AudioManager.Instance?.Play(AudioManager.Sfx.HandHigh);
            }
            else if (rarity >= HandRarity.RankOf(HandCategory.Straight))
            {
                AudioManager.Instance?.Play(AudioManager.Sfx.HandMid);
            }
            else
            {
                AudioManager.Instance?.Play(AudioManager.Sfx.HandLow);
            }

            System.Text.StringBuilder keyCards = new System.Text.StringBuilder("키카드");

            for (int i = 0; i < result.KeyCards.Count; i++)
            {
                keyCards.Append(' ').Append(CardText.Of(result.KeyCards[i]));
            }

            statusLabel.text = keyCards.ToString();

            if (HandRarity.RankOf(result.Category) >= CelebrateRarityThreshold)
            {
                UnitDefinition unit = unitTable.GetDefinition(result.Category);
                string banner = $"{HandCategoryNames.Of(result.Category)}!\n{unit.DisplayName} 소환";

                if (celebrateRoutine != null)
                {
                    StopCoroutine(celebrateRoutine);
                }

                celebrateRoutine = StartCoroutine(PlayCelebration(banner));
            }
        }

        // 상위 족보 확정 축포 코루틴
        // (화면 플래시 + 배너 팝인 -> 유지 -> 페이드아웃)
        IEnumerator PlayCelebration(string banner)
        {
            // 배너 텍스트를 켜고 알파를 1로 초기화
            celebrateStamp.text = banner;
            celebrateStamp.gameObject.SetActive(true);

            Color textColor = celebrateStamp.color;
            textColor.a = 1f;
            celebrateStamp.color = textColor;

            Color flashColor = celebrateFlash.color;

            // 배너 팝인: 배너가 scale 0 ~ 1.15로 튀어나오며 동시에 화면 플래시 알파가 0에서 최대까지 밝아짐
            for (float t = 0f; t < CelebratePopSeconds; t += Time.deltaTime)
            {
                float scale = Mathf.Lerp(0f, CelebratePopScale, t / CelebratePopSeconds);
                celebrateStamp.transform.localScale = new Vector3(scale, scale, 1f);
                flashColor.a = Mathf.Lerp(0f, CelebrateFlashPeakAlpha, t / CelebratePopSeconds);
                celebrateFlash.color = flashColor;
                yield return null;
            }

            // 1.15배로 오버된 배너가 1배로 가라앉고, 플래시는 0으로 페이드아웃
            for (float t = 0f; t < CelebrateSettleSeconds; t += Time.deltaTime)
            {
                float scale = Mathf.Lerp(CelebratePopScale, 1f, t / CelebrateSettleSeconds);
                celebrateStamp.transform.localScale = new Vector3(scale, scale, 1f);
                flashColor.a = Mathf.Lerp(CelebrateFlashPeakAlpha, 0f, t / CelebrateSettleSeconds);
                celebrateFlash.color = flashColor;
                yield return null;
            }

            // 위에서 t가 CelebrateSettleSeconds에 도달하지 않고 끝나므로
            // 마지막 프레임에서 Lerp 목표값 직전에서 끝남
            // 따라서 정확한 목표값으로 보정
            celebrateStamp.transform.localScale = Vector3.one;
            flashColor.a = 0f;
            celebrateFlash.color = flashColor;

            // 배너 유지 (값 갱신 없이 그대로 대기만)
            yield return new WaitForSeconds(CelebrateHoldSeconds);

            // 배너 페이드 아웃
            for (float t = 0f; t < CelebrateFadeSeconds; t += Time.deltaTime)
            {
                textColor.a = Mathf.Lerp(1f, 0f, t / CelebrateFadeSeconds);
                celebrateStamp.color = textColor;
                yield return null;
            }

            // 재생 종료
            // (배너를 끄고 celebrateRoutine을 null로 되돌려 다음 코루틴 트리거를 받을 수 있게 함)
            celebrateStamp.gameObject.SetActive(false);
            celebrateRoutine = null;
        }

        void OnCardClicked(CardView card)
        {
            // 아니면 교체 대상 선택/해제 토글
            AudioManager.Instance?.Play(AudioManager.Sfx.CardSelect);
            card.SetState(card.Selected ? CardView.CardState.Normal : CardView.CardState.Pick);
            Refresh();
        }

        void OnExchange()
        {
            List<int> indices = SelectedIndices();

            controller.ExchangeCards(indices);

            if (indices.Count > 0)
            {
                AudioManager.Instance?.Play(AudioManager.Sfx.Exchange);
            }
        }

        // 카드 상태, 조작 가능 여부, 버튼 상태, 안내 문구를 현재 상태에 맞추기
        void Refresh()
        {
            bool exchanging = phase == RoundPhase.Exchange;
            goalPanel.SetActive(exchanging);
            int picked = 0;

            for (int i = 0; i < cardViews.Length; i++)
            {
                CardView card = cardViews[i];
                bool locked = controller.IsLocked(i);

                // 배치 모드에서는 잠긴 자리도 눌러 안내를 받게 열어 둔다
                card.SetInteractable(exchanging && locked == false);

                if (locked)
                {
                    card.SetState(CardView.CardState.Locked);
                }
                else if (card.Selected)
                {
                    card.SetState(CardView.CardState.Pick);
                    picked++;
                }
                else
                {
                    card.SetState(CardView.CardState.Normal);
                }
            }

            confirmButton.interactable = exchanging;

            exchangeButton.interactable = exchanging && picked > 0;
            exchangeLabel.text = picked > 0 ? $"일반 교체\n<size=30>선택한 {picked}장 바꾸기</size>"
                : controller.ExchangeableCount == 0 ? "일반 교체\n<size=30>교체할 카드 없음</size>"
                : "일반 교체\n<size=30>손패에서 카드 선택</size>";

            if (!exchanging)
            {
                confirmLabel.text = "손패 확정";
                return;
            }

            ShowPreview(picked);
        }

        // 프리뷰: 지금 확정하면 나올 족보·유닛, 그리고 현재 보너스와 '교체하면 바뀔' 예상 보너스를 나눠 보여줌
        void ShowPreview(int picked)
        {
            HandResult preview = controller.PreviewHand();
            UnitDefinition unit = unitTable.GetDefinition(preview.Category);
            categoryLabel.text = HandCategoryNames.Of(preview.Category);
            confirmLabel.text = "손패 확정\n<size=30>유닛 소환</size>";
            statusLabel.text = picked > 0 ? $"선택한 {picked}장을 새 카드로 바꿉니다"
                : $"확정하면 {unit.DisplayName} 소환 · 일반 교체 가능 {controller.ExchangeableCount}장";

            // 카드를 선택했을 때만 그 교체의 확률을 보여준다. 선택 전 족보 추천은 하지 않는다
            bool showOdds = picked > 0 && picked <= HandOdds.MaxSlots;
            goalPanel.SetActive(showOdds);

            if (showOdds)
            {
                goalLabel.text = HandOddsText.Describe(controller.FindExchangeOdds(SelectedIndices()));
            }
        }

        List<int> SelectedIndices()
        {
            List<int> indices = new List<int>();

            for (int i = 0; i < cardViews.Length; i++)
            {
                if (cardViews[i].Selected)
                {
                    indices.Add(i);
                }
            }

            return indices;
        }
    }
}
