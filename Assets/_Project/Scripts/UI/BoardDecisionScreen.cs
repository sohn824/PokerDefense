using System;
using PokerDefense.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    // 유닛이 사라지는 행동은 내용을 확인한 뒤 실행한다.
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
            if (GameSession.IsLoading == false) GameSession.SetPaused(false);
        }

        void OnDisable()
        {
            Close();
        }
    }
}
