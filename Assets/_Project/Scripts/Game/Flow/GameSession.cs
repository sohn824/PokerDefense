using UnityEngine;
using UnityEngine.SceneManagement;

namespace PokerDefense.Game
{
    /**
     * GameSession
     *
     * 씬 전환·일시정지·배속 상태 관리 (메뉴 표현은 UI가 맡음)
     * 새 씬에 이전 시간 배율이나 오디오 정지가 남지 않게 한곳에서 복원함
     */
    public static class GameSession
    {
        public const string TitleScene = "Title";
        public const string GameScene = "Game";

        public static bool IsPaused { get; private set; }
        public static bool IsLoading { get; private set; }
        public static float Speed { get; private set; } = 1f;

        // 타이틀에서 이어하기를 골랐는지 (Game 씬의 RunSaver가 한 번 읽고 지움)
        static bool continueRequested;

        // 플레이를 시작할 때마다 static 멤버를 초기값으로 되돌림 (도메인 리로드를 꺼도 이전 플레이 값이 남지 않게)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            IsLoading = false;
            Speed = 1f;
            continueRequested = false;
            SetPaused(false);
        }

        // 저장한 Run을 이어서 하도록 표시하고 Game 씬을 엶
        public static void LoadContinue()
        {
            continueRequested = true;
            Load(GameScene);
        }

        // 이어하기 요청을 읽고 지움 (한 번만 true)
        public static bool ConsumeContinueRequest()
        {
            bool requested = continueRequested;
            continueRequested = false;
            return requested;
        }

        public static void SetPaused(bool paused)
        {
            IsPaused = paused;
            Time.timeScale = paused ? 0f : Speed;
            AudioListener.pause = paused;
        }

        // 배속 토글이 호출함, 일시정지 중에는 실제 timeScale에 반영하지 않고 값만 기억해 둠
        public static void SetSpeed(float speed)
        {
            Speed = speed;

            if (IsPaused == false)
            {
                Time.timeScale = Speed;
            }
        }

        public static void Load(string scene)
        {
            if (IsLoading)
            {
                return;
            }

            IsLoading = true;
            Speed = 1f;
            SetPaused(false);
            SceneManager.sceneLoaded += OnLoaded;
            try
            {
                SceneManager.LoadSceneAsync(scene);
            }
            catch
            {
                SceneManager.sceneLoaded -= OnLoaded;
                IsLoading = false;
                throw;
            }
        }

        static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnLoaded;
            IsLoading = false;
        }
    }
}
