using System.Collections;
using System.Collections.Generic;
using PokerDefense.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerDefense.UI
{
    /**
     * RandomSummonMachine
     *
     * 랜덤 소환이 확정되면 슬롯머신 패널을 띄운다
     * 한 줄에 [유닛 아트][이름]이 세로로 흐르다가 감속해 뽑힌 유닛에서 멈추는 연출
     *
     * 결과는 소환 확정 시점에서 이미 정해졌고 뽑힌 유닛은 배치 대기 상태
     * 슬롯은 연출일뿐 게임을 멈추지 않음
     * 패널이 닫히면 유닛 배치 가능
     */
    public sealed class RandomSummonMachine : MonoBehaviour
    {
        // 슬롯에 쌓을 줄 수와 줄 높이 (마지막 줄이 결과)
        const int RowCount = 20;
        const float RowHeight = 150f;

        // 스핀 시간, 멈춘 뒤 유지 시간, 패널 페이드아웃
        const float SpinSeconds = 1.5f;
        const float HoldSeconds = 0.8f;
        const float FadeSeconds = 0.2f;

        [SerializeField] PlacementController placement;
        [SerializeField] HandUnitTable unitTable;
        [SerializeField] EconomyDefinition economy;

        [SerializeField] GameObject panel;
        [SerializeField] CanvasGroup panelGroup;
        [SerializeField] RectTransform strip;
        [SerializeField] RectTransform rowTemplate;

        readonly List<Sprite> faces = new List<Sprite>();
        readonly List<string> names = new List<string>();
        readonly List<Image> rowArt = new List<Image>();
        readonly List<TMP_Text> rowName = new List<TMP_Text>();

        Coroutine run;

        void Awake()
        {
            // 슬롯에 돌릴 후보(아트 + 이름)를 한 번만 모은다
            IReadOnlyList<EconomyDefinition.RandomSummonEntry> pool = economy.RandomPool;

            for (int i = 0; pool != null && i < pool.Count; i++)
            {
                if (pool[i].weight <= 0)
                {
                    continue;
                }

                UnitDefinition definition = unitTable.GetDefinition(pool[i].category);
                Sprite art = definition != null ? definition.ArtFor(AimDirection.Down) : null;

                if (art != null)
                {
                    faces.Add(art);
                    names.Add(definition.DisplayName);
                }
            }

            // 줄을 미리 만들어 쌓아 둔다
            for (int i = 0; i < RowCount; i++)
            {
                RectTransform row = Instantiate(rowTemplate, strip);
                row.gameObject.SetActive(true);
                row.anchoredPosition = new Vector2(0f, -i * RowHeight);

                rowArt.Add(row.GetComponentInChildren<Image>(true));
                rowName.Add(row.GetComponentInChildren<TMP_Text>(true));
            }

            rowTemplate.gameObject.SetActive(false);
            placement.RandomSummoned += OnRandomSummoned;
            panel.SetActive(false);
        }

        void OnDestroy()
        {
            placement.RandomSummoned -= OnRandomSummoned;
        }

        // 뽑힌 유닛(drawn)을 받아 슬롯을 돌림
        // 배치는 플레이어가 패널이 닫힌 뒤에 직접 함
        void OnRandomSummoned(UnitDefinition drawn)
        {
            if (faces.Count == 0 || drawn == null)
            {
                return;
            }

            if (run != null)
            {
                StopCoroutine(run);
            }

            run = StartCoroutine(Spin(drawn.ArtFor(AimDirection.Down), drawn.DisplayName));
        }

        IEnumerator Spin(Sprite resultArt, string resultName)
        {
            panel.SetActive(true);
            panelGroup.alpha = 1f;

            // 마지막 줄만 결과, 나머지는 후보 랜덤
            for (int i = 0; i < RowCount; i++)
            {
                bool last = i == RowCount - 1;
                int pick = Random.Range(0, faces.Count);
                rowArt[i].sprite = last ? resultArt : faces[pick];
                rowName[i].text = last ? resultName : names[pick];
            }

            // 슬롯을 0에서 마지막 줄까지 밀어 올린다
            // 뒤로 갈수록 감속
            float distance = (RowCount - 1) * RowHeight;

            for (float t = 0f; t < SpinSeconds; t += Time.deltaTime)
            {
                float ratio = t / SpinSeconds;
                float eased = 1f - Mathf.Pow(1f - ratio, 4f);
                strip.anchoredPosition = new Vector2(0f, distance * eased);
                yield return null;
            }

            strip.anchoredPosition = new Vector2(0f, distance);
            yield return new WaitForSeconds(HoldSeconds);

            for (float t = 0f; t < FadeSeconds; t += Time.deltaTime)
            {
                panelGroup.alpha = 1f - t / FadeSeconds;
                yield return null;
            }

            panel.SetActive(false);
            run = null;
        }
    }
}
