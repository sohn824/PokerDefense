using System.Collections.Generic;
using System.Text;
using PokerDefense.Game;

namespace PokerDefense.UI
{
    /**
     * EnemyTypeNames
     *
     * 화면 표기용 적 종류명과 특성 한 줄
     * 특성은 속도·체력의 사실만 적고 평가("위험", "약함")는 넣지 않는다
     */
    public static class EnemyTypeNames
    {
        public static string Of(EnemyType type) => type switch
        {
            EnemyType.Swarm => "무리",
            EnemyType.Runner => "러너",
            EnemyType.Tank => "탱커",
            EnemyType.Boss => "보스",
            _ => "일반",
        };

        // 종류별 특성 한 줄 (사거리 없음, HP·속도 경향만)
        public static string TraitOf(EnemyType type) => type switch
        {
            EnemyType.Swarm => "다수 · 체력 낮음",
            EnemyType.Runner => "빠름 · 체력 낮음",
            EnemyType.Tank => "느림 · 체력 높음",
            EnemyType.Boss => "체력 매우 높음 · 남아 있어도 라이프 상한 피해",
            _ => "표준 속도 · 표준 체력",
        };

        // 미해결 적 목록을 "러너 3, 탱커 1 (미등장 2)" 한 줄로. 없으면 빈 문자열
        public static string Remnants(IReadOnlyList<CombatContext.Remnant> remnants)
        {
            if (remnants == null || remnants.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            int pending = 0;

            for (int i = 0; i < remnants.Count; i++)
            {
                CombatContext.Remnant r = remnants[i];
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
