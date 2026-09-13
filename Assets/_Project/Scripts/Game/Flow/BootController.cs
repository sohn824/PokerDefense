using UnityEngine;

namespace PokerDefense.Game
{
    public sealed class BootController : MonoBehaviour
    {
        void Start()
        {
            GameSession.Load(GameSession.TitleScene);
        }
    }
}
