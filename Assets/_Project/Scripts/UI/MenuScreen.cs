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
     * 타이틀과 게임에서 공유하는 옵션·도움말. 씬에 배치된 위젯에 입력만 연결한다.
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

        void Back()
        {
            AudioPreferences.Save();
            if (titleScene || pausePage.activeSelf)
            {
                Close();
            }
            else OpenPause();
        }

        public void Close()
        {
            AudioPreferences.Save();
            if (overlay.activeSelf == false)
            {
                return;
            }
            if (overlay.activeSelf && previousInput != null)
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
            // 안내 팝업 위에서 메뉴를 열었다면 안내의 일시정지를 유지한다.
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
