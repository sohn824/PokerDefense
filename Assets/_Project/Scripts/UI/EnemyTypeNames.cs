using System.Collections.Generic;
using System.Text;
using PokerDefense.Game;

namespace PokerDefense.UI
{
    /**
     * EnemyTypeNames
     *
     * 적 종류를 화면에 표시할 문자열로 바꾸는 클래스
     */
    public static class EnemyTypeNames
    {
        public static string Of(EnemyType type) => type switch
        {
            EnemyType.Swarm => "스웜",
            EnemyType.Runner => "러너",
            EnemyType.Tank => "탱커",
            EnemyType.Boss => "보스",
            _ => "일반",
        };

        // 종류별 특성 한 줄 (속도·체력 경향만)
        public static string TraitOf(EnemyType type) => type switch
        {
            EnemyType.Swarm => "다수 · 체력 낮음",
            EnemyType.Runner => "빠름 · 체력 낮음",
            EnemyType.Tank => "느림 · 체력 높음",
            EnemyType.Boss => "체력 매우 높음 · 못 잡으면 큰 피해",
            _ => "표준 속도 · 표준 체력",
        };

        // 처리하지 못한 적을 표현하는 한 줄의 문자열을 만듦 (없으면 빈 문자열)
        public static string Unresolved(IReadOnlyList<CombatContext.UnresolvedCount> unresolved)
        {
            if (unresolved == null || unresolved.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            int pending = 0;

            for (int i = 0; i < unresolved.Count; i++)
            {
                CombatContext.UnresolvedCount r = unresolved[i];
                pending += r.Pending;

                if (r.Total <= 0)
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(Of(r.Type)).Append(' ').Append(r.Total);
            }

            if (pending > 0)
            {
                sb.Append(" (미등장 ").Append(pending).Append(')');
            }

            return sb.ToString();
        }
    }
}
