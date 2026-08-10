using System.Collections.Generic;
using System.Text;
using PokerDefense.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * PerkScreen
     *
     * 보스를 잡으면 딜러 특전 3중 택1을 띄운다 (DESIGN §11)
     *
     * 유닛 상세와 달리 전면 패널이다. 웨이브 사이라 보드에 볼 것이 없고,
     * 고르기 전에는 다음 라운드가 열리지 않아 무엇을 기다리는지 화면으로 보여야 한다
     * 화면을 덮는 Image가 보드 클릭까지 함께 막아 준다 (BoardScreen이 UI 위 클릭을 거른다)
     */
    public sealed class PerkScreen : MonoBehaviour
    {
        [SerializeField] GameFlowController flow;
        [SerializeField] StageController stage;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text ownedLabel;
        [SerializeField] Button[] optionButtons;
        [SerializeField] TMP_Text[] optionLabels;

        IReadOnlyList<PerkId> offer;

        void Awake()
        {
            for (int i = 0; i < optionButtons.Length; i++)
            {
                int index = i;
                optionButtons[i].onClick.AddListener(() => Choose(index));
            }

            flow.PerkOffered += Show;

            panel.SetActive(false);
        }

        void Show(IReadOnlyList<PerkId> offered)
        {
            offer = offered;
            panel.SetActive(true);
            ownedLabel.text = DescribeOwned();

            for (int i = 0; i < optionButtons.Length; i++)
            {
                bool filled = i < offered.Count;
                optionButtons[i].gameObject.SetActive(filled);

                if (filled)
                {
                    optionLabels[i].text = Describe(stage.PerkTable.For(offered[i]));
                }
            }
        }

        void Choose(int index)
        {
            if (offer == null || index >= offer.Count)
            {
                return;
            }

            PerkId chosen = offer[index];

            // 패널을 먼저 닫는다. ChoosePerk가 다음 라운드를 열면서 화면 전체를 다시 그린다
            offer = null;
            panel.SetActive(false);
            flow.ChoosePerk(chosen);
        }

        static string Describe(PerkTable.PerkEntry entry)
            => $"{entry.displayName}   <size=70%>{PerkCategoryNames.Of(entry.category)}</size>\n"
               + $"<size=70%>{entry.description}</size>";

        // 두 번째 보스에서는 이미 고른 특전이 있다. 같은 방향으로 더 밀지 고민할 재료다
        string DescribeOwned()
        {
            IReadOnlyList<PerkId> owned = stage.Stage.Perks.Owned;

            if (owned.Count == 0)
            {
                return "스테이지가 끝날 때까지 유지됩니다";
            }

            var text = new StringBuilder("가진 특전   ");

            for (int i = 0; i < owned.Count; i++)
            {
                if (i > 0)
                {
                    text.Append("   ·   ");
                }

                text.Append(stage.PerkTable.For(owned[i]).displayName);
            }

            return text.ToString();
        }
    }
}
