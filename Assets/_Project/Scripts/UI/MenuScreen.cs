using PokerDefense.Game;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * MenuScreen
     *
     * 타이틀과 게임 씬에서 함께 쓰는 메뉴를 관리하는 클래스
     * 메뉴 화면 - 일시정지, 오디오 옵션, 도움말, 포기 확인
     */
    public sealed class MenuScreen : MonoBehaviour
    {
        [SerializeField] CanvasGroup[] inputGroups;
        bool[] previousInput;
        bool previousPaused;

        [SerializeField] bool titleScene;
        [SerializeField] GameFlowController flow;
        [SerializeField] GameObject home;
        [SerializeField] GameObject overlay;
        [SerializeField] GameObject pausePage;
        [SerializeField] GameObject optionsPage;
        [SerializeField] GameObject helpPage;
        [SerializeField] GameObject abandonPage;
        [SerializeField] Button startButton;
        [SerializeField] Button menuButton;
        [SerializeField] Button resumeButton;
        [SerializeField] Button optionsButton;
        [SerializeField] Button helpButton;
        [SerializeField] Button titleButton;
        [SerializeField] Button quitButton;
        [SerializeField] Button backButton;
        [SerializeField] Button resetButton;
        [SerializeField] Button abandonButton;
        [SerializeField] Button cancelButton;
        [SerializeField] Slider master;
        [SerializeField] Slider music;
        [SerializeField] Slider effects;
        [SerializeField] Toggle mute;
        [SerializeField] TMP_Text volumeLabel;

        void Awake()
        {
            overlay.SetActive(false);
            if (startButton != null)
            {
                startButton.onClick.AddListener(() => GameSession.Load(GameSession.GameScene));
            }
            if (menuButton != null)
            {
                menuButton.onClick.AddListener(OpenPause);
            }
            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(Close);
            }
            if (titleButton != null)
            {
                titleButton.onClick.AddListener(RequestTitle);
            }
            optionsButton.onClick.AddListener(OpenOptions);
            helpButton.onClick.AddListener(() => Show(helpPage));
            // 종료 버튼은 PC·에디터에서만 보이도록 함
            // (모바일은 OS가 앱 종료를 맡으므로)
            quitButton.gameObject.SetActive(Application.platform == RuntimePlatform.WindowsPlayer
                || Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.OSXPlayer
                || Application.isEditor);
            quitButton.onClick.AddListener(Application.Quit);
            backButton.onClick.AddListener(Back);
            resetButton.onClick.AddListener(() => { AudioPreferences.Set(1f, 1f, 1f, false); RefreshOptions(); });
            abandonButton.onClick.AddListener(() => GameSession.Load(GameSession.TitleScene));
            cancelButton.onClick.AddListener(OpenPause);
            master.onValueChanged.AddListener(_ => ChangeOptions());
            music.onValueChanged.AddListener(_ => ChangeOptions());
            effects.onValueChanged.AddListener(_ => ChangeOptions());
            mute.onValueChanged.AddListener(_ => ChangeOptions());
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (overlay.activeSelf)
                {
                    Back();
                }
                else if (titleScene == false)
                {
                    OpenPause();
                }
            }
        }

        // 앱이 백그라운드로 가면 게임 씬에서는 일시정지 메뉴를 열고, 음량 설정을 저장
        void OnApplicationPause(bool paused)
        {
            if (paused && titleScene == false)
            {
                OpenPause();
            }
            if (paused)
            {
                AudioPreferences.Save();
            }
        }

        void OnDestroy()
        {
            AudioPreferences.Save();
            GameSession.SetPaused(false);
        }

        public void OpenPause()
        {
            Show(pausePage);
        }

        // Run이 끝났으면 바로 타이틀로, 아니면 포기 확인 페이지 띄우기
        public void RequestTitle()
        {
            if (flow != null && flow.IsFinished)
            {
                GameSession.Load(GameSession.TitleScene);
                return;
            }

            Show(abandonPage);
        }

        void OpenOptions()
        {
            RefreshOptions();
            Show(optionsPage);
        }

        // 오버레이를 처음 열 때만 입력·일시정지 상태를 저장하고 끔
        void Show(GameObject page)
        {
            if (GameSession.IsLoading)
            {
                return;
            }
            if (overlay.activeSelf == false)
            {
                previousPaused = GameSession.IsPaused;
                if (titleScene == false)
                {
                    GameSession.SetPaused(true);
                }
                previousInput = new bool[inputGroups.Length];
                for (int i = 0; i < inputGroups.Length; i++)
                {
                    previousInput[i] = inputGroups[i].interactable;
                    inputGroups[i].interactable = false;
                }
            }
            overlay.SetActive(true);
            pausePage.SetActive(page == pausePage);
            optionsPage.SetActive(page == optionsPage);
            helpPage.SetActive(page == helpPage);
            abandonPage.SetActive(page == abandonPage);
            backButton.gameObject.SetActive(page == optionsPage || page == helpPage);
            if (home != null)
            {
                home.SetActive(false);
            }
        }

        // 옵션·도움말이면 일시정지 페이지로, 타이틀이거나 일시정지 페이지면 닫음
        void Back()
        {
            AudioPreferences.Save();
            if (titleScene || pausePage.activeSelf)
            {
                Close();
            }
            else
            {
                OpenPause();
            }
        }

        public void Close()
        {
            AudioPreferences.Save();
            if (overlay.activeSelf == false)
            {
                return;
            }
            if (previousInput != null)
            {
                for (int i = 0; i < inputGroups.Length; i++)
                {
                    inputGroups[i].interactable = previousInput[i];
                }
            }
            overlay.SetActive(false);
            if (home != null)
            {
                home.SetActive(true);
            }
            // 메뉴를 열기 전에 다른 창이 건 일시정지는 그대로 유지
            GameSession.SetPaused(previousPaused);
        }

        void RefreshOptions()
        {
            master.SetValueWithoutNotify(AudioPreferences.Master);
            music.SetValueWithoutNotify(AudioPreferences.Music);
            effects.SetValueWithoutNotify(AudioPreferences.Effects);
            mute.SetIsOnWithoutNotify(AudioPreferences.Muted);
            RefreshVolumeLabel();
        }

        void ChangeOptions()
        {
            AudioPreferences.Set(master.value, music.value, effects.value, mute.isOn);
            RefreshVolumeLabel();
        }

        void RefreshVolumeLabel()
        {
            volumeLabel.text = $"전체 {master.value:P0}   BGM {music.value:P0}   효과음 {effects.value:P0}";
        }
    }
}
