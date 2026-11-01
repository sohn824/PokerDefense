using System;
using PokerDefense.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * BoardDecisionScreen
     *
     * 유닛이 사라지는 행동(교체·퇴장·소환 포기)을 실행하기 전에 내용을 보여주고 확인받는 창
     * 열려 있는 동안 게임 입력(gameInput)을 끄고 GameSession.SetPaused(true)로 게임을 멈춤
     *
     * Open - 문구와 실행할 동작을 받아 창을 엶 (일시정지 중이거나, 씬 로딩 중이거나, 이미 열려 있으면 무시)
     * Confirm - 창을 닫은 뒤 동작 실행
     * Close - 동작 없이 닫고 입력을 열기 전 상태로, 일시정지는 해제 (취소 버튼·비활성화 시)
     */
    public sealed class BoardDecisionScreen : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text message;
        [SerializeField] Button confirm;
        [SerializeField] Button cancel;
        [SerializeField] CanvasGroup gameInput;
        bool previousInput;
        Action pending;

        void Awake()
        {
            panel.SetActive(false);
            confirm.onClick.AddListener(Confirm);
            cancel.onClick.AddListener(Close);
        }

        public void Open(string text, Action action)
        {
            if (GameSession.IsPaused || GameSession.IsLoading || panel.activeSelf)
            {
                return;
            }
            pending = action;
            message.text = text;
            previousInput = gameInput.interactable;
            gameInput.interactable = false;
            panel.SetActive(true);
            GameSession.SetPaused(true);
        }

        // 확인 버튼 - 창을 닫아 일시정지를 푼 뒤 저장해 둔 동작 실행
        // PlacementController의 교체·퇴장·소환 포기는 일시정지 중이면 실패하므로 닫기가 먼저임
        void Confirm()
        {
            Action action = pending;
            Close();
            action?.Invoke();
        }

        public void Close()
        {
            if (panel.activeSelf == false) return;
            pending = null;
            panel.SetActive(false);
            gameInput.interactable = previousInput;
            // 씬 로딩 중에는 GameSession.Load가 이미 일시정지를 풀었으므로 건드리지 않음
            if (GameSession.IsLoading == false) GameSession.SetPaused(false);
        }

        void OnDisable()
        {
            Close();
        }
    }
}
