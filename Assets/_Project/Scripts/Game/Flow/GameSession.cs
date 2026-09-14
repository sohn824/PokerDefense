using UnityEngine;
using UnityEngine.SceneManagement;

namespace PokerDefense.Game
{
    /**
     * GameSession
     *
     * 씬 전환과 일시정지 상태. 메뉴 표현은 UI가 맡는다.
     * 새 씬에 이전 시간 배율이나 오디오 정지가 남지 않게 한곳에서 복원한다.
     */
    public static class GameSession
    {
        public const string TitleScene = "Title";
        public const string GameScene = "Game";

        public static bool IsPaused { get; private set; }
        public static bool IsLoading { get; private set; }
        public static float Speed { get; private set; } = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            IsLoading = false;
            Speed = 1f;
            SetPaused(false);
        }

        public static void SetPaused(bool paused)
        {
            IsPaused = paused;
            Time.timeScale = paused ? 0f : Speed;
            AudioListener.pause = paused;
        }

        // 배속 토글이 호출한다. 일시정지 중에는 실제 timeScale에 반영하지 않고 값만 기억해 둔다
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
