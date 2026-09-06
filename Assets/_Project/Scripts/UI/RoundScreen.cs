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
     * 교체 중에도 '지금 확정하면 나올 족보·유닛·유지 보너스'를 보여줌
     */
    public sealed class RoundScreen : MonoBehaviour
    {
        [SerializeField] RoundController controller;
        [SerializeField] StageController stage;
        [SerializeField] HandUnitTable unitTable;
        [SerializeField] CardVisualSet cardVisuals;
        [SerializeField] CardView[] cardViews;
        [SerializeField] TMP_Text categoryLabel;
        [SerializeField] TMP_Text statusLabel;
        [SerializeField] Button exchangeButton;
        [SerializeField] TMP_Text exchangeLabel;
        [SerializeField] Button confirmButton;
        [SerializeField] TMP_Text confirmLabel;

        [Tooltip("상점에서 산 보유 카드 Tray")]
        [SerializeField] GameObject heldTray;
        [SerializeField] CardView[] heldCardViews;

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

        // 배치하려고 고른 트레이 카드 (상점에서 구매한 카드 중)
        CardView selectedTrayCard;

        void Awake()
        {
            for (int i = 0; i < cardViews.Length; i++)
            {
                cardViews[i].Bind(cardVisuals);
                cardViews[i].Clicked += OnCardClicked;
            }

            for (int i = 0; i < heldCardViews.Length; i++)
            {
                heldCardViews[i].Bind(cardVisuals);
                heldCardViews[i].Clicked += OnHeldClicked;
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

            RefreshTray();
            Refresh();
        }

        // 손패 5장 뷰 갱신
        void ShowHand(IReadOnlyList<Card> hand)
        {
            for (int i = 0; i < cardViews.Length; i++)
            {
                cardViews[i].Show(hand[i]);
                cardViews[i].SetSelected(false);
            }

            RefreshTray();
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

            var keyCards = new System.Text.StringBuilder("키카드");

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
            // 트레이 카드를 고른 상태면 클릭한 손패 칸에 놓는다
            if (selectedTrayCard != null)
            {
                PlaceSelectedTrayCardOn(card);
                return;
            }

            // 아니면 그냥 클릭한 카드 선택/해제 토글
            AudioManager.Instance?.Play(AudioManager.Sfx.CardSelect);
            card.SetSelected(!card.Selected);
            Refresh();
        }

        // 트레이 카드 탭 - 배치할 카드를 고르거나(선택) 다시 눌러 해제
        void OnHeldClicked(CardView trayCard)
        {
            AudioManager.Instance?.Play(AudioManager.Sfx.CardSelect);

            if (selectedTrayCard == trayCard)
            {
                selectedTrayCard = null;
            }
            else
            {
                // 배치 모드로 들어가면 교체 선택은 버린다
                for (int i = 0; i < cardViews.Length; i++)
                {
                    cardViews[i].SetSelected(false);
                }

                selectedTrayCard = trayCard;
            }

            RefreshTray();
            Refresh();
        }

        void PlaceSelectedTrayCardOn(CardView handCard)
        {
            int slot = System.Array.IndexOf(cardViews, handCard);
            int trayIndex = System.Array.IndexOf(heldCardViews, selectedTrayCard);

            if (slot < 0 || controller.IsLocked(slot) || trayIndex < 0 || trayIndex >= stage.HeldCards.Count)
            {
                return;
            }

            Card held = stage.HeldCards[trayIndex];

            selectedTrayCard = null;
            stage.RemoveHeldCard(held);
            // HandChanged -> ShowHand 가 RefreshTray + Refresh 를 부른다
            controller.PlaceHeldCard(slot, held);
            AudioManager.Instance?.Play(AudioManager.Sfx.CardFlip);
        }

        // 보유 카드 트레이를 현재 상태에 맞춘다
        void RefreshTray()
        {
            IReadOnlyList<Card> held = stage.HeldCards;
            bool show = phase == RoundPhase.Exchange && held.Count > 0;

            heldTray.SetActive(show);

            if (show == false)
            {
                selectedTrayCard = null;
            }

            for (int i = 0; i < heldCardViews.Length; i++)
            {
                bool filled = show && i < held.Count;
                heldCardViews[i].gameObject.SetActive(filled);

                if (filled == false)
                {
                    continue;
                }

                heldCardViews[i].Show(held[i]);
                heldCardViews[i].SetSelected(heldCardViews[i] == selectedTrayCard);
                heldCardViews[i].SetInteractable(true);
            }
        }

        void OnExchange()
        {
            var indices = new List<int>();

            for (int i = 0; i < cardViews.Length; i++)
            {
                if (cardViews[i].Selected)
                {
                    indices.Add(i);
                }
            }

            controller.ExchangeCards(indices);

            if (indices.Count > 0)
            {
                AudioManager.Instance?.Play(AudioManager.Sfx.Exchange);
            }
        }

        // 카드 조작 가능 여부, 버튼 상태, 안내 문구를 현재 상태에 맞추기
        void Refresh()
        {
            bool exchanging = phase == RoundPhase.Exchange;
            int selected = 0;

            for (int i = 0; i < cardViews.Length; i++)
            {
                // 잠긴 자리는 이번 라운드에 다시 못 바꾸므로 못 고르도록 함
                cardViews[i].SetInteractable(exchanging && controller.IsLocked(i) == false);

                if (cardViews[i].Selected)
                {
                    selected++;
                }
            }

            exchangeButton.interactable = exchanging && selected > 0;
            confirmButton.interactable = exchanging;
            exchangeLabel.text = selected == 0 ? "교체" : $"교체 {selected}장";

            if (!exchanging)
            {
                confirmLabel.text = "확정";
                return;
            }

            ShowPreview();
        }

        // 프리뷰: 지금 확정하면 어떤 족보로 어떤 유닛이 나오고 유지 보너스 칩이 얼마인지 미리 보여줌
        void ShowPreview()
        {
            HandResult preview = controller.PreviewHand();
            UnitDefinition unit = unitTable.GetDefinition(preview.Category);
            int bonus = stage.HoldBonusFor(controller.UsedExchanges);
            int left = controller.ExchangeableCount;

            categoryLabel.text = HandCategoryNames.Of(preview.Category);
            confirmLabel.text = bonus > 0 ? $"확정 +{bonus}" : "확정";

            if (selectedTrayCard != null)
            {
                statusLabel.text = "손패 칸을 눌러 카드를 놓으세요 (교체 아님)";
                return;
            }

            statusLabel.text = left == 0
                ? $"확정하면 {unit.DisplayName} 소환 - 더 바꿀 카드가 없습니다"
                : $"확정하면 {unit.DisplayName} 소환 - 남은 교체 {left}칸 (자리당 1회)";
        }
    }
}
