using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * BootController
     *
     * Boot 씬에서 Title 씬으로 바로 넘김
     * 실행 시 한 번만 필요한 초기화를 넣을 자리 (지금은 없음)
     */
    public sealed class BootController : MonoBehaviour
    {
        void Start()
        {
            GameSession.Load(GameSession.TitleScene);
        }
    }
}
